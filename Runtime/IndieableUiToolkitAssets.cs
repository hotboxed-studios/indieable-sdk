using UnityEngine;
using UnityEngine.UIElements;

namespace IndieableSdk
{
    /// <summary>
    /// Optional project- or sample-owned replacements for the SDK support UI.
    /// Leave any field empty to use the packaged default for that asset.
    /// </summary>
    [CreateAssetMenu(
        fileName = "IndieableUiToolkitAssets",
        menuName = "Indieable/UI Toolkit Assets")]
    public sealed class IndieableUiToolkitAssets : ScriptableObject
    {
        [SerializeField] private ThemeStyleSheet themeStyleSheet;
        [SerializeField] private VisualTreeAsset privacyLayout;
        [SerializeField] private StyleSheet privacyStyles;
        [SerializeField] private VisualTreeAsset feedbackLayout;
        [SerializeField] private StyleSheet feedbackStyles;

        public ThemeStyleSheet ThemeStyleSheet => themeStyleSheet;
        public VisualTreeAsset PrivacyLayout => privacyLayout;
        public StyleSheet PrivacyStyles => privacyStyles;
        public VisualTreeAsset FeedbackLayout => feedbackLayout;
        public StyleSheet FeedbackStyles => feedbackStyles;
    }

    internal enum IndieableUiToolkitView
    {
        Privacy,
        Feedback
    }

    internal static class IndieableUiToolkitFactory
    {
        private const string DefaultThemeResource =
            "IndieableDefaultRuntimeTheme";
        private const string DefaultPrivacyResource =
            "IndieablePrivacyPreferences";
        private const string DefaultFeedbackResource =
            "IndieableFeedback";

        private static IndieableUiToolkitAssets _assets;

        internal static void Configure(
            IndieableUiToolkitAssets assets)
        {
            _assets = assets;
        }

        internal static void ResetForRuntimeStartup()
        {
            _assets = null;
        }

        internal static bool TryCreateDocument(
            GameObject host,
            IndieableUiToolkitView view,
            int sortingOrder,
            out UIDocument document,
            out PanelSettings panelSettings)
        {
            document = null;
            panelSettings = null;
            if (host == null) return false;

            ThemeStyleSheet theme =
                _assets != null
                    ? _assets.ThemeStyleSheet
                    : null;
            if (theme == null)
            {
                theme = Resources.Load<ThemeStyleSheet>(
                    DefaultThemeResource);
            }

            VisualTreeAsset layout = ResolveLayout(view);
            StyleSheet styles = ResolveStyles(view);
            if (theme == null || layout == null || styles == null)
            {
                Debug.LogWarning(
                    "[Indieable] UI Toolkit assets are incomplete for " +
                    view + ".");
                return false;
            }

            panelSettings =
                ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "Indieable " + view + " Panel";
            panelSettings.themeStyleSheet = theme;
            panelSettings.scaleMode =
                PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution =
                new Vector2Int(1920, 1080);
            panelSettings.screenMatchMode =
                PanelScreenMatchMode.Expand;

            document = host.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.sortingOrder = sortingOrder;

            VisualElement documentRoot =
                document.rootVisualElement;
            documentRoot.pickingMode = PickingMode.Ignore;
            layout.CloneTree(documentRoot);
            documentRoot.styleSheets.Add(styles);

            string cardName = view == IndieableUiToolkitView.Privacy
                ? "privacy-card"
                : "feedback-card";
            VisualElement card = documentRoot.Q<VisualElement>(cardName);
            if (card == null)
            {
                Debug.LogWarning(
                    "[Indieable] UI Toolkit layout is missing '" +
                    cardName + "'.");
                Object.Destroy(document);
                Object.Destroy(panelSettings);
                document = null;
                panelSettings = null;
                return false;
            }

            card.pickingMode = PickingMode.Position;
            return true;
        }

        private static VisualTreeAsset ResolveLayout(
            IndieableUiToolkitView view)
        {
            VisualTreeAsset configured = null;
            if (_assets != null)
            {
                configured = view == IndieableUiToolkitView.Privacy
                    ? _assets.PrivacyLayout
                    : _assets.FeedbackLayout;
            }

            return configured != null
                ? configured
                : Resources.Load<VisualTreeAsset>(
                    view == IndieableUiToolkitView.Privacy
                        ? DefaultPrivacyResource
                        : DefaultFeedbackResource);
        }

        private static StyleSheet ResolveStyles(
            IndieableUiToolkitView view)
        {
            StyleSheet configured = null;
            if (_assets != null)
            {
                configured = view == IndieableUiToolkitView.Privacy
                    ? _assets.PrivacyStyles
                    : _assets.FeedbackStyles;
            }

            return configured != null
                ? configured
                : Resources.Load<StyleSheet>(
                    view == IndieableUiToolkitView.Privacy
                        ? DefaultPrivacyResource
                        : DefaultFeedbackResource);
        }
    }
}
