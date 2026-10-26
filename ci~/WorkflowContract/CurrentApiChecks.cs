using System.Net;
using System.Text;
using System.Text.Json;
using IndieableSdk.DeveloperWorkflow;

// Local harness only. Optional first-party player control is deliberately
// separate from the backend fixture and from every SDK runtime type/method.
internal static class CurrentApiChecks
{
    internal static async Task<string[]> RunAsync(WorkflowOptions options, WorkflowSession session,
        WorkflowBackendClient backend, WorkflowSessionRequest originalRequest, string? controlPath)
    {
        var checks = new List<string>();
        using var native = new WorkflowSessionClient(options, session.SessionToken);
        var known = new WorkflowEvent { Key = "session.started" };
        var rejected = await native.TrySendReportedEventsAsync(new[] { known,
            new WorkflowEvent { Key = "milestone.unknown_sdk_" + Guid.NewGuid().ToString("N") } }, Guid.NewGuid());
        Check(rejected.Error is { Retryable: false } && rejected.Error.StatusCode is >= 400 and < 500, "Unknown milestone did not reject batch");
        Check((await native.SendReportedEventsAsync(new[] { known }, Guid.NewGuid())).Accepted == 1, "Rejected batch partially committed an event");
        checks.Add("atomic_rejected_batch_no_partial_commit");

        var failedAfterCommit = false;
        using var timeoutHttp = new HttpClient(new BoundaryHandler(async (request, next, token) => {
            var response = await next(request, token);
            if (response.IsSuccessStatusCode && !failedAfterCommit) {
                failedAfterCommit = true; response.Dispose();
                throw new TaskCanceledException("Synthetic response timeout after real local commit");
            }
            return response;
        }));
        using var timeoutClient = new WorkflowSessionClient(Copy(options, timeoutHttp), session.SessionToken);
        var observation = new WorkflowEvent { Key = "session.started" }; var requestId = Guid.NewGuid();
        var uncertain = await timeoutClient.TrySendReportedEventsAsync(new[] { observation }, requestId);
        Check(uncertain.Error is { Code: "request_timeout", Retryable: true }, "Post-commit timeout was not uncertain");
        Check((await timeoutClient.SendReportedEventsAsync(new[] { observation }, requestId)).Duplicates == 1, "Post-commit retry did not deduplicate");
        checks.Add("injected_timeout_after_actual_commit_stable_retry");

        var form = await native.GetFeedbackConfigAsync();
        var stale = (form.FormRevision[0] == 'a' ? "b" : "a") + form.FormRevision[1..];
        var staleDraft = new WorkflowFeedbackRequest { Rating = 4, FormRevision = stale, Liked = "Synthetic preserved stale-form draft" };
        try { await native.SubmitFeedbackAsync(staleDraft); throw new InvalidOperationException("Stale revision accepted"); }
        catch (WorkflowException error) when (error.Code == "stale_form" && !error.Retryable) { }
        var reviewed = await native.GetFeedbackConfigAsync();
        Check((await native.SubmitFeedbackAsync(new() { Rating = staleDraft.Rating, Liked = staleDraft.Liked, FormRevision = reviewed.FormRevision })).Id != Guid.Empty, "Reviewed draft did not submit");
        checks.Add("stale_revision_rejected_draft_explicitly_reviewed");

        if (controlPath != null)
        {
            await ConsentAsync(controlPath, options, session, backend, originalRequest);
            checks.Add("separate_simulated_player_withdraw_regrant_old_queue_discard");
            checks.Add("fresh_context_rejects_old_capture_time");
            checks.Add("feedback_survives_collection_invalidation");
        }
        return checks.ToArray();
    }

    private static async Task ConsentAsync(string path, WorkflowOptions options, WorkflowSession old,
        WorkflowBackendClient backend, WorkflowSessionRequest originalRequest)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("Synthetic player-control path must be absolute");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path)); var root = document.RootElement;
        var origin = new Uri(options.BaseUrl);
        if (!origin.IsLoopback || !root.GetProperty("synthetic").GetBoolean() || new Uri(root.GetProperty("base_url").GetString()!) != origin ||
            root.GetProperty("expected_player_ref").GetString() != old.PlayerRef)
            throw new ArgumentException("Player controls must select this same synthetic local player");
        var proof = root.GetProperty("management_token").GetString()!; var purposes = root.GetProperty("purposes");
        if (!proof.StartsWith("dw_manage_", StringComparison.Ordinal) || proof.Length > 200 || !purposes.GetProperty("gameplay").GetBoolean())
            throw new ArgumentException("Synthetic control must start with explicit gameplay permission");
        using var playerHttp = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
        async Task SetGameplay(bool gameplay)
        {
            var body = JsonSerializer.Serialize(new { management_token = proof, purposes = new {
                updates = purposes.GetProperty("updates").GetBoolean(), playtests = purposes.GetProperty("playtests").GetBoolean(), gameplay } });
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(origin, "api/audience/v1/preferences")) {
                Content = new StringContent(body, Encoding.UTF8, "application/json") };
            request.Headers.Add("Origin", origin.GetLeftPart(UriPartial.Authority));
            using var response = await playerHttp.SendAsync(request);
            Check(response.IsSuccessStatusCode, "Synthetic player preference operation failed");
        }
        var intercepted = false;
        using var queueHttp = new HttpClient(new BoundaryHandler(async (request, next, token) => {
            if (!intercepted) {
                intercepted = true;
                var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) {
                    Content = new StringContent("{\"error\":{\"code\":\"unavailable\",\"retryable\":true}}", Encoding.UTF8, "application/json") };
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero); return response;
            }
            return await next(request, token);
        }));
        using var client = new WorkflowSessionClient(Copy(options, queueHttp), old.SessionToken);
        await using var buffer = client.CreateEventBuffer(capacity: 2);
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var invalidated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        buffer.DeliveryCompleted += result => { if (result.Error?.InvalidatesCollectionContext == true) invalidated.TrySetResult(); else if (result.Error?.Retryable == true) paused.TrySetResult(); };
        var beforeWithdrawal = new WorkflowEvent { Key = "session.started" };
        Check(buffer.TryEnqueue(beforeWithdrawal), "Old collection enqueue failed"); await paused.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Check(buffer.IsPaused && buffer.PendingCount == 1, "Injected unavailable result lost original event");
        await SetGameplay(false); await SetGameplay(true);
        try { await backend.CreateSessionAsync(originalRequest); throw new InvalidOperationException("Old session request acquired a new grant"); }
        catch (WorkflowException error) when (error.Code == "consent_context_expired" && !error.Retryable) { }
        Check(buffer.RetryPending(), "Old-context probe could not resume exact pending event");
        await invalidated.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Check(buffer.CollectionInvalidated && buffer.PendingCount == 0 && !buffer.TryEnqueue(new() { Key = "session.started" }), "Old buffer survived withdrawal/regrant");
        var fresh = await backend.CreateSessionAsync(new() { Subject = originalRequest.Subject, TelemetryConsent = true,
            RoundId = originalRequest.RoundId, Build = "sdk-fresh-consent-context" });
        Check(fresh.ConsentGeneration != old.ConsentGeneration, "Regrant reused old generation");
        using var freshClient = new WorkflowSessionClient(options, fresh.SessionToken);
        var stale = await freshClient.TrySendReportedEventsAsync(new[] { beforeWithdrawal }, Guid.NewGuid());
        Check(stale.Error is { Code: "event_outside_consent_context", Retryable: false }, "Fresh session accepted old capture time");
        Check((await freshClient.SendReportedEventsAsync(new[] { new WorkflowEvent { Key = "session.started" } }, Guid.NewGuid())).Accepted == 1, "Fresh collection failed");
        var form = await client.GetFeedbackConfigAsync();
        Check((await client.SubmitFeedbackAsync(new() { Rating = 4, FormRevision = form.FormRevision,
            Liked = "Synthetic feedback after collection invalidation" })).Id != Guid.Empty, "Collection invalidation disabled independent feedback");
    }

    private static WorkflowOptions Copy(WorkflowOptions source, HttpClient http) => new() {
        BaseUrl = source.BaseUrl, HttpClient = http, MaxRetries = 0, RequestTimeout = source.RequestTimeout };
    private static void Check(bool condition, string check) { if (!condition) throw new InvalidOperationException(check); }
    private sealed class BoundaryHandler(Func<HttpRequestMessage, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>, CancellationToken, Task<HttpResponseMessage>> intercept)
        : DelegatingHandler(new HttpClientHandler { AllowAutoRedirect = false })
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => intercept(request, (message, cancellation) => base.SendAsync(message, cancellation), token);
    }
}
