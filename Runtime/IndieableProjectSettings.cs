using System;
using IndieableSdk.EventBus;
using UnityEngine;

namespace IndieableSdk
{
    /// <summary>
    /// Project-owned, build-included Indieable configuration. The SDK never
    /// creates or mutates this asset implicitly; use the Indieable Editor
    /// settings page or assign an explicitly authored instance.
    /// </summary>
    public sealed class IndieableProjectSettings : ScriptableObject
    {
        public const string ResourcePath =
            "Indieable/IndieableProjectSettings";
        public const string DefaultAssetPath =
            "Assets/Resources/Indieable/IndieableProjectSettings.asset";

        [SerializeField] private string baseUrl =
            "https://preview.indieable.com";
        [SerializeField] private string publicGameKey = "";
        [SerializeField] private string environment = "development";
        [SerializeField] private string localProfileRef = "";
        [SerializeField, Min(1)] private int requestTimeoutSeconds = 15;
        [SerializeField, Range(0, 10)] private int maxTransientRetries = 2;
        [SerializeField] private bool logErrors = true;
        [SerializeField] private bool autoClearInvalidIdentity = true;
        [SerializeField] private IndieableEventRoutingSettings eventRouting;

        public string BaseUrl => (baseUrl ?? "").Trim();
        public string PublicGameKey => (publicGameKey ?? "").Trim();
        public string Environment => (environment ?? "").Trim();
        public string LocalProfileRef => (localProfileRef ?? "").Trim();
        public int RequestTimeoutSeconds =>
            Mathf.Clamp(requestTimeoutSeconds, 1, 120);
        public int MaxTransientRetries =>
            Mathf.Clamp(maxTransientRetries, 0, 10);
        public bool LogErrors => logErrors;
        public bool AutoClearInvalidIdentity => autoClearInvalidIdentity;
        public IndieableEventRoutingSettings EventRouting => eventRouting;
        public bool IsConfigured => TryValidate(out _);

        public static IndieableProjectSettings Load()
        {
            return Resources.Load<IndieableProjectSettings>(ResourcePath);
        }

        public IndieableOptions CreateOptions(
            Action<bool> privacyVisibilityChanged = null,
            Action<bool> feedbackVisibilityChanged = null)
        {
            return new IndieableOptions
            {
                BaseUrl = BaseUrl,
                PublicGameKey = PublicGameKey,
                BuildVersion = Application.version,
                Platform = Application.platform.ToString(),
                Environment = Environment,
                Engine = "Unity",
                EngineVersion = Application.unityVersion,
                LocalProfileRef = LocalProfileRef,
                RequestTimeoutSeconds = RequestTimeoutSeconds,
                MaxTransientRetries = MaxTransientRetries,
                LogErrors = LogErrors,
                AutoClearInvalidIdentity = AutoClearInvalidIdentity,
                PrivacyVisibilityChanged = privacyVisibilityChanged,
                FeedbackVisibilityChanged = feedbackVisibilityChanged
            };
        }

        public bool TryValidate(out string issue)
        {
            if (string.IsNullOrWhiteSpace(PublicGameKey))
            {
                issue = "Enter the game's Indieable Public Game Key.";
                return false;
            }

            if (!IsAllowedBaseUrl(BaseUrl))
            {
                issue =
                    "Base URL must use HTTPS, or HTTP only for a loopback development URL.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Environment))
            {
                issue = "Environment is required.";
                return false;
            }

            issue = "";
            return true;
        }

        private static bool IsAllowedBaseUrl(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri uri) ||
                !string.IsNullOrEmpty(uri.UserInfo))
            {
                return false;
            }

            if (string.Equals(
                    uri.Scheme,
                    Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return uri.IsLoopback && string.Equals(
                uri.Scheme,
                Uri.UriSchemeHttp,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
