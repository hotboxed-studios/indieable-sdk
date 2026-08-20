// Minimal compile-only Unity API surface for zero-secret CI.
// This file is not included in the released Unity package.

using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeField : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class HeaderAttribute : Attribute
    {
        public HeaderAttribute(string header) { }
    }

    public class Object
    {
        public static void Destroy(Object target) { }
        public static void DontDestroyOnLoad(Object target) { }
        public int GetInstanceID() { return 0; }
    }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new()
        {
            return new T();
        }
    }

    public class Component : Object
    {
        public GameObject gameObject { get; } = new GameObject();

        public T GetComponent<T>() where T : Component
        {
            return default(T);
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
        public static RuntimePlatform platform { get; set; }
        public static string persistentDataPath { get; set; } = string.Empty;
        public static string unityVersion { get; set; } = string.Empty;
        public static string version { get; set; } = string.Empty;
        public static SystemLanguage systemLanguage { get; set; }
        public static void OpenURL(string url) { }
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

    public struct Vector2Int
    {
        public int x;
        public int y;

        public Vector2Int(int x, int y)
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

        public static Color white { get { return new Color(1f, 1f, 1f, 1f); } }
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

    public enum KeyCode
    {
        F7,
        F8,
        F9,
        Escape
    }
}

namespace UnityEngine.UIElements
{
    using UnityEngine;

    public enum PanelScaleMode
    {
        ConstantPixelSize,
        ConstantPhysicalSize,
        ScaleWithScreenSize
    }

    public enum PanelScreenMatchMode
    {
        MatchWidthOrHeight,
        Shrink,
        Expand
    }

    public enum Position
    {
        Relative,
        Absolute
    }

    public enum DisplayStyle
    {
        Flex,
        None
    }

    public enum PickingMode
    {
        Position,
        Ignore
    }

    public enum FlexDirection
    {
        Column,
        Row,
        ColumnReverse,
        RowReverse
    }

    public enum Align
    {
        Auto,
        FlexStart,
        Center,
        FlexEnd,
        Stretch
    }

    public enum Justify
    {
        FlexStart,
        Center,
        FlexEnd,
        SpaceBetween,
        SpaceAround
    }

    public enum Wrap
    {
        NoWrap,
        Wrap,
        WrapReverse
    }

    public enum WhiteSpace
    {
        Normal,
        NoWrap
    }

    public enum LengthUnit
    {
        Pixel,
        Percent
    }

    public enum ScrollViewMode
    {
        Vertical,
        Horizontal,
        VerticalAndHorizontal
    }

    public struct Length
    {
        public float value;
        public LengthUnit unit;

        public Length(float value, LengthUnit unit)
        {
            this.value = value;
            this.unit = unit;
        }
    }

    public sealed class IStyle
    {
        public object flexGrow { get; set; }
        public object position { get; set; }
        public object color { get; set; }
        public object left { get; set; }
        public object right { get; set; }
        public object top { get; set; }
        public object bottom { get; set; }
        public object width { get; set; }
        public object maxWidth { get; set; }
        public object minWidth { get; set; }
        public object height { get; set; }
        public object maxHeight { get; set; }
        public object minHeight { get; set; }
        public object paddingLeft { get; set; }
        public object paddingRight { get; set; }
        public object paddingTop { get; set; }
        public object paddingBottom { get; set; }
        public object marginLeft { get; set; }
        public object marginRight { get; set; }
        public object marginTop { get; set; }
        public object marginBottom { get; set; }
        public object backgroundColor { get; set; }
        public object borderTopLeftRadius { get; set; }
        public object borderTopRightRadius { get; set; }
        public object borderBottomLeftRadius { get; set; }
        public object borderBottomRightRadius { get; set; }
        public object borderTopWidth { get; set; }
        public object borderRightWidth { get; set; }
        public object borderBottomWidth { get; set; }
        public object borderLeftWidth { get; set; }
        public object borderTopColor { get; set; }
        public object borderRightColor { get; set; }
        public object borderBottomColor { get; set; }
        public object borderLeftColor { get; set; }
        public object unityFontStyleAndWeight { get; set; }
        public object unityTextAlign { get; set; }
        public object fontSize { get; set; }
        public object letterSpacing { get; set; }
        public object whiteSpace { get; set; }
        public object display { get; set; }
        public object flexDirection { get; set; }
        public object flexWrap { get; set; }
        public object alignItems { get; set; }
        public object justifyContent { get; set; }
    }

    public class VisualElement
    {
        private readonly List<VisualElement> _children = new List<VisualElement>();

        public string name { get; set; }
        public string tooltip { get; set; }
        public object userData { get; set; }
        public PickingMode pickingMode { get; set; }
        public IStyle style { get; } = new IStyle();

        public void Add(VisualElement child)
        {
            if (child != null) _children.Add(child);
        }

        public void Clear()
        {
            _children.Clear();
        }

        public void SetEnabled(bool enabled) { }

        public T Q<T>(string queryName = null) where T : VisualElement
        {
            return default(T);
        }
    }

    public class Label : VisualElement
    {
        public Label() { }
        public Label(string text) { this.text = text; }
        public string text { get; set; }
    }

    public class Button : VisualElement
    {
        public Button() { }
        public Button(Action clicked) { }
        public string text { get; set; }
    }

    public class BaseField<T> : VisualElement
    {
        public BaseField() { }
        public BaseField(string label) { this.label = label; }
        public string label { get; set; }
        public T value { get; set; }
    }

    public class Toggle : BaseField<bool>
    {
        public Toggle() { }
        public Toggle(string label) : base(label) { }
    }

    public class TextField : BaseField<string>
    {
        public TextField() { }
        public TextField(string label) : base(label) { }
    }

    public class IntegerField : BaseField<int>
    {
        public IntegerField() { }
        public IntegerField(string label) : base(label) { }
    }

    public class DropdownField : BaseField<string>
    {
        public DropdownField(string label, List<string> choices, int defaultIndex) : base(label)
        {
            if (choices != null && defaultIndex >= 0 && defaultIndex < choices.Count)
                value = choices[defaultIndex];
        }
    }

    public class ScrollView : VisualElement
    {
        public ScrollView() { }
        public ScrollView(ScrollViewMode mode) { }
    }

    public class PanelSettings : ScriptableObject
    {
        public PanelScaleMode scaleMode { get; set; }
        public Vector2Int referenceResolution { get; set; }
        public PanelScreenMatchMode screenMatchMode { get; set; }
        public float match { get; set; }
        public float sortingOrder { get; set; }
    }

    public class UIDocument : Behaviour
    {
        public PanelSettings panelSettings { get; set; }
        public float sortingOrder { get; set; }
        public VisualElement rootVisualElement { get; } = new VisualElement();
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
