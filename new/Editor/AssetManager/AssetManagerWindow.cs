using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager
{
    internal sealed class AssetManagerWindow : EditorWindow
    {
        private const string WindowTitle = "Asset Manager";
        private const string RootClassName = "ee4v-ui";
        private const string WindowClassName = "ee4v-asset-manager-window";
        private const string LayoutClassName = "ee4v-asset-manager-window__layout";
        private const float DefaultNavigationWidth = 240f;
        private const float DefaultInspectorWidth = 300f;
        private const float NavigationMinWidth = 180f;
        private const float NavigationMaxWidth = 360f;
        private const float ContentMinWidth = 420f;
        private const float InspectorMinWidth = 240f;
        private const float InspectorMaxWidth = 420f;

        [SerializeField]
        private float _navigationWidth = DefaultNavigationWidth;

        [SerializeField]
        private float _inspectorWidth = DefaultInspectorWidth;

        [SerializeField]
        private bool _navigationCollapsed;

        [SerializeField]
        private bool _inspectorCollapsed;

        [MenuItem("ee4v/Asset Manager", false, 0)]
        private static void ShowWindow()
        {
            var window = GetWindow<AssetManagerWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(UiTokens.WindowMinWidth, UiTokens.WindowMinHeight);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            minSize = new Vector2(UiTokens.WindowMinWidth, UiTokens.WindowMinHeight);
        }

        private void CreateGUI()
        {
            RebuildWindow();
        }

        private void RebuildWindow()
        {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList(RootClassName);
            root.AddToClassList(WindowClassName);

            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Domain/AssetManager/asset-manager-window-layout.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Domain/AssetManager/panels.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Domain/AssetManager/toolbar.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Display/info-card.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Interactive/single-select-button-group.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/asset-manager-window.uss");

            var layout = new AssetManagerWindowLayout(CreateLayoutState());
            layout.AddToClassList(LayoutClassName);
            layout.NavigationPaneWidthChanged += value => _navigationWidth = value;
            layout.InspectorPaneWidthChanged += value => _inspectorWidth = value;
            layout.NavigationCollapsedChanged += value => _navigationCollapsed = value;
            layout.InspectorCollapsedChanged += value => _inspectorCollapsed = value;

            layout.NavigationPaneContent.Add(new NavigationPanel());
            layout.ContentPaneContent.Add(new MainView());
            layout.InspectorPaneContent.Add(new InfomationPanel());

            root.Add(layout);
            WindowToastApi.EnsureHost(this);
        }

        private AssetManagerWindowLayoutState CreateLayoutState()
        {
            return new AssetManagerWindowLayoutState(
                navigationWidth: Mathf.Max(0f, _navigationWidth),
                inspectorWidth: Mathf.Max(0f, _inspectorWidth),
                navigationMinWidth: NavigationMinWidth,
                navigationMaxWidth: NavigationMaxWidth,
                contentMinWidth: ContentMinWidth,
                inspectorMinWidth: InspectorMinWidth,
                inspectorMaxWidth: InspectorMaxWidth,
                navigationCollapsed: _navigationCollapsed,
                inspectorCollapsed: _inspectorCollapsed);
        }
    }
}
