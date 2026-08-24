using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndieableSdk
{
    internal sealed class IndieablePrivacyUI : MonoBehaviour
    {
        private static IndieablePrivacyUI _instance;

        private IndieableClient _client;
        private IndieableProjectSettings _settings;
        private IndieablePrivacyManifest _manifest;
        private UIDocument _document;
        private PanelSettings _panelSettings;
        private Label _gameTitle;
        private Label _controller;
        private Label _notice;
        private Label _status;
        private Label _telemetryDescription;
        private Label _diagnosticsDescription;
        private Toggle _telemetryToggle;
        private Toggle _diagnosticsToggle;
        private Button _privacyPolicy;
        private Button _decline;
        private Button _save;
        private Button _retry;
        private bool _busy;
        private bool _canChoose;
        private bool _telemetryAvailable;
        private bool _diagnosticsAvailable;
        private bool _visibilityReported;

        internal static bool IsOpen => _instance != null;

        internal static void Open(IndieableClient client)
        {
            OpenInternal(client, null);
        }

        internal static void OpenInitial(
            IndieableClient client,
            IndieablePrivacyManifest manifest)
        {
            OpenInternal(client, manifest);
        }

        internal static void Close()
        {
            if (_instance == null) return;
            _instance.ReleaseVisibility();
            Destroy(_instance.gameObject);
        }

        internal static void ResetForRuntimeStartup()
        {
            _instance = null;
        }

        private static void OpenInternal(
            IndieableClient client,
            IndieablePrivacyManifest manifest)
        {
            if (client == null) return;
            if (_instance != null)
            {
                _instance._client = client;
                if (manifest != null)
                {
                    _instance._manifest = manifest;
                    _instance.PopulateManifest();
                }
                return;
            }

            var host = new GameObject(
                "Indieable Privacy Preferences");
            host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(host);
            var instance = host.AddComponent<IndieablePrivacyUI>();
            if (!instance.Initialize(client, manifest))
            {
                Destroy(host);
                return;
            }

            _instance = instance;
            instance._visibilityReported = true;
            client.NotifyPrivacyVisibility(true);
            instance.Load();
        }

        private bool Initialize(
            IndieableClient client,
            IndieablePrivacyManifest manifest)
        {
            _client = client;
            _settings = IndieableAutoBootstrap.Settings ??
                IndieableProjectSettings.Load();
            _manifest = manifest;

            if (!IndieableUiToolkitFactory.TryCreateDocument(
                    gameObject,
                    IndieableUiToolkitView.Privacy,
                    32000,
                    out _document,
                    out _panelSettings))
            {
                return false;
            }

            VisualElement root = _document.rootVisualElement;
            _gameTitle = Require<Label>(root, "game-title");
            _controller = Require<Label>(root, "controller");
            _notice = Require<Label>(root, "notice");
            _status = Require<Label>(root, "status");
            _telemetryDescription =
                Require<Label>(root, "telemetry-description");
            _diagnosticsDescription =
                Require<Label>(root, "diagnostics-description");
            _telemetryToggle =
                Require<Toggle>(root, "telemetry-toggle");
            _diagnosticsToggle =
                Require<Toggle>(root, "diagnostics-toggle");
            _privacyPolicy = Require<Button>(root, "privacy-policy");
            _decline = Require<Button>(root, "decline");
            _save = Require<Button>(root, "save");
            _retry = Require<Button>(root, "retry");

            _privacyPolicy.clicked += OpenPrivacyPolicy;
            _decline.clicked += () => SaveChoices(false, false);
            _save.clicked += () => SaveChoices(
                _telemetryToggle.value,
                _diagnosticsToggle.value);
            _retry.clicked += Load;
            _privacyPolicy.style.display = DisplayStyle.None;
            _retry.style.display = DisplayStyle.None;
            return true;
        }

        private void Load()
        {
            _canChoose = false;
            if (_retry != null)
                _retry.style.display = DisplayStyle.None;
            SetBusy(true, "Loading privacy notice…");
            if (_manifest != null)
            {
                OnManifestLoaded(_manifest);
                return;
            }

            Indieable.GetPrivacyManifest(
                OnManifestLoaded,
                error => SetUnavailable(
                    error != null
                        ? error.Message
                        : "The privacy notice could not be loaded."));
        }

        private void OnManifestLoaded(
            IndieablePrivacyManifest manifest)
        {
            if (manifest == null ||
                !manifest.Configured ||
                string.IsNullOrWhiteSpace(manifest.NoticeVersion))
            {
                _manifest = null;
                SetUnavailable(
                    "This game has not published a Player Data notice.");
                return;
            }

            _manifest = manifest;
            _canChoose = true;
            PopulateManifest();
            if (Indieable.IsConnected)
            {
                Indieable.GetPrivacyPreferences(
                    preferences =>
                    {
                        ApplyPreferences(preferences);
                        SetBusy(false, "Review or change each choice.");
                    },
                    error =>
                    {
                        ApplyLocalDecision();
                        SetBusy(
                            false,
                            (error != null
                                ? error.Message
                                : "Current preferences could not be loaded.") +
                            " You can still save a new choice.");
                    });
                return;
            }

            ApplyLocalDecision();
            SetBusy(false, "Choose separately for each optional purpose.");
        }

        private void SetUnavailable(string message)
        {
            _canChoose = false;
            SetBusy(false, message);
            if (_retry != null)
                _retry.style.display = DisplayStyle.Flex;
        }

        private void PopulateManifest()
        {
            if (_manifest == null) return;

            _gameTitle.text = string.IsNullOrWhiteSpace(
                    _manifest.GameTitle)
                ? "Game privacy notice"
                : _manifest.GameTitle;
            _notice.text = "Notice " +
                           Safe(_manifest.NoticeVersion) + " · " +
                           Safe(_manifest.AudienceClassification);

            IndieablePrivacyController controller =
                _manifest.Controller;
            _controller.text = controller == null
                ? "Controller information has not been published."
                : "Controller: " + Safe(controller.Name) +
                  "\nPrivacy contact: " + Safe(controller.Contact);
            bool hasPolicy = controller != null &&
                             !string.IsNullOrWhiteSpace(
                                 controller.PrivacyPolicyUrl);
            _privacyPolicy.style.display = hasPolicy
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            IndieablePrivacyPurpose telemetryPurpose =
                _manifest.FindPurpose(
                    Indieable.GameplayTelemetryPurpose);
            IndieablePrivacyPurpose diagnosticsPurpose =
                _manifest.FindPurpose(
                    Indieable.DiagnosticsPurpose);
            _telemetryAvailable = telemetryPurpose != null &&
                                  telemetryPurpose.Enabled;
            _diagnosticsAvailable = diagnosticsPurpose != null &&
                                    diagnosticsPurpose.Enabled;
            ConfigurePurpose(
                telemetryPurpose,
                _telemetryToggle,
                _telemetryDescription,
                "Structured gameplay facts used to understand how the " +
                "game plays.");
            ConfigurePurpose(
                diagnosticsPurpose,
                _diagnosticsToggle,
                _diagnosticsDescription,
                "Optional technical facts used to understand failures.");
        }

        private void ApplyLocalDecision()
        {
            if (_manifest == null || _settings == null) return;
            if (IndieableStartupConsent.TryGetDecision(
                    _settings,
                    _manifest.NoticeVersion,
                    out bool telemetry,
                    out bool diagnostics))
            {
                _telemetryToggle.value = telemetry;
                _diagnosticsToggle.value = diagnostics;
            }
        }

        private void ApplyPreferences(
            IndieablePrivacyPreferences preferences)
        {
            _telemetryToggle.value = preferences != null &&
                preferences.IsGranted(
                    Indieable.GameplayTelemetryPurpose);
            _diagnosticsToggle.value = preferences != null &&
                preferences.IsGranted(
                    Indieable.DiagnosticsPurpose);
        }

        private void SaveChoices(bool telemetry, bool diagnostics)
        {
            if (_busy || _manifest == null) return;

            IndieablePrivacyPurpose telemetryPurpose =
                _manifest.FindPurpose(
                    Indieable.GameplayTelemetryPurpose);
            IndieablePrivacyPurpose diagnosticsPurpose =
                _manifest.FindPurpose(
                    Indieable.DiagnosticsPurpose);
            telemetry &= telemetryPurpose != null &&
                         telemetryPurpose.Enabled;
            diagnostics &= diagnosticsPurpose != null &&
                           diagnosticsPurpose.Enabled;

            if (!telemetry && !diagnostics && !Indieable.IsConnected)
            {
                CompleteDecision(false, false);
                return;
            }

            SetBusy(true, "Saving optional Player Data choices…");
            Action save = () => SavePreference(
                Indieable.GameplayTelemetryPurpose,
                telemetry,
                () => SavePreference(
                    Indieable.DiagnosticsPurpose,
                    diagnostics,
                    () => CompleteDecision(
                        telemetry,
                        diagnostics)));

            if (Indieable.IsConnected)
            {
                save();
                return;
            }

            Indieable.Connect(
                _ => save(),
                error => SetBusy(
                    false,
                    error != null
                        ? error.Message
                        : "The choice could not be saved."));
        }

        private void SavePreference(
            string purpose,
            bool enabledValue,
            Action after)
        {
            Indieable.SetPrivacyPreference(
                purpose,
                enabledValue,
                preferences =>
                {
                    ApplyPreferences(preferences);
                    after?.Invoke();
                },
                error => SetBusy(
                    false,
                    error != null
                        ? error.Message
                        : "The choice could not be saved."),
                Application.systemLanguage.ToString(),
                false);
        }

        private void CompleteDecision(
            bool telemetry,
            bool diagnostics)
        {
            if (_settings != null && _manifest != null)
            {
                IndieableStartupConsent.RecordDecision(
                    _settings,
                    _manifest.NoticeVersion,
                    telemetry,
                    diagnostics);
            }

            SetBusy(false, "Choices saved.");
            Close();
        }

        private void OpenPrivacyPolicy()
        {
            string url = _manifest?.Controller?.PrivacyPolicyUrl;
            if (!string.IsNullOrWhiteSpace(url))
                Application.OpenURL(url);
        }

        private void SetBusy(bool busy, string message)
        {
            _busy = busy;
            if (_status != null) _status.text = message ?? "";
            bool choicesEnabled = !busy && _canChoose;
            _telemetryToggle?.SetEnabled(
                choicesEnabled && _telemetryAvailable);
            _diagnosticsToggle?.SetEnabled(
                choicesEnabled && _diagnosticsAvailable);
            _decline?.SetEnabled(choicesEnabled);
            _save?.SetEnabled(choicesEnabled);
            _retry?.SetEnabled(!busy);
        }

        private void ReleaseVisibility()
        {
            if (!_visibilityReported) return;
            _visibilityReported = false;
            _client?.NotifyPrivacyVisibility(false);
        }

        private void OnDestroy()
        {
            ReleaseVisibility();
            if (_panelSettings != null)
                Destroy(_panelSettings);
            if (_instance == this) _instance = null;
        }

        private static void ConfigurePurpose(
            IndieablePrivacyPurpose purpose,
            Toggle toggle,
            Label description,
            string fallback)
        {
            if (purpose == null)
            {
                toggle.value = false;
                toggle.SetEnabled(false);
                description.text = fallback +
                    "\nThis purpose is absent from the published notice.";
                return;
            }

            toggle.SetEnabled(purpose.Enabled);
            if (!purpose.Enabled) toggle.value = false;
            description.text =
                (string.IsNullOrWhiteSpace(purpose.Description)
                    ? fallback
                    : purpose.Description) +
                (purpose.HasRetentionDays
                    ? "\nRetention: up to " +
                      purpose.RetentionDays + " days."
                    : "");
        }

        private static T Require<T>(
            VisualElement root,
            string name)
            where T : VisualElement
        {
            T value = root.Q<T>(name);
            if (value == null)
            {
                throw new InvalidOperationException(
                    "Indieable privacy UI is missing element '" +
                    name + "'.");
            }
            return value;
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "not provided"
                : value;
        }
    }
}
