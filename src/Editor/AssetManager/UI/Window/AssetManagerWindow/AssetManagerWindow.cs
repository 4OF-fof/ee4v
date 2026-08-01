using Ee4v.Core.Background;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetManagerWindow : EditorWindow
    {
        private static string WindowTitle =>
            I18N.Get("assetManager.window.assetManager");
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
        private MainViewHost _mainViewHost;

        [MenuItem("ee4v/Asset Manager", false, 0)]
        private static void ShowWindow()
        {
            var window = GetWindow<AssetManagerWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(
                UiSizeTokens.WindowMinWidth,
                UiSizeTokens.WindowMinHeight);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            minSize = new Vector2(
                UiSizeTokens.WindowMinWidth,
                UiSizeTokens.WindowMinHeight);
        }

        private void OnDisable()
        {
            BackgroundStatusOverlayApi.ReleaseHost(this);
            _mainViewHost?.Dispose();
            _mainViewHost = null;
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
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Inputs/Button/ui-button.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Layout/ThreePaneLayout/three-pane-layout.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/Panels/NavigationPanel/navigation-panel.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/Panels/MainView/main-view.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/Toolbar/MainToolbar/main-toolbar.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Content/ItemImage/item-image.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Content/ImageStack/image-stack.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Content/Icon/icon.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Inputs/InputField/input-field.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Inputs/CommaSeparatedListField/comma-separated-list-field.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Inputs/SearchField/search-field.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Inputs/NumericSlider/numeric-slider.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Collections/SearchableTreeView/searchable-tree-view.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Content/Interactive/ViewToggleTabs/view-toggle-tabs.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/Panels/InfomationPanel/infomation-panel.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Content/ItemCard/item-card.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Collections/ItemGrid/item-grid.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Inputs/Selection/SelectableItemGrid/selectable-item-grid.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/DataView/AssetItemGrid/asset-item-grid.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Inputs/Selection/SingleSelectButtonGroup/single-select-button-group.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/Window/AssetManagerWindow/asset-manager-window.uss");

            var layout = new ThreePaneLayout(CreateLayoutState());
            layout.AddToClassList(LayoutClassName);
            layout.LeftPaneWidthChanged += value => _navigationWidth = value;
            layout.RightPaneWidthChanged += value => _inspectorWidth = value;
            layout.LeftCollapsedChanged += value => _navigationCollapsed = value;
            layout.RightCollapsedChanged += value => _inspectorCollapsed = value;

            _mainViewHost?.Dispose();
            _mainViewHost = new MainViewHost();
            var mainView = _mainViewHost.MainView;
            var toolbar = _mainViewHost.Toolbar;
            var infomationPanel = new InfomationPanel();
            mainView.SelectionChanged += infomationPanel.SetSelectedAssetItems;
            toolbar.style.flexGrow = 1f;

            layout.MainToolbarContent.Add(toolbar);
            layout.LeftPaneContent.Add(_mainViewHost.NavigationPanel);
            layout.MainContent.Add(mainView);
            layout.RightPaneContent.Add(infomationPanel);
            mainView.SetExternalFileDropSurface(
                layout,
                layout.MainOverlayContent);

            root.Add(layout);
            WindowToastApi.EnsureHost(this);
            BackgroundStatusOverlayApi.EnsureHost(this);
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
