using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using IndieableSdk.DeveloperWorkflow;

// This harness deliberately permits loopback only. Fixture credentials are read from an ignored local file and never printed.
try
{
    if (args.Length != 1) throw new ArgumentException("Usage: WorkflowContract <absolute-loopback-fixture-config.json>");
    using var config = JsonDocument.Parse(await File.ReadAllTextAsync(args[0]));
    var root = config.RootElement;
    string Required(string key) => root.GetProperty(key).GetString() ?? throw new ArgumentException("Missing configuration field: " + key);
    var origin = new Uri(Required("base_url"));
    if (!origin.IsLoopback) throw new ArgumentException("The live contract harness only accepts a loopback fixture server.");
    var options = new WorkflowOptions { BaseUrl = origin.ToString(), RequestTimeout = TimeSpan.FromSeconds(30) };
    var subject = Required("subject");
    var proof = new WorkflowLinkProof(Required("audience_link_token"));
    var roundId = Guid.Parse(Required("round_id"));
    using var backend = new WorkflowBackendClient(options, Required("server_credential"));
    // Keep the link operation stable if a later check fails and this local harness is rerun.
    // Normal hosts persist their own operation ID; this derivation is only fixture bookkeeping.
    var linkRequestId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(subject + ":" + proof.Token)).AsSpan(0, 16));
    var link = await backend.LinkAsync(subject, proof, linkRequestId);
    var repeatedLink = await backend.LinkAsync(subject, proof, linkRequestId);
    Check(link.Linked && repeatedLink.PlayerRef == link.PlayerRef, "Link idempotency");

    var sessionRequest = new WorkflowSessionRequest
    {
        Subject = subject, TelemetryConsent = true, RoundId = roundId, Build = "sdk-synthetic-contract"
    };
    var session = await backend.CreateSessionAsync(sessionRequest);
    Check(session.GameplayEnabled, "Stored gameplay permission");
    Check(session.PlayerRef == link.PlayerRef, "Session scoped player");
    var observations = new List<WorkflowEvent>
    {
        new() { Key = "session.started", OccurredAt = DateTimeOffset.UtcNow.AddSeconds(-90), Properties = new() { InstallationRef = "synthetic-installation", CharacterRef = "synthetic-character" } },
        new() { Key = "session.ended", Properties = new() { DurationSeconds = 90 } }
    };
    if (root.TryGetProperty("milestone_key", out var milestone) && !string.IsNullOrWhiteSpace(milestone.GetString()))
        observations.Add(new() { Key = "milestone." + milestone.GetString() });
    var requestId = Guid.NewGuid();
    var events = await backend.SendVerifiedEventsAsync(session.SessionToken, observations, requestId);
    var repeatedEvents = await backend.SendVerifiedEventsAsync(session.SessionToken, observations, requestId);
    Check(events.Trust == "server_verified" && events.Accepted == observations.Count, "Verified ingestion");
    Check(repeatedEvents.Duplicates == observations.Count && repeatedEvents.Accepted == 0, "Duplicate ingestion");
    // Different request ID with the same event IDs must remain duplicate-safe too.
    var repeatedIds = await backend.SendVerifiedEventsAsync(session.SessionToken, observations, Guid.NewGuid());
    Check(repeatedIds.Duplicates == observations.Count && repeatedIds.Accepted == 0, "Event ID idempotency");

    using var client = new WorkflowSessionClient(options, session.SessionToken);
    var feedbackConfig = await client.GetFeedbackConfigAsync();
    Check(feedbackConfig.RoundId == roundId && feedbackConfig.Fields.Rating.Min == 1 && feedbackConfig.Fields.Rating.Max == 5, "Configured round feedback form");
    var reported = await client.SendReportedEventsAsync(new[] { new WorkflowEvent { Key = "session.started" } }, Guid.NewGuid());
    Check(reported.Trust == "client_reported" && reported.Accepted == 1, "Client reported trust");
    var feedbackRequest = new WorkflowFeedbackRequest
    {
        Rating = 4, Liked = "Synthetic SDK contract feedback", Confused = "Synthetic next-test question", RoundId = roundId,
        Answers = feedbackConfig.SurveyQuestions.Distinct(StringComparer.Ordinal).ToDictionary(question => question, _ => "Synthetic configured answer")
    };
    var feedback = await client.SubmitFeedbackAsync(feedbackRequest);
    var repeatedFeedback = await client.SubmitFeedbackAsync(feedbackRequest);
    Check(feedback.Id != Guid.Empty && repeatedFeedback.Id == feedback.Id && repeatedFeedback.Duplicate, "Feedback idempotency");
    var anonymous = await client.SubmitFeedbackAsync(new() { Rating = 3, Anonymous = true, RoundId = roundId, Liked = "Synthetic anonymous SDK feedback" });
    Check(anonymous.Id != Guid.Empty, "Anonymous feedback");

    var narrowSession = await backend.CreateSessionAsync(new()
    {
        Subject = subject, TelemetryConsent = false, RoundId = roundId, Build = "sdk-feedback-only"
    });
    Check(!narrowSession.GameplayEnabled, "Feedback-only session");
    using var narrowClient = new WorkflowSessionClient(options, narrowSession.SessionToken);
    Check((await narrowClient.SubmitFeedbackAsync(new() { Rating = 4, RoundId = roundId, Liked = "Feedback without optional telemetry" })).Id != Guid.Empty, "No-telemetry feedback");
    var narrowed = await narrowClient.TrySendReportedEventsAsync(new[] { new WorkflowEvent { Key = "session.started" } }, Guid.NewGuid());
    Check(!narrowed.Succeeded && narrowed.Error?.StatusCode is >= 400 and < 500, "Feedback-only telemetry denied");

    using var invalid = new WorkflowBackendClient(options, "dw_server_example_invalid_placeholder");
    var denied = await invalid.TrySendVerifiedEventsAsync(session.SessionToken, observations, Guid.NewGuid());
    Check(denied.Error?.StatusCode == 401 && !denied.Error.Retryable, "Invalid credential");
    var crossOwnerChecked = false;
    if (root.TryGetProperty("other_server_credential", out var other) && !string.IsNullOrWhiteSpace(other.GetString()))
    {
        using var otherBackend = new WorkflowBackendClient(options, other.GetString()!);
        try
        {
            await otherBackend.LinkAsync(subject, proof, Guid.NewGuid());
            throw new InvalidOperationException("Cross-owner possession proof was accepted.");
        }
        catch (WorkflowException error) when (error.StatusCode is >= 400 and < 500) { crossOwnerChecked = true; }
        var crossSession = await otherBackend.TrySendVerifiedEventsAsync(session.SessionToken, observations, Guid.NewGuid());
        Check(!crossSession.Succeeded && crossSession.Error?.StatusCode is >= 400 and < 500, "Cross-owner session denied");
    }

    Console.WriteLine(JsonSerializer.Serialize(new
    {
        suite = "sdk_live_developer_workflow_contract", passed = true,
        verified_events = observations.Count, duplicate_events = repeatedEvents.Duplicates,
        client_reported_events = 1, feedback_submissions = 3,
        checks = new[] { "link", "session", "server_verified", "client_reported", "request_retries", "event_duplicates", "feedback_config", "configured_answers", "feedback_duplicates", "anonymous_feedback", "feedback_without_telemetry", "invalid_credential" },
        cross_owner_checked = crossOwnerChecked
    }));
    return 0;
}
catch (WorkflowException error)
{
    Console.Error.WriteLine($"SDK live contract failed: {error.Code} (HTTP {error.StatusCode}, retryable {error.Retryable}).");
    return 1;
}
catch (Exception error)
{
    // Never print the fixture document, request body, or full exception with local credentials.
    Console.Error.WriteLine("SDK live contract failed: " + error.GetType().Name + ": " + error.Message);
    return 1;
}

static void Check(bool condition, string check)
{
    if (!condition) throw new InvalidOperationException(check + " did not satisfy the v2 contract.");
}
