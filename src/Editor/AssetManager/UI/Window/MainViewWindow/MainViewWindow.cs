using Ee4v.Core.Background;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class MainViewWindow : EditorWindow
    {
        private static string WindowTitle =>
            I18N.Get("assetManager.window.mainView");
        private const string RootClassName = "ee4v-ui";
        private const string WindowClassName = "ee4v-asset-manager-window";
        private const string BodyClassName = "ee4v-asset-manager-window__main-view-window-body";
        private const string ContentClassName = "ee4v-asset-manager-window__main-view-window-content";
        private MainView _mainView;
        private MainViewHost _mainViewHost;
        private FileTreeDetailState _pendingFileDetailState;
        private StandaloneAssetManagerViewSession _standaloneViewSession;

        [MenuItem("ee4v/Window/Main View", false, 3)]
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

        private void OnDisable()
        {
            BackgroundStatusOverlayApi.ReleaseHost(this);
            UnbindStandaloneSession();
            _mainViewHost?.Dispose();
            _mainViewHost = null;
            _mainView = null;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList(RootClassName);
            root.AddToClassList(WindowClassName);

            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Inputs/Button/ui-button.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/Panels/MainView/main-view.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/Toolbar/MainToolbar/main-toolbar.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Content/ItemImage/item-image.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Content/ImageStack/image-stack.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Content/Icon/icon.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Inputs/SearchField/search-field.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Inputs/NumericSlider/numeric-slider.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Content/ItemCard/item-card.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Collections/ItemGrid/item-grid.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Inputs/Selection/SelectableItemGrid/selectable-item-grid.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/DataView/AssetItemGrid/asset-item-grid.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/AssetManager/UI/Window/MainViewWindow/main-view-window.uss");

            var body = new VisualElement();
            body.AddToClassList(BodyClassName);

            UnbindStandaloneSession();
            _mainViewHost?.Dispose();
            _mainViewHost = new MainViewHost();
            _mainView = _mainViewHost.MainView;
            _standaloneViewSession =
                AssetManagerUiDependencies.StandaloneViewSession;
            _mainViewHost.SetSelectedNavigationItem(
                _standaloneViewSession.SelectedNavigationItemId);
            _mainView.SelectionChanged +=
                _standaloneViewSession.SetSelection;
            _mainView.DetailTabRequested +=
                _standaloneViewSession.RequestDetailTab;
            _standaloneViewSession.NavigationChanged +=
                OnStandaloneNavigationChanged;
            _standaloneViewSession.SetSelection(
                null,
                AssetSelectionContentKind.AssetItem);
            var toolbar = _mainViewHost.Toolbar;
            _mainView.AddToClassList(ContentClassName);

            body.Add(toolbar);
            body.Add(_mainView);

            root.Add(body);
            WindowToastApi.EnsureHost(this);
            BackgroundStatusOverlayApi.EnsureHost(this);
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

        private void UnbindStandaloneSession()
        {
            if (_mainView == null || _standaloneViewSession == null)
            {
                return;
            }

            _mainView.SelectionChanged -=
                _standaloneViewSession.SetSelection;
            _mainView.DetailTabRequested -=
                _standaloneViewSession.RequestDetailTab;
            _standaloneViewSession.NavigationChanged -=
                OnStandaloneNavigationChanged;
            _standaloneViewSession = null;
        }

        private void OnStandaloneNavigationChanged(
            string itemId)
        {
            _mainViewHost?.SetSelectedNavigationItem(itemId);
        }
    }
}
