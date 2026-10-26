using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using IndieableSdk.DeveloperWorkflow;

internal static class WorkflowReliabilitySmoke
{
    private const string Session = "dw_session_synthetic_reliability_context";
    private static WorkflowOptions Options(HttpClient http, string origin = "https://example.test") =>
        new() { BaseUrl = origin, HttpClient = http, MaxRetries = 0, RetryBaseDelay = TimeSpan.Zero };

    internal static async Task RunAsync()
    {
        await LostResponseAfterCommit();
        await PendingRetryKeepsOriginalContext();
        await ConsentInvalidationAndPermanentErrors();
        await BackoffMetadata();
        await StaleFeedbackAndPartialReceipt();
        Console.WriteLine("Workflow reliability: lost response, stable in-memory retry, fixed session context, consent invalidation, permanent errors, long backoff and versioned feedback passed.");
    }

    private static async Task LostResponseAfterCommit()
    {
      foreach (var timeoutAfterCommit in new[] { false, true })
      {
        var bodies = new List<string>(); var committed = false;
        using var http = Http(async (request, token) => {
            bodies.Add(await request.Content!.ReadAsStringAsync(token));
            if (!committed) {
                committed = true;
                if (timeoutAfterCommit) await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new HttpRequestException("Synthetic response lost AFTER commit");
            }
            return Receipt(0, 1);
        });
        using var client = new WorkflowSessionClient(new() { BaseUrl = "https://example.test", HttpClient = http, RetryBaseDelay = TimeSpan.Zero,
            RequestTimeout = TimeSpan.FromMilliseconds(100) }, Session);
        var observation = new WorkflowEvent { Key = "session.started" };
        var requestId = Guid.NewGuid();
        if (timeoutAfterCommit) {
            var uncertain = await client.TrySendReportedEventsAsync(new[] { observation }, requestId);
            Check(uncertain.Error is { Code: "request_timeout", Retryable: true }, "Timeout after commit was reported as a permanent denial");
        }
        var result = await client.SendReportedEventsAsync(new[] { observation }, requestId);
        Check(result.Duplicates == 1 && result.Accepted == 0 && bodies.Count == 2 && bodies[0] == bodies[1], "Uncertain retry changed committed operation");
      }
    }

    private static async Task PendingRetryKeepsOriginalContext()
    {
        var original = new List<string>(); var resumed = new List<string>(); var online = false;
        using var http = Http(async (request, token) => {
            Check(request.Headers.Authorization?.Parameter == Session, "Pending retry changed session authority");
            var body = await request.Content!.ReadAsStringAsync(token);
            if (online) { resumed.Add(body); return Receipt(0, 1); }
            original.Add(body);
            var response = Error("rate_limited", HttpStatusCode.TooManyRequests, true);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero); return response;
        });
        using var client = new WorkflowSessionClient(Options(http), Session);
        await using var buffer = client.CreateEventBuffer(capacity: 2);
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        buffer.DeliveryCompleted += _ => paused.TrySetResult();
        var first = new WorkflowEvent { Key = "session.started" };
        Check(buffer.TryEnqueue(first), "First observation rejected");
        await paused.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Check(buffer.IsPaused && buffer.PendingCount == 1 && !buffer.TryEnqueue(first), "Transient failure lost pending event or duplicate was enqueued");
        Check(buffer.TryEnqueue(new() { Key = "session.ended" }) && !buffer.TryEnqueue(new() { Key = "session.started" }), "Capacity was not bounded including retained events");
        online = true;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var deliveries = 0;
        buffer.DeliveryCompleted += _ => { if (Interlocked.Increment(ref deliveries) == 2) completed.TrySetResult(); };
        Check(buffer.RetryPending(), "Eligible explicit retry rejected");
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Check(buffer.PendingCount == 0 && resumed[0] == original[0], "Retry changed original IDs, time, body or lost receipt");
    }

    private static async Task ConsentInvalidationAndPermanentErrors()
    {
        foreach (var code in new[] { "consent_context_expired", "consent_required", "invalid_session", "invalid_credential" })
        {
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var count = 0;
            using var http = Http(async (_, token) => { count++; entered.TrySetResult(); await release.Task.WaitAsync(token); return Error(code, HttpStatusCode.Forbidden, false); });
            using var client = new WorkflowSessionClient(Options(http), Session);
            await using var buffer = client.CreateEventBuffer(capacity: 2);
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            buffer.DeliveryCompleted += _ => done.TrySetResult();
            buffer.TryEnqueue(new() { Key = "session.started" }); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            buffer.TryEnqueue(new() { Key = "session.ended" }); release.TrySetResult();
            await done.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Check(count == 1 && buffer.CollectionInvalidated && buffer.PendingCount == 0 && !buffer.RetryPending() && !buffer.TryEnqueue(new() { Key = "session.started" }), "Retired collection context survived denial");
        }
        var attempts = 0;
        using var permanentHttp = Http((_, _) => Task.FromResult(++attempts == 1 ? Error("event_outside_consent_context", HttpStatusCode.Conflict, false) : Receipt(1, 0)));
        using var permanentClient = new WorkflowSessionClient(Options(permanentHttp), Session);
        await using var permanentBuffer = permanentClient.CreateEventBuffer(capacity: 2);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var received = 0;
        permanentBuffer.DeliveryCompleted += _ => { if (Interlocked.Increment(ref received) == 2) finished.TrySetResult(); };
        permanentBuffer.TryEnqueue(new() { Key = "session.started" }); permanentBuffer.TryEnqueue(new() { Key = "session.ended" });
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Check(attempts == 2 && permanentBuffer.PendingCount == 0 && !permanentBuffer.CollectionInvalidated, "An event-level permanent error retried or poisoned other eligible observations");
        permanentBuffer.InvalidateCollection();
        Check(!permanentBuffer.TryEnqueue(new() { Key = "session.started" }), "Explicit logout/withdrawal did not stop collection");
    }

    private static async Task BackoffMetadata()
    {
        foreach (var header in new[] { "60", DateTimeOffset.UtcNow.AddSeconds(90).ToString("r"), "not-a-delay" })
        {
            var attempts = 0;
            using var http = Http((_, _) => { attempts++; var response = Error("rate_limited", HttpStatusCode.TooManyRequests, true); response.Headers.TryAddWithoutValidation("Retry-After", header); return Task.FromResult(response); });
            using var client = new WorkflowSessionClient(Options(http), Session);
            var result = await client.TrySendReportedEventsAsync(new[] { new WorkflowEvent { Key = "session.started" } }, Guid.NewGuid());
            Check(attempts == 1 && result.Error != null, "Backoff was retried early");
            if (header == "not-a-delay") Check(result.Error is { RetryAfterUnusable: true, Retryable: false }, "Malformed delay became retry permission");
            else Check(result.Error!.RetryAfter > TimeSpan.FromSeconds(55) && result.Error.RetryAt > DateTimeOffset.UtcNow.AddSeconds(55), "Long minimum delay was lost");
        }
        using var limitedHttp = Http((_, _) => { var response = Error("rate_limited", HttpStatusCode.TooManyRequests, true); response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(60)); return Task.FromResult(response); });
        using var limited = new WorkflowSessionClient(Options(limitedHttp), Session);
        await using var buffer = limited.CreateEventBuffer(); var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        buffer.DeliveryCompleted += _ => done.TrySetResult(); buffer.TryEnqueue(new() { Key = "session.started" }); await done.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Check(!buffer.RetryPending() && buffer.RetryAt > DateTimeOffset.UtcNow.AddSeconds(55), "Paused queue discarded the minimum delay");
    }

    private static async Task StaleFeedbackAndPartialReceipt()
    {
        var calls = 0; var revision = new string('a', 64); var operation = new WorkflowFeedbackRequest { Rating = 4, FormRevision = revision, Liked = "Keep this draft" };
        using var http = Http(async (request, token) => {
            calls++; var body = await request.Content!.ReadAsStringAsync(token);
            Check(JsonDocument.Parse(body).RootElement.GetProperty("form_revision").GetString() == revision, "Feedback revision changed");
            return Error("stale_form", HttpStatusCode.Conflict, false);
        });
        using var client = new WorkflowSessionClient(Options(http), Session);
        try { await client.SubmitFeedbackAsync(operation); throw new InvalidOperationException("Stale form accepted"); }
        catch (WorkflowException error) when (error.Code == "stale_form" && !error.Retryable) { }
        Check(calls == 1 && operation.Liked == "Keep this draft" && operation.FormRevision == revision, "Stale form retried or changed the draft");
        ThrowsArgument(() => client.SubmitFeedbackAsync(new() { Rating = 4 }));
        using var partialHttp = Http((_, _) => Task.FromResult(Receipt(1, 0)));
        using var partial = new WorkflowSessionClient(Options(partialHttp), Session);
        var result = await partial.TrySendReportedEventsAsync(new[] { new WorkflowEvent { Key = "session.started" }, new WorkflowEvent { Key = "session.ended" } }, Guid.NewGuid());
        Check(result.Error is { Code: "invalid_response", Retryable: false }, "Partial receipt was reported as atomic batch success");
    }

    private static HttpClient Http(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) => new(new Handler(send));
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token); }
    private static HttpResponseMessage Error(string code, HttpStatusCode status, bool retryable) => Json(new { error = new { code, retryable } }, status);
    private static HttpResponseMessage Receipt(int accepted, int duplicates) => Json(new { accepted, duplicates, trust = "client_reported" });
    private static HttpResponseMessage Json(object body, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void ThrowsArgument(Action callback) { try { callback(); } catch (ArgumentException) { return; } throw new InvalidOperationException("Invalid scoped operation accepted"); }
}
