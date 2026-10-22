namespace IndieableSdk.DeveloperWorkflow;

/// <summary>
/// A client scoped to one short-lived game session. Holds no backend credential and persists no identity.
/// A session credential can produce client-reported activity and feedback only.
/// </summary>
public sealed class WorkflowSessionClient : IDisposable
{
    private readonly WorkflowTransport _transport;
    private readonly string _sessionToken;

    public WorkflowSessionClient(WorkflowOptions options, string playerSessionToken)
    {
        _sessionToken = WorkflowValidation.Token(playerSessionToken, nameof(playerSessionToken));
        if (!_sessionToken.StartsWith("dw_session_", StringComparison.Ordinal))
            throw new ArgumentException("A short-lived developer-workflow session credential is required.", nameof(playerSessionToken));
        _transport = new WorkflowTransport(options);
    }

    public Task<WorkflowEventReceipt> SendReportedEventsAsync(
        IEnumerable<WorkflowEvent> events, Guid requestId, CancellationToken cancellationToken = default)
    {
        WorkflowValidation.RequestId(requestId);
        var batch = WorkflowValidation.Events(events);
        return WorkflowReceipts.EventsAsync(_transport.PostAsync<WorkflowEventReceipt>("api/connect/v2/events", new
        {
            request_id = requestId,
            events = batch
        }, _sessionToken, cancellationToken), batch.Length, "client_reported");
    }

    public async Task<WorkflowDelivery<WorkflowEventReceipt>> TrySendReportedEventsAsync(
        IEnumerable<WorkflowEvent> events, Guid requestId, CancellationToken cancellationToken = default)
    {
        try
        {
            return new(await SendReportedEventsAsync(events, requestId, cancellationToken).ConfigureAwait(false), null);
        }
        catch (WorkflowException error) { return new(null, error); }
    }

    /// <summary>Optional bounded, in-memory delivery. Overflow/offline failures drop analytics without blocking gameplay.</summary>
    public WorkflowEventBuffer CreateEventBuffer(int capacity = 128) => new(TrySendReportedEventsAsync, capacity);

    /// <summary>Fetches the configured feedback fields and developer questions for this session's pinned playtest round.</summary>
    public async Task<WorkflowFeedbackConfig> GetFeedbackConfigAsync(CancellationToken cancellationToken = default)
    {
        var config = await _transport.GetAsync<WorkflowFeedbackConfig>("api/connect/v2/feedback/config", _sessionToken, cancellationToken).ConfigureAwait(false);
        if (config.RoundId == Guid.Empty || string.IsNullOrWhiteSpace(config.GameTitle) ||
            config.SurveyQuestions == null || config.SurveyQuestions.Length > 5 ||
            config.SurveyQuestions.Any(question => string.IsNullOrWhiteSpace(question) || question.Length > 200) ||
            config.Fields?.Rating is not { Min: 1, Max: 5, Required: true } ||
            config.Fields.Liked?.MaxLength != 2000 || config.Fields.Confused?.MaxLength != 2000 ||
            config.Fields.Answers is not { MaxQuestions: 5, MaxLength: 1000 })
            throw new WorkflowException("invalid_response", 0, false);
        return config;
    }

    public Task<WorkflowFeedbackReceipt> SubmitFeedbackAsync(
        WorkflowFeedbackRequest input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        WorkflowValidation.RequestId(input.RequestId);
        if (input.Rating is < 1 or > 5) throw new ArgumentException("Rating must be between one and five.", nameof(input));
        if (input.Liked?.Length > 2000 || input.Confused?.Length > 2000)
            throw new ArgumentException("Feedback fields must contain at most 2000 characters.", nameof(input));
        if (input.RoundId == Guid.Empty) throw new ArgumentException("RoundId must be nonempty when supplied.", nameof(input));
        if (input.Answers is { } answers && (answers.Count > 5 || answers.Any(answer =>
                string.IsNullOrWhiteSpace(answer.Key) || answer.Key.Length > 200 || answer.Value == null || answer.Value.Length > 1000)))
            throw new ArgumentException("Answer at most five configured questions, with question keys up to 200 characters and values up to 1000 characters.", nameof(input));
        return WorkflowReceipts.FeedbackAsync(_transport.PostAsync<WorkflowFeedbackReceipt>("api/connect/v2/feedback", input, _sessionToken, cancellationToken));
    }

    public void Dispose() => _transport.Dispose();
}
