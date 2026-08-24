using System.Text.Json;
using System.Text.Json.Serialization;

namespace IndieableSdk
{
    public sealed class IndieableApiException : Exception
    {
        public string Code { get; }
        public int HttpStatus { get; }

        public IndieableApiException(
            string code,
            string message,
            int httpStatus = 0,
            Exception? innerException = null)
            : base(
                string.IsNullOrWhiteSpace(message)
                    ? "Indieable request failed."
                    : message,
                innerException)
        {
            Code = string.IsNullOrWhiteSpace(code)
                ? "request_failed"
                : code;
            HttpStatus = httpStatus;
        }
    }

    public sealed class IndieableSessionInfo
    {
        public string SessionType { get; set; } = "";
        public string IdentityState { get; set; } = "";
        public bool PersistentIdentity { get; set; }
        public string PublicPlayerRef { get; set; } = "";
        public string ExpiresAt { get; set; } = "";
        public string GameTitle { get; set; } = "";
        public string SteamTicketIdentity { get; set; } = "";
    }

    public sealed class IndieableDeviceLink
    {
        public string DeviceCode { get; set; } = "";
        public string UserCode { get; set; } = "";
        public string VerificationUrl { get; set; } = "";
        public string VerificationUrlComplete { get; set; } = "";
        public string ExpiresAt { get; set; } = "";
        public int PollIntervalSeconds { get; set; } = 5;
    }

    public sealed class IndieablePrivacyController
    {
        public string Name { get; set; } = "";
        public string Contact { get; set; } = "";
        public string PrivacyPolicyUrl { get; set; } = "";
    }

    public sealed class IndieablePrivacyPurpose
    {
        public string PurposeKey { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Description { get; set; } = "";
        public bool Enabled { get; set; }
        public bool RequiresAffirmativePermission { get; set; }
        public bool AllowsPersistentIdentifier { get; set; }
        public string AuthorityModel { get; set; } = "";
        public string TerminalStorageAuthority { get; set; } = "";
        public int? RetentionDays { get; set; }
    }

    public sealed class IndieablePrivacyManifest
    {
        public bool Configured { get; set; }
        public string GameTitle { get; set; } = "";
        public int ManifestVersion { get; set; }
        public string NoticeVersion { get; set; } = "";
        public string AudienceClassification { get; set; } = "";
        public string PublishedAt { get; set; } = "";
        public IndieablePrivacyController? Controller { get; set; }
        public IndieablePrivacyPurpose[] Purposes { get; set; } =
            Array.Empty<IndieablePrivacyPurpose>();

        public IndieablePrivacyPurpose? FindPurpose(string purposeKey)
        {
            return Purposes.FirstOrDefault(
                purpose => string.Equals(
                    purpose.PurposeKey,
                    purposeKey,
                    StringComparison.OrdinalIgnoreCase));
        }
    }

    public sealed class IndieablePrivacyPreference
    {
        public string PurposeKey { get; set; } = "";
        public string State { get; set; } = "";
        public string EffectiveAt { get; set; } = "";
        public bool RequiresAffirmativePermission { get; set; }
    }

    public sealed class IndieablePrivacyPreferences
    {
        public bool PersistentIdentity { get; set; }
        public string IdentityState { get; set; } = "";
        public string PublicPlayerRef { get; set; } = "";
        public IndieablePrivacyManifest? Manifest { get; set; }
        public IndieablePrivacyPreference[] Permissions { get; set; } =
            Array.Empty<IndieablePrivacyPreference>();

        public bool IsGranted(string purposeKey)
        {
            return Permissions.Any(
                permission =>
                    string.Equals(
                        permission.PurposeKey,
                        purposeKey,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        permission.State,
                        "GRANTED",
                        StringComparison.OrdinalIgnoreCase));
        }
    }

    public sealed class IndieableEventOptions
    {
        public bool Test { get; set; }
        public string IdempotencyKey { get; set; } = "";
        public int? SchemaVersion { get; set; }
        public DateTimeOffset? OccurredAtUtc { get; set; }
        public string TraceType { get; set; } = "";
        public string TraceId { get; set; } = "";
        public string RunId { get; set; } = "";
    }

    public sealed class IndieableEventReceipt
    {
        public bool Accepted { get; set; }
        public bool Duplicate { get; set; }
        public bool Test { get; set; }
        public string EventKey { get; set; } = "";
        public string EventId { get; set; } = "";
        public int SchemaVersion { get; set; }
        public string ProcessingPurpose { get; set; } = "";
    }

    public sealed class IndieablePlaytestRound
    {
        public string Id { get; set; } = "";
        public int Number { get; set; }
        public string BuildLabel { get; set; } = "";
        public string Focus { get; set; } = "";
        public string EndsAt { get; set; } = "";
    }

    public sealed class IndieableFeedbackConfig
    {
        public bool Available { get; set; }
        public string Reason { get; set; } = "";
        public string CampaignId { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string[] SurveyQuestions { get; set; } = Array.Empty<string>();
        public bool Anonymous { get; set; }
        public bool AnonymousAllowed { get; set; }
        public IndieablePlaytestRound? Round { get; set; }
    }

    public sealed class IndieableSurveyAnswer
    {
        public string Question { get; set; } = "";
        public string Answer { get; set; } = "";
    }

    public sealed class IndieableFeedbackSubmission
    {
        public int Rating { get; set; }
        public string Liked { get; set; } = "";
        public string Confused { get; set; } = "";
        public bool WouldWishlist { get; set; }
        public bool IncludeWouldWishlist { get; set; }
        public string PlayLength { get; set; } = "";
        public string Pitch { get; set; } = "";
        public string BuildVersion { get; set; } = "";
        public IndieableSurveyAnswer[] Answers { get; set; } =
            Array.Empty<IndieableSurveyAnswer>();
    }

    public sealed class IndieableBugReportSubmission
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Severity { get; set; } = "major";
        public string BuildVersion { get; set; } = "";
    }

    public sealed class IndieableChallengeSummary
    {
        public string Slug { get; set; } = "";
        public string Name { get; set; } = "";
        public string Visibility { get; set; } = "";
        public string MetricName { get; set; } = "";
        public string SortDirection { get; set; } = "";
        public string MembershipStatus { get; set; } = "";
        public string StartsAt { get; set; } = "";
        public string EndsAt { get; set; } = "";
        public bool HasCurrentScore { get; set; }
        public double CurrentScore { get; set; }
    }

    public sealed class IndieableChallengeCollection
    {
        public bool Linked { get; set; }
        public bool PersistentIdentity { get; set; }
        public string IdentityState { get; set; } = "";
        public string PublicPlayerRef { get; set; } = "";
        public IndieableChallengeSummary[] Joined { get; set; } =
            Array.Empty<IndieableChallengeSummary>();
        public IndieableChallengeSummary[] Joinable { get; set; } =
            Array.Empty<IndieableChallengeSummary>();
    }

    public sealed class IndieableLeaderboardItem
    {
        public int Rank { get; set; }
        public string PublicPlayerRef { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string IdentityState { get; set; } = "";
        public double BestValue { get; set; }
        public string AchievedAt { get; set; } = "";
        public bool IsCurrentPlayer { get; set; }
        public int PrCount { get; set; }
        public double? PreviousValue { get; set; }
        public double? Improvement { get; set; }
    }

    public sealed class IndieableChallengeMetric
    {
        public string DisplayName { get; set; } = "";
        public string ValueField { get; set; } = "";
        public string ValueType { get; set; } = "";
        public string SortDirection { get; set; } = "";
    }

    public sealed class IndieableChallengeLeaderboard
    {
        public string ChallengeId { get; set; } = "";
        public string Slug { get; set; } = "";
        public string MinimumEventTrust { get; set; } = "";
        public IndieableChallengeMetric? Metric { get; set; }
        public int Total { get; set; }
        public IndieableLeaderboardItem[] Items { get; set; } =
            Array.Empty<IndieableLeaderboardItem>();
    }

    public sealed class IndieableSubmissionReceipt
    {
        public bool Accepted { get; set; }
        public string SubmissionId { get; set; } = "";
        public string ReportId { get; set; } = "";
    }

    internal sealed class ErrorEnvelope
    {
        public ErrorBody? Error { get; set; }
    }

    internal sealed class ErrorBody
    {
        public string Code { get; set; } = "";
        public string Message { get; set; } = "";
    }

    internal sealed class SessionResponse
    {
        public string SessionToken { get; set; } = "";
        public string SessionType { get; set; } = "";
        public string IdentityState { get; set; } = "";
        public bool PersistentIdentity { get; set; }
        public string PublicPlayerRef { get; set; } = "";
        public string ExpiresAt { get; set; } = "";
        public GameResponse? Game { get; set; }
        public string SteamTicketIdentity { get; set; } = "";
    }

    internal sealed class GameResponse
    {
        public string Title { get; set; } = "";
    }

    internal sealed class LinkResponse
    {
        public string Status { get; set; } = "";
        public string SessionType { get; set; } = "";
        public string IdentityState { get; set; } = "";
        public string PlayerRef { get; set; } = "";
    }

    internal sealed class PreferenceUpdateResponse
    {
        public string PurposeKey { get; set; } = "";
        public string State { get; set; } = "";
        public string InstallationCredential { get; set; } = "";
        public bool PersistentIdentity { get; set; }
        public string SessionType { get; set; } = "";
        public string IdentityState { get; set; } = "";
        public string PublicPlayerRef { get; set; } = "";
    }

    internal sealed class PreferenceManifestResponse
    {
        public int Version { get; set; }
        public string NoticeVersion { get; set; } = "";
        public string ControllerName { get; set; } = "";
        public string ControllerContact { get; set; } = "";
        public string PrivacyPolicyUrl { get; set; } = "";
        public string AudienceClassification { get; set; } = "";
        public string PublishedAt { get; set; } = "";
        public IndieablePrivacyPurpose[] Purposes { get; set; } =
            Array.Empty<IndieablePrivacyPurpose>();
    }

    internal sealed class PreferencesResponse
    {
        public bool PersistentIdentity { get; set; }
        public string IdentityState { get; set; } = "";
        public string PublicPlayerRef { get; set; } = "";
        public PreferenceManifestResponse? Manifest { get; set; }
        public IndieablePrivacyPreference[] Permissions { get; set; } =
            Array.Empty<IndieablePrivacyPreference>();
    }

    internal sealed class LocalProfileResponse
    {
        public string PublicPlayerRef { get; set; } = "";
        public string IdentityState { get; set; } = "";
        public bool PersistentIdentity { get; set; }
    }

    internal sealed class FeedbackConfigResponse
    {
        public bool Available { get; set; }
        public string Reason { get; set; } = "";
        public CampaignResponse? Campaign { get; set; }
        public IndieablePlaytestRound? Round { get; set; }
        public string[] SurveyQuestions { get; set; } = Array.Empty<string>();
        public bool Anonymous { get; set; }
        public bool AnonymousAllowed { get; set; }
    }

    internal sealed class CampaignResponse
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
