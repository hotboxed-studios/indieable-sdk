using System;
using System.Globalization;
using IndieableSdk;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndieableSdk.Examples
{
    /// <summary>
    /// Importable UI Toolkit example for Indieable Connect.
    ///
    /// The example intentionally starts with optional gameplay telemetry and
    /// diagnostics disabled. It creates no persistent Installation and sends no
    /// gameplay event until the Player makes an affirmative choice.
    /// </summary>
    public sealed class IndieableUiToolkitExample : MonoBehaviour
    {
        private const string ResourceName = "IndieableUiToolkitExample";
        private const string PlaceholderPublicKey = "ind_pub_replace_me";

        private UIDocument _document;
        private PanelSettings _panelSettings;
        private IndieablePrivacyManifest _manifest;
        private IndieablePrivacyPreferences _preferences;

        private TextField _baseUrl;
        private TextField _publicGameKey;
        private TextField _environment;
        private TextField _localProfile;
        private Label _sdkState;
        private Label _sessionState;
        private ScrollView _log;

        private IntegerField _floor;
        private IntegerField _timeMs;
        private IntegerField _deaths;
        private IntegerField _players;
        private Toggle _testEvent;
        private TextField _challengeSlug;

        private VisualElement _permissionOverlay;
        private Label _permissionController;
        private Label _permissionNotice;
        private Label _telemetryDescription;
        private Label _diagnosticsDescription;
        private Toggle _telemetryToggle;
        private Toggle _diagnosticsToggle;
        private Button _permissionDecline;
        private Button _permissionSave;

        private bool _busy;
        private static bool _exampleCreated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExampleExists()
        {
            if (_exampleCreated) return;
            _exampleCreated = true;
            var host = new GameObject("Indieable UI Toolkit Example");
            DontDestroyOnLoad(host);
            host.AddComponent<IndieableUiToolkitExample>();
        }

        private void Awake()
        {
            BuildDocument();
        }

        private void OnDestroy()
        {
            if (_panelSettings != null) Destroy(_panelSettings);
        }

        private void BuildDocument()
        {
            var visualTree = Resources.Load<VisualTreeAsset>(ResourceName);
            var styleSheet = Resources.Load<StyleSheet>(ResourceName);
            if (visualTree == null || styleSheet == null)
            {
                Debug.LogWarning("[Indieable Example] Missing UI Toolkit Resources assets.");
                enabled = false;
                return;
            }

            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _panelSettings.referenceResolution = new Vector2Int(1440, 900);
            _panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            _panelSettings.match = 0.5f;
            _panelSettings.sortingOrder = 1000;

            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            visualTree.CloneTree(_document.rootVisualElement);
            _document.rootVisualElement.styleSheets.Add(styleSheet);

            BindElements();
            BindActions();
            SetDefaultValues();
            Log("Example loaded. No network request or persistent identifier has been created.");
        }

        private void BindElements()
        {
            var root = _document.rootVisualElement;
            _baseUrl = Require<TextField>(root, "base-url");
            _publicGameKey = Require<TextField>(root, "public-game-key");
            _environment = Require<TextField>(root, "environment");
            _localProfile = Require<TextField>(root, "local-profile");
            _sdkState = Require<Label>(root, "sdk-state");
            _sessionState = Require<Label>(root, "session-state");
            _log = Require<ScrollView>(root, "activity-log");

            _floor = Require<IntegerField>(root, "floor");
            _timeMs = Require<IntegerField>(root, "time-ms");
            _deaths = Require<IntegerField>(root, "deaths");
            _players = Require<IntegerField>(root, "players");
            _testEvent = Require<Toggle>(root, "test-event");
            _challengeSlug = Require<TextField>(root, "challenge-slug");

            _permissionOverlay = Require<VisualElement>(root, "permission-overlay");
            _permissionController = Require<Label>(root, "permission-controller");
            _permissionNotice = Require<Label>(root, "permission-notice");
            _telemetryDescription = Require<Label>(root, "telemetry-description");
            _diagnosticsDescription = Require<Label>(root, "diagnostics-description");
            _telemetryToggle = Require<Toggle>(root, "telemetry-toggle");
            _diagnosticsToggle = Require<Toggle>(root, "diagnostics-toggle");
            _permissionDecline = Require<Button>(root, "permission-decline");
            _permissionSave = Require<Button>(root, "permission-save");
        }

        private void BindActions()
        {
            var root = _document.rootVisualElement;
            Require<Button>(root, "initialize").clicked += InitializeSdk;
            Require<Button>(root, "load-manifest").clicked += LoadManifest;
            Require<Button>(root, "connect").clicked += Connect;
            Require<Button>(root, "open-permissions").clicked += OpenPermissionDialog;
            Require<Button>(root, "send-connect-test").clicked += SendConnectTest;
            Require<Button>(root, "send-run").clicked += SendRun;
            Require<Button>(root, "link-account").clicked += LinkAccount;
            Require<Button>(root, "open-feedback").clicked += Indieable.OpenFeedback;
            Require<Button>(root, "open-bug-report").clicked += Indieable.OpenBugReport;
            Require<Button>(root, "list-challenges").clicked += ListChallenges;
            Require<Button>(root, "join-challenge").clicked += JoinChallenge;
            Require<Button>(root, "get-leaderboard").clicked += GetLeaderboard;
            Require<Button>(root, "reset-identity").clicked += ResetIdentity;
            Require<Button>(root, "permission-close").clicked += ClosePermissionDialog;

            _permissionDecline.clicked += delegate
            {
                _telemetryToggle.value = false;
                _diagnosticsToggle.value = false;
                SavePermissionChoices();
            };
            _permissionSave.clicked += SavePermissionChoices;
        }

        private void SetDefaultValues()
        {
            _baseUrl.value = "https://preview.indieable.com";
            _publicGameKey.value = PlaceholderPublicKey;
            _environment.value = "development";
            _localProfile.value = string.Empty;
            _floor.value = 1;
            _timeMs.value = 60000;
            _deaths.value = 0;
            _players.value = 1;
            _testEvent.value = true;

            // Optional purposes are never preselected. Existing saved choices are
            // loaded later from Indieable and reflected without broadening them.
            _telemetryToggle.value = false;
            _diagnosticsToggle.value = false;
            _permissionOverlay.style.display = DisplayStyle.None;
            UpdateStateLabels();
        }

        private void InitializeSdk()
        {
            var key = (_publicGameKey.value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(key) || key == PlaceholderPublicKey)
            {
                Log("Enter a Preview Public Game Key before initializing.", true);
                return;
            }

            Indieable.Initialize(new IndieableOptions
            {
                BaseUrl = (_baseUrl.value ?? string.Empty).Trim(),
                PublicGameKey = key,
                BuildVersion = Application.version,
                Environment = string.IsNullOrWhiteSpace(_environment.value)
                    ? "development"
                    : _environment.value.Trim(),
                Engine = "Unity",
                EngineVersion = Application.unityVersion,
                LocalProfileRef = (_localProfile.value ?? string.Empty).Trim(),
                LogErrors = true,
                PrivacyVisibilityChanged = delegate(bool visible)
                {
                    Log("SDK default privacy UI visible: " + visible);
                },
                FeedbackVisibilityChanged = delegate(bool visible)
                {
                    Log("SDK feedback UI visible: " + visible);
                }
            });

            if (!Indieable.IsInitialized)
            {
                Log("Initialization was rejected. Check the Base URL and Public Game Key.", true);
                return;
            }

            Log("SDK initialized locally. Initialize itself made no network request.");
            UpdateStateLabels();
        }

        private bool EnsureInitialized()
        {
            if (Indieable.IsInitialized) return true;
            InitializeSdk();
            return Indieable.IsInitialized;
        }

        private void LoadManifest()
        {
            if (!EnsureInitialized() || _busy) return;
            SetBusy(true, "Loading the public privacy notice…");
            Indieable.GetPrivacyManifest(
                delegate(IndieablePrivacyManifest manifest)
                {
                    _manifest = manifest;
                    SetBusy(false, manifest.Configured
                        ? "Privacy notice loaded without creating a session or persistent identity."
                        : "The game has not published a Player Data notice.");
                    PopulatePermissionDialog();
                },
                HandleError);
        }

        private void Connect()
        {
            if (!EnsureInitialized() || _busy) return;
            SetBusy(true, "Creating or resuming a Connect session…");
            Indieable.Connect(
                delegate(IndieableSessionInfo session)
                {
                    SetBusy(false, "Connected as " + Safe(session.IdentityState) + ".");
                    UpdateStateLabels();
                    RefreshPreferences(delegate
                    {
                        // This appears only after the developer/tester explicitly
                        // pressed Connect. It never preselects optional purposes.
                        OpenPermissionDialog();
                    });
                },
                HandleError);
        }

        private void RefreshPreferences(Action after = null)
        {
            if (!Indieable.IsConnected)
            {
                if (after != null) after();
                return;
            }

            Indieable.GetPrivacyPreferences(
                delegate(IndieablePrivacyPreferences preferences)
                {
                    _preferences = preferences;
                    ReflectExistingPreferences();
                    UpdateStateLabels();
                    if (after != null) after();
                },
                delegate(IndieableError error)
                {
                    Log("Could not load current preferences: " + error.Message, true);
                    if (after != null) after();
                });
        }

        private void OpenPermissionDialog()
        {
            if (_manifest == null)
            {
                LoadManifest();
                return;
            }

            PopulatePermissionDialog();
            _permissionOverlay.style.display = DisplayStyle.Flex;
        }

        private void ClosePermissionDialog()
        {
            _permissionOverlay.style.display = DisplayStyle.None;
        }

        private void PopulatePermissionDialog()
        {
            if (_manifest == null) return;

            var controller = _manifest.Controller;
            _permissionController.text = controller == null
                ? "Controller information has not been published."
                : "Controller: " + Safe(controller.Name) + "\nPrivacy contact: " + Safe(controller.Contact);
            _permissionNotice.text =
                "Notice " + Safe(_manifest.NoticeVersion) + " · " +
                Safe(_manifest.AudienceClassification) +
                "\nOptional choices are separate from account linking, Challenges, forms, and marketing.";

            ConfigurePurpose(
                _manifest.FindPurpose(Indieable.GameplayTelemetryPurpose),
                _telemetryToggle,
                _telemetryDescription,
                "Structured, schema-approved gameplay facts associated with this game's pseudonymous Player.");
            ConfigurePurpose(
                _manifest.FindPurpose(Indieable.DiagnosticsPurpose),
                _diagnosticsToggle,
                _diagnosticsDescription,
                "Optional diagnostic facts used to understand technical failures.");

            ReflectExistingPreferences();
        }

        private static void ConfigurePurpose(
            IndieablePrivacyPurpose purpose,
            Toggle toggle,
            Label description,
            string fallback)
        {
            if (purpose == null)
            {
                toggle.SetEnabled(false);
                toggle.value = false;
                description.text = fallback + "\nThis purpose is not present in the published notice.";
                return;
            }

            toggle.SetEnabled(purpose.Enabled);
            if (!purpose.Enabled) toggle.value = false;
            description.text =
                (string.IsNullOrWhiteSpace(purpose.Description) ? fallback : purpose.Description) +
                (purpose.HasRetentionDays ? "\nRetention: up to " + purpose.RetentionDays + " days." : string.Empty);
        }

        private void ReflectExistingPreferences()
        {
            if (_preferences == null) return;
            _telemetryToggle.value = _preferences.IsGranted(Indieable.GameplayTelemetryPurpose);
            _diagnosticsToggle.value = _preferences.IsGranted(Indieable.DiagnosticsPurpose);
        }

        private void SavePermissionChoices()
        {
            if (_busy) return;
            EnsureConnected(delegate
            {
                SetBusy(true, "Saving optional Player Data choices…");
                SavePreference(
                    Indieable.GameplayTelemetryPurpose,
                    _telemetryToggle.value,
                    delegate
                    {
                        SavePreference(
                            Indieable.DiagnosticsPurpose,
                            _diagnosticsToggle.value,
                            delegate
                            {
                                SetBusy(false,
                                    "Choices saved. Gameplay telemetry: " +
                                    OnOff(_telemetryToggle.value) +
                                    "; diagnostics: " + OnOff(_diagnosticsToggle.value) + ".");
                                ClosePermissionDialog();
                                UpdateStateLabels();
                            });
                    });
            });
        }

        private void SavePreference(string purpose, bool enabledValue, Action after)
        {
            Indieable.SetPrivacyPreference(
                purpose,
                enabledValue,
                delegate(IndieablePrivacyPreferences preferences)
                {
                    _preferences = preferences;
                    ReflectExistingPreferences();
                    if (after != null) after();
                },
                HandleError,
                Application.systemLanguage.ToString(),
                true);
        }

        private void EnsureConnected(Action after)
        {
            if (Indieable.IsConnected)
            {
                if (after != null) after();
                return;
            }

            if (!EnsureInitialized()) return;
            SetBusy(true, "Creating a short-lived Connect session…");
            Indieable.Connect(
                delegate(IndieableSessionInfo session)
                {
                    SetBusy(false, "Connected as " + Safe(session.IdentityState) + ".");
                    UpdateStateLabels();
                    if (after != null) after();
                },
                HandleError);
        }

        private void SendConnectTest()
        {
            EnsureConnected(delegate
            {
                SetBusy(true, "Sending reserved Connect test event…");
                Indieable.SendEvent(
                    "indieable.connect_test",
                    "{\"message\":\"UI Toolkit example reached Indieable.\"}",
                    true,
                    delegate
                    {
                        SetBusy(false, "Connect test accepted.");
                    },
                    HandleError,
                    "example-connect-" + Guid.NewGuid().ToString("N"));
            });
        }

        private void SendRun()
        {
            if (_preferences == null ||
                !_preferences.IsGranted(Indieable.GameplayTelemetryPurpose))
            {
                Log("Gameplay telemetry is off. Review optional permissions before sending run_completed.", true);
                OpenPermissionDialog();
                return;
            }

            var floor = Math.Max(0, _floor.value);
            var time = Math.Max(0, _timeMs.value);
            var deaths = Math.Max(0, _deaths.value);
            var players = Math.Max(1, _players.value);
            var payload = string.Format(
                CultureInfo.InvariantCulture,
                "{{\"floor\":{0},\"time_ms\":{1},\"deaths\":{2},\"players\":{3}}}",
                floor, time, deaths, players);

            EnsureConnected(delegate
            {
                SetBusy(true, "Sending schema-approved run_completed event…");
                IndieableTelemetry.Send(
                    "run_completed",
                    payload,
                    _testEvent.value,
                    delegate
                    {
                        SetBusy(false, "run_completed accepted. Payload: " + payload);
                    },
                    HandleError,
                    "example-run-" + Guid.NewGuid().ToString("N"));
            });
        }

        private void LinkAccount()
        {
            EnsureConnected(delegate
            {
                SetBusy(true, "Requesting browser account-link code…");
                Indieable.LinkAccount(
                    delegate(IndieableDeviceLink link)
                    {
                        SetBusy(false,
                            "Account link code " + Safe(link.UserCode) +
                            ". Opening " + Safe(link.VerificationUrlComplete));
                        if (!string.IsNullOrWhiteSpace(link.VerificationUrlComplete))
                            Application.OpenURL(link.VerificationUrlComplete);
                    },
                    delegate(IndieableSessionInfo session)
                    {
                        Log("Account linked. Future activity uses " + Safe(session.PublicPlayerRef) + ".");
                        UpdateStateLabels();
                    },
                    HandleError,
                    true);
            });
        }

        private void ListChallenges()
        {
            EnsureConnected(delegate
            {
                SetBusy(true, "Loading Challenges…");
                Indieable.GetChallenges(
                    delegate(IndieableChallengeCollection collection)
                    {
                        SetBusy(false,
                            "Challenges loaded. Joined: " + Length(collection.Joined) +
                            "; joinable: " + Length(collection.Joinable) + ".");
                        var joined = collection.Joined ?? new IndieableChallengeSummary[0];
                        for (var i = 0; i < joined.Length; i++)
                            Log("Joined · " + Safe(joined[i].Name) + " · " + Safe(joined[i].Slug));
                    },
                    HandleError);
            });
        }

        private void JoinChallenge()
        {
            var slug = (_challengeSlug.value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(slug))
            {
                Log("Enter a Challenge slug.", true);
                return;
            }

            EnsureConnected(delegate
            {
                SetBusy(true, "Joining Challenge " + slug + "…");
                Indieable.JoinChallenge(
                    slug,
                    delegate(string status)
                    {
                        SetBusy(false, "Challenge membership status: " + Safe(status) + ".");
                    },
                    HandleError);
            });
        }

        private void GetLeaderboard()
        {
            var slug = (_challengeSlug.value ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(slug))
            {
                Log("Enter a Challenge slug.", true);
                return;
            }

            EnsureConnected(delegate
            {
                SetBusy(true, "Loading leaderboard " + slug + "…");
                Indieable.GetLeaderboard(
                    slug,
                    delegate(IndieableChallengeLeaderboard leaderboard)
                    {
                        SetBusy(false,
                            Safe(leaderboard.MetricName) + " leaderboard: " +
                            leaderboard.Total + " entries.");
                        var rows = leaderboard.Items ?? new IndieableLeaderboardItem[0];
                        for (var i = 0; i < rows.Length && i < 10; i++)
                            Log("#" + rows[i].Rank + " " + Safe(rows[i].DisplayName) +
                                " · " + rows[i].BestScore.ToString(CultureInfo.InvariantCulture));
                    },
                    HandleError,
                    25,
                    0);
            });
        }

        private void ResetIdentity()
        {
            if (!Indieable.IsInitialized)
            {
                Log("The SDK is not initialized; there is no loaded local identity to reset.");
                return;
            }

            SetBusy(true, "Revoking and clearing the local Indieable identity…");
            Indieable.ResetLocalIdentity(
                delegate
                {
                    _preferences = null;
                    _telemetryToggle.value = false;
                    _diagnosticsToggle.value = false;
                    SetBusy(false, "Local identity reset. The next Connect session is ephemeral.");
                    UpdateStateLabels();
                },
                HandleError);
        }

        private void HandleError(IndieableError error)
        {
            SetBusy(false, error == null ? "Unknown Indieable error." : error.Code + ": " + error.Message, true);
        }

        private void SetBusy(bool busy, string message, bool isError = false)
        {
            _busy = busy;
            _permissionDecline.SetEnabled(!busy);
            _permissionSave.SetEnabled(!busy);
            Log(message, isError);
            UpdateStateLabels();
        }

        private void UpdateStateLabels()
        {
            if (_sdkState == null || _sessionState == null) return;
            _sdkState.text = Indieable.IsInitialized
                ? (_busy ? "SDK: busy" : "SDK: initialized")
                : "SDK: not initialized";

            var session = Indieable.Session;
            _sessionState.text = session == null
                ? "Session: none"
                : "Session: " + Safe(session.SessionType) +
                  " · " + Safe(session.IdentityState) +
                  (session.PersistentIdentity ? " · persistent" : " · ephemeral") +
                  (string.IsNullOrWhiteSpace(session.PublicPlayerRef)
                      ? string.Empty
                      : "\nGame Player: " + session.PublicPlayerRef);
        }

        private void Log(string message, bool isError = false)
        {
            if (_log == null) return;
            var line = new Label(DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "  " + message);
            line.AddToClassList(isError ? "log-error" : "log-line");
            _log.Add(line);
            if (_log.childCount > 80) _log.RemoveAt(0);
        }

        private static T Require<T>(VisualElement root, string name) where T : VisualElement
        {
            var value = root.Q<T>(name);
            if (value == null) throw new InvalidOperationException("Missing UI element: " + name);
            return value;
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "—" : value;
        }

        private static string OnOff(bool value)
        {
            return value ? "on" : "off";
        }

        private static int Length(Array value)
        {
            return value == null ? 0 : value.Length;
        }
    }
}
