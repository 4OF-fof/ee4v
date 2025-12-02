using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Component;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.AssetManager.Presenter;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Window {
    public class AssetManagerWindow : EditorWindow {
        private AssetViewController _assetController;
        private AssetManagerBootstrapper _bootstrapper;

        private Ulid _currentPreviewFolderId = Ulid.Empty;
        private AssetGridPresenter _gridPresenter;
        private AssetNavigationPresenter _navigationPresenter;
        private AssetPropertyPresenter _propertyPresenter;
        private AssetMetadata _selectedAsset;
        private ToastManager _toastManager;

        public Navigation Navigation { get; private set; }
        public AssetView AssetView { get; private set; }
        public TagListView TagListView { get; private set; }
        public AssetInfo AssetInfo { get; private set; }

        private void OnEnable() {
            _bootstrapper = new AssetManagerBootstrapper(this);
        }

        private void OnDisable() {
            _bootstrapper?.Dispose();
            _bootstrapper = null;
        }

        private void CreateGUI() {
            _bootstrapper ??= new AssetManagerBootstrapper(this);
            _bootstrapper.Initialize();
        }

        public void SetViews(
            Navigation navigation,
            AssetView assetView,
            TagListView tagListView,
            AssetInfo assetInfo,
            AssetViewController assetController,
            AssetNavigationPresenter navigationPresenter,
            AssetGridPresenter gridPresenter,
            AssetPropertyPresenter propertyPresenter,
            ToastManager toastManager) {
            Navigation = navigation;
            AssetView = assetView;
            TagListView = tagListView;
            AssetInfo = assetInfo;
            _assetController = assetController;
            _navigationPresenter = navigationPresenter;
            _gridPresenter = gridPresenter;
            _propertyPresenter = propertyPresenter;
            _toastManager = toastManager;
        }

        internal void OnNavigationChanged(NavigationMode mode, string contextName, Func<AssetMetadata, bool> filter) {
            _navigationPresenter.OnNavigationChanged(mode, contextName, filter);
            _propertyPresenter.ClearSelection();
        }

        internal void OnFolderSelected(Ulid folderId) {
            _navigationPresenter.OnFolderSelected(folderId);
            _propertyPresenter.ClearSelection();
        }

        internal void OnTagListClicked() {
            _navigationPresenter.OnTagListClicked();
            _propertyPresenter.ClearSelection();
        }

        internal void OnTagSelected(string tag) {
            _navigationPresenter.OnTagSelected(tag);
            _propertyPresenter.ClearSelection();
        }

        internal void OnSelectionChanged(List<object> selectedItems) {
            _propertyPresenter.UpdateSelection(selectedItems);
            AssetInfo.UpdateSelection(selectedItems);
        }

        internal void OnModeChanged(NavigationMode mode) {
            if (mode == NavigationMode.TagList)
                ShowTagListView();
            else
                ShowAssetView();
        }

        internal void OnControllerHistoryChanged() {
            Navigation?.SelectState(_assetController.CurrentMode, _assetController.SelectedFolderId);

            AssetView?.ClearSelection();
            _selectedAsset = null;
            AssetInfo?.UpdateSelection(new List<object>());
            _propertyPresenter.ClearSelection();
        }

        internal void OnAssetSelected(AssetMetadata asset) {
            _currentPreviewFolderId = Ulid.Empty;
        }

        internal void OnFolderPreviewSelected(BaseFolder folder) {
            _currentPreviewFolderId = folder?.ID ?? Ulid.Empty;
        }

        internal void OnFoldersChanged(List<BaseFolder> folders) {
            Navigation.SetFolders(folders);
        }

        internal void OnBoothItemFoldersChanged(List<BoothItemFolder> folders) {
            AssetView.ShowBoothItemFolders(folders);
        }

        internal void OnFolderUpdated(BaseFolder folder) {
            var folders = AssetManagerContainer.FolderService.GetRootFolders();
            Navigation.SetFolders(folders);

            if (_currentPreviewFolderId != Ulid.Empty && _currentPreviewFolderId == folder.ID)
                AssetInfo.UpdateSelection(new List<object> { folder });
        }

        internal void OnDependencyClicked(Ulid dependencyId) {
            var depAsset = AssetManagerContainer.Repository.GetAsset(dependencyId);
            if (depAsset == null || depAsset.IsDeleted) {
                ShowToast(I18N.Get("UI.AssetManager.Toast.DependencyAssetNotSelected"), 3, ToastType.Error);
                return;
            }

            var folder = depAsset.Folder;
            if (folder != Ulid.Empty) {
                Navigation.SelectState(NavigationMode.Folders, folder);
                _navigationPresenter.OnFolderSelected(folder);
            }
            else {
                Navigation.SelectAll();
            }
        }

        internal void OnNavigationDropRequested(Ulid targetFolderId, string[] assetIdsData, string[] folderIdsData) {
            if (assetIdsData is { Length: > 0 }) {
                var assetIds = assetIdsData.Select(Ulid.Parse).ToList();

                var assetsFromBoothItemFolder = _gridPresenter.FindAssetsFromBoothItemFolder(assetIds);
                if (assetsFromBoothItemFolder.Count > 0)
                    ShowBoothItemFolderWarningDialog(assetIds, targetFolderId, assetsFromBoothItemFolder);
                else
                    _gridPresenter.PerformSetFolderForAssets(assetIds, targetFolderId);
            }

            if (folderIdsData is not { Length: > 0 }) return;
            var folderIds = folderIdsData.Select(Ulid.Parse).ToList();
            foreach (var sourceFolderId in folderIds.Where(id => id != targetFolderId))
                _navigationPresenter.OnFolderMoved(sourceFolderId, targetFolderId);
        }

        internal void OnItemsDroppedToFolder(List<Ulid> assetIds, List<Ulid> folderIds, Ulid targetFolderId) {
            if (assetIds.Count > 0) {
                var assetsFromBoothItemFolder = _gridPresenter.FindAssetsFromBoothItemFolder(assetIds);

                if (assetsFromBoothItemFolder.Count > 0) {
                    ShowBoothItemFolderWarningDialog(assetIds, targetFolderId, assetsFromBoothItemFolder);
                    return;
                }
            }

            _gridPresenter.PerformItemsDroppedToFolder(assetIds, folderIds, targetFolderId);
        }

        public void RefreshUI(bool fullRefresh = true) {
            if (fullRefresh) _assetController.Refresh();

            if (_selectedAsset == null) return;
            var freshAsset = AssetManagerContainer.Repository.GetAsset(_selectedAsset.ID);
            _selectedAsset = freshAsset;
            AssetInfo.UpdateSelection(new List<object> { freshAsset });
        }

        public void ShowAssetView() {
            AssetView.style.display = DisplayStyle.Flex;
            TagListView.style.display = DisplayStyle.None;
        }

        private void ShowTagListView() {
            AssetView.style.display = DisplayStyle.None;
            TagListView.style.display = DisplayStyle.Flex;
        }

        public void SetSelectedAsset(AssetMetadata asset) {
            _selectedAsset = asset;
        }

        public AssetMetadata GetSelectedAsset() {
            return _selectedAsset;
        }

        public void SetCurrentPreviewFolderId(Ulid id) {
            _currentPreviewFolderId = id;
        }

        public VisualElement ShowDialog(VisualElement dialogContent) {
            var container = new VisualElement {
                style = {
                    position = Position.Absolute,
                    left = 0, right = 0, top = 0, bottom = 0,
                    backgroundColor = ColorPreset.TransparentBlack50Style,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center
                }
            };

            var dialog = new VisualElement {
                style = {
                    backgroundColor = ColorPreset.DefaultBackground,
                    paddingLeft = 20, paddingRight = 20, paddingTop = 20, paddingBottom = 20,
                    borderTopLeftRadius = 8, borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8, borderBottomRightRadius = 8,
                    minWidth = 300,
                    maxWidth = 500
                }
            };

            dialog.Add(dialogContent);
            container.Add(dialog);
            rootVisualElement.Add(container);

            return container;
        }

        private void ShowToast(string message, float? duration = 3f, ToastType type = ToastType.Info) {
            _toastManager?.Show(message, duration, type);
        }

        public static void ShowToastMessage(string message, float? duration = 3f, ToastType type = ToastType.Info) {
            var window = GetWindow<AssetManagerWindow>();
            window?.ShowToast(message, duration, type);
        }

        [MenuItem("ee4v/Asset Manager")]
        public static void ShowWindow() {
            var window = GetWindow<AssetManagerWindow>(I18N.Get("UI.AssetManager.Window.Title"));
            window.minSize = new Vector2(800, 400);
            AssetManagerContainer.Repository.Load();
            window.Show();
        }

        private void ShowBoothItemFolderWarningDialog(List<Ulid> assetIds, Ulid targetFolderId,
            List<AssetMetadata> assetsFromBoothItemFolder) {
            var content = new VisualElement();

            var titleLabel = new Label(I18N.Get("UI.AssetManager.Dialog.BoothItemWarning.Title")) {
                style = {
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 10
                }
            };
            content.Add(titleLabel);

            var messageText = assetsFromBoothItemFolder.Count == 1
                ? I18N.Get("UI.AssetManager.Dialog.BoothItemWarning.Single", assetsFromBoothItemFolder[0].Name)
                : I18N.Get("UI.AssetManager.Dialog.BoothItemWarning.Multi", assetsFromBoothItemFolder.Count);

            var message = new Label(messageText) {
                style = {
                    marginBottom = 15,
                    whiteSpace = WhiteSpace.Normal
                }
            };
            content.Add(message);

            var buttonRow = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexEnd
                }
            };

            var cancelBtn = new Button {
                text = I18N.Get("UI.AssetManager.Dialog.Button.Cancel"),
                style = { marginRight = 5 }
            };
            buttonRow.Add(cancelBtn);

            var continueBtn = new Button {
                text = I18N.Get("UI.AssetManager.Dialog.Button.Continue"),
                style = {
                    backgroundColor = new StyleColor(ColorPreset.WarningButton)
                }
            };
            buttonRow.Add(continueBtn);

            content.Add(buttonRow);

            var dialogContainer = ShowDialog(content);

            cancelBtn.clicked += () => dialogContainer?.RemoveFromHierarchy();
            continueBtn.clicked += () =>
            {
                foreach (var assetId in assetIds) AssetManagerContainer.AssetService.SetFolder(assetId, targetFolderId);

                RefreshUI();
                dialogContainer?.RemoveFromHierarchy();
            };
        }
    }
}