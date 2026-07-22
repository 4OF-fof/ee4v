using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager
{
    internal sealed class MainViewWindow : EditorWindow
    {
        private const string WindowTitle = "Main View";
        private const string RootClassName = "ee4v-ui";
        private const string WindowClassName = "ee4v-asset-manager-window";
        private const string BodyClassName = "ee4v-asset-manager-window__main-view-window-body";
        private const string ContentClassName = "ee4v-asset-manager-window__main-view-window-content";
        private MainView _mainView;
        private FileTreeDetailState _pendingFileDetailState;

        [MenuItem("ee4v/Asset Manager/Main View", false, 3)]
        private static void ShowWindow()
        {
            var window = GetWindow<MainViewWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(640f, 420f);
            window.Show();
        }

        public static void ShowFileDetail(FileTreeDetailState state)
        {
            if (state == null)
            {
                return;
            }

            var window = GetWindow<MainViewWindow>();
            window._pendingFileDetailState = state;
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(640f, 420f);
            window.Show();
            window.ApplyPendingFileDetail();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            minSize = new Vector2(640f, 420f);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList(RootClassName);
            root.AddToClassList(WindowClassName);

            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/Panels/MainView/main-view.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/Toolbar/MainToolbar/main-toolbar.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/Content/ItemImage/item-image.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/Content/Icon/icon.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/Inputs/SearchField/search-field.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/Inputs/NumericSlider/numeric-slider.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/Content/ItemCard/item-card.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/Collections/ItemGrid/item-grid.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/Inputs/Selection/SelectableItemGrid/selectable-item-grid.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/DataView/AssetItemGrid/asset-item-grid.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/Window/MainViewWindow/main-view-window.uss");

            var body = new VisualElement();
            body.AddToClassList(BodyClassName);

            _mainView = new MainView();
            var toolbar = new MainToolbar(
                _mainView,
                _mainView.GridSize,
                _mainView.HistoryOverlayMaximumItems);
            toolbar.GridSizeChanged += _mainView.SetGridSize;
            toolbar.SearchTextChanged += _mainView.SetSearchText;
            _mainView.AddToClassList(ContentClassName);

            body.Add(toolbar);
            body.Add(_mainView);

            root.Add(body);
            WindowToastApi.EnsureHost(this);
            StatusOverlayApi.EnsureHost(this);
            ApplyPendingFileDetail();
        }

        private void ApplyPendingFileDetail()
        {
            if (_mainView == null || _pendingFileDetailState == null)
            {
                return;
            }

            var state = _pendingFileDetailState;
            _pendingFileDetailState = null;
            _mainView.ShowFileDetail(state);
        }
    }
}
