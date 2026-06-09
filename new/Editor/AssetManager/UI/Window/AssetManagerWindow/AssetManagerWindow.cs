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

            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/Layout/ThreePaneLayout/three-pane-layout.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/Panels/NavigationPanel/navigation-panel.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/Panels/MainView/main-view.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/Panels/InfomationPanel/infomation-panel.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/Toolbar/AssetManagerToolbar/asset-manager-toolbar.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/Display/ItemImage/item-image.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/Display/ItemCard/item-card.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/DataView/ItemGrid/item-grid.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/DataView/ItemGrid/selectable-item-grid.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/DataView/AssetItemGrid/asset-item-grid.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/Interactive/SingleSelectButtonGroup/single-select-button-group.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/Window/AssetManagerWindow/asset-manager-window.uss");

            var layout = new ThreePaneLayout(CreateLayoutState());
            layout.AddToClassList(LayoutClassName);
            layout.LeftPaneWidthChanged += value => _navigationWidth = value;
            layout.RightPaneWidthChanged += value => _inspectorWidth = value;
            layout.LeftCollapsedChanged += value => _navigationCollapsed = value;
            layout.RightCollapsedChanged += value => _inspectorCollapsed = value;

            var toolbar = new AssetManagerToolbar();
            toolbar.style.flexGrow = 1f;

            layout.MainToolbarContent.Add(toolbar);
            layout.LeftPaneContent.Add(new NavigationPanel());
            layout.MainContent.Add(new MainView());
            layout.RightPaneContent.Add(new InfomationPanel());

            root.Add(layout);
            WindowToastApi.EnsureHost(this);
        }

        private ThreePaneLayoutState CreateLayoutState()
        {
            return new ThreePaneLayoutState(
                leftWidth: Mathf.Max(0f, _navigationWidth),
                rightWidth: Mathf.Max(0f, _inspectorWidth),
                leftMinWidth: NavigationMinWidth,
                leftMaxWidth: NavigationMaxWidth,
                mainMinWidth: ContentMinWidth,
                rightMinWidth: InspectorMinWidth,
                rightMaxWidth: InspectorMaxWidth,
                leftCollapsed: _navigationCollapsed,
                rightCollapsed: _inspectorCollapsed);
        }
    }
}
