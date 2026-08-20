using System;
using IndieableSdk.Steam;
using UnityEngine;

namespace IndieableSdk
{
    public static class Indieable
    {
        public const string GameplayTelemetryPurpose = "GAMEPLAY_TELEMETRY";
        public const string DiagnosticsPurpose = "DIAGNOSTICS";

        private static IndieableClient _client;

        public static bool IsInitialized { get { return _client != null; } }
        public static bool IsConnected { get { return _client != null && _client.IsConnected; } }
        public static IndieableSessionInfo Session { get { return _client != null ? _client.Session : null; } }

        // Initialize is local and side-effect-free. It loads configuration and a
        // previously granted local credential, but performs no network request.
        public static void Initialize(IndieableOptions options)
        {
            if (options == null || string.IsNullOrWhiteSpace(options.PublicGameKey))
            {
                _client = null;
                Debug.LogWarning("[Indieable] PublicGameKey is required. Indieable is disabled; the host game can continue normally.");
                return;
            }
            if (!IsAllowedBaseUrl(options.BaseUrl))
            {
                _client = null;
                Debug.LogWarning("[Indieable] BaseUrl must use HTTPS. Plain HTTP is allowed only for loopback development URLs; Indieable is disabled.");
                return;
            }
            _client = new IndieableClient(options);
            var ignored = IndieableRuntime.Instance;
        }

        public static void Initialize(string publicGameKey, string baseUrl = "https://indieable.com")
        {
            Initialize(new IndieableOptions { PublicGameKey = publicGameKey, BaseUrl = baseUrl });
        }

        public static void Connect(Action<IndieableSessionInfo> onSuccess = null, Action<IndieableError> onError = null)
        {
            if (!RequireClient(onError)) return;
            IndieableRuntime.Instance.Run(_client.Connect(onSuccess, onError));
        }

        // Safe before Connect and before optional permission. This reads only the
        // public versioned notice and creates no Installation or Game Player.
        public static void GetPrivacyManifest(Action<IndieablePrivacyManifest> onSuccess,
            Action<IndieableError> onError = null)
        {
            if (!RequireClient(onError)) return;
            IndieableRuntime.Instance.Run(_client.GetPrivacyManifest(onSuccess, onError));
        }

        public static void GetPrivacyPreferences(Action<IndieablePrivacyPreferences> onSuccess,
            Action<IndieableError> onError = null)
        {
            if (!RequireClient(onError)) return;
            IndieableRuntime.Instance.Run(_client.GetPrivacyPreferences(onSuccess, onError));
        }

        public static void SetPrivacyPreference(string purposeKey, bool enabled,
            Action<IndieablePrivacyPreferences> onSuccess = null,
            Action<IndieableError> onError = null,
            string locale = null,
            bool customUi = false)
        {
            if (!RequireClient(onError)) return;
            IndieableRuntime.Instance.Run(_client.SetPrivacyPreference(
                purposeKey, enabled, locale, customUi, onSuccess, onError));
        }

        public static void SetLocalProfile(string localProfileRef,
            Action<IndieableSessionInfo> onSuccess = null,
            Action<IndieableError> onError = null)
        {
            if (!RequireClient(onError)) return;
            IndieableRuntime.Instance.Run(_client.SetLocalProfile(localProfileRef, onSuccess, onError));
        }

        public static void ResetLocalIdentity(Action onSuccess = null,
            Action<IndieableError> onError = null)
        {
            if (!RequireClient(onError)) return;
            IndieableRuntime.Instance.Run(_client.ResetLocalIdentity(onSuccess, onError));
        }

        public static void OpenPrivacyPreferences()
        {
            if (!RequireClient(null)) return;
            IndieablePrivacyUI.Open(_client);
        }

        public static void ClosePrivacyPreferences() { IndieablePrivacyUI.Close(); }

        public static void LinkAccount(Action<IndieableDeviceLink> onCode,
            Action<IndieableSessionInfo> onLinked = null, Action<IndieableError> onError = null,
            bool pollUntilLinked = true)
        {
            if (!RequireClient(onError)) return;
            IndieableRuntime.Instance.Run(_client.BeginDeviceLink(pollUntilLinked, onCode, onLinked, onError));
        }

        public static void PollAccountLink(IndieableDeviceLink link,
            Action<IndieableSessionInfo> onLinked = null, Action<IndieableError> onError = null)
        {
            if (!RequireClient(onError)) return;
            IndieableRuntime.Instance.Run(_client.PollDeviceLink(link, onLinked, onError));
        }

        public static void ConnectWithSteam(IIndieableSteamTicketProvider provider,
            Action<IndieableSessionInfo> onSuccess = null, Action<IndieableError> onError = null)
        {
            if (!RequireClient(onError)) return;
            _client.ConnectWithSteam(provider, onSuccess, onError);
        }

        public static void SendEvent(string eventKey, string payloadJson = "{}", bool test = false,
            Action onSuccess = null, Action<IndieableError> onError = null,
            string idempotencyKey = null)
        {
            if (!RequireClient(onError)) return;
            IndieableRuntime.Instance.Run(_client.SendEvent(
                eventKey, payloadJson, test, idempotencyKey, onSuccess, onError));
        }

        public static void GetFeedbackConfig(Action<IndieableFeedbackConfig> onSuccess,
            Action<IndieableError> onError = null)
        {
            if (!RequireClient(onError)) return;
            IndieableRuntime.Instance.Run(_client.GetFeedbackConfig(onSuccess, onError));
        }

        public static void SubmitFeedback(IndieableFeedbackSubmission submission,
            Action<string> onSuccess = null, Action<IndieableError> onError = null)
        {
            if (!RequireClient(onError)) return;
            IndieableRuntime.Instance.Run(_client.SubmitFeedback(submission, onSuccess, onError));
        }

        public static void SubmitBugReport(IndieableBugReportSubmission submission,
            Action<string> onSuccess = null, Action<IndieableError> onError = null)
        {
            if (!RequireClient(onError)) return;
            IndieableRuntime.Instance.Run(_client.SubmitBugReport(submission, onSuccess, onError));
        }

        public static void OpenFeedback()
        {
            if (!RequireClient(null)) return;
            IndieableFeedbackUI.Open(_client, false);
        }

        public static void OpenBugReport()
        {
            if (!RequireClient(null)) return;
            IndieableFeedbackUI.Open(_client, true);
        }

        public static void CloseFeedback() { IndieableFeedbackUI.Close(); }

        public static void GetChallenges(Action<IndieableChallengeCollection> onSuccess,
            Action<IndieableError> onError = null)
        {
            if (!RequireClient(onError)) return;
            IndieableRuntime.Instance.Run(_client.GetChallenges(onSuccess, onError));
        }

        public static void JoinChallenge(string slug, Action<string> onSuccess = null,
            Action<IndieableError> onError = null)
        {
            if (!RequireClient(onError)) return;
            IndieableRuntime.Instance.Run(_client.JoinChallenge(slug, onSuccess, onError));
        }

        public static void GetLeaderboard(string slug,
            Action<IndieableChallengeLeaderboard> onSuccess,
            Action<IndieableError> onError = null,
            int limit = 50,
            int offset = 0)
        {
            if (!RequireClient(onError)) return;
            IndieableRuntime.Instance.Run(_client.GetLeaderboard(slug, limit, offset, onSuccess, onError));
        }

        private static bool IsAllowedBaseUrl(string baseUrl)
        {
            var value = string.IsNullOrWhiteSpace(baseUrl) ? "https://indieable.com" : baseUrl.Trim();
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri)) return false;
            if (!string.IsNullOrEmpty(uri.UserInfo)) return false;
            if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return true;
            return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && uri.IsLoopback;
        }

        private static bool RequireClient(Action<IndieableError> onError)
        {
            if (_client != null) return true;
            var error = new IndieableError("not_initialized", "Call Indieable.Initialize first.");
            if (onError != null) onError(error);
            return false;
        }
    }
}
