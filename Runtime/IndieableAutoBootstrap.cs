using System;
using IndieableSdk.EventBus;
using IndieableSdk.Events;
using UnityEngine;
using UnityEngine.Rendering;

namespace IndieableSdk
{
    internal static class IndieableAutoBootstrap
    {
        private static IndieableProjectSettings _settings;

        internal static IndieableProjectSettings Settings => _settings;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            Indieable.ResetForRuntimeStartup();
            IndieableRuntime.ResetForRuntimeStartup();
            IndieablePrivacyUI.ResetForRuntimeStartup();
            IndieableFeedbackUI.ResetForRuntimeStartup();
            IndieableStartupConsent.ResetForRuntimeStartup();
            GlobalEventBus.Clear();
            IndieableEventPayloadJson.CustomSerializer = null;
            _settings = null;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeFirstSceneAwake()
        {
            _settings = IndieableProjectSettings.Load();
            if (_settings == null || !_settings.AutoInitialize)
                return;

            if (!_settings.TryValidate(out string issue))
            {
                if (_settings.LogErrors)
                {
                    Debug.LogWarning(
                        "[Indieable] Automatic initialization is disabled: " +
                        issue);
                }
                return;
            }

            Indieable.Initialize(_settings.CreateOptions());
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void PromptAfterFirstSceneLoad()
        {
            if (_settings == null ||
                !_settings.AutoInitialize ||
                !_settings.ShowStartupConsent ||
                !Indieable.IsInitialized ||
                IndieableStartupConsent.ShouldSuppressAutomaticUi())
            {
                return;
            }

            IndieableStartupConsent.RequestAutomatic(_settings);
        }
    }

    internal static class IndieableStartupConsent
    {
        private const string PreferencePrefix =
            "Indieable.PrivacyNotice.";

        private static bool _automaticAttempted;
        private static bool _manifestRequestInFlight;

        internal static void ResetForRuntimeStartup()
        {
            _automaticAttempted = false;
            _manifestRequestInFlight = false;
        }

        internal static bool ShouldSuppressAutomaticUi()
        {
            if (Application.isBatchMode ||
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                return true;
            }

            return IsTruthyEnvironmentValue(
                       Environment.GetEnvironmentVariable(
                           "UNITY_NON_INTERACTIVE")) ||
                   IsTruthyEnvironmentValue(
                       Environment.GetEnvironmentVariable("CI"));
        }

        internal static void RequestAutomatic(
            IndieableProjectSettings settings)
        {
            if (_automaticAttempted ||
                _manifestRequestInFlight ||
                settings == null ||
                !settings.IsConfigured ||
                !Indieable.IsInitialized)
            {
                return;
            }

            _automaticAttempted = true;
            _manifestRequestInFlight = true;
            Indieable.GetPrivacyManifest(
                manifest =>
                {
                    _manifestRequestInFlight = false;
                    if (manifest == null ||
                        !manifest.Configured ||
                        string.IsNullOrWhiteSpace(
                            manifest.NoticeVersion))
                    {
                        return;
                    }

                    if (TryGetDecision(
                            settings,
                            manifest.NoticeVersion,
                            out bool telemetry,
                            out bool diagnostics))
                    {
                        if (telemetry || diagnostics)
                        {
                            Indieable.Connect(
                                null,
                                null);
                        }
                        return;
                    }

                    IndieablePrivacyUI.OpenInitial(
                        Indieable.Client,
                        manifest);
                },
                _ =>
                {
                    _manifestRequestInFlight = false;
                });
        }

        internal static void RecordDecision(
            IndieableProjectSettings settings,
            string noticeVersion,
            bool telemetry,
            bool diagnostics)
        {
            string key = BuildDecisionKey(settings, noticeVersion);
            if (string.IsNullOrEmpty(key)) return;

            PlayerPrefs.SetInt(key + ".saved", 1);
            PlayerPrefs.SetInt(key + ".telemetry", telemetry ? 1 : 0);
            PlayerPrefs.SetInt(key + ".diagnostics", diagnostics ? 1 : 0);
            PlayerPrefs.Save();
        }

        internal static bool TryGetDecision(
            IndieableProjectSettings settings,
            string noticeVersion,
            out bool telemetry,
            out bool diagnostics)
        {
            telemetry = false;
            diagnostics = false;
            string key = BuildDecisionKey(settings, noticeVersion);
            if (string.IsNullOrEmpty(key) ||
                PlayerPrefs.GetInt(key + ".saved", 0) != 1)
            {
                return false;
            }

            telemetry = PlayerPrefs.GetInt(
                key + ".telemetry",
                0) == 1;
            diagnostics = PlayerPrefs.GetInt(
                key + ".diagnostics",
                0) == 1;
            return true;
        }

        internal static string BuildDecisionKey(
            IndieableProjectSettings settings,
            string noticeVersion)
        {
            if (settings == null ||
                string.IsNullOrWhiteSpace(settings.PublicGameKey) ||
                string.IsNullOrWhiteSpace(noticeVersion))
            {
                return string.Empty;
            }

            return PreferencePrefix +
                   settings.PublicGameKey + "." +
                   settings.Environment + "." +
                   noticeVersion.Trim();
        }

        private static bool IsTruthyEnvironmentValue(string value)
        {
            return string.Equals(
                       value,
                       "1",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       value,
                       "true",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       value,
                       "yes",
                       StringComparison.OrdinalIgnoreCase);
        }
    }
}
