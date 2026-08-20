using IndieableSdk;
using UnityEngine;

public sealed class IndieableQuickStart : MonoBehaviour
{
    [SerializeField] private string publicGameKey = "ind_pub_replace_me";
    [SerializeField] private string baseUrl = "https://indieable.com";
    [SerializeField] private string localProfileRef = "";

    private void Start()
    {
        Indieable.Initialize(new IndieableOptions
        {
            PublicGameKey = publicGameKey,
            BaseUrl = baseUrl,
            BuildVersion = Application.version,
            Environment = Debug.isDebugBuild ? "development" : "production",
            LocalProfileRef = localProfileRef,
            FeedbackVisibilityChanged = delegate(bool visible)
            {
                Debug.Log("Indieable feedback visible: " + visible);
            },
            PrivacyVisibilityChanged = delegate(bool visible)
            {
                Debug.Log("Indieable privacy UI visible: " + visible);
            }
        });

        // Manifest lookup is safe before Connect: no session or persistent
        // Installation/Game Player is created by this request.
        Indieable.GetPrivacyManifest(
            delegate(IndieablePrivacyManifest manifest)
            {
                Debug.Log("Indieable privacy manifest configured: " + manifest.Configured);
                Connect();
            },
            delegate(IndieableError error)
            {
                Debug.LogWarning(error);
                Connect();
            });
    }

    private void Connect()
    {
        Indieable.Connect(
            delegate(IndieableSessionInfo session)
            {
                Debug.Log("Indieable identity: " + session.IdentityState + " / " + session.PublicPlayerRef);
                Indieable.SendEvent(
                    "indieable.connect_test",
                    "{\"message\":\"Unity quick-start connected.\"}",
                    true);
            },
            delegate(IndieableError error) { Debug.LogWarning(error); });
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F7)) Indieable.OpenPrivacyPreferences();
        if (Input.GetKeyDown(KeyCode.F8)) Indieable.OpenFeedback();
        if (Input.GetKeyDown(KeyCode.F9)) Indieable.OpenBugReport();
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Indieable.ClosePrivacyPreferences();
            Indieable.CloseFeedback();
        }
    }
}
