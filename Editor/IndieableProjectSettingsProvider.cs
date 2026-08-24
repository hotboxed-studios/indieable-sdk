using IndieableSdk.EventBus;
using UnityEditor;
using UnityEngine;

namespace IndieableSdk.Editor
{
    internal sealed class IndieableProjectSettingsProvider : SettingsProvider
    {
        private const string ProviderPath = "Project/Indieable";

        private IndieableProjectSettings settings;
        private SerializedObject serializedSettings;

        private IndieableProjectSettingsProvider()
            : base(ProviderPath, SettingsScope.Project)
        {
            keywords = new[]
            {
                "Indieable",
                "Public Game Key",
                "Privacy",
                "Telemetry",
                "Feedback",
                "Event Routing",
                "Request Headers",
                "Vercel"
            };
        }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new IndieableProjectSettingsProvider();
        }

        [MenuItem("Tools/Indieable/Open Settings", priority = 100)]
        private static void OpenSettings()
        {
            SettingsService.OpenProjectSettings(ProviderPath);
        }

        public override void OnActivate(
            string searchContext,
            UnityEngine.UIElements.VisualElement rootElement)
        {
            LoadSettings();
        }

        public override void OnGUI(string searchContext)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Indieable Connect",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This is project-owned client configuration. A Public Game Key " +
                "is intended to ship in the game; never enter a Server Secret or " +
                "another private credential here.",
                MessageType.Info);

            if (settings == null)
            {
                EditorGUILayout.HelpBox(
                    "No Indieable project settings asset exists. Indieable remains disabled.",
                    MessageType.Warning);
                if (GUILayout.Button("Create Indieable Project Settings"))
                {
                    CreateSettings();
                }
                return;
            }

            if (serializedSettings == null ||
                serializedSettings.targetObject != settings)
            {
                serializedSettings = new SerializedObject(settings);
            }

            serializedSettings.Update();
            DrawProperty("baseUrl", "Base URL");
            DrawProperty("publicGameKey", "Public Game Key");
            DrawProperty("environment", "Environment");
            DrawProperty("localProfileRef", "Local Profile Reference");
            DrawProperty("requestTimeoutSeconds", "Request Timeout Seconds");
            DrawProperty("maxTransientRetries", "Maximum Transient Retries");
            DrawProperty("logErrors", "Log Errors");
            DrawProperty("autoClearInvalidIdentity", "Recover Invalid Identity");
            DrawProperty("requestHeaders", "Optional Request Headers");
            DrawRequestHeaderPresets();
            DrawProperty("eventRouting", "Event Routing");
            serializedSettings.ApplyModifiedProperties();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("SDK Version", "unity-0.4.2");
                EditorGUILayout.TextField("Build Version", Application.version);
                EditorGUILayout.TextField("Platform", Application.platform.ToString());
                EditorGUILayout.TextField("Engine", "Unity " + Application.unityVersion);
                EditorGUILayout.TextField(
                    "Asset Path",
                    IndieableProjectSettings.DefaultAssetPath);
            }

            if (settings.TryValidate(out string issue))
            {
                EditorGUILayout.HelpBox(
                    settings.EventRouting == null
                        ? "Connection settings are valid. Assign Event Routing before forwarding bus events."
                        : "Connection settings are valid.",
                    settings.EventRouting == null
                        ? MessageType.Warning
                        : MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Indieable is disabled: " + issue,
                    MessageType.Warning);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select Settings Asset"))
            {
                Selection.activeObject = settings;
                EditorGUIUtility.PingObject(settings);
            }
            using (new EditorGUI.DisabledScope(settings.EventRouting == null))
            {
                if (GUILayout.Button("Select Event Routing"))
                {
                    Selection.activeObject = settings.EventRouting;
                    EditorGUIUtility.PingObject(settings.EventRouting);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawProperty(string propertyName, string label)
        {
            SerializedProperty property =
                serializedSettings.FindProperty(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(
                    property,
                    new GUIContent(label));
            }
        }

        private void DrawRequestHeaderPresets()
        {
            SerializedProperty headers =
                serializedSettings.FindProperty("requestHeaders");
            if (headers == null) return;

            EditorGUILayout.HelpBox(
                "Optional headers apply to every Indieable request. Never " +
                "store a private credential as a literal value in this " +
                "asset; use an environment-variable value source instead.",
                MessageType.Info);

            bool hasVercelPreset = false;
            for (int index = 0; index < headers.arraySize; index++)
            {
                SerializedProperty name = headers
                    .GetArrayElementAtIndex(index)
                    .FindPropertyRelative("Name");
                if (name != null && string.Equals(
                        name.stringValue,
                        IndieableRequestHeader.VercelProtectionBypassHeader,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    hasVercelPreset = true;
                    break;
                }
            }

            using (new EditorGUI.DisabledScope(hasVercelPreset))
            {
                if (GUILayout.Button("Add Vercel Protection Bypass"))
                {
                    int index = headers.arraySize;
                    headers.InsertArrayElementAtIndex(index);
                    SerializedProperty element =
                        headers.GetArrayElementAtIndex(index);
                    element.FindPropertyRelative("Enabled").boolValue = true;
                    element.FindPropertyRelative("Name").stringValue =
                        IndieableRequestHeader.VercelProtectionBypassHeader;
                    element.FindPropertyRelative("Value").stringValue = "";
                    element.FindPropertyRelative(
                            "ValueEnvironmentVariable")
                        .stringValue = IndieableRequestHeader
                        .VercelProtectionBypassEnvironmentVariable;
                }
            }

            string bypass = System.Environment.GetEnvironmentVariable(
                IndieableRequestHeader
                    .VercelProtectionBypassEnvironmentVariable);
            if (hasVercelPreset && string.IsNullOrEmpty(bypass))
            {
                EditorGUILayout.HelpBox(
                    IndieableRequestHeader
                        .VercelProtectionBypassEnvironmentVariable +
                    " is not available to this Editor process. Restart the " +
                    "Editor after defining it locally.",
                    MessageType.Warning);
            }
        }

        private void LoadSettings()
        {
            settings = AssetDatabase.LoadAssetAtPath<IndieableProjectSettings>(
                IndieableProjectSettings.DefaultAssetPath);
            serializedSettings = settings != null
                ? new SerializedObject(settings)
                : null;
        }

        private void CreateSettings()
        {
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "Indieable");

            settings = ScriptableObject.CreateInstance<IndieableProjectSettings>();
            AssetDatabase.CreateAsset(
                settings,
                IndieableProjectSettings.DefaultAssetPath);
            AssetDatabase.SaveAssets();
            serializedSettings = new SerializedObject(settings);
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
