using System;
using System.Collections.Generic;
using IndieableSdk;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// End-to-end Indieable integration example built entirely with runtime UI Toolkit.
///
/// Add this component to an empty GameObject, paste a Public Game Key, and enter
/// Play Mode. Optional gameplay telemetry and diagnostics are off by default.
/// Essential session/security processing and features the Player explicitly asks
/// to use are described separately and are not bundled into analytics consent.
/// </summary>
public sealed class IndieableUIToolkitExample : MonoBehaviour
{
    [Header("Indieable")]
    [SerializeField] private string publicGameKey = "ind_pub_replace_me";
    [SerializeField] private string baseUrl = "https://preview.indieable.com";
    [SerializeField] private string environment = "development";
    [SerializeField] private string buildVersion = "";
    [SerializeField] private string localProfileRef = "";
    [SerializeField] private bool promptForOptionalDataOnStart = true;

    [Header("Demo gameplay event")]
    [SerializeField] private string runCompletedEventKey = "run_completed";
    [SerializeField] private bool sendDemoEventsAsTest = true;
    [SerializeField] private int initialFloor = 1;
    [SerializeField] private int initialTimeMilliseconds = 60000;
    [SerializeField] private int initialDeaths = 0;
    [SerializeField] private int initialPlayers = 1;
    [SerializeField] private string initialDifficulty = "normal";

    private PanelSettings _panelSettings;
    private UIDocument _document;
    private VisualElement _root;
    private VisualElement _appWindow;
    private VisualElement _consentOverlay;
    private VisualElement _linkOverlay;
    private Label _statusLabel;
    private Label _identityLabel;
    private Label _permissionSummary;
    private Label _linkCodeLabel;
    private Label _challengeLabel;
    private ScrollView _activityLog;
    private Toggle _telemetryToggle;
    private Toggle _diagnosticsToggle;
    private Toggle _testEventToggle;
    private TextField _eventKeyField;
    private IntegerField _floorField;
    private IntegerField _timeField;
    private IntegerField _deathsField;
    private IntegerField _playersField;
    private DropdownField _difficultyField;

    private IndieablePrivacyManifest _manifest;
    private IndieablePrivacyPreferences _preferences;
    private IndieableDeviceLink _activeDeviceLink;
    private readonly List<string> _logLines = new List<string>();
    private bool _busy;

    private static readonly Color Ink = new Color(0.075f, 0.078f, 0.09f, 1f);
    private static readonly Color Paper = new Color(0.965f, 0.957f, 0.925f, 1f);
    private static readonly Color PaperRaised = new Color(1f, 0.995f, 0.975f, 1f);
    private static readonly Color Muted = new Color(0.37f, 0.38f, 0.42f, 1f);
    private static readonly Color Line = new Color(0.78f, 0.76f, 0.69f, 1f);
    private static readonly Color Accent = new Color(0.42f, 0.22f, 0.78f, 1f);
    private static readonly Color Success = new Color(0.10f, 0.46f, 0.30f, 1f);
    private static readonly Color Danger = new Color(0.68f, 0.14f, 0.17f, 1f);

    [Serializable]
    private sealed class RunCompletedPayload
    {
        public int floor;
        public int time_ms;
        public int deaths;
        public int players;
        public string difficulty;
    }

    private void Awake()
    {
        BuildRuntimeDocument();
        BuildInterface();

        if (string.IsNullOrWhiteSpace(buildVersion)) buildVersion = Application.version;

        Indieable.Initialize(new IndieableOptions
        {
            BaseUrl = baseUrl,
            PublicGameKey = publicGameKey,
            BuildVersion = buildVersion,
            Environment = environment,
            Engine = "Unity",
            EngineVersion = Application.unityVersion,
            Platform = Application.platform.ToString(),
            LocalProfileRef = localProfileRef,
            LogErrors = true,
            PrivacyVisibilityChanged = delegate(bool visible)
            {
                Log("SDK privacy UI visibility changed: " + visible + ".");
            },
            FeedbackVisibilityChanged = delegate(bool visible)
            {
                Log("SDK feedback UI visibility changed: " + visible + ".");
            }
        });
    }

    private void Start()
    {
        SetStatus("Loading the public privacy manifest…", false);
        Indieable.GetPrivacyManifest(OnManifestLoaded, OnError);
    }

    private void OnDestroy()
    {
        if (_panelSettings != null) Destroy(_panelSettings);
    }

    private void BuildRuntimeDocument()
    {
        _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        _panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        _panelSettings.referenceResolution = new Vector2Int(1920, 1080);
        _panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        _panelSettings.match = 0.5f;
        _panelSettings.sortingOrder = 10000;

        _document = GetComponent<UIDocument>();
        if (_document == null) _document = gameObject.AddComponent<UIDocument>();
        _document.panelSettings = _panelSettings;
        _document.sortingOrder = 10000;

        _root = _document.rootVisualElement;
        _root.name = "indieable-example-root";
        _root.style.flexGrow = 1;
        _root.style.position = Position.Relative;
        _root.style.color = Ink;
        _root.pickingMode = PickingMode.Ignore;
    }

    private void BuildInterface()
    {
        var launcher = new Button(delegate { SetWindowVisible(true); })
        {
            text = "Indieable Example"
        };
        launcher.name = "indieable-example-launcher";
        launcher.pickingMode = PickingMode.Position;
        launcher.style.position = Position.Absolute;
        launcher.style.right = 18;
        launcher.style.bottom = 18;
        launcher.style.height = 42;
        launcher.style.paddingLeft = 18;
        launcher.style.paddingRight = 18;
        launcher.style.backgroundColor = Ink;
        launcher.style.color = Color.white;
        launcher.style.borderTopLeftRadius = 9;
        launcher.style.borderTopRightRadius = 9;
        launcher.style.borderBottomLeftRadius = 9;
        launcher.style.borderBottomRightRadius = 9;
        launcher.style.unityFontStyleAndWeight = FontStyle.Bold;
        launcher.tooltip = "Open the Indieable UI Toolkit integration example.";
        _root.Add(launcher);

        _appWindow = MakeOverlay(false);
        var shell = MakeCard(980, 860);
        shell.style.flexGrow = 1;
        shell.style.marginTop = 24;
        shell.style.marginBottom = 24;
        shell.style.marginLeft = 24;
        shell.style.marginRight = 24;
        _appWindow.Add(shell);

        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.marginBottom = 14;
        shell.Add(header);

        var titleGroup = new VisualElement();
        titleGroup.style.flexGrow = 1;
        header.Add(titleGroup);
        titleGroup.Add(MakeEyebrow("UNITY UI TOOLKIT EXAMPLE"));
        titleGroup.Add(MakeHeading("Indieable Player Data Lab", 28));

        var close = MakeButton("Close", delegate { SetWindowVisible(false); }, false);
        close.tooltip = "Close this example panel. Indieable remains initialized.";
        header.Add(close);

        _statusLabel = MakeBody("Starting…");
        _statusLabel.style.color = Muted;
        _statusLabel.style.marginBottom = 5;
        shell.Add(_statusLabel);

        _identityLabel = MakeBody("Identity: not connected");
        _identityLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _identityLabel.style.marginBottom = 12;
        shell.Add(_identityLabel);

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexGrow = 1;
        shell.Add(scroll);

        BuildPrivacySection(scroll);
        BuildTrackingSection(scroll);
        BuildFeaturesSection(scroll);
        BuildActivitySection(scroll);

        _consentOverlay = MakeOverlay(true);
        _consentOverlay.style.display = DisplayStyle.None;
        _root.Add(_consentOverlay);
        BuildConsentPopup(_consentOverlay);

        _linkOverlay = MakeOverlay(true);
        _linkOverlay.style.display = DisplayStyle.None;
        _root.Add(_linkOverlay);
        BuildLinkPopup(_linkOverlay);

        _root.Add(_appWindow);
        SetWindowVisible(true);
    }

    private void BuildPrivacySection(VisualElement parent)
    {
        var section = MakeSection("Privacy and optional Player Data",
            "Optional gameplay telemetry and diagnostics are separate choices. They are off until the Player enables them. Essential session/security work and features the Player explicitly requests are handled separately.");
        parent.Add(section);

        _permissionSummary = MakeBody("Preferences have not loaded yet.");
        _permissionSummary.style.marginBottom = 10;
        section.Add(_permissionSummary);

        var row = MakeButtonRow();
        row.Add(MakeButton("Review privacy choices", ShowPrivacyPopup, true));
        row.Add(MakeButton("Use SDK default privacy UI", Indieable.OpenPrivacyPreferences, false));
        row.Add(MakeButton("Reset local Indieable identity", ResetIdentity, false));
        section.Add(row);
    }

    private void BuildTrackingSection(VisualElement parent)
    {
        var section = MakeSection("Demo stat tracking",
            "This sends one schema-approved run_completed fact. Configure the same event key and fields in the Indieable game dashboard before testing. Test mode is on by default so the example cannot affect production analytics or Challenge rankings accidentally.");
        parent.Add(section);

        _eventKeyField = new TextField("Event key") { value = runCompletedEventKey };
        StyleField(_eventKeyField);
        section.Add(_eventKeyField);

        var grid = new VisualElement();
        grid.style.flexDirection = FlexDirection.Row;
        grid.style.flexWrap = Wrap.Wrap;
        grid.style.marginTop = 8;
        section.Add(grid);

        _floorField = AddIntegerField(grid, "Floor", initialFloor, 150);
        _timeField = AddIntegerField(grid, "Time (ms)", initialTimeMilliseconds, 180);
        _deathsField = AddIntegerField(grid, "Deaths", initialDeaths, 150);
        _playersField = AddIntegerField(grid, "Players", initialPlayers, 150);

        var difficulties = new List<string> { "easy", "normal", "hard" };
        var initialDifficultyIndex = Mathf.Max(0, difficulties.IndexOf(initialDifficulty));
        _difficultyField = new DropdownField("Difficulty", difficulties, initialDifficultyIndex);
        StyleField(_difficultyField);
        _difficultyField.style.width = 180;
        _difficultyField.style.marginRight = 10;
        grid.Add(_difficultyField);

        _testEventToggle = new Toggle("Send as test event") { value = sendDemoEventsAsTest };
        StyleField(_testEventToggle);
        _testEventToggle.style.marginTop = 8;
        section.Add(_testEventToggle);

        var row = MakeButtonRow();
        row.Add(MakeButton("Send run_completed", SendDemoEvent, true));
        row.Add(MakeButton("Add one floor", delegate
        {
            _floorField.value = Mathf.Max(0, _floorField.value + 1);
        }, false));
        section.Add(row);
    }

    private void BuildFeaturesSection(VisualElement parent)
    {
        var section = MakeSection("Requested features",
            "These actions are not optional broad analytics consent. Each feature is processed only when the Player asks to use it and remains subject to the game's published notice and Indieable authorization rules.");
        parent.Add(section);

        var row = MakeButtonRow();
        row.Add(MakeButton("Link Indieable account", BeginAccountLink, true));
        row.Add(MakeButton("Open feedback", Indieable.OpenFeedback, false));
        row.Add(MakeButton("Open bug report", Indieable.OpenBugReport, false));
        row.Add(MakeButton("Load Challenges", LoadChallenges, false));
        section.Add(row);

        _challengeLabel = MakeBody("No Challenge request has been made.");
        _challengeLabel.style.marginTop = 10;
        section.Add(_challengeLabel);
    }

    private void BuildActivitySection(VisualElement parent)
    {
        var section = MakeSection("Local integration activity",
            "This log exists only in the running Unity example. It intentionally does not print session tokens, Installation credentials, provider subjects, email addresses, or server secrets.");
        parent.Add(section);

        _activityLog = new ScrollView(ScrollViewMode.Vertical);
        _activityLog.style.height = 190;
        _activityLog.style.backgroundColor = Ink;
        _activityLog.style.paddingLeft = 10;
        _activityLog.style.paddingRight = 10;
        _activityLog.style.paddingTop = 8;
        _activityLog.style.paddingBottom = 8;
        _activityLog.style.borderTopLeftRadius = 7;
        _activityLog.style.borderTopRightRadius = 7;
        _activityLog.style.borderBottomLeftRadius = 7;
        _activityLog.style.borderBottomRightRadius = 7;
        section.Add(_activityLog);
        RenderLog();
    }

    private void BuildConsentPopup(VisualElement overlay)
    {
        var card = MakeCard(680, 720);
        overlay.Add(card);

        card.Add(MakeEyebrow("OPTIONAL PLAYER DATA"));
        card.Add(MakeHeading("Your choice, purpose by purpose", 25));

        var intro = MakeBody(
            "The game can work without optional gameplay telemetry or diagnostics. Neither option is preselected. Account linking, playtest forms, Community Challenges, and marketing are separate decisions.");
        intro.style.marginTop = 7;
        intro.style.marginBottom = 12;
        card.Add(intro);

        var controller = MakeBody("Loading controller and retention information…");
        controller.name = "consent-controller-copy";
        controller.style.color = Muted;
        controller.style.marginBottom = 12;
        card.Add(controller);

        _telemetryToggle = MakePermissionToggle("Gameplay telemetry",
            "Structured gameplay facts such as floor reached, completion time, or deaths. Used only under the exact developer-defined schema and retention shown in the published notice.");
        card.Add(_telemetryToggle);

        _diagnosticsToggle = MakePermissionToggle("Diagnostics",
            "Optional technical facts used to diagnose integration or game problems. This does not enable freeform log or screenshot collection.");
        card.Add(_diagnosticsToggle);

        var legal = MakeBody(
            "Optional collection is off by default. You can change or withdraw these choices later from Privacy settings. Declining does not remove access to ordinary gameplay.");
        legal.style.marginTop = 10;
        legal.style.marginBottom = 12;
        legal.style.color = Muted;
        card.Add(legal);

        var policyRow = MakeButtonRow();
        var policyButton = MakeButton("Open full privacy policy", OpenPrivacyPolicy, false);
        policyButton.name = "consent-policy-button";
        policyRow.Add(policyButton);
        card.Add(policyRow);

        var decisions = MakeButtonRow();
        var decline = MakeDecisionButton("Continue without optional data", delegate { SavePrivacyChoices(false); });
        var accept = MakeDecisionButton("Save selected choices", delegate { SavePrivacyChoices(true); });
        decisions.Add(decline);
        decisions.Add(accept);
        card.Add(decisions);
    }

    private void BuildLinkPopup(VisualElement overlay)
    {
        var card = MakeCard(560, 450);
        overlay.Add(card);
        card.Add(MakeEyebrow("ACCOUNT LINK"));
        card.Add(MakeHeading("Continue in your browser", 24));

        var explanation = MakeBody(
            "Indieable never asks for a password inside the game. Open the verification link, sign in normally, and approve this game link. Linking does not enable telemetry or marketing.");
        explanation.style.marginTop = 8;
        explanation.style.marginBottom = 14;
        card.Add(explanation);

        _linkCodeLabel = MakeHeading("Requesting code…", 30);
        _linkCodeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        _linkCodeLabel.style.marginBottom = 12;
        card.Add(_linkCodeLabel);

        var row = MakeButtonRow();
        row.Add(MakeButton("Open verification link", OpenActiveLink, true));
        row.Add(MakeButton("Close", delegate { _linkOverlay.style.display = DisplayStyle.None; }, false));
        card.Add(row);
    }

    private void OnManifestLoaded(IndieablePrivacyManifest manifest)
    {
        _manifest = manifest;
        Log("Privacy manifest loaded. Configured: " + manifest.Configured + ".");
        SetStatus("Connecting with a short-lived Indieable session…", false);
        Indieable.Connect(OnConnected, OnError);
    }

    private void OnConnected(IndieableSessionInfo session)
    {
        Log("Connected as " + session.IdentityState + ".");
        RefreshIdentityLabel();
        SetStatus("Loading current privacy choices…", false);
        Indieable.GetPrivacyPreferences(OnPreferencesLoaded, OnError);
    }

    private void OnPreferencesLoaded(IndieablePrivacyPreferences preferences)
    {
        _preferences = preferences;
        _busy = false;
        RefreshIdentityLabel();
        RefreshPermissionSummary();
        SetStatus("Ready. Optional data remains off unless the Player enabled it.", false);

        if (promptForOptionalDataOnStart && HasUndecidedOptionalPurpose())
            ShowPrivacyPopup();
    }

    private void ShowPrivacyPopup()
    {
        if (_manifest == null)
        {
            SetStatus("The public privacy manifest has not loaded yet.", true);
            return;
        }

        var telemetry = _manifest.FindPurpose(Indieable.GameplayTelemetryPurpose);
        var diagnostics = _manifest.FindPurpose(Indieable.DiagnosticsPurpose);
        _telemetryToggle.SetEnabled(telemetry != null && telemetry.Enabled);
        _diagnosticsToggle.SetEnabled(diagnostics != null && diagnostics.Enabled);
        _telemetryToggle.value = IsGranted(Indieable.GameplayTelemetryPurpose);
        _diagnosticsToggle.value = IsGranted(Indieable.DiagnosticsPurpose);

        var controller = _consentOverlay.Q<Label>("consent-controller-copy");
        if (controller != null)
        {
            var controllerName = _manifest.Controller != null && !string.IsNullOrWhiteSpace(_manifest.Controller.Name)
                ? _manifest.Controller.Name
                : "the game developer";
            controller.text = "Controller: " + controllerName + ". " +
                PurposeRetentionCopy(telemetry, "Gameplay telemetry") + " " +
                PurposeRetentionCopy(diagnostics, "Diagnostics");
        }

        var policyButton = _consentOverlay.Q<Button>("consent-policy-button");
        if (policyButton != null)
            policyButton.SetEnabled(_manifest.Controller != null &&
                !string.IsNullOrWhiteSpace(_manifest.Controller.PrivacyPolicyUrl));

        _consentOverlay.style.display = DisplayStyle.Flex;
    }

    private void SavePrivacyChoices(bool useSelectedValues)
    {
        if (_busy) return;
        _busy = true;
        SetStatus("Saving privacy choices…", false);

        var telemetry = useSelectedValues && _telemetryToggle.value;
        var diagnostics = useSelectedValues && _diagnosticsToggle.value;

        SaveOnePreference(Indieable.GameplayTelemetryPurpose, telemetry, delegate
        {
            SaveOnePreference(Indieable.DiagnosticsPurpose, diagnostics, delegate
            {
                _busy = false;
                _consentOverlay.style.display = DisplayStyle.None;
                RefreshIdentityLabel();
                RefreshPermissionSummary();
                SetStatus("Privacy choices saved.", false);
                Log("Privacy choices saved: telemetry=" + telemetry + ", diagnostics=" + diagnostics + ".");
            });
        });
    }

    private void SaveOnePreference(string purpose, bool enabled, Action next)
    {
        var manifestPurpose = _manifest != null ? _manifest.FindPurpose(purpose) : null;
        if (manifestPurpose == null || !manifestPurpose.Enabled)
        {
            next();
            return;
        }

        Indieable.SetPrivacyPreference(
            purpose,
            enabled,
            delegate(IndieablePrivacyPreferences preferences)
            {
                _preferences = preferences;
                next();
            },
            delegate(IndieableError error)
            {
                _busy = false;
                OnError(error);
            },
            Application.systemLanguage.ToString(),
            true);
    }

    private void SendDemoEvent()
    {
        if (_busy) return;
        var eventKey = (_eventKeyField.value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(eventKey))
        {
            SetStatus("Enter the event key configured in the Indieable dashboard.", true);
            return;
        }

        var payload = new RunCompletedPayload
        {
            floor = Mathf.Max(0, _floorField.value),
            time_ms = Mathf.Max(0, _timeField.value),
            deaths = Mathf.Max(0, _deathsField.value),
            players = Mathf.Max(1, _playersField.value),
            difficulty = _difficultyField.value
        };

        _busy = true;
        var idempotencyKey = "unity-example-" + Guid.NewGuid().ToString("N");
        SetStatus("Sending " + eventKey + "…", false);
        IndieableTelemetry.Send(
            eventKey,
            JsonUtility.ToJson(payload),
            _testEventToggle.value,
            delegate
            {
                _busy = false;
                SetStatus("Event accepted. Check the Connect console or analytics view.", false);
                Log("Event accepted: " + eventKey + " (test=" + _testEventToggle.value + ").");
            },
            delegate(IndieableError error)
            {
                _busy = false;
                OnError(error);
            },
            idempotencyKey);
    }

    private void BeginAccountLink()
    {
        if (_busy) return;
        _busy = true;
        _activeDeviceLink = null;
        _linkCodeLabel.text = "Requesting code…";
        _linkOverlay.style.display = DisplayStyle.Flex;
        SetStatus("Requesting an Indieable account-link code…", false);

        Indieable.LinkAccount(
            delegate(IndieableDeviceLink link)
            {
                _activeDeviceLink = link;
                _linkCodeLabel.text = link.UserCode;
                _busy = false;
                SetStatus("Approve the game link in your browser.", false);
                Log("Account-link code issued. It expires at " + link.ExpiresAt + ".");
            },
            delegate(IndieableSessionInfo session)
            {
                _busy = false;
                _linkOverlay.style.display = DisplayStyle.None;
                SetStatus("Indieable account linked.", false);
                Log("Account linked as " + session.IdentityState + ".");
                RefreshIdentityLabel();
                Indieable.GetPrivacyPreferences(OnPreferencesLoaded, OnError);
            },
            delegate(IndieableError error)
            {
                _busy = false;
                OnError(error);
            },
            true);
    }

    private void OpenActiveLink()
    {
        if (_activeDeviceLink == null)
        {
            SetStatus("No active account-link code is available.", true);
            return;
        }

        var url = !string.IsNullOrWhiteSpace(_activeDeviceLink.VerificationUrlComplete)
            ? _activeDeviceLink.VerificationUrlComplete
            : _activeDeviceLink.VerificationUrl;
        if (!string.IsNullOrWhiteSpace(url)) Application.OpenURL(url);
    }

    private void LoadChallenges()
    {
        if (_busy) return;
        _busy = true;
        SetStatus("Loading Challenges…", false);
        Indieable.GetChallenges(
            delegate(IndieableChallengeCollection collection)
            {
                _busy = false;
                var joined = collection.Joined != null ? collection.Joined.Length : 0;
                var joinable = collection.Joinable != null ? collection.Joinable.Length : 0;
                _challengeLabel.text = "Joined: " + joined + " · Joinable public Challenges: " + joinable +
                    " · Linked identity: " + collection.Linked;
                SetStatus("Challenges loaded.", false);
                Log("Challenges loaded: " + joined + " joined, " + joinable + " joinable.");
            },
            delegate(IndieableError error)
            {
                _busy = false;
                OnError(error);
            });
    }

    private void ResetIdentity()
    {
        if (_busy) return;
        _busy = true;
        SetStatus("Revoking this local Indieable identity…", false);
        Indieable.ResetLocalIdentity(
            delegate
            {
                _busy = false;
                _preferences = null;
                SetStatus("Local Indieable identity reset. Reconnecting ephemerally…", false);
                Log("Local Indieable identity reset and active session revoked.");
                Indieable.Connect(OnConnected, OnError);
            },
            delegate(IndieableError error)
            {
                _busy = false;
                OnError(error);
            });
    }

    private void OpenPrivacyPolicy()
    {
        if (_manifest != null && _manifest.Controller != null &&
            !string.IsNullOrWhiteSpace(_manifest.Controller.PrivacyPolicyUrl))
            Application.OpenURL(_manifest.Controller.PrivacyPolicyUrl);
    }

    private bool HasUndecidedOptionalPurpose()
    {
        return IsPurposeEnabledAndUndecided(Indieable.GameplayTelemetryPurpose) ||
               IsPurposeEnabledAndUndecided(Indieable.DiagnosticsPurpose);
    }

    private bool IsPurposeEnabledAndUndecided(string purpose)
    {
        var manifestPurpose = _manifest != null ? _manifest.FindPurpose(purpose) : null;
        if (manifestPurpose == null || !manifestPurpose.Enabled) return false;
        var permissions = _preferences != null ? _preferences.Permissions : null;
        if (permissions == null) return true;
        for (var i = 0; i < permissions.Length; i++)
            if (string.Equals(permissions[i].PurposeKey, purpose, StringComparison.OrdinalIgnoreCase))
                return string.IsNullOrWhiteSpace(permissions[i].State);
        return true;
    }

    private bool IsGranted(string purpose)
    {
        return _preferences != null && _preferences.IsGranted(purpose);
    }

    private void RefreshIdentityLabel()
    {
        var session = Indieable.Session;
        if (session == null)
        {
            _identityLabel.text = "Identity: not connected";
            return;
        }

        _identityLabel.text = "Identity: " + session.IdentityState +
            (string.IsNullOrWhiteSpace(session.PublicPlayerRef) ? "" : " · " + session.PublicPlayerRef) +
            " · persistent=" + session.PersistentIdentity;
    }

    private void RefreshPermissionSummary()
    {
        if (_permissionSummary == null) return;
        _permissionSummary.text = "Gameplay telemetry: " +
            (IsGranted(Indieable.GameplayTelemetryPurpose) ? "enabled" : "off") +
            " · Diagnostics: " +
            (IsGranted(Indieable.DiagnosticsPurpose) ? "enabled" : "off") +
            ". Optional choices can be changed at any time.";
    }

    private void SetWindowVisible(bool visible)
    {
        if (_appWindow != null) _appWindow.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void SetStatus(string text, bool error)
    {
        if (_statusLabel != null)
        {
            _statusLabel.text = text;
            _statusLabel.style.color = error ? Danger : Muted;
        }
        if (error) Log("ERROR: " + text);
    }

    private void OnError(IndieableError error)
    {
        _busy = false;
        var message = error != null ? error.Code + ": " + error.Message : "Unknown Indieable error.";
        SetStatus(message, true);
    }

    private void Log(string line)
    {
        var safe = (line ?? "").Replace("\r", " ").Replace("\n", " ");
        _logLines.Insert(0, DateTime.Now.ToString("HH:mm:ss") + "  " + safe);
        if (_logLines.Count > 30) _logLines.RemoveAt(_logLines.Count - 1);
        RenderLog();
    }

    private void RenderLog()
    {
        if (_activityLog == null) return;
        _activityLog.Clear();
        if (_logLines.Count == 0)
        {
            var empty = MakeBody("No activity yet.");
            empty.style.color = Color.white;
            _activityLog.Add(empty);
            return;
        }

        for (var i = 0; i < _logLines.Count; i++)
        {
            var label = MakeBody(_logLines[i]);
            label.style.color = Color.white;
            label.style.fontSize = 11;
            label.style.marginBottom = 4;
            _activityLog.Add(label);
        }
    }

    private static string PurposeRetentionCopy(IndieablePrivacyPurpose purpose, string label)
    {
        if (purpose == null || !purpose.Enabled) return label + " is unavailable.";
        return purpose.HasRetentionDays
            ? label + " retention: up to " + purpose.RetentionDays + " days."
            : label + " uses the published retention policy.";
    }

    private static VisualElement MakeOverlay(bool modal)
    {
        var overlay = new VisualElement();
        overlay.pickingMode = PickingMode.Position;
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0;
        overlay.style.right = 0;
        overlay.style.top = 0;
        overlay.style.bottom = 0;
        overlay.style.alignItems = Align.Center;
        overlay.style.justifyContent = Justify.Center;
        overlay.style.backgroundColor = modal
            ? new Color(0.02f, 0.02f, 0.025f, 0.78f)
            : new Color(0.02f, 0.02f, 0.025f, 0.58f);
        return overlay;
    }

    private static VisualElement MakeCard(float maxWidth, float maxHeight)
    {
        var card = new VisualElement();
        card.pickingMode = PickingMode.Position;
        card.style.width = new Length(92, LengthUnit.Percent);
        card.style.maxWidth = maxWidth;
        card.style.maxHeight = maxHeight;
        card.style.backgroundColor = Paper;
        card.style.paddingLeft = 22;
        card.style.paddingRight = 22;
        card.style.paddingTop = 20;
        card.style.paddingBottom = 20;
        card.style.borderTopLeftRadius = 12;
        card.style.borderTopRightRadius = 12;
        card.style.borderBottomLeftRadius = 12;
        card.style.borderBottomRightRadius = 12;
        card.style.borderTopWidth = 1;
        card.style.borderRightWidth = 1;
        card.style.borderBottomWidth = 1;
        card.style.borderLeftWidth = 1;
        card.style.borderTopColor = Line;
        card.style.borderRightColor = Line;
        card.style.borderBottomColor = Line;
        card.style.borderLeftColor = Line;
        return card;
    }

    private static VisualElement MakeSection(string title, string description)
    {
        var section = new VisualElement();
        section.style.backgroundColor = PaperRaised;
        section.style.paddingLeft = 16;
        section.style.paddingRight = 16;
        section.style.paddingTop = 14;
        section.style.paddingBottom = 14;
        section.style.marginBottom = 12;
        section.style.borderTopLeftRadius = 9;
        section.style.borderTopRightRadius = 9;
        section.style.borderBottomLeftRadius = 9;
        section.style.borderBottomRightRadius = 9;
        section.style.borderTopWidth = 1;
        section.style.borderRightWidth = 1;
        section.style.borderBottomWidth = 1;
        section.style.borderLeftWidth = 1;
        section.style.borderTopColor = Line;
        section.style.borderRightColor = Line;
        section.style.borderBottomColor = Line;
        section.style.borderLeftColor = Line;
        section.Add(MakeHeading(title, 19));
        var copy = MakeBody(description);
        copy.style.color = Muted;
        copy.style.marginTop = 5;
        copy.style.marginBottom = 10;
        section.Add(copy);
        return section;
    }

    private static Label MakeEyebrow(string text)
    {
        var label = new Label(text);
        label.style.fontSize = 10;
        label.style.letterSpacing = 1.5f;
        label.style.color = Accent;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.marginBottom = 3;
        return label;
    }

    private static Label MakeHeading(string text, int size)
    {
        var label = new Label(text);
        label.style.fontSize = size;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.whiteSpace = WhiteSpace.Normal;
        return label;
    }

    private static Label MakeBody(string text)
    {
        var label = new Label(text);
        label.style.fontSize = 13;
        label.style.whiteSpace = WhiteSpace.Normal;
        return label;
    }

    private static VisualElement MakeButtonRow()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;
        row.style.marginTop = 7;
        return row;
    }

    private static Button MakeButton(string text, Action clicked, bool primary)
    {
        var button = new Button(clicked) { text = text };
        button.style.height = 38;
        button.style.paddingLeft = 14;
        button.style.paddingRight = 14;
        button.style.marginRight = 8;
        button.style.marginBottom = 8;
        button.style.borderTopLeftRadius = 7;
        button.style.borderTopRightRadius = 7;
        button.style.borderBottomLeftRadius = 7;
        button.style.borderBottomRightRadius = 7;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.backgroundColor = primary ? Accent : Paper;
        button.style.color = primary ? Color.white : Ink;
        button.style.borderTopColor = primary ? Accent : Line;
        button.style.borderRightColor = primary ? Accent : Line;
        button.style.borderBottomColor = primary ? Accent : Line;
        button.style.borderLeftColor = primary ? Accent : Line;
        return button;
    }

    private static Button MakeDecisionButton(string text, Action clicked)
    {
        var button = MakeButton(text, clicked, false);
        button.style.flexGrow = 1;
        button.style.minWidth = 250;
        button.style.height = 44;
        button.style.backgroundColor = PaperRaised;
        button.style.color = Ink;
        button.style.borderTopColor = Accent;
        button.style.borderRightColor = Accent;
        button.style.borderBottomColor = Accent;
        button.style.borderLeftColor = Accent;
        return button;
    }

    private static Toggle MakePermissionToggle(string title, string description)
    {
        var container = new VisualElement();
        container.style.paddingLeft = 12;
        container.style.paddingRight = 12;
        container.style.paddingTop = 10;
        container.style.paddingBottom = 10;
        container.style.marginBottom = 8;
        container.style.backgroundColor = PaperRaised;
        container.style.borderTopLeftRadius = 8;
        container.style.borderTopRightRadius = 8;
        container.style.borderBottomLeftRadius = 8;
        container.style.borderBottomRightRadius = 8;
        container.style.borderTopWidth = 1;
        container.style.borderRightWidth = 1;
        container.style.borderBottomWidth = 1;
        container.style.borderLeftWidth = 1;
        container.style.borderTopColor = Line;
        container.style.borderRightColor = Line;
        container.style.borderBottomColor = Line;
        container.style.borderLeftColor = Line;

        var toggle = new Toggle(title) { value = false };
        toggle.style.unityFontStyleAndWeight = FontStyle.Bold;
        toggle.style.marginBottom = 5;
        toggle.tooltip = description;
        container.Add(toggle);

        var copy = MakeBody(description);
        copy.style.color = Muted;
        container.Add(copy);
        toggle.userData = container;
        return toggle;
    }

    private static IntegerField AddIntegerField(VisualElement parent, string label, int value, float width)
    {
        var field = new IntegerField(label) { value = value };
        StyleField(field);
        field.style.width = width;
        field.style.marginRight = 10;
        parent.Add(field);
        return field;
    }

    private static void StyleField(VisualElement field)
    {
        field.style.marginBottom = 8;
        field.style.minHeight = 38;
    }
}
