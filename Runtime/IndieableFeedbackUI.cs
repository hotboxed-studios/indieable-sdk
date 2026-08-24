using System;
using UnityEngine;

namespace IndieableSdk
{
    // Small dependency-free IMGUI form. It creates its own hidden runtime object,
    // never requires a host Canvas, and never changes Time.timeScale. Games that
    // already have UI can bypass it and call the lower-level submission methods.
    internal sealed class IndieableFeedbackUI : MonoBehaviour
    {
        private static IndieableFeedbackUI _instance;
        private IndieableClient _client;
        private bool _bugMode;
        private bool _loading;
        private bool _submitting;
        private bool _success;
        private string _error = "";
        private IndieableFeedbackConfig _config;
        private Rect _window;
        private Vector2 _scroll;

        private int _rating;
        private string _liked = "";
        private string _confused = "";
        private string _pitch = "";
        private bool _includeWishlist;
        private bool _wouldWishlist;
        private int _playLengthIndex = -1;
        private string[] _answers = new string[0];

        private string _bugTitle = "";
        private string _bugDescription = "";
        private int _severityIndex = 1;

        private static readonly string[] PlayLengthKeys = { "lt15", "15to30", "30to60", "1to2h", "2hplus" };
        private static readonly string[] PlayLengthLabels = { "Under 15 min", "15–30 min", "30–60 min", "1–2 hours", "2+ hours" };
        private static readonly string[] SeverityKeys = { "minor", "major", "blocker" };
        private static readonly string[] SeverityLabels = { "Minor", "Major", "Blocker" };

        internal static void Open(IndieableClient client, bool bugMode)
        {
            if (_instance == null)
            {
                var gameObject = new GameObject("Indieable Feedback UI");
                gameObject.hideFlags = HideFlags.HideAndDontSave;
                DontDestroyOnLoad(gameObject);
                _instance = gameObject.AddComponent<IndieableFeedbackUI>();
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

        private void Show(IndieableClient client, bool bugMode)
        {
            _client = client;
            _bugMode = bugMode;
            _loading = true;
            _submitting = false;
            _success = false;
            _error = "";
            _config = null;
            enabled = true;
            _client.NotifyFeedbackVisibility(true);
            StartCoroutine(_client.GetFeedbackConfig(OnConfigLoaded, OnRequestError));
        }

        private void Hide()
        {
            var client = _client;
            _client = null;
            if (client != null) client.NotifyFeedbackVisibility(false);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            var client = _client;
            _client = null;
            if (client != null) client.NotifyFeedbackVisibility(false);
            if (_instance == this) _instance = null;
        }

        private void OnConfigLoaded(IndieableFeedbackConfig config)
        {
            _loading = false;
            _config = config;
            var questions = config != null && config.SurveyQuestions != null
                ? config.SurveyQuestions
                : new string[0];
            _answers = new string[questions.Length];
        }

        private void OnRequestError(IndieableError error)
        {
            _loading = false;
            _submitting = false;
            _error = error != null ? error.Message : "Indieable request failed.";
        }

        private void OnGUI()
        {
            if (!enabled) return;
            var width = Mathf.Min(680f, Mathf.Max(360f, Screen.width - 40f));
            var height = Mathf.Min(760f, Mathf.Max(360f, Screen.height - 40f));
            _window = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);
            _window = GUI.ModalWindow(GetInstanceID(), _window, DrawWindow, _bugMode ? "Report a bug" : "Playtest feedback");
        }

        private void DrawWindow(int id)
        {
            GUILayout.Space(8f);
            if (_loading)
            {
                GUILayout.Label("Loading Indieable…");
                DrawCloseButton();
                GUI.DragWindow();
                return;
            }

            if (!string.IsNullOrEmpty(_error))
            {
                GUILayout.Label(_error);
                GUILayout.Space(8f);
                if (GUILayout.Button("Try again", GUILayout.Height(32f))) Show(_client, _bugMode);
                DrawCloseButton();
                GUI.DragWindow();
                return;
            }

            if (_config == null || !_config.Available)
            {
                GUILayout.Label("This game does not have an active in-game playtest form.");
                DrawCloseButton();
                GUI.DragWindow();
                return;
            }

            if (_success)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(_bugMode ? "Bug report sent. Thank you." : "Feedback sent. Thank you.", CenteredLabel());
                GUILayout.Space(12f);
                if (GUILayout.Button("Close", GUILayout.Height(34f))) Hide();
                GUILayout.FlexibleSpace();
                GUI.DragWindow();
                return;
            }

            if (!string.IsNullOrWhiteSpace(_config.Title)) GUILayout.Label(_config.Title, HeadingLabel());
            if (!string.IsNullOrWhiteSpace(_config.Description)) GUILayout.Label(_config.Description, WrapLabel());
            if (_config.Round != null)
            {
                var round = "Round " + _config.Round.Number;
                if (!string.IsNullOrWhiteSpace(_config.Round.BuildLabel)) round += " · " + _config.Round.BuildLabel;
                GUILayout.Label(round);
                if (!string.IsNullOrWhiteSpace(_config.Round.Focus)) GUILayout.Label("Focus: " + _config.Round.Focus, WrapLabel());
            }
            if (_config.Anonymous) GUILayout.Label("Submitting anonymously to the developer.");
            GUILayout.Space(8f);

            _scroll = GUILayout.BeginScrollView(_scroll);
            if (_bugMode) DrawBugForm();
            else DrawFeedbackForm();
            GUILayout.EndScrollView();

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel", GUILayout.Height(34f))) Hide();
            GUI.enabled = !_submitting && CanSubmit();
            if (GUILayout.Button(_submitting ? "Sending…" : "Send", GUILayout.Height(34f))) Submit();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUI.DragWindow(new Rect(0, 0, _window.width, 28f));
        }

        private void DrawFeedbackForm()
        {
            GUILayout.Label("Overall rating");
            GUILayout.BeginHorizontal();
            for (var i = 1; i <= 5; i++)
            {
                var label = i <= _rating ? "★" : "☆";
                if (GUILayout.Button(label, GUILayout.Height(34f))) _rating = i;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("What did you enjoy?");
            _liked = GUILayout.TextArea(_liked, GUILayout.MinHeight(64f));
            GUILayout.Label("What was confusing or frustrating?");
            _confused = GUILayout.TextArea(_confused, GUILayout.MinHeight(64f));

            GUILayout.Label("How long did you play?");
            _playLengthIndex = GUILayout.SelectionGrid(_playLengthIndex, PlayLengthLabels, 2);

            _includeWishlist = GUILayout.Toggle(_includeWishlist, "Answer wishlist question");
            if (_includeWishlist)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(_wouldWishlist, "I would wishlist it")) _wouldWishlist = true;
                if (GUILayout.Toggle(!_wouldWishlist, "Not yet")) _wouldWishlist = false;
                GUILayout.EndHorizontal();
            }

            GUILayout.Label("How would you describe the game to a friend?");
            _pitch = GUILayout.TextArea(_pitch, GUILayout.MinHeight(56f));

            var questions = _config.SurveyQuestions ?? new string[0];
            for (var i = 0; i < questions.Length; i++)
            {
                GUILayout.Label(questions[i]);
                _answers[i] = GUILayout.TextArea(_answers[i] ?? "", GUILayout.MinHeight(56f));
            }
        }

        private void DrawBugForm()
        {
            GUILayout.Label("Title");
            _bugTitle = GUILayout.TextField(_bugTitle);
            GUILayout.Label("What happened? Include reproduction steps when possible.");
            _bugDescription = GUILayout.TextArea(_bugDescription, GUILayout.MinHeight(160f));
            GUILayout.Label("Severity");
            _severityIndex = GUILayout.SelectionGrid(_severityIndex, SeverityLabels, 3);
        }

        private bool CanSubmit()
        {
            return _bugMode ? !string.IsNullOrWhiteSpace(_bugTitle) : _rating >= 1 && _rating <= 5;
        }

        private void Submit()
        {
            _submitting = true;
            _error = "";
            if (_bugMode)
            {
                var submission = new IndieableBugReportSubmission
                {
                    Title = _bugTitle,
                    Description = _bugDescription,
                    Severity = SeverityKeys[Mathf.Clamp(_severityIndex, 0, SeverityKeys.Length - 1)]
                };
                StartCoroutine(_client.SubmitBugReport(submission, delegate(string _) { SubmissionComplete(); }, OnRequestError));
                return;
            }

            var questions = _config.SurveyQuestions ?? new string[0];
            var answerRows = new IndieableSurveyAnswer[questions.Length];
            for (var i = 0; i < questions.Length; i++)
            {
                answerRows[i] = new IndieableSurveyAnswer { Question = questions[i], Answer = _answers[i] ?? "" };
            }
            var feedback = new IndieableFeedbackSubmission
            {
                Rating = _rating,
                Liked = _liked,
                Confused = _confused,
                IncludeWouldWishlist = _includeWishlist,
                WouldWishlist = _wouldWishlist,
                PlayLength = _playLengthIndex >= 0 && _playLengthIndex < PlayLengthKeys.Length
                    ? PlayLengthKeys[_playLengthIndex]
                    : "",
                Pitch = _pitch,
                Answers = answerRows
            };
            StartCoroutine(_client.SubmitFeedback(feedback, delegate(string _) { SubmissionComplete(); }, OnRequestError));
        }

        private void SubmissionComplete()
        {
            _submitting = false;
            _success = true;
        }

        private void DrawCloseButton()
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Height(34f))) Hide();
        }

        private static GUIStyle HeadingLabel()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontSize = 18;
            style.fontStyle = FontStyle.Bold;
            style.wordWrap = true;
            return style;
        }

        private static GUIStyle WrapLabel()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.wordWrap = true;
            return style;
        }

        private static GUIStyle CenteredLabel()
        {
            var style = HeadingLabel();
            style.alignment = TextAnchor.MiddleCenter;
            return style;
        }
    }
}
