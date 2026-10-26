namespace IndieableSdk.DeveloperWorkflow;

/// <summary>HTTP policy for the developer-workflow v2 contract. No request is made by construction.</summary>
public sealed class WorkflowOptions
{
    public string BaseUrl { get; set; } = "https://indieable.com";
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public int MaxRetries { get; set; } = 2;
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(100);
    /// <summary>
    /// Optional host-owned client; it is never disposed by this SDK.
    /// Its handler must disable automatic redirects. The SDK cannot control a supplied handler's redirect behavior.
    /// </summary>
    public HttpClient? HttpClient { get; set; }
}

/// <summary>
/// A short-lived, one-use link proof explicitly minted by the player. It has no consent-management authority.
/// Never give a game backend the original audience management or personal feedback token.
/// </summary>
public sealed class WorkflowLinkProof
{
    public string Token { get; }

    public WorkflowLinkProof(string linkToken)
    {
        Token = WorkflowValidation.Token(linkToken, nameof(linkToken));
        if (!Token.StartsWith("dw_link_", StringComparison.Ordinal))
            throw new ArgumentException("A short-lived player-minted link proof is required.", nameof(linkToken));
    }

    public override string ToString() => "WorkflowLinkProof([redacted])";
}

public sealed class WorkflowLinkReceipt
{
    public string PlayerRef { get; init; } = "";
    public bool Linked { get; init; }
}

/// <summary>A short-lived, game-scoped credential. Return only this credential to the authenticated game client.</summary>
public sealed class WorkflowSession
{
    public string SessionToken { get; init; } = "";
    public string PlayerRef { get; init; } = "";
    public DateTimeOffset ExpiresAt { get; init; }
    public bool GameplayEnabled { get; init; }
    /// <summary>Opaque server consent generation. Never infer or manufacture a grant from this value.</summary>
    public string? ConsentGeneration { get; init; }
    public DateTimeOffset? ConsentGrantedAt { get; init; }
    public DateTimeOffset? CollectionStartedAt { get; init; }
    public override string ToString() => $"WorkflowSession({PlayerRef}, expires {ExpiresAt:O}, [redacted])";
}

public sealed class WorkflowSessionRequest
{
    public Guid RequestId { get; init; } = Guid.NewGuid();
    /// <summary>The issuer-local account identifier the game backend has authenticated; never an email or local save slot.</summary>
    public required string Subject { get; init; }
    /// <summary>
    /// Asserts the current game-side telemetry choice; false narrows this session to non-telemetry features.
    /// True never grants permission: Indieable independently checks the player's stored gameplay choice.
    /// </summary>
    public bool TelemetryConsent { get; init; }
    public Guid? RoundId { get; init; }
    public string? Build { get; init; }
}

/// <summary>Only the bounded, documented v2 event properties. Installation and character references are context, not accounts.</summary>
public sealed class WorkflowEventProperties
{
    public int? DurationSeconds { get; init; }
    public string? InstallationRef { get; init; }
    public string? CharacterRef { get; init; }
}

public sealed class WorkflowEvent
{
    /// <summary>Keep the same ID when retrying an observation, including after restarting the host.</summary>
    public Guid EventId { get; init; } = Guid.NewGuid();
    public required string Key { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public WorkflowEventProperties? Properties { get; init; }
}

public sealed class WorkflowEventReceipt
{
    public int Accepted { get; init; }
    public int Duplicates { get; init; }
    /// <summary>server_verified or client_reported. A client credential can never produce verified play.</summary>
    public string Trust { get; init; } = "";
}

public sealed class WorkflowFeedbackRequest
{
    public Guid RequestId { get; init; } = Guid.NewGuid();
    public int Rating { get; init; }
    /// <summary>Return the exact revision fetched before the player reviewed the configured form.</summary>
    public string FormRevision { get; init; } = "";
    public string? Liked { get; init; }
    public string? Confused { get; init; }
    /// <summary>Anonymous feedback is never silently attached to the player's history.</summary>
    public bool Anonymous { get; init; }
    public Guid? RoundId { get; init; }
    /// <summary>Answers keyed by the current configuration's developer-defined question text; the server validates the known questions.</summary>
    public IReadOnlyDictionary<string, string>? Answers { get; init; }
}

/// <summary>The game's configured round feedback form; render these questions instead of duplicating them in game code.</summary>
public sealed class WorkflowFeedbackConfig
{
    public string GameTitle { get; init; } = "";
    public Guid RoundId { get; init; }
    public string? BuildLabel { get; init; }
    public string? Focus { get; init; }
    public string[] SurveyQuestions { get; init; } = Array.Empty<string>();
    public WorkflowFeedbackFields Fields { get; init; } = new();
    public bool AnonymousSupported { get; init; }
    public string FormRevision { get; init; } = "";
    public DateTimeOffset? FeedbackExpiresAt { get; init; }
    public bool LegacyUnversionedSupported { get; init; }
}

public sealed class WorkflowFeedbackFields
{
    public WorkflowRatingField Rating { get; init; } = new();
    public WorkflowTextField Liked { get; init; } = new();
    public WorkflowTextField Confused { get; init; } = new();
    public WorkflowAnswersField Answers { get; init; } = new();
}

public sealed class WorkflowRatingField
{
    public int Min { get; init; }
    public int Max { get; init; }
    public bool Required { get; init; }
}

public sealed class WorkflowTextField
{
    public int MaxLength { get; init; }
}

public sealed class WorkflowAnswersField
{
    public int MaxQuestions { get; init; }
    public int MaxLength { get; init; }
}

public sealed class WorkflowFeedbackReceipt
{
    public Guid Id { get; init; }
    public bool Duplicate { get; init; }
}

/// <summary>Bounded transport or API failure. The exception never includes the request body or credential.</summary>
public sealed class WorkflowException : Exception
{
    public string Code { get; }
    public int StatusCode { get; }
    public bool Retryable { get; }
    /// <summary>Server's minimum delay, including long backoffs returned to the host without sleeping.</summary>
    public TimeSpan? RetryAfter { get; }
    public DateTimeOffset? RetryAt { get; }
    /// <summary>An invalid/unbounded Retry-After must not become permission to retry early.</summary>
    public bool RetryAfterUnusable { get; }
    /// <summary>Discard all pending observations for this session. A new session must never inherit them.</summary>
    public bool InvalidatesCollectionContext => StatusCode is 401 or 403 ||
        Code is "consent_context_expired" or "consent_required" or "invalid_session" or "invalid_credential";

    internal WorkflowException(string code, int statusCode, bool retryable,
        TimeSpan? retryAfter = null, DateTimeOffset? retryAt = null, bool retryAfterUnusable = false)
        : base($"Indieable request failed ({code}, HTTP {statusCode}).")
    {
        Code = code;
        StatusCode = statusCode;
        Retryable = retryable;
        RetryAfter = retryAfter;
        RetryAt = retryAt;
        RetryAfterUnusable = retryAfterUnusable;
    }
}

/// <summary>A nonthrowing analytics delivery result. Explicit caller cancellation still propagates.</summary>
public sealed class WorkflowDelivery<T> where T : class
{
    public T? Receipt { get; }
    public WorkflowException? Error { get; }
    public bool Succeeded => Receipt != null;
    internal WorkflowDelivery(T? receipt, WorkflowException? error) { Receipt = receipt; Error = error; }
}

internal static class WorkflowReceipts
{
    internal static async Task<WorkflowEventReceipt> EventsAsync(Task<WorkflowEventReceipt> response, int count, string trust)
    {
        var receipt = await response.ConfigureAwait(false);
        if (receipt.Trust != trust || receipt.Accepted < 0 || receipt.Duplicates < 0 || receipt.Accepted + receipt.Duplicates != count)
            throw new WorkflowException("invalid_response", 0, false);
        return receipt;
    }
    internal static async Task<WorkflowFeedbackReceipt> FeedbackAsync(Task<WorkflowFeedbackReceipt> response)
    {
        var receipt = await response.ConfigureAwait(false);
        if (receipt.Id == Guid.Empty) throw new WorkflowException("invalid_response", 0, false);
        return receipt;
    }
}
