using System;

namespace IndieableSdk
{
    [Serializable]
    public sealed class IndieableRequestHeader
    {
        public bool Enabled = true;
        public string Name = "";
        public string Value = "";
        public string ValueEnvironmentVariable = "";

        internal bool TryValidate(out string issue)
        {
            issue = "";
            if (!Enabled) return true;

            var name = (Name ?? "").Trim();
            if (!IsValidHeaderName(name))
            {
                issue = "header name is missing or invalid";
                return false;
            }
            if (IsReservedHeaderName(name))
            {
                issue = "header name is owned by the Indieable SDK";
                return false;
            }

            var literal = Value ?? "";
            var environmentVariable =
                (ValueEnvironmentVariable ?? "").Trim();
            if (ContainsNewline(literal))
            {
                issue = "literal header value cannot contain newlines";
                return false;
            }
            if (literal.Length > 0 && environmentVariable.Length > 0)
            {
                issue = "choose either a literal value or an environment variable";
                return false;
            }
            if (literal.Length == 0 && environmentVariable.Length == 0)
            {
                issue = "header value source is required";
                return false;
            }
            if (environmentVariable.Length > 0 &&
                !IsValidEnvironmentVariableName(environmentVariable))
            {
                issue = "environment variable name is invalid";
                return false;
            }

            return true;
        }

        internal bool TryResolve(out string name, out string value)
        {
            name = "";
            value = "";
            if (!Enabled || !TryValidate(out _)) return false;

            name = Name.Trim();
            var environmentVariable =
                (ValueEnvironmentVariable ?? "").Trim();
            value = environmentVariable.Length > 0
                ? Environment.GetEnvironmentVariable(environmentVariable) ?? ""
                : Value ?? "";
            if (value.Length == 0 || ContainsNewline(value))
            {
                name = "";
                value = "";
                return false;
            }
            return true;
        }

        private static bool IsValidHeaderName(string value)
        {
            if (value.Length == 0) return false;
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (char.IsLetterOrDigit(character) ||
                    "!#$%&'*+-.^_`|~".IndexOf(character) >= 0)
                    continue;
                return false;
            }
            return true;
        }

        private static bool IsValidEnvironmentVariableName(string value)
        {
            if (value.Length == 0 ||
                !(char.IsLetter(value[0]) || value[0] == '_'))
                return false;
            for (var index = 1; index < value.Length; index++)
            {
                if (!char.IsLetterOrDigit(value[index]) && value[index] != '_')
                    return false;
            }
            return true;
        }

        private static bool IsReservedHeaderName(string value)
        {
            return string.Equals(value, "Accept", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "Authorization", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "Content-Length", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "Content-Type", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "Cookie", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "Host", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "User-Agent", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsNewline(string value)
        {
            return value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0;
        }
    }

    [Serializable]
    public sealed class IndieableOptions
    {
        public string BaseUrl = "https://indieable.com";
        public string PublicGameKey = "";
        public string SdkVersion = "unity-0.6.1";
        public string BuildVersion = "";
        public string Platform = "";
        public string Environment = "production";
        public string Engine = "Unity";
        public string EngineVersion = "";
        public string LocalProfileRef = "";
        public int RequestTimeoutSeconds = 15;
        public int MaxTransientRetries = 2;
        public bool LogErrors = true;
        public bool AutoClearInvalidIdentity = true;
        public IndieableRequestHeader[] RequestHeaders =
            new IndieableRequestHeader[0];

        [NonSerialized] public IIndieableIdentityStorage IdentityStorage;
        [NonSerialized] public Action<bool> FeedbackVisibilityChanged;
        [NonSerialized] public Action<bool> PrivacyVisibilityChanged;
    }

    /// <summary>
    /// Optional delivery and correlation metadata for one gameplay event.
    /// These values never replace the session, permission, schema, or trust
    /// checks performed by Indieable.
    /// </summary>
    public sealed class IndieableEventOptions
    {
        public bool Test;
        public string IdempotencyKey = "";
        public int? SchemaVersion;
        public DateTime? OccurredAtUtc;
        public string TraceType = "";
        public string TraceId = "";
        public string RunId = "";
    }

    public sealed class IndieableError
    {
        public string Code { get; private set; }
        public string Message { get; private set; }
        public long HttpStatus { get; private set; }

        public IndieableError(string code, string message, long httpStatus = 0)
        {
            Code = string.IsNullOrWhiteSpace(code) ? "request_failed" : code;
            Message = string.IsNullOrWhiteSpace(message) ? "Indieable request failed." : message;
            HttpStatus = httpStatus;
        }

        public override string ToString() { return string.Format("{0}: {1}", Code, Message); }
    }

    public sealed class IndieableSessionInfo
    {
        public string SessionType { get; internal set; }
        public string IdentityState { get; internal set; }
        public bool PersistentIdentity { get; internal set; }
        public string PublicPlayerRef { get; internal set; }
        public string ExpiresAt { get; internal set; }
        public string GameTitle { get; internal set; }
        public string SteamTicketIdentity { get; internal set; }
    }

    public sealed class IndieableDeviceLink
    {
        public string DeviceCode { get; internal set; }
        public string UserCode { get; internal set; }
        public string VerificationUrl { get; internal set; }
        public string VerificationUrlComplete { get; internal set; }
        public string ExpiresAt { get; internal set; }
        public int PollIntervalSeconds { get; internal set; }
    }

    public sealed class IndieablePrivacyController
    {
        public string Name { get; internal set; }
        public string Contact { get; internal set; }
        public string PrivacyPolicyUrl { get; internal set; }
    }

    public sealed class IndieablePrivacyPurpose
    {
        public string PurposeKey { get; internal set; }
        public string DisplayName { get; internal set; }
        public string Description { get; internal set; }
        public bool Enabled { get; internal set; }
        public bool RequiresAffirmativePermission { get; internal set; }
        public bool AllowsPersistentIdentifier { get; internal set; }
        public string AuthorityModel { get; internal set; }
        public string TerminalStorageAuthority { get; internal set; }
        public int RetentionDays { get; internal set; }
        public bool HasRetentionDays { get; internal set; }
    }

    public sealed class IndieablePrivacyManifest
    {
        public bool Configured { get; internal set; }
        public string GameTitle { get; internal set; }
        public int ManifestVersion { get; internal set; }
        public string NoticeVersion { get; internal set; }
        public string AudienceClassification { get; internal set; }
        public string PublishedAt { get; internal set; }
        public IndieablePrivacyController Controller { get; internal set; }
        public IndieablePrivacyPurpose[] Purposes { get; internal set; }

        public IndieablePrivacyPurpose FindPurpose(string purposeKey)
        {
            var values = Purposes ?? new IndieablePrivacyPurpose[0];
            for (var i = 0; i < values.Length; i++)
                if (string.Equals(values[i].PurposeKey, purposeKey, StringComparison.OrdinalIgnoreCase)) return values[i];
            return null;
        }
    }

    public sealed class IndieablePrivacyPreference
    {
        public string PurposeKey { get; internal set; }
        public string State { get; internal set; }
        public string EffectiveAt { get; internal set; }
        public bool RequiresAffirmativePermission { get; internal set; }
    }

    public sealed class IndieablePrivacyPreferences
    {
        public bool PersistentIdentity { get; internal set; }
        public string IdentityState { get; internal set; }
        public string PublicPlayerRef { get; internal set; }
        public IndieablePrivacyManifest Manifest { get; internal set; }
        public IndieablePrivacyPreference[] Permissions { get; internal set; }

        public bool IsGranted(string purposeKey)
        {
            var values = Permissions ?? new IndieablePrivacyPreference[0];
            for (var i = 0; i < values.Length; i++)
                if (string.Equals(values[i].PurposeKey, purposeKey, StringComparison.OrdinalIgnoreCase))
                    return string.Equals(values[i].State, "GRANTED", StringComparison.OrdinalIgnoreCase);
            return false;
        }
    }

    public sealed class IndieablePlaytestRound
    {
        public string Id { get; internal set; }
        public int Number { get; internal set; }
        public string BuildLabel { get; internal set; }
        public string Focus { get; internal set; }
        public string EndsAt { get; internal set; }
    }

    public sealed class IndieableFeedbackConfig
    {
        public bool Available { get; internal set; }
        public string Reason { get; internal set; }
        public string CampaignId { get; internal set; }
        public string Title { get; internal set; }
        public string Description { get; internal set; }
        public string[] SurveyQuestions { get; internal set; }
        public bool Anonymous { get; internal set; }
        public bool AnonymousAllowed { get; internal set; }
        public IndieablePlaytestRound Round { get; internal set; }
    }

    [Serializable]
    public sealed class IndieableSurveyAnswer
    {
        public string Question = "";
        public string Answer = "";
    }

    [Serializable]
    public sealed class IndieableFeedbackSubmission
    {
        public int Rating = 0;
        public string Liked = "";
        public string Confused = "";
        public bool WouldWishlist = false;
        public bool IncludeWouldWishlist = false;
        public string PlayLength = "";
        public string Pitch = "";
        public string BuildVersion = "";
        public IndieableSurveyAnswer[] Answers = new IndieableSurveyAnswer[0];
    }

    [Serializable]
    public sealed class IndieableBugReportSubmission
    {
        public string Title = "";
        public string Description = "";
        public string Severity = "major";
        public string BuildVersion = "";
    }

    public sealed class IndieableChallengeSummary
    {
        public string Slug { get; internal set; }
        public string Name { get; internal set; }
        public string Visibility { get; internal set; }
        public string MetricName { get; internal set; }
        public string SortDirection { get; internal set; }
        public string MembershipStatus { get; internal set; }
        public string StartsAt { get; internal set; }
        public string EndsAt { get; internal set; }
        public bool HasCurrentScore { get; internal set; }
        public double CurrentScore { get; internal set; }
    }

    public sealed class IndieableChallengeCollection
    {
        public bool Linked { get; internal set; }
        public IndieableChallengeSummary[] Joined { get; internal set; }
        public IndieableChallengeSummary[] Joinable { get; internal set; }
    }

    public sealed class IndieableLeaderboardItem
    {
        public int Rank { get; internal set; }
        public string DisplayName { get; internal set; }
        public double BestScore { get; internal set; }
        public string AchievedAt { get; internal set; }
        public bool IsCurrentPlayer { get; internal set; }
        public int PersonalRecordCount { get; internal set; }
    }

    public sealed class IndieableChallengeLeaderboard
    {
        public string ChallengeSlug { get; internal set; }
        public string MetricName { get; internal set; }
        public string ValueType { get; internal set; }
        public string SortDirection { get; internal set; }
        public int Total { get; internal set; }
        public IndieableLeaderboardItem[] Items { get; internal set; }
    }

    [Serializable] internal sealed class SessionRequest
    {
        public string public_game_key;
        public string sdk_version;
        public string build_version;
        public string platform;
        public string environment;
        public string engine;
        public string engine_version;
        public string installation_credential;
        public string local_profile_ref;
    }

    [Serializable] internal sealed class SessionResponse
    {
        public string session_token;
        public string session_type;
        public string identity_state;
        public bool persistent_identity;
        public string public_player_ref;
        public string expires_at;
        public GameResponse game;
        public string steam_ticket_identity;
    }

    [Serializable] internal sealed class GameResponse { public string title; }

    [Serializable] internal sealed class DeviceResponse
    {
        public string device_code;
        public string user_code;
        public string verification_uri;
        public string verification_uri_complete;
        public string expires_at;
        public int interval;
    }

    [Serializable] internal sealed class DevicePollRequest { public string device_code; }

    [Serializable] internal sealed class LinkResponse
    {
        public string status;
        public string session_type;
        public string identity_state;
        public string player_ref;
    }

    [Serializable] internal sealed class SteamRequest { public string ticket; }

    [Serializable] internal sealed class EventResponse
    {
        public bool accepted;
        public bool test;
        public string event_key;
    }

    [Serializable] internal sealed class PrivacyControllerResponse
    {
        public string name;
        public string contact;
        public string privacy_policy_url;
    }

    [Serializable] internal sealed class PrivacyPurposeResponse
    {
        public string purpose_key;
        public string display_name;
        public string description;
        public bool enabled;
        public bool requires_affirmative_permission;
        public bool allows_persistent_identifier;
        public string authority_model;
        public string terminal_storage_authority;
        public int retention_days;
    }

    [Serializable] internal sealed class PrivacyManifestResponse
    {
        public bool configured;
        public string game_title;
        public int manifest_version;
        public string notice_version;
        public PrivacyControllerResponse controller;
        public string audience_classification;
        public string published_at;
        public PrivacyPurposeResponse[] purposes;
    }

    [Serializable] internal sealed class PrivacyPreferenceResponse
    {
        public string purpose_key;
        public string state;
        public string effective_at;
        public bool requires_affirmative_permission;
    }

    [Serializable] internal sealed class PrivacyPreferencesResponse
    {
        public bool persistent_identity;
        public string identity_state;
        public string public_player_ref;
        public PrivacyManifestOwnerResponse manifest;
        public PrivacyPreferenceResponse[] permissions;
    }

    [Serializable] internal sealed class PrivacyManifestOwnerResponse
    {
        public int version;
        public string notice_version;
        public string controller_name;
        public string controller_contact;
        public string privacy_policy_url;
        public string audience_classification;
        public string published_at;
        public PrivacyPurposeResponse[] purposes;
    }

    [Serializable] internal sealed class PrivacyPreferenceRequest
    {
        public string purpose_key;
        public bool enabled;
        public string source;
        public string locale;
        public string local_profile_ref;
    }

    [Serializable] internal sealed class PrivacyPreferenceUpdateResponse
    {
        public string purpose_key;
        public string state;
        public string installation_credential;
        public bool persistent_identity;
        public string session_type;
        public string identity_state;
        public string public_player_ref;
    }

    [Serializable] internal sealed class LocalProfileRequest { public string local_profile_ref; }

    [Serializable] internal sealed class LocalProfileResponse
    {
        public string public_player_ref;
        public string identity_state;
        public bool persistent_identity;
    }

    [Serializable] internal sealed class ResetIdentityResponse
    {
        public bool revoked;
        public string reason;
    }

    [Serializable] internal sealed class FeedbackConfigResponse
    {
        public bool available;
        public string reason;
        public CampaignResponse campaign;
        public RoundResponse round;
        public string[] survey_questions;
        public bool anonymous;
        public bool anonymous_allowed;
    }

    [Serializable] internal sealed class CampaignResponse
    {
        public string id;
        public string title;
        public string description;
    }

    [Serializable] internal sealed class RoundResponse
    {
        public string id;
        public int number;
        public string build_label;
        public string focus;
        public string ends_at;
    }

    [Serializable] internal sealed class BugReportRequest
    {
        public string title;
        public string description;
        public string severity;
        public string build_version;
    }

    [Serializable] internal sealed class SubmissionResponse
    {
        public bool accepted;
        public string submission_id;
        public string report_id;
    }

    [Serializable] internal sealed class ChallengeListResponse
    {
        public bool linked;
        public ChallengeSummaryResponse[] joined;
        public ChallengeSummaryResponse[] joinable;
    }

    [Serializable] internal sealed class ChallengeSummaryResponse
    {
        public string slug;
        public string name;
        public string visibility;
        public string metric_name;
        public string sort_direction;
        public string membership_status;
        public string starts_at;
        public string ends_at;
        public bool has_current_score;
        public double current_score;
    }

    [Serializable] internal sealed class ChallengeSlugRequest { public string slug; }

    [Serializable] internal sealed class ChallengeJoinResponse
    {
        public string status;
        public bool duplicate;
        public string slug;
    }

    [Serializable] internal sealed class ChallengeLeaderboardRequest
    {
        public string slug;
        public int limit;
        public int offset;
    }

    [Serializable] internal sealed class ChallengeLeaderboardResponse
    {
        public string slug;
        public ChallengeMetricResponse metric;
        public int total;
        public ChallengeLeaderboardItemResponse[] items;
    }

    [Serializable] internal sealed class ChallengeMetricResponse
    {
        public string display_name;
        public string value_type;
        public string sort_direction;
    }

    [Serializable] internal sealed class ChallengeLeaderboardItemResponse
    {
        public int rank;
        public string display_name;
        public double best_value;
        public string achieved_at;
        public bool is_current_player;
        public int pr_count;
    }

    [Serializable] internal sealed class ErrorEnvelope { public ErrorBody error; }

    [Serializable] internal sealed class ErrorBody
    {
        public string code;
        public string message;
    }
}
