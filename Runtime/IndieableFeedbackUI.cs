using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace IndieableSdk
{
    internal sealed class IndieableFeedbackUI : MonoBehaviour
    {
        private static readonly string[] PlayLengthKeys =
            { "lt15", "15to30", "30to60", "1to2h", "2hplus" };
        private static readonly string[] SeverityKeys =
            { "minor", "major", "blocker" };

        private static IndieableFeedbackUI _instance;

        private IndieableClient _client;
        private IndieableFeedbackConfig _config;
        private UIDocument _document;
        private PanelSettings _panelSettings;
        private Label _eyebrow;
        private Label _title;
        private Label _description;
        private Label _meta;
        private Label _status;
        private VisualElement _feedbackForm;
        private VisualElement _bugForm;
        private VisualElement _wishlistChoices;
        private VisualElement _surveyQuestions;
        private TextField _liked;
        private TextField _confused;
        private TextField _pitch;
        private Toggle _includeWishlist;
        private TextField _bugTitle;
        private TextField _bugDescription;
        private Button _cancel;
        private Button _send;
        private Button _retry;
        private Button[] _ratingButtons;
        private Button[] _playLengthButtons;
        private Button[] _wishlistButtons;
        private Button[] _severityButtons;
        private TextField[] _answerFields = new TextField[0];
        private int _rating;
        private int _playLengthIndex = -1;
        private int _severityIndex = 1;
        private bool _wouldWishlist;
        private bool _bugMode;
        private bool _ready;
        private bool _loading;
        private bool _submitting;
        private bool _visibilityReported;

        internal static void Open(
            IndieableClient client,
            bool bugMode)
        {
            if (client == null) return;
            if (_instance == null)
            {
                var host = new GameObject("Indieable Feedback UI");
                host.hideFlags = HideFlags.HideAndDontSave;
                DontDestroyOnLoad(host);
                var instance = host.AddComponent<IndieableFeedbackUI>();
                if (!instance.Initialize())
                {
                    Destroy(host);
                    return;
                }

                _instance = instance;
            }

            _instance.Show(client, bugMode);
        }

        internal static void Close()
        {
            if (_instance != null) _instance.Hide();
        }

        internal static void ResetForRuntimeStartup()
        {
            _instance = null;
        }

        private bool Initialize()
        {
            if (!IndieableUiToolkitFactory.TryCreateDocument(
                    gameObject,
                    IndieableUiToolkitView.Feedback,
                    32010,
                    out _document,
                    out _panelSettings))
            {
                return false;
            }

            VisualElement root = _document.rootVisualElement;
            _eyebrow = Require<Label>(root, "feedback-eyebrow");
            _title = Require<Label>(root, "feedback-title");
            _description = Require<Label>(root, "feedback-description");
            _meta = Require<Label>(root, "feedback-meta");
            _status = Require<Label>(root, "feedback-status");
            _feedbackForm = Require<VisualElement>(root, "feedback-form");
            _bugForm = Require<VisualElement>(root, "bug-form");
            _wishlistChoices =
                Require<VisualElement>(root, "wishlist-choices");
            _surveyQuestions =
                Require<VisualElement>(root, "survey-questions");
            _liked = Require<TextField>(root, "liked");
            _confused = Require<TextField>(root, "confused");
            _pitch = Require<TextField>(root, "pitch");
            _includeWishlist =
                Require<Toggle>(root, "include-wishlist");
            _bugTitle = Require<TextField>(root, "bug-title");
            _bugDescription =
                Require<TextField>(root, "bug-description");
            _cancel = Require<Button>(root, "feedback-cancel");
            _send = Require<Button>(root, "feedback-send");
            _retry = Require<Button>(root, "feedback-retry");

            _ratingButtons = BindButtonRange(root, "rating", 5, SetRating);
            _playLengthButtons = BindButtonRange(
                root,
                "play-length",
                PlayLengthKeys.Length,
                SetPlayLength);
            _wishlistButtons = BindButtonRange(
                root,
                "wishlist",
                2,
                SetWishlist);
            _severityButtons = BindButtonRange(
                root,
                "severity",
                SeverityKeys.Length,
                SetSeverity);

            _includeWishlist.RegisterValueChangedCallback(
                change =>
                {
                    _wishlistChoices.style.display = change.newValue
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                    RefreshSelectionClasses();
                });
            _bugTitle.RegisterValueChangedCallback(_ => RefreshSendState());
            _cancel.clicked += Hide;
            _send.clicked += Submit;
            _retry.clicked += Load;
            return true;
        }

        private void Show(
            IndieableClient client,
            bool bugMode)
        {
            _client = client;
            _bugMode = bugMode;
            ResetForm();
            if (!_visibilityReported)
            {
                _visibilityReported = true;
                _client.NotifyFeedbackVisibility(true);
            }
            Load();
        }

        private void Load()
        {
            if (_client == null) return;
            _loading = true;
            _submitting = false;
            _ready = false;
            _config = null;
            ApplyPresentation(
                "Loading Indieable…",
                showRetry: false,
                showForms: false);
            StartCoroutine(
                _client.GetFeedbackConfig(
                    OnConfigLoaded,
                    OnLoadError));
        }

        private void OnConfigLoaded(
            IndieableFeedbackConfig config)
        {
            _loading = false;
            _config = config;
            if (config == null || !config.Available)
            {
                ApplyPresentation(
                    config != null &&
                    !string.IsNullOrWhiteSpace(config.Reason)
                        ? config.Reason
                        : "This game does not have an active playtest form.",
                    showRetry: false,
                    showForms: false);
                return;
            }

            _ready = true;
            _title.text = _bugMode
                ? "Report a bug"
                : string.IsNullOrWhiteSpace(config.Title)
                    ? "Playtest feedback"
                    : config.Title;
            _description.text = string.IsNullOrWhiteSpace(config.Description)
                ? (_bugMode
                    ? "Tell the developer what happened and how to reproduce it."
                    : "Tell the developer what was fun, unclear, or frustrating.")
                : config.Description;
            _meta.text = BuildMeta(config);
            BuildSurveyQuestions(config.SurveyQuestions);
            ApplyPresentation(
                _bugMode
                    ? "Add enough detail to reproduce the issue."
                    : "Your feedback is optional and sent only when you choose Send.",
                showRetry: false,
                showForms: true);
        }

        private void OnLoadError(IndieableError error)
        {
            _loading = false;
            _ready = false;
            ApplyPresentation(
                error != null
                    ? error.Message
                    : "The playtest form could not be loaded.",
                showRetry: true,
                showForms: false);
        }

        private void ApplyPresentation(
            string status,
            bool showRetry,
            bool showForms)
        {
            _eyebrow.text = _bugMode
                ? "BUG REPORT"
                : "PLAYTEST FEEDBACK";
            if (!showForms)
            {
                _title.text = _bugMode
                    ? "Report a bug"
                    : "Playtest feedback";
                _description.text = "";
                _meta.text = "";
            }

            _status.text = status ?? "";
            _retry.style.display = showRetry
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _feedbackForm.style.display =
                showForms && !_bugMode
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            _bugForm.style.display =
                showForms && _bugMode
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            _send.style.display = showForms
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _cancel.text = showForms ? "Cancel" : "Close";
            RefreshSendState();
        }

        private void ResetForm()
        {
            _rating = 0;
            _playLengthIndex = -1;
            _severityIndex = 1;
            _wouldWishlist = false;
            _liked.value = "";
            _confused.value = "";
            _pitch.value = "";
            _includeWishlist.value = false;
            _wishlistChoices.style.display = DisplayStyle.None;
            _bugTitle.value = "";
            _bugDescription.value = "";
            _surveyQuestions.Clear();
            _answerFields = new TextField[0];
            RefreshSelectionClasses();
        }

        private void BuildSurveyQuestions(string[] questions)
        {
            _surveyQuestions.Clear();
            questions = questions ?? new string[0];
            _answerFields = new TextField[questions.Length];
            for (int index = 0; index < questions.Length; index++)
            {
                var field = new TextField(questions[index])
                {
                    multiline = true
                };
                field.AddToClassList("feedback-text-area");
                _surveyQuestions.Add(field);
                _answerFields[index] = field;
            }
        }

        private Button[] BindButtonRange(
            VisualElement root,
            string prefix,
            int count,
            Action<int> select)
        {
            var buttons = new Button[count];
            for (int index = 0; index < count; index++)
            {
                int selectedIndex = index;
                buttons[index] = Require<Button>(
                    root,
                    prefix + "-" + index);
                buttons[index].clicked += () => select(selectedIndex);
            }
            return buttons;
        }

        private void SetRating(int index)
        {
            _rating = index + 1;
            RefreshSelectionClasses();
            RefreshSendState();
        }

        private void SetPlayLength(int index)
        {
            _playLengthIndex = index;
            RefreshSelectionClasses();
        }

        private void SetWishlist(int index)
        {
            _wouldWishlist = index == 0;
            RefreshSelectionClasses();
        }

        private void SetSeverity(int index)
        {
            _severityIndex = index;
            RefreshSelectionClasses();
        }

        private void RefreshSelectionClasses()
        {
            EnableSelection(_ratingButtons, _rating - 1);
            EnableSelection(_playLengthButtons, _playLengthIndex);
            EnableSelection(
                _wishlistButtons,
                _includeWishlist.value
                    ? (_wouldWishlist ? 0 : 1)
                    : -1);
            EnableSelection(_severityButtons, _severityIndex);
        }

        private static void EnableSelection(
            Button[] buttons,
            int selectedIndex)
        {
            if (buttons == null) return;
            for (int index = 0; index < buttons.Length; index++)
            {
                buttons[index].EnableInClassList(
                    "is-selected",
                    index == selectedIndex);
            }
        }

        private void RefreshSendState()
        {
            bool canSubmit = _ready &&
                !_loading &&
                !_submitting &&
                (_bugMode
                    ? !string.IsNullOrWhiteSpace(_bugTitle.value)
                    : _rating >= 1 && _rating <= 5);
            _send.SetEnabled(canSubmit);
        }

        private void Submit()
        {
            if (!_ready || _submitting || _client == null) return;
            _submitting = true;
            _status.text = "Sending…";
            RefreshSendState();

            if (_bugMode)
            {
                var submission = new IndieableBugReportSubmission
                {
                    Title = _bugTitle.value ?? "",
                    Description = _bugDescription.value ?? "",
                    Severity = SeverityKeys[Mathf.Clamp(
                        _severityIndex,
                        0,
                        SeverityKeys.Length - 1)]
                };
                StartCoroutine(
                    _client.SubmitBugReport(
                        submission,
                        _ => SubmissionComplete(),
                        OnSubmitError));
                return;
            }

            string[] questions = _config.SurveyQuestions ??
                                 new string[0];
            var answers = new IndieableSurveyAnswer[questions.Length];
            for (int index = 0; index < questions.Length; index++)
            {
                answers[index] = new IndieableSurveyAnswer
                {
                    Question = questions[index],
                    Answer = _answerFields[index].value ?? ""
                };
            }

            var feedback = new IndieableFeedbackSubmission
            {
                Rating = _rating,
                Liked = _liked.value ?? "",
                Confused = _confused.value ?? "",
                IncludeWouldWishlist = _includeWishlist.value,
                WouldWishlist = _wouldWishlist,
                PlayLength = _playLengthIndex >= 0 &&
                             _playLengthIndex < PlayLengthKeys.Length
                    ? PlayLengthKeys[_playLengthIndex]
                    : "",
                Pitch = _pitch.value ?? "",
                Answers = answers
            };
            StartCoroutine(
                _client.SubmitFeedback(
                    feedback,
                    _ => SubmissionComplete(),
                    OnSubmitError));
        }

        private void SubmissionComplete()
        {
            _submitting = false;
            _ready = false;
            ApplyPresentation(
                _bugMode
                    ? "Bug report sent. Thank you."
                    : "Feedback sent. Thank you.",
                showRetry: false,
                showForms: false);
        }

        private void OnSubmitError(IndieableError error)
        {
            _submitting = false;
            _status.text = error != null
                ? error.Message
                : "The submission could not be sent.";
            RefreshSendState();
        }

        private void Hide()
        {
            ReleaseVisibility();
            Destroy(gameObject);
        }

        private void ReleaseVisibility()
        {
            if (!_visibilityReported) return;
            _visibilityReported = false;
            _client?.NotifyFeedbackVisibility(false);
        }

        private void OnDestroy()
        {
            ReleaseVisibility();
            if (_panelSettings != null)
                Destroy(_panelSettings);
            if (_instance == this) _instance = null;
        }

        private static string BuildMeta(
            IndieableFeedbackConfig config)
        {
            string value = config.Anonymous
                ? "Submitted anonymously to the developer."
                : "Submitted to the developer.";
            if (config.Round == null) return value;

            value += " Round " + config.Round.Number;
            if (!string.IsNullOrWhiteSpace(config.Round.BuildLabel))
                value += " · " + config.Round.BuildLabel;
            if (!string.IsNullOrWhiteSpace(config.Round.Focus))
                value += " · Focus: " + config.Round.Focus;
            return value;
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
                    "Indieable feedback UI is missing element '" +
                    name + "'.");
            }
            return value;
        }
    }
}
