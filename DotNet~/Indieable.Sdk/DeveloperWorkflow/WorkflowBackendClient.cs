namespace IndieableSdk.DeveloperWorkflow;

/// <summary>
/// Trusted game-backend integration for the developer-workflow v2 API.
/// This type is not included in the Unity package. Never put its credential in a game binary, browser, or log.
/// Authenticate the game's account before calling it. Gameplay delivery additionally requires the player's current telemetry choice.
/// </summary>
public sealed class WorkflowBackendClient : IDisposable
{
    private readonly WorkflowTransport _transport;
    private readonly string _credential;

    public WorkflowBackendClient(WorkflowOptions options, string serverCredential)
    {
        _credential = WorkflowValidation.Token(serverCredential, nameof(serverCredential));
        if (!_credential.StartsWith("dw_server_", StringComparison.Ordinal))
            throw new ArgumentException("A developer-workflow backend credential is required.", nameof(serverCredential));
        _transport = new WorkflowTransport(options);
    }

    /// <summary>
    /// Explicitly links an authenticated game account using a short-lived player-minted link proof.
    /// The issuer and game come from the backend credential. Email never serves as an identity key.
    /// Use the same requestId for retries; a proof cannot authorize another link operation.
    /// </summary>
    public async Task<WorkflowLinkReceipt> LinkAsync(
        string subject, WorkflowLinkProof proof, Guid requestId, CancellationToken cancellationToken = default)
    {
        WorkflowValidation.Opaque(subject, nameof(subject), 200);
        ArgumentNullException.ThrowIfNull(proof);
        WorkflowValidation.RequestId(requestId);
        var result = await _transport.PostAsync<WorkflowLinkReceipt>("api/connect/v2/links", new
        {
            subject,
            link_token = proof.Token,
            request_id = requestId
        }, _credential, cancellationToken).ConfigureAwait(false);
        if (!result.Linked || result.PlayerRef?.StartsWith("gp_", StringComparison.Ordinal) != true)
            throw new WorkflowException("invalid_response", 0, false);
        return result;
    }

    /// <summary>
    /// Creates a short-lived session for a previously linked account, including feedback-only sessions.
    /// GameplayEnabled reflects the independently checked stored gameplay choice; linking never grants telemetry or marketing consent.
    /// </summary>
    public async Task<WorkflowSession> CreateSessionAsync(
        WorkflowSessionRequest input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        WorkflowValidation.RequestId(input.RequestId);
        WorkflowValidation.Opaque(input.Subject, nameof(input.Subject), 200);
        if (input.RoundId == Guid.Empty) throw new ArgumentException("RoundId must be nonempty when supplied.", nameof(input));
        if (input.Build != null) WorkflowValidation.Text(input.Build, nameof(input.Build), 80, allowEmpty: true);
        var session = await _transport.PostAsync<WorkflowSession>("api/connect/v2/sessions", input, _credential, cancellationToken).ConfigureAwait(false);
        if (session.SessionToken?.StartsWith("dw_session_", StringComparison.Ordinal) != true ||
            session.PlayerRef?.StartsWith("gp_", StringComparison.Ordinal) != true || session.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new WorkflowException("invalid_response", 0, false);
        return session;
    }

    /// <summary>Submit only observations the trusted game backend actually verified. Never relay arbitrary client events through this method.</summary>
    public Task<WorkflowEventReceipt> SendVerifiedEventsAsync(
        string playerSessionToken, IEnumerable<WorkflowEvent> events, Guid requestId, CancellationToken cancellationToken = default)
    {
        WorkflowValidation.Token(playerSessionToken, nameof(playerSessionToken));
        WorkflowValidation.RequestId(requestId);
        var batch = WorkflowValidation.Events(events);
        return WorkflowReceipts.EventsAsync(_transport.PostAsync<WorkflowEventReceipt>("api/connect/v2/events", new
        {
            request_id = requestId,
            player_session_token = playerSessionToken,
            events = batch
        }, _credential, cancellationToken), batch.Length, "server_verified");
    }

    /// <summary>Bounded, nonthrowing delivery for optional analytics. Explicit caller cancellation and programming errors still propagate.</summary>
    public async Task<WorkflowDelivery<WorkflowEventReceipt>> TrySendVerifiedEventsAsync(
        string playerSessionToken, IEnumerable<WorkflowEvent> events, Guid requestId, CancellationToken cancellationToken = default)
    {
        try
        {
            return new(await SendVerifiedEventsAsync(playerSessionToken, events, requestId, cancellationToken).ConfigureAwait(false), null);
        }
        catch (WorkflowException error) { return new(null, error); }
    }

    /// <summary>Optional bounded, in-memory delivery. Overflow/offline failures drop analytics without blocking gameplay.</summary>
    public WorkflowEventBuffer CreateEventBuffer(string playerSessionToken, int capacity = 128)
    {
        WorkflowValidation.Token(playerSessionToken, nameof(playerSessionToken));
        return new((events, requestId, token) => TrySendVerifiedEventsAsync(playerSessionToken, events, requestId, token), capacity);
    }

    public void Dispose() => _transport.Dispose();
}
