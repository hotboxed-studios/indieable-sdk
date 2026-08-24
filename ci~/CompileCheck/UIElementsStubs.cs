// Compile-only UI Toolkit and related Unity APIs used by the example.
// This file lives under ci~/ and is never included in the released package.

using System;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType loadType) { }
    }

    public enum RuntimeInitializeLoadType
    {
        AfterSceneLoad,
        BeforeSceneLoad,
        SubsystemRegistration
    }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new()
        {
            return new T();
        }
    }

    public static class Resources
    {
        public static T Load<T>(string path) where T : Object
        {
            return null;
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
}

namespace UnityEngine.UIElements
{
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

    public enum PanelScaleMode
    {
        ConstantPixelSize,
        ScaleWithScreenSize,
        ConstantPhysicalSize
    }

    public enum PanelScreenMatchMode
    {
        MatchWidthOrHeight,
        Shrink,
        Expand
    }

    public sealed class StyleData
    {
        public DisplayStyle display { get; set; }
    }

    public sealed class VisualElementStyleSheetSet
    {
        public void Add(StyleSheet styleSheet) { }
    }

    public class VisualElement : UnityEngine.Object
    {
        public StyleData style { get; } = new StyleData();
        public VisualElementStyleSheetSet styleSheets { get; } = new VisualElementStyleSheetSet();
        public int childCount { get; }
        public PickingMode pickingMode { get; set; }

        public T Q<T>(string name = null) where T : VisualElement
        {
            return null;
        }

        public void Add(VisualElement child) { }
        public void Clear() { }
        public void RemoveAt(int index) { }
        public void AddToClassList(string className) { }
        public void EnableInClassList(string className, bool enable) { }
        public void SetEnabled(bool enabled) { }
    }

    public sealed class PanelSettings : UnityEngine.ScriptableObject
    {
        public PanelScaleMode scaleMode { get; set; }
        public UnityEngine.Vector2Int referenceResolution { get; set; }
        public PanelScreenMatchMode screenMatchMode { get; set; }
        public ThemeStyleSheet themeStyleSheet { get; set; }
        public float match { get; set; }
        public float sortingOrder { get; set; }
    }

    public sealed class UIDocument : UnityEngine.Behaviour
    {
        public PanelSettings panelSettings { get; set; }
        public int sortingOrder { get; set; }
        public VisualElement rootVisualElement { get; } = new VisualElement();
    }

    public sealed class VisualTreeAsset : UnityEngine.Object
    {
        public void CloneTree(VisualElement target) { }
    }

    public class StyleSheet : UnityEngine.Object { }
    public sealed class ThemeStyleSheet : StyleSheet { }

    public class Label : VisualElement
    {
        public Label() { }
        public Label(string text) { this.text = text; }
        public string text { get; set; }
    }

    public class BaseField<TValueType> : VisualElement
    {
        public TValueType value { get; set; }

        public void RegisterValueChangedCallback(
            Action<ChangeEvent<TValueType>> callback) { }
    }

    public sealed class ChangeEvent<T>
    {
        public T newValue { get; set; }
    }

    public sealed class TextField : BaseField<string>
    {
        public TextField() { }
        public TextField(string label) { }
        public bool multiline { get; set; }
    }
    public sealed class IntegerField : BaseField<int> { }
    public sealed class Toggle : BaseField<bool> { }

    public sealed class Button : VisualElement
    {
        public event Action clicked;
        public string text { get; set; }
    }

    public sealed class ScrollView : VisualElement { }
}
