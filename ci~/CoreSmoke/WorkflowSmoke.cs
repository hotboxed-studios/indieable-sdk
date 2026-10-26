using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using IndieableSdk.DeveloperWorkflow;

internal static class WorkflowSmoke
{
    private const string BackendCredential = "dw_server_example_placeholder";
    private const string SessionCredential = "dw_session_example_placeholder";

    internal static async Task RunAsync()
    {
        await HappyPathAndRetryAsync();
        await DeliveryFailureAsync();
        await CancellationAndDeadlineAsync();
        await ResponseBoundsAsync();
        await LongRetryAfterAsync();
        await MalformedErrorEnvelopesAsync();
        await OwnedTransportRedirectAsync();
        await BufferAsync();
        ValidateInputs();
        Console.WriteLine("Developer-workflow SDK transport, trust, retry, cancellation, bounds, and credential checks passed.");
    }

    private static async Task HappyPathAndRetryAsync()
    {
        var calls = new List<(string Path, string Credential, string Body)>();
        var eventAttempts = 0;
        using var http = new HttpClient(new Handler(async (request, token) =>
        {
            var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(token);
            var path = request.RequestUri!.AbsolutePath;
            var credential = request.Headers.Authorization?.Parameter ?? "";
            calls.Add((path, credential, body));
            return path switch
            {
                "/api/connect/v2/links" => Json("{\"player_ref\":\"gp_example\",\"linked\":true}"),
                "/api/connect/v2/sessions" => Json(JsonSerializer.Serialize(new
                {
                    session_token = SessionCredential,
                    player_ref = "gp_example",
                    expires_at = DateTimeOffset.UtcNow.AddHours(1), gameplay_enabled = true,
                    consent_generation = "9007199254740993", consent_granted_at = DateTimeOffset.UtcNow.AddMinutes(-1),
                    collection_started_at = DateTimeOffset.UtcNow
                })),
                "/api/connect/v2/events" when ++eventAttempts == 1 => Json("{\"error\":{\"code\":\"temporarily_unavailable\",\"retryable\":true}}", HttpStatusCode.ServiceUnavailable),
                "/api/connect/v2/events" => Json("{\"accepted\":1,\"duplicates\":0,\"trust\":\"" + (credential == BackendCredential ? "server_verified" : "client_reported") + "\"}"),
                "/api/connect/v2/feedback" => Json("{\"id\":\"a3c5f0cf-7709-46eb-9842-9687ebebda80\",\"duplicate\":false}"),
                "/api/connect/v2/feedback/config" when request.Method == HttpMethod.Get => Json(JsonSerializer.Serialize(new {
                    game_title = "Synthetic Game", round_id = "a3c5f0cf-7709-46eb-9842-9687ebebda80", build_label = "build-1", focus = "Navigation",
                    survey_questions = new[] { "Which path did you take?" }, form_revision = new string('a', 64), feedback_expires_at = DateTimeOffset.UtcNow.AddDays(7),
                    fields = new { rating = new { min = 1, max = 5, required = true }, liked = new { max_length = 2000 }, confused = new { max_length = 2000 }, answers = new { max_questions = 5, max_length = 1000 } },
                    anonymous_supported = true, legacy_unversioned_supported = true })),
                _ => throw new InvalidOperationException("Unexpected request path.")
            };
        }));
        var options = new WorkflowOptions { BaseUrl = "https://example.test", HttpClient = http, RetryBaseDelay = TimeSpan.Zero };
        using var backend = new WorkflowBackendClient(options, BackendCredential);
        Assert(calls.Count == 0, "Constructing backend client made a request.");
        var proof = new WorkflowLinkProof("dw_link_example_placeholder");
        Assert(!proof.ToString().Contains(proof.Token), "Proof ToString exposed a credential.");
        var linkId = Guid.NewGuid();
        var link = await backend.LinkAsync("opaque-account-42", proof, linkId);
        Assert(link.PlayerRef == "gp_example" && link.Linked, "Link receipt was not mapped.");
        using (var linked = JsonDocument.Parse(calls[0].Body))
        {
            var root = linked.RootElement;
            Assert(root.GetProperty("request_id").GetGuid() == linkId, "Link request ID was not mapped.");
            Assert(root.GetProperty("link_token").GetString() == proof.Token, "Scoped link proof was not mapped.");
            Assert(!root.TryGetProperty("management_token", out _) && !root.TryGetProperty("proof", out _), "Link request exposed a broader management credential.");
            Assert(!root.TryGetProperty("game_id", out _) && !root.TryGetProperty("issuer", out _) && !root.TryGetProperty("email", out _), "Identity request accepted an authority override.");
        }
        var session = await backend.CreateSessionAsync(new WorkflowSessionRequest
        {
            Subject = "opaque-account-42", TelemetryConsent = true, Build = "example-build"
        });
        Assert(session.PlayerRef == link.PlayerRef, "Session player reference was not mapped.");
        Assert(session.ConsentGeneration == "9007199254740993" && session.CollectionStartedAt >= session.ConsentGrantedAt, "Consent context was truncated or omitted.");
        Assert(!session.ToString().Contains(SessionCredential), "Session ToString exposed the token.");
        var requestId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var events = new[] { new WorkflowEvent { Key = "session.ended", EventId = eventId, Properties = new() { DurationSeconds = 125, CharacterRef = "character-2" } } };
        var verified = await backend.TrySendVerifiedEventsAsync(session.SessionToken, events, requestId);
        Assert(verified.Succeeded && verified.Receipt!.Trust == "server_verified", "Verified receipt did not preserve trust.");
        var attempts = calls.Where(c => c.Path.EndsWith("/events")).ToArray();
        Assert(attempts.Length == 2 && attempts[0].Body == attempts[1].Body, "Retry changed idempotency IDs or event body.");
        Assert(attempts.All(c => c.Credential == BackendCredential), "Verified event used a client credential.");
        using (var document = JsonDocument.Parse(attempts[0].Body))
        {
            var root = document.RootElement;
            Assert(root.GetProperty("player_session_token").GetString() == session.SessionToken, "Verified event did not include the session proof.");
            Assert(root.GetProperty("request_id").GetGuid() == requestId, "Retry request ID changed.");
            Assert(root.GetProperty("events")[0].GetProperty("event_id").GetGuid() == eventId, "Retry event ID changed.");
            Assert(root.GetProperty("events")[0].GetProperty("properties").GetProperty("duration_seconds").GetInt32() == 125, "Event property was not serialized.");
        }
        using var client = new WorkflowSessionClient(options, session.SessionToken);
        var config = await client.GetFeedbackConfigAsync();
        Assert(config.SurveyQuestions.SequenceEqual(new[] { "Which path did you take?" }) && config.AnonymousSupported, "Configured feedback fields were not mapped.");
        Assert(calls.Last().Body == "" && calls.Last().Credential == SessionCredential, "Feedback config retrieval had an unexpected body or credential.");
        var reported = await client.SendReportedEventsAsync(events, Guid.NewGuid());
        Assert(reported.Trust == "client_reported", "Reported receipt did not preserve trust.");
        var feedback = await client.SubmitFeedbackAsync(new() { Rating = 4, FormRevision = config.FormRevision, Anonymous = true, Liked = "Clear onboarding\nUseful feedback", Answers = new Dictionary<string, string> { [config.SurveyQuestions[0]] = "The left path" } });
        Assert(feedback.Id != Guid.Empty && !feedback.Duplicate, "Feedback receipt was not mapped.");
        Assert(calls.Last().Credential == SessionCredential && !calls.Last().Body.Contains(BackendCredential), "Feedback carried the backend secret.");
        Assert(calls.Take(2).All(c => c.Credential == BackendCredential), "Link/session backend authentication was absent.");
        // Disposing SDK clients does not dispose the injected host-owned HttpClient.
        backend.Dispose();
        _ = await client.SendReportedEventsAsync(events, Guid.NewGuid());
    }

    private static async Task DeliveryFailureAsync()
    {
        var attempts = 0;
        using var http = new HttpClient(new Handler((_, _) =>
        {
            attempts++;
            throw new HttpRequestException("Expected offline test.");
        }));
        using var backend = new WorkflowBackendClient(new() { BaseUrl = "https://example.test", HttpClient = http, RetryBaseDelay = TimeSpan.Zero }, BackendCredential);
        var result = await backend.TrySendVerifiedEventsAsync(SessionCredential, new[] { new WorkflowEvent { Key = "session.started" } }, Guid.NewGuid());
        Assert(!result.Succeeded && result.Error?.Code == "network_error" && attempts == 3, "Analytics did not fail nonfatally after bounded retries.");
        attempts = 0;
        using var rejectedHttp = new HttpClient(new Handler((_, _) =>
        {
            attempts++;
            return Task.FromResult(Json("{\"error\":{\"code\":\"invalid_credential\",\"message\":\"" + BackendCredential + "\",\"retryable\":false}}", HttpStatusCode.Unauthorized));
        }));
        using var rejected = new WorkflowBackendClient(new() { BaseUrl = "https://example.test", HttpClient = rejectedHttp }, BackendCredential);
        var denial = await rejected.TrySendVerifiedEventsAsync(SessionCredential, new[] { new WorkflowEvent { Key = "session.started" } }, Guid.NewGuid());
        Assert(denial.Error?.Code == "invalid_credential" && attempts == 1, "Permanent denial was retried.");
        Assert(!denial.Error!.ToString().Contains(BackendCredential), "Upstream error body exposed a credential.");
    }

    private static async Task CancellationAndDeadlineAsync()
    {
        using var http = new HttpClient(new Handler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("Unreachable.");
        }));
        using var client = new WorkflowSessionClient(new() { BaseUrl = "https://example.test", HttpClient = http, RequestTimeout = TimeSpan.FromMilliseconds(25) }, SessionCredential);
        var expired = await client.TrySendReportedEventsAsync(new[] { new WorkflowEvent { Key = "session.started" } }, Guid.NewGuid());
        Assert(expired.Error?.Code == "request_timeout", "Total delivery deadline was not enforced.");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            await client.TrySendReportedEventsAsync(new[] { new WorkflowEvent { Key = "session.started" } }, Guid.NewGuid(), cancellation.Token);
            throw new InvalidOperationException("Caller cancellation was swallowed.");
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
    }

    private static async Task ResponseBoundsAsync()
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Json(new string('x', 65537)))));
        using var client = new WorkflowSessionClient(new() { BaseUrl = "https://example.test", HttpClient = http }, SessionCredential);
        var result = await client.TrySendReportedEventsAsync(new[] { new WorkflowEvent { Key = "session.started" } }, Guid.NewGuid());
        Assert(result.Error?.Code == "response_too_large", "Response bound was not enforced.");
    }

    private static async Task BufferAsync()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new Handler(async (_, token) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("Unreachable.");
        }));
        using var client = new WorkflowSessionClient(new() { BaseUrl = "https://example.test", HttpClient = http }, SessionCredential);
        var buffer = client.CreateEventBuffer(capacity: 2);
        Assert(buffer.TryEnqueue(new() { Key = "session.started" }), "Buffer rejected its first event.");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert(buffer.TryEnqueue(new() { Key = "session.ended" }), "Buffer rejected an available slot.");
        Assert(!buffer.TryEnqueue(new() { Key = "session.ended" }), "Full buffer did not drop without blocking.");
        await buffer.DisposeAsync();
        Assert(!buffer.TryEnqueue(new() { Key = "session.started" }), "Disposed buffer accepted analytics.");
    }

    private static async Task LongRetryAfterAsync()
    {
        var attempts = 0;
        using var http = new HttpClient(new Handler((_, _) =>
        {
            attempts++;
            var response = Json("{\"error\":{\"code\":\"rate_limited\",\"retryable\":true}}", HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(60));
            return Task.FromResult(response);
        }));
        using var client = new WorkflowSessionClient(new() { BaseUrl = "https://example.test", HttpClient = http }, SessionCredential);
        var delivery = await client.TrySendReportedEventsAsync(new[] { new WorkflowEvent { Key = "session.started" } }, Guid.NewGuid());
        Assert(delivery.Error is { Code: "rate_limited", Retryable: true } && attempts == 1, "The SDK retried earlier than the server's Retry-After.");
        Assert(delivery.Error!.RetryAfter == TimeSpan.FromSeconds(60) && delivery.Error.RetryAt > DateTimeOffset.UtcNow.AddSeconds(55), "Long backoff timing was lost before reaching the host.");
    }

    private static async Task MalformedErrorEnvelopesAsync()
    {
        foreach (var body in new[] { "[]", "null", "\"error\"", "{\"error\":[]}" })
        {
            using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Json(body, HttpStatusCode.InternalServerError))));
            using var client = new WorkflowSessionClient(new() { BaseUrl = "https://example.test", HttpClient = http, MaxRetries = 0 }, SessionCredential);
            var result = await client.TrySendReportedEventsAsync(new[] { new WorkflowEvent { Key = "session.started" } }, Guid.NewGuid());
            Assert(result.Error is { Code: "request_failed", Retryable: true, StatusCode: 500 }, "A malformed error envelope escaped the nonfatal delivery contract.");
        }
    }

    private static async Task OwnedTransportRedirectAsync()
    {
        var origin = new TcpListener(IPAddress.Loopback, 0);
        var destination = new TcpListener(IPAddress.Loopback, 0);
        origin.Start();
        destination.Start();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            var originPort = ((IPEndPoint)origin.LocalEndpoint).Port;
            var destinationPort = ((IPEndPoint)destination.LocalEndpoint).Port;
            var responder = Task.Run(async () =>
            {
                using var connection = await origin.AcceptTcpClientAsync(deadline.Token);
                await using var stream = connection.GetStream();
                var request = new StringBuilder();
                var buffer = new byte[2048];
                while (!request.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
                {
                    var read = await stream.ReadAsync(buffer, deadline.Token);
                    if (read == 0 || request.Length + read > 8192) throw new InvalidOperationException("Invalid local redirect test request.");
                    request.Append(Encoding.ASCII.GetString(buffer, 0, read));
                }
                var response = Encoding.ASCII.GetBytes($"HTTP/1.1 302 Found\r\nLocation: http://127.0.0.1:{destinationPort}/capture\r\nContent-Type: application/json\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{{}}");
                await stream.WriteAsync(response, deadline.Token);
            }, deadline.Token);
            using var client = new WorkflowSessionClient(new()
            {
                BaseUrl = $"http://127.0.0.1:{originPort}", RequestTimeout = TimeSpan.FromSeconds(2)
            }, SessionCredential);
            var result = await client.TrySendReportedEventsAsync(new[] { new WorkflowEvent { Key = "session.started" } }, Guid.NewGuid(), deadline.Token);
            await responder;
            Assert(result.Error?.StatusCode == 302 && !destination.Pending(), "The SDK-owned transport followed a cross-origin redirect.");
        }
        finally
        {
            origin.Stop();
            destination.Stop();
        }
    }

    private static void ValidateInputs()
    {
        foreach (var url in new[] { "http://example.test", "https://user:secret@example.test", "https://example.test/path", "https://example.test?token=secret" })
            ThrowsArgument(() => new WorkflowBackendClient(new() { BaseUrl = url }, BackendCredential));
        ThrowsArgument(() => new WorkflowBackendClient(new() { MaxRetries = 100 }, BackendCredential));
        ThrowsArgument(() => new WorkflowSessionClient(new(), BackendCredential));
        ThrowsArgument(() => new WorkflowLinkProof("bad\ncredential"));
        ThrowsArgument(() => new WorkflowLinkProof("dw_management_example_placeholder"));
        using var client = new WorkflowSessionClient(new(), SessionCredential);
        ThrowsArgument(() => client.SendReportedEventsAsync(Enumerable.Range(0, 51).Select(_ => new WorkflowEvent { Key = "session.started" }), Guid.NewGuid()));
        ThrowsArgument(() => client.SendReportedEventsAsync(new[] { new WorkflowEvent { Key = "arbitrary.workbench" } }, Guid.NewGuid()));
        ThrowsArgument(() => client.SendReportedEventsAsync(new[] { new WorkflowEvent { Key = "session.started", Properties = new() { DurationSeconds = 10 } } }, Guid.NewGuid()));
        ThrowsArgument(() => client.SubmitFeedbackAsync(new() { Rating = 6 }));
        ThrowsArgument(() => client.SubmitFeedbackAsync(new() { Rating = 4, Answers = new Dictionary<string, string> { ["question"] = new string('x', 1001) } }));
    }

    private static void ThrowsArgument(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("Invalid SDK input was accepted.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => callback(request, cancellationToken);
    }
}
