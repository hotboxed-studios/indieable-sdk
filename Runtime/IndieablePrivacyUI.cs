using System;
using UnityEngine;

namespace IndieableSdk
{
    internal sealed class IndieablePrivacyUI : MonoBehaviour
    {
        private static IndieablePrivacyUI _instance;
        private IndieableClient _client;
        private IndieablePrivacyManifest _manifest;
        private IndieablePrivacyPreferences _preferences;
        private Vector2 _scroll;
        private string _status = "Loading privacy notice…";
        private bool _busy;
        private Rect _windowRect;

        internal static void Open(IndieableClient client)
        {
            if (_instance != null)
            {
                _instance._client = client;
                return;
            }
            var host = new GameObject("Indieable Privacy Preferences");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<IndieablePrivacyUI>();
            _instance._client = client;
            client.NotifyPrivacyVisibility(true);
        }

        internal static void Close()
        {
            if (_instance == null) return;
            var client = _instance._client;
            Destroy(_instance.gameObject);
            _instance = null;
            if (client != null) client.NotifyPrivacyVisibility(false);
        }

        private void Start()
        {
            _windowRect = new Rect(0, 0, Mathf.Min(680, Screen.width - 32), Mathf.Min(650, Screen.height - 32));
            _windowRect.center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Load();
        }

        private void Load()
        {
            if (_client == null) return;
            _busy = true;
            IndieableRuntime.Instance.Run(_client.GetPrivacyManifest(
                delegate(IndieablePrivacyManifest manifest)
                {
                    _manifest = manifest;
                    _status = manifest.Configured
                        ? "Choose separately for each optional purpose."
                        : "This game has not published an optional Player Data notice.";
                    if (_client.IsConnected)
                    {
                        IndieableRuntime.Instance.Run(_client.GetPrivacyPreferences(
                            delegate(IndieablePrivacyPreferences preferences)
                            {
                                _preferences = preferences;
                                _busy = false;
                            },
                            delegate(IndieableError error)
                            {
                                _status = error.Message;
                                _busy = false;
                            }));
                    }
                    else _busy = false;
                },
                delegate(IndieableError error)
                {
                    _status = error.Message;
                    _busy = false;
                }));
        }

        private void OnGUI()
        {
            var previousDepth = GUI.depth;
            GUI.depth = -10000;
            var overlay = new Color(0f, 0f, 0f, 0.62f);
            var previousColor = GUI.color;
            GUI.color = overlay;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;
            _windowRect = GUI.Window(GetInstanceID(), _windowRect, DrawWindow, "Indieable Player Data Preferences");
            GUI.depth = previousDepth;
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();
            _scroll = GUILayout.BeginScrollView(_scroll, false, true);

            GUILayout.Label(_manifest != null && !string.IsNullOrWhiteSpace(_manifest.GameTitle)
                ? _manifest.GameTitle
                : "Game privacy notice", GUI.skin.box);

            if (_manifest != null && _manifest.Controller != null)
            {
                GUILayout.Label("Data controller: " + (_manifest.Controller.Name ?? "Not provided"));
                if (!string.IsNullOrWhiteSpace(_manifest.Controller.Contact))
                    GUILayout.Label("Privacy contact: " + _manifest.Controller.Contact);
                if (!string.IsNullOrWhiteSpace(_manifest.Controller.PrivacyPolicyUrl) &&
                    GUILayout.Button("Open full privacy policy"))
                    Application.OpenURL(_manifest.Controller.PrivacyPolicyUrl);
            }

            GUILayout.Space(8);
            GUILayout.Label(_status ?? "");
            GUILayout.Space(8);

            if (_manifest != null)
            {
                DrawPurpose(_manifest.FindPurpose(Indieable.GameplayTelemetryPurpose));
                GUILayout.Space(10);
                DrawPurpose(_manifest.FindPurpose(Indieable.DiagnosticsPurpose));
            }

            GUILayout.Space(12);
            GUILayout.Label("Account linking, Steam, Challenges, feedback, and marketing use separate permissions. Choosing one option here does not enable the others.");
            if (_client != null && _client.Session != null && _client.Session.PersistentIdentity)
            {
                GUILayout.Space(8);
                GUILayout.Label("Current game-scoped Player: " + (_client.Session.PublicPlayerRef ?? "persistent Player"));
                GUI.enabled = !_busy;
                if (GUILayout.Button("Reset this local Indieable identity"))
                {
                    _busy = true;
                    _status = "Resetting local identity…";
                    IndieableRuntime.Instance.Run(_client.ResetLocalIdentity(
                        delegate
                        {
                            _preferences = null;
                            _status = "Local Indieable identity reset. Optional collection is stopped on this installation.";
                            _busy = false;
                        },
                        delegate(IndieableError error)
                        {
                            _status = error.Message;
                            _busy = false;
                        }));
                }
                GUI.enabled = true;
            }

            GUILayout.EndScrollView();
            GUILayout.Space(8);
            if (GUILayout.Button("Close")) Close();
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, _windowRect.width, 24));
        }

        private void DrawPurpose(IndieablePrivacyPurpose purpose)
        {
            if (purpose == null) return;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(purpose.DisplayName ?? purpose.PurposeKey);
            GUILayout.Label(purpose.Description ?? "");
            if (purpose.HasRetentionDays) GUILayout.Label("Retention: up to " + purpose.RetentionDays + " days");

            var granted = _preferences != null && _preferences.IsGranted(purpose.PurposeKey);
            GUILayout.Label("Current choice: " + (granted ? "Enabled" : "Off"));

            GUI.enabled = !_busy && purpose.Enabled;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Keep off / disable", GUILayout.MinWidth(170)))
                ChangePreference(purpose.PurposeKey, false);
            if (GUILayout.Button("Enable", GUILayout.MinWidth(170)))
                ChangePreference(purpose.PurposeKey, true);
            GUILayout.EndHorizontal();
            GUI.enabled = true;

            if (!purpose.Enabled)
                GUILayout.Label("This optional purpose is not enabled by the game developer.");
            GUILayout.EndVertical();
        }

        private void ChangePreference(string purposeKey, bool enabled)
        {
            if (_client == null || _busy) return;
            _busy = true;
            _status = enabled ? "Saving permission…" : "Saving withdrawal…";

            Action apply = delegate
            {
                IndieableRuntime.Instance.Run(_client.SetPrivacyPreference(
                    purposeKey, enabled, Application.systemLanguage.ToString(), false,
                    delegate(IndieablePrivacyPreferences preferences)
                    {
                        _preferences = preferences;
                        _status = enabled ? "Preference enabled." : "Preference disabled.";
                        _busy = false;
                    },
                    delegate(IndieableError error)
                    {
                        _status = error.Message;
                        _busy = false;
                    }));
            };

            if (_client.IsConnected) apply();
            else IndieableRuntime.Instance.Run(_client.Connect(
                delegate(IndieableSessionInfo _) { apply(); },
                delegate(IndieableError error)
                {
                    _status = error.Message;
                    _busy = false;
                }));
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
