// Minimal compile-only Unity API surface for zero-secret CI.
// This file is not included in the released Unity package.

using System;
using System.Collections;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeField : Attribute { }

    public class Object
    {
        public string name { get; set; }
        public static void Destroy(Object target) { }
        public static void DontDestroyOnLoad(Object target) { }
        public int GetInstanceID() { return 0; }
    }

    public class Component : Object
    {
        public GameObject gameObject { get; } = new GameObject();

        public T GetComponent<T>() where T : Component, new()
        {
            return new T();
        }
    }

    public class Behaviour : Component
    {
        public bool enabled { get; set; }
    }

    public class MonoBehaviour : Behaviour
    {
        protected Coroutine StartCoroutine(IEnumerator routine) { return null; }
    }

    public sealed class Coroutine { }

    public class GameObject : Object
    {
        public HideFlags hideFlags;

        public GameObject() { }
        public GameObject(string name) { }

        public T AddComponent<T>() where T : Component, new()
        {
            return new T();
        }
    }

    [Flags]
    public enum HideFlags
    {
        None = 0,
        HideAndDontSave = 61
    }

    public static class Debug
    {
        public static bool isDebugBuild { get; set; }
        public static void Log(object message) { }
        public static void LogWarning(object message) { }
    }

    public enum RuntimePlatform
    {
        WindowsPlayer,
        OSXPlayer,
        LinuxPlayer
    }

    public enum SystemLanguage
    {
        English
    }

    public static class Application
    {
        public static bool isBatchMode { get; set; }
        public static RuntimePlatform platform { get; set; }
        public static string persistentDataPath { get; set; } = string.Empty;
        public static string unityVersion { get; set; } = string.Empty;
        public static string version { get; set; } = string.Empty;
        public static SystemLanguage systemLanguage { get; set; }
        public static void OpenURL(string url) { }
    }

    public static class PlayerPrefs
    {
        public static int GetInt(string key, int defaultValue = 0)
        {
            return defaultValue;
        }

        public static void SetInt(string key, int value) { }
        public static void Save() { }
    }

    public static class SystemInfo
    {
        public static Rendering.GraphicsDeviceType graphicsDeviceType
        {
            get;
            set;
        }
    }

    public static class JsonUtility
    {
        public static string ToJson(object value) { return "{}"; }
        public static T FromJson<T>(string json) { return default(T); }
    }

    public sealed class WaitForSecondsRealtime
    {
        public WaitForSecondsRealtime(float seconds) { }
    }

    public struct Vector2
    {
        public float x;
        public float y;

        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }
    }

    public struct Rect
    {
        public float x;
        public float y;
        public float width;
        public float height;

        public Rect(float x, float y, float width, float height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }

        public Vector2 center { get; set; }
    }

    public static class Mathf
    {
        public static float Min(float a, float b) { return a < b ? a : b; }
        public static int Min(int a, int b) { return a < b ? a : b; }
        public static float Max(float a, float b) { return a > b ? a : b; }
        public static int Max(int a, int b) { return a > b ? a : b; }
        public static int Clamp(int value, int minimum, int maximum)
        {
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }
    }

    public static class Screen
    {
        public static int width { get; set; } = 1920;
        public static int height { get; set; } = 1080;
    }

    public delegate void WindowFunction(int id);

    public static class GUI
    {
        public static bool enabled { get; set; } = true;
        public static int depth { get; set; }
        public static Color color { get; set; }
        public static GUISkin skin { get; } = new GUISkin();

        public static Rect ModalWindow(int id, Rect rect, WindowFunction function, string title)
        {
            return rect;
        }

        public static Rect Window(int id, Rect rect, WindowFunction function, string title)
        {
            return rect;
        }

        public static void DrawTexture(Rect position, Texture image) { }
        public static void DragWindow() { }
        public static void DragWindow(Rect position) { }
    }

    public static class GUILayout
    {
        public static void Space(float pixels) { }
        public static void FlexibleSpace() { }
        public static void Label(string text, params GUILayoutOption[] options) { }
        public static void Label(string text, GUIStyle style, params GUILayoutOption[] options) { }
        public static bool Button(string text, params GUILayoutOption[] options) { return false; }
        public static string TextArea(string text, params GUILayoutOption[] options) { return text; }
        public static string TextField(string text, params GUILayoutOption[] options) { return text; }
        public static bool Toggle(bool value, string text, params GUILayoutOption[] options) { return value; }
        public static int SelectionGrid(int selected, string[] texts, int xCount, params GUILayoutOption[] options)
        {
            return selected;
        }

        public static void BeginHorizontal(params GUILayoutOption[] options) { }
        public static void EndHorizontal() { }
        public static void BeginVertical(params GUILayoutOption[] options) { }
        public static void BeginVertical(GUIStyle style, params GUILayoutOption[] options) { }
        public static void EndVertical() { }

        public static Vector2 BeginScrollView(Vector2 scrollPosition, params GUILayoutOption[] options)
        {
            return scrollPosition;
        }

        public static Vector2 BeginScrollView(
            Vector2 scrollPosition,
            bool alwaysShowHorizontal,
            bool alwaysShowVertical,
            params GUILayoutOption[] options)
        {
            return scrollPosition;
        }

        public static void EndScrollView() { }
        public static GUILayoutOption Height(float value) { return new GUILayoutOption(); }
        public static GUILayoutOption MinHeight(float value) { return new GUILayoutOption(); }
        public static GUILayoutOption MinWidth(float value) { return new GUILayoutOption(); }
    }

    public sealed class GUILayoutOption { }

    public sealed class GUISkin
    {
        public GUIStyle label { get; } = new GUIStyle();
        public GUIStyle box { get; } = new GUIStyle();
    }

    public class GUIStyle
    {
        public GUIStyle() { }
        public GUIStyle(GUIStyle other) { }

        public int fontSize { get; set; }
        public FontStyle fontStyle { get; set; }
        public bool wordWrap { get; set; }
        public TextAnchor alignment { get; set; }
    }

    public enum FontStyle
    {
        Normal,
        Bold
    }

    public enum TextAnchor
    {
        UpperLeft,
        MiddleCenter
    }

    public struct Color
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public Color(float r, float g, float b, float a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }
    }

    public class Texture : Object { }

    public class Texture2D : Texture
    {
        public static Texture2D whiteTexture { get; } = new Texture2D();
    }

    public static class Input
    {
        public static bool GetKeyDown(KeyCode key) { return false; }
    }

    public static class Cursor
    {
        public static bool visible { get; set; }
        public static CursorLockMode lockState { get; set; }
    }

    public enum CursorLockMode
    {
        None,
        Locked,
        Confined
    }

    public enum KeyCode
    {
        F7,
        F8,
        F9,
        Escape
    }
}

namespace UnityEngine.Rendering
{
    public enum GraphicsDeviceType
    {
        Null,
        Direct3D11
    }
}

namespace UnityEngine.Networking
{
    public abstract class UploadHandler : IDisposable
    {
        public virtual void Dispose() { }
    }

    public sealed class UploadHandlerRaw : UploadHandler
    {
        public UploadHandlerRaw(byte[] data) { }
    }

    public abstract class DownloadHandler : IDisposable
    {
        public string text { get; set; } = string.Empty;
        public virtual void Dispose() { }
    }

    public sealed class DownloadHandlerBuffer : DownloadHandler { }

    public sealed class UnityWebRequest : IDisposable
    {
        public const string kHttpVerbGET = "GET";
        public const string kHttpVerbPOST = "POST";

        public enum Result
        {
            InProgress,
            Success,
            ConnectionError,
            ProtocolError,
            DataProcessingError
        }

        public UploadHandler uploadHandler { get; set; }
        public DownloadHandler downloadHandler { get; set; }
        public int timeout { get; set; }
        public Result result { get; set; }
        public long responseCode { get; set; }

        public UnityWebRequest(string url, string method) { }

        public static string EscapeURL(string value) { return value ?? string.Empty; }
        public void SetRequestHeader(string name, string value) { }
        public object SendWebRequest() { return null; }
        public void Dispose() { }
    }
}
