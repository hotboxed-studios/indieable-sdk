using IndieableSdk;
using UnityEngine;

public sealed class IndieableQuickStart : MonoBehaviour
{
    private bool _sdkUiVisible;
    private bool _previousCursorVisible;
    private CursorLockMode _previousCursorLock;

    private void OnEnable()
    {
        Indieable.PrivacyVisibilityChanged += OnSdkUiVisibilityChanged;
        Indieable.FeedbackVisibilityChanged += OnSdkUiVisibilityChanged;
        Indieable.SessionConnected += OnSessionConnected;
        ReconcileSdkUiVisibility();
    }

    private void Start()
    {
        if (Indieable.IsInitialized)
        {
            Debug.Log(
                "[Indieable Sample] SDK initialized automatically from " +
                "Project Settings. The startup consent form is SDK-owned.");
        }
        else
        {
            Debug.LogWarning(
                "[Indieable Sample] Create and configure Project Settings > " +
                "Indieable, then enter Play Mode again.");
        }
    }

    private void OnDisable()
    {
        Indieable.PrivacyVisibilityChanged -= OnSdkUiVisibilityChanged;
        Indieable.FeedbackVisibilityChanged -= OnSdkUiVisibilityChanged;
        Indieable.SessionConnected -= OnSessionConnected;
        SetSdkUiVisible(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F7))
            Indieable.OpenPrivacyPreferences();
        if (Input.GetKeyDown(KeyCode.F8))
            Indieable.OpenFeedback();
        if (Input.GetKeyDown(KeyCode.F9))
            Indieable.OpenBugReport();
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Indieable.ClosePrivacyPreferences();
            Indieable.CloseFeedback();
        }
    }

    private void OnSessionConnected(IndieableSessionInfo session)
    {
        Debug.Log(
            "[Indieable Sample] Connected as " +
            session.IdentityState + ".");
        Indieable.SendEvent(
            "indieable.connect_test",
            "{\"message\":\"Unity auto-bootstrap sample connected.\"}",
            true);
    }

    private void OnSdkUiVisibilityChanged(bool _)
    {
        ReconcileSdkUiVisibility();
    }

    private void ReconcileSdkUiVisibility()
    {
        SetSdkUiVisible(
            Indieable.IsPrivacyPreferencesVisible ||
            Indieable.IsFeedbackVisible);
    }

    private void SetSdkUiVisible(bool visible)
    {
        if (_sdkUiVisible == visible) return;
        _sdkUiVisible = visible;
        if (visible)
        {
            _previousCursorVisible = Cursor.visible;
            _previousCursorLock = Cursor.lockState;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        Cursor.lockState = _previousCursorLock;
        Cursor.visible = _previousCursorVisible;
    }
}
