using System;
using IndieableSdk.EventBus;
using IndieableSdk.Events;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndieableSdk.Samples.EventBus
{
    /// <summary>
    /// Scene-facing sample controller. Gameplay components publish only through
    /// GlobalEventBus. This controller owns integration UI and permission actions.
    /// </summary>
    public sealed class IndieableEventBusSampleController : MonoBehaviour
    {
        private const string ResourceName = "IndieableEventBusSample";
        private const string PlaceholderPublicKey = "ind_pub_replace_me";

        [Header("Preview configuration")]
        [SerializeField] private string baseUrl = "https://preview.indieable.com";
        [SerializeField] private string publicGameKey = PlaceholderPublicKey;
        [SerializeField] private string environment = "development";
        [SerializeField] private string localProfileRef = "";

        [Header("Integration")]
        [SerializeField] private IndieableEventBusBridge eventBusBridge;

        [Header("Sample gameplay systems")]
        [SerializeField] private SampleDoor door;
        [SerializeField] private SampleWorkorderTerminal workorderTerminal;
        [SerializeField] private SampleNode node;
        [SerializeField] private SamplePlayerLifecycle playerLifecycle;
        [SerializeField] private SampleRunTracker runTracker;

        private UIDocument _document;
        private PanelSettings _panelSettings;
        private IDisposable _localEventSubscription;
        private IndieablePrivacyManifest _manifest;
        private IndieablePrivacyPreferences _preferences;

        private TextField _baseUrlField;
        private TextField _publicGameKeyField;
        private TextField _environmentField;
        private TextField _localProfileField;
        private TextField _challengeSlugField;

        private Label _sdkState;
        private Label _sessionState;
        private Label _routingState;
        private Label _challengeState;
        private ScrollView _activityLog;

        private VisualElement _permissionOverlay;
        private Label _permissionController;
        private Label _permissionNotice;
        private Label _telemetryDescription;
        private Label _diagnosticsDescription;
        private Toggle _telemetryToggle;
        private Toggle _diagnosticsToggle;

        private bool _busy;

        private void Awake()
        {
            if (eventBusBridge == null)
                eventBusBridge = GetComponent<IndieableEventBusBridge>();

            BuildDocument();
            BindBridge();
            BindLocalBus();
            SetDefaultValues();
            UpdateStateLabels();

            Log(
                "Sample scene loaded. No network request, persistent identifier, " +
                "or gameplay event has been sent.");
        }

        private void OnDestroy()
        {
            if (_localEventSubscription != null)
            {
                _localEventSubscription.Dispose();
                _localEventSubscription = null;
            }

            UnbindBridge();

            if (_panelSettings != null)
                Destroy(_panelSettings);
        }

        private void BuildDocument()
        {
            var visualTree = Resources.Load<VisualTreeAsset>(ResourceName);
            var styleSheet = Resources.Load<StyleSheet>(ResourceName);
            if (visualTree == null || styleSheet == null)
            {
                Debug.LogWarning(
                    "[Indieable Sample] Missing Event Bus sample UI Toolkit resources.");
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
        }

        private void BindElements()
        {
            var root = _document.rootVisualElement;

            _baseUrlField = Require<TextField>(root, "base-url");
            _publicGameKeyField = Require<TextField>(root, "public-game-key");
            _environmentField = Require<TextField>(root, "environment");
            _localProfileField = Require<TextField>(root, "local-profile");
            _challengeSlugField = Require<TextField>(root, "challenge-slug");

            _sdkState = Require<Label>(root, "sdk-state");
            _sessionState = Require<Label>(root, "session-state");
            _routingState = Require<Label>(root, "routing-state");
            _challengeState = Require<Label>(root, "challenge-state");
            _activityLog = Require<ScrollView>(root, "activity-log");

            _permissionOverlay = Require<VisualElement>(root, "permission-overlay");
            _permissionController = Require<Label>(root, "permission-controller");
            _permissionNotice = Require<Label>(root, "permission-notice");
            _telemetryDescription = Require<Label>(root, "telemetry-description");
            _diagnosticsDescription = Require<Label>(root, "diagnostics-description");
            _telemetryToggle = Require<Toggle>(root, "telemetry-toggle");
            _diagnosticsToggle = Require<Toggle>(root, "diagnostics-toggle");
        }

        private void BindActions()
        {
            var root = _document.rootVisualElement;

            Require<Button>(root, "initialize").clicked += InitializeSdk;
            Require<Button>(root, "load-manifest").clicked += delegate
            {
                LoadManifest(null);
            };
            Require<Button>(root, "connect").clicked += Connect;
            Require<Button>(root, "open-permissions").clicked += OpenPermissionDialog;

            Require<Button>(root, "door-open").clicked += delegate
            {
                if (door != null) door.Open();
                else Log("Door event source is not assigned.", true);
            };
            Require<Button>(root, "workorder-done").clicked += delegate
            {
                if (workorderTerminal != null) workorderTerminal.CompleteWorkorder();
                else Log("Workorder event source is not assigned.", true);
            };
            Require<Button>(root, "node-close").clicked += delegate
            {
                if (node != null) node.CloseNode();
                else Log("Node event source is not assigned.", true);
            };
            Require<Button>(root, "player-death").clicked += delegate
            {
                if (playerLifecycle != null) playerLifecycle.Die();
                else Log("Player event source is not assigned.", true);
            };
            Require<Button>(root, "run-floor").clicked += delegate
            {
                if (runTracker != null)
                {
                    runTracker.AddFloor();
                    Log("Local run advanced to floor " + runTracker.Floor + ".");
                }
                else Log("Run tracker is not assigned.", true);
            };
            Require<Button>(root, "run-complete").clicked += delegate
            {
                if (runTracker != null) runTracker.CompleteRun();
                else Log("Run tracker is not assigned.", true);
            };

            Require<Button>(root, "direct-test").clicked += SendDirectConnectTest;
            Require<Button>(root, "link-account").clicked += LinkAccount;
            Require<Button>(root, "open-feedback").clicked += Indieable.OpenFeedback;
            Require<Button>(root, "open-bug-report").clicked += Indieable.OpenBugReport;
            Require<Button>(root, "list-challenges").clicked += ListChallenges;
            Require<Button>(root, "join-challenge").clicked += JoinChallenge;
            Require<Button>(root, "reset-identity").clicked += ResetIdentity;

            Require<Button>(root, "permission-close").clicked += ClosePermissionDialog;
            Require<Button>(root, "permission-decline").clicked += delegate
            {
                _telemetryToggle.value = false;
                _diagnosticsToggle.value = false;
                SavePermissionChoices();
            };
            Require<Button>(root, "permission-save").clicked += SavePermissionChoices;
        }

        private void SetDefaultValues()
        {
            if (_baseUrlField == null) return;

            IndieableProjectSettings projectSettings =
                IndieableProjectSettings.Load();
            if (projectSettings != null)
            {
                baseUrl = projectSettings.BaseUrl;
                environment = projectSettings.Environment;
                localProfileRef = projectSettings.LocalProfileRef;
                if (!string.IsNullOrWhiteSpace(
                        projectSettings.PublicGameKey))
                {
                    publicGameKey = projectSettings.PublicGameKey;
                }
                if (eventBusBridge != null &&
                    projectSettings.EventRouting != null)
                {
                    eventBusBridge.RoutingSettings =
                        projectSettings.EventRouting;
                }
            }

            _baseUrlField.value = baseUrl;
            _publicGameKeyField.value = publicGameKey;
            _environmentField.value = environment;
            _localProfileField.value = localProfileRef;
            _challengeSlugField.value = "";

            // Optional purposes are not preselected. Existing saved grants are
            // reflected only after the Player connects and preferences are read.
            _telemetryToggle.value = false;
            _diagnosticsToggle.value = false;
            _permissionOverlay.style.display = DisplayStyle.None;
        }

        private void BindLocalBus()
        {
            _localEventSubscription = GlobalEventBus.SubscribeAll(
                delegate(GameEventEnvelope envelope)
                {
                    Log(
                        "BUS  " + envelope.Name +
                        "  payload=" + envelope.PayloadType.Name +
                        "  sequence=" + envelope.Sequence +
                        (string.IsNullOrWhiteSpace(envelope.Context.RunId)
                            ? "."
                            : "  run=" + envelope.Context.RunId + "."));
                });
        }

        private void BindBridge()
        {
            if (eventBusBridge == null) return;
            eventBusBridge.EventDropped += OnBridgeDropped;
            eventBusBridge.EventForwarded += OnBridgeForwarded;
            eventBusBridge.EventFailed += OnBridgeFailed;
        }

        private void UnbindBridge()
        {
            if (eventBusBridge == null) return;
            eventBusBridge.EventDropped -= OnBridgeDropped;
            eventBusBridge.EventForwarded -= OnBridgeForwarded;
            eventBusBridge.EventFailed -= OnBridgeFailed;
        }

        private void OnBridgeDropped(GameEventEnvelope envelope, string reason)
        {
            Log("DROP " + EventName(envelope) + " · " + reason + ".");
        }

        private void OnBridgeForwarded(GameEventEnvelope envelope, string message)
        {
            Log("SEND " + message + ".");
        }

        private void OnBridgeFailed(GameEventEnvelope envelope, IndieableError error)
        {
            Log(
                "FAIL " + EventName(envelope) + " · " +
                error.Code + ": " + error.Message,
                true);
        }

        private void InitializeSdk()
        {
            if (_busy) return;

            var key = (_publicGameKeyField.value ?? "").Trim();
            if (string.IsNullOrEmpty(key) || key == PlaceholderPublicKey)
            {
                Log("Enter a Preview Public Game Key before initializing.", true);
                return;
            }

            Indieable.Initialize(new IndieableOptions
            {
                BaseUrl = (_baseUrlField.value ?? "").Trim(),
                PublicGameKey = key,
                BuildVersion = Application.version,
                Environment = string.IsNullOrWhiteSpace(_environmentField.value)
                    ? "development"
                    : _environmentField.value.Trim(),
                Engine = "Unity",
                EngineVersion = Application.unityVersion,
                Platform = Application.platform.ToString(),
                LocalProfileRef = (_localProfileField.value ?? "").Trim(),
                LogErrors = true,
                PrivacyVisibilityChanged = delegate(bool visible)
                {
                    Log("SDK default privacy UI visible: " + visible + ".");
                },
                FeedbackVisibilityChanged = delegate(bool visible)
                {
                    Log("SDK feedback UI visible: " + visible + ".");
                }
            });

            if (!Indieable.IsInitialized)
            {
                Log(
                    "Initialization was rejected. Check the Base URL and Public Game Key.",
                    true);
                return;
            }

            Log("SDK initialized locally. Initialize made no network request.");
            UpdateStateLabels();
        }

        private bool EnsureInitialized()
        {
            if (Indieable.IsInitialized) return true;
            InitializeSdk();
            return Indieable.IsInitialized;
        }

        private void LoadManifest(Action after)
        {
            if (!EnsureInitialized() || _busy) return;

            SetBusy(true, "Loading the public privacy notice...");
            Indieable.GetPrivacyManifest(
                delegate(IndieablePrivacyManifest manifest)
                {
                    _manifest = manifest;
                    SetBusy(
                        false,
                        manifest.Configured
                            ? "Privacy notice loaded without creating a session."
                            : "This game has not published a Player Data notice.");
                    PopulatePermissionDialog();
                    if (after != null) after();
                },
                HandleError);
        }

        private void Connect()
        {
            if (!EnsureInitialized() || _busy) return;

            SetBusy(true, "Creating or resuming a Connect session...");
            Indieable.Connect(
                delegate(IndieableSessionInfo session)
                {
                    SetBusy(
                        false,
                        "Connected as " + Safe(session.IdentityState) + ".");
                    UpdateStateLabels();
                    RefreshPreferences(delegate
                    {
                        if (_manifest == null)
                            LoadManifest(OpenPermissionDialog);
                        else
                            OpenPermissionDialog();
                    });
                },
                HandleError);
        }

        private void EnsureConnected(Action after)
        {
            if (Indieable.IsConnected)
            {
                if (after != null) after();
                return;
            }

            if (!EnsureInitialized() || _busy) return;
            SetBusy(true, "Creating a short-lived Connect session...");
            Indieable.Connect(
                delegate(IndieableSessionInfo session)
                {
                    SetBusy(
                        false,
                        "Connected as " + Safe(session.IdentityState) + ".");
                    UpdateStateLabels();
                    if (after != null) after();
                },
                HandleError);
        }

        private void RefreshPreferences(Action after)
        {
            if (!Indieable.IsConnected)
            {
                ApplyPreferences(null);
                if (after != null) after();
                return;
            }

            Indieable.GetPrivacyPreferences(
                delegate(IndieablePrivacyPreferences preferences)
                {
                    ApplyPreferences(preferences);
                    if (after != null) after();
                },
                delegate(IndieableError error)
                {
                    ApplyPreferences(null);
                    Log(
                        "Could not load current preferences: " + error.Message,
                        true);
                    if (after != null) after();
                });
        }

        private void ApplyPreferences(IndieablePrivacyPreferences preferences)
        {
            _preferences = preferences;

            if (preferences == null)
            {
                _telemetryToggle.value = false;
                _diagnosticsToggle.value = false;
            }
            else
            {
                _telemetryToggle.value =
                    preferences.IsGranted(Indieable.GameplayTelemetryPurpose);
                _diagnosticsToggle.value =
                    preferences.IsGranted(Indieable.DiagnosticsPurpose);
            }

            if (eventBusBridge != null)
                eventBusBridge.ApplyPrivacyPreferences(preferences);

            UpdateStateLabels();
        }

        private void OpenPermissionDialog()
        {
            if (_manifest == null)
            {
                LoadManifest(OpenPermissionDialog);
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
                : "Controller: " + Safe(controller.Name) +
                  "\nPrivacy contact: " + Safe(controller.Contact);

            _permissionNotice.text =
                "Notice " + Safe(_manifest.NoticeVersion) +
                " · " + Safe(_manifest.AudienceClassification) +
                "\nOptional choices are separate from account linking, " +
                "Challenges, forms, and marketing.";

            ConfigurePurpose(
                _manifest.FindPurpose(Indieable.GameplayTelemetryPurpose),
                _telemetryToggle,
                _telemetryDescription,
                "Structured, schema-approved gameplay facts associated with " +
                "this game's pseudonymous Player.");

            ConfigurePurpose(
                _manifest.FindPurpose(Indieable.DiagnosticsPurpose),
                _diagnosticsToggle,
                _diagnosticsDescription,
                "Optional technical facts used to understand failures.");

            if (_preferences != null)
            {
                _telemetryToggle.value =
                    _preferences.IsGranted(Indieable.GameplayTelemetryPurpose);
                _diagnosticsToggle.value =
                    _preferences.IsGranted(Indieable.DiagnosticsPurpose);
            }
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
                description.text =
                    fallback + "\nThis purpose is absent from the published notice.";
                return;
            }

            toggle.SetEnabled(purpose.Enabled);
            if (!purpose.Enabled) toggle.value = false;
            description.text =
                (string.IsNullOrWhiteSpace(purpose.Description)
                    ? fallback
                    : purpose.Description) +
                (purpose.HasRetentionDays
                    ? "\nRetention: up to " + purpose.RetentionDays + " days."
                    : "");
        }

        private void SavePermissionChoices()
        {
            if (_busy) return;

            EnsureConnected(delegate
            {
                SetBusy(true, "Saving optional Player Data choices...");
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
                                SetBusy(
                                    false,
                                    "Choices saved. Gameplay telemetry: " +
                                    OnOff(_telemetryToggle.value) +
                                    "; diagnostics: " +
                                    OnOff(_diagnosticsToggle.value) + ".");
                                ClosePermissionDialog();
                                UpdateStateLabels();
                            });
                    });
            });
        }

        private void SavePreference(
            string purpose,
            bool enabledValue,
            Action after)
        {
            Indieable.SetPrivacyPreference(
                purpose,
                enabledValue,
                delegate(IndieablePrivacyPreferences preferences)
                {
                    ApplyPreferences(preferences);
                    if (after != null) after();
                },
                HandleError,
                Application.systemLanguage.ToString(),
                true);
        }

        private void SendDirectConnectTest()
        {
            EnsureConnected(delegate
            {
                SetBusy(true, "Sending direct reserved Connect test...");
                Indieable.SendEvent(
                    "indieable.connect_test",
                    "{\"message\":\"Direct SDK path reached Indieable.\"}",
                    true,
                    delegate
                    {
                        SetBusy(false, "Direct Connect test accepted.");
                    },
                    HandleError,
                    "sample-direct-" + Guid.NewGuid().ToString("N"));
            });
        }

        private void LinkAccount()
        {
            EnsureConnected(delegate
            {
                SetBusy(true, "Requesting a browser account-link code...");
                Indieable.LinkAccount(
                    delegate(IndieableDeviceLink link)
                    {
                        Log(
                            "Account-link code " + Safe(link.UserCode) +
                            " opened in the system browser.");
                        var url = string.IsNullOrWhiteSpace(
                            link.VerificationUrlComplete)
                            ? link.VerificationUrl
                            : link.VerificationUrlComplete;
                        if (!string.IsNullOrWhiteSpace(url))
                            Application.OpenURL(url);
                    },
                    delegate(IndieableSessionInfo session)
                    {
                        SetBusy(
                            false,
                            "Account linked as " +
                            Safe(session.IdentityState) + ".");
                        UpdateStateLabels();
                        RefreshPreferences(null);
                    },
                    HandleError,
                    true);
            });
        }

        private void ListChallenges()
        {
            EnsureConnected(delegate
            {
                SetBusy(true, "Loading Community Challenges...");
                Indieable.GetChallenges(
                    delegate(IndieableChallengeCollection collection)
                    {
                        var joined = collection.Joined != null
                            ? collection.Joined.Length
                            : 0;
                        var joinable = collection.Joinable != null
                            ? collection.Joinable.Length
                            : 0;
                        _challengeState.text =
                            "Joined: " + joined + " · Joinable: " + joinable + ".";
                        SetBusy(false, "Challenges loaded.");
                    },
                    HandleError);
            });
        }

        private void JoinChallenge()
        {
            var slug = (_challengeSlugField.value ?? "").Trim();
            if (string.IsNullOrEmpty(slug))
            {
                Log("Enter a Challenge slug first.", true);
                return;
            }

            EnsureConnected(delegate
            {
                SetBusy(true, "Joining Challenge...");
                Indieable.JoinChallenge(
                    slug,
                    delegate(string status)
                    {
                        _challengeState.text =
                            "Challenge " + slug + ": " + Safe(status) + ".";
                        SetBusy(false, "Challenge request completed.");
                    },
                    HandleError);
            });
        }

        private void ResetIdentity()
        {
            if (!Indieable.IsInitialized)
            {
                Log("Initialize the SDK before resetting identity.", true);
                return;
            }

            SetBusy(true, "Revoking the local Installation identity...");
            Indieable.ResetLocalIdentity(
                delegate
                {
                    ApplyPreferences(null);
                    SetBusy(
                        false,
                        "Local Installation identity revoked and cleared.");
                    UpdateStateLabels();
                },
                HandleError);
        }

        private void SetBusy(bool busy, string message)
        {
            _busy = busy;
            Log(message);
            UpdateStateLabels();
        }

        private void HandleError(IndieableError error)
        {
            _busy = false;
            Log(error.Code + ": " + error.Message, true);
            UpdateStateLabels();
        }

        private void UpdateStateLabels()
        {
            if (_sdkState == null) return;

            _sdkState.text = Indieable.IsInitialized
                ? "SDK: initialized"
                : "SDK: not initialized";

            var session = Indieable.Session;
            _sessionState.text = session == null
                ? "Session: none"
                : "Session: " + Safe(session.IdentityState) +
                  (session.PersistentIdentity
                      ? " · persistent " + Safe(session.PublicPlayerRef)
                      : " · ephemeral");

            var settings = eventBusBridge != null
                ? eventBusBridge.RoutingSettings
                : null;
            _routingState.text = settings == null
                ? "Routing: missing"
                : "Routing: " + settings.SelectionMode +
                  " · " + (settings.Routes != null
                      ? settings.Routes.Length
                      : 0) +
                  " configured · default " +
                  (settings.TestByDefault ? "test" : "production");

            if (_busy)
            {
                _sdkState.text += " · busy";
            }
        }

        private void Log(string message, bool error = false)
        {
            if (_activityLog == null)
            {
                if (error)
                    Debug.LogWarning("[Indieable Sample] " + message);
                else
                    Debug.Log("[Indieable Sample] " + message);
                return;
            }

            var label = new Label(
                DateTime.Now.ToString("HH:mm:ss") + "  " + message);
            label.AddToClassList("activity-entry");
            if (error) label.AddToClassList("activity-error");
            _activityLog.Add(label);

            while (_activityLog.childCount > 80)
                _activityLog.RemoveAt(0);

        }

        private static T Require<T>(
            VisualElement root,
            string name)
            where T : VisualElement
        {
            var value = root.Q<T>(name);
            if (value == null)
                throw new InvalidOperationException(
                    "UI Toolkit sample is missing element '" + name + "'.");
            return value;
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
        }

        private static string OnOff(bool value)
        {
            return value ? "on" : "off";
        }

        private static string EventName(GameEventEnvelope envelope)
        {
            return envelope == null ? "event" : envelope.Name;
        }
    }
}
