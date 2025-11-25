using System;
using System.Collections.Generic;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Service;
using _4OF.ee4v.AssetManager.UI.Model;
using _4OF.ee4v.AssetManager.UI.Presenter;
using _4OF.ee4v.AssetManager.UI.Window._Component;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window {
    public class AssetManagerBootstrapper : IDisposable {
        private readonly AssetService _assetService;
        private readonly FolderService _folderService;
        private readonly IAssetRepository _repository;
        private readonly TextureService _textureService;
        private readonly AssetManagerWindow _window;
        private AssetViewController _assetController;

        private Action<VisualElement> _assetViewSortMenuHandler;
        private AssetGridPresenter _gridPresenter;
        private Action<Ulid, string, VisualElement> _navigationContextMenuHandler;

        private AssetNavigationPresenter _navigationPresenter;
        private Action<Ulid> _presenterPreviewFolderHandler;
        private Action<AssetMetadata> _presenterSelectedAssetHandler;
        private AssetPropertyPresenter _propertyPresenter;
        private AssetSelectionModel _selectionModel;
        private ToastManager _toastManager;

        public AssetManagerBootstrapper(AssetManagerWindow window) {
            _window = window;
            _repository = AssetManagerContainer.Repository;
            _assetService = AssetManagerContainer.AssetService;
            _folderService = AssetManagerContainer.FolderService;
            _textureService = AssetManagerContainer.TextureService;
        }

        public void Dispose() {
            if (_window.Navigation != null) {
                _window.Navigation.NavigationChanged -= _window.OnNavigationChanged;
                _window.Navigation.FolderSelected -= _window.OnFolderSelected;
                _window.Navigation.TagListClicked -= _window.OnTagListClicked;
                _window.Navigation.OnFolderRenamed -= _navigationPresenter.OnFolderRenamed;
                _window.Navigation.OnFolderDeleted -= _navigationPresenter.OnFolderDeleted;
                _window.Navigation.OnFolderMoved -= _navigationPresenter.OnFolderMoved;
                _window.Navigation.OnFolderCreated -= _navigationPresenter.OnFolderCreated;
                _window.Navigation.OnDropRequested -= _window.OnNavigationDropRequested;
                if (_navigationContextMenuHandler != null)
                    _window.Navigation.OnFolderContextMenuRequested -= _navigationContextMenuHandler;
                _window.Navigation.OnFolderReordered -= _navigationPresenter.OnFolderReordered;
                _window.Navigation.OnAssetCreated -= _navigationPresenter.OnAssetCreated;
            }

            if (_window.TagListView != null) {
                _window.TagListView.OnTagSelected -= _window.OnTagSelected;
                _window.TagListView.OnTagRenamed -= _navigationPresenter.OnTagRenamed;
                _window.TagListView.OnTagDeleted -= _navigationPresenter.OnTagDeleted;
            }

            if (_assetController != null) {
                _assetController.AssetSelected -= _window.OnAssetSelected;
                _assetController.FolderPreviewSelected -= _window.OnFolderPreviewSelected;
                _assetController.FolderUpdated -= _window.OnFolderUpdated;
                _assetController.FoldersChanged -= _window.OnFoldersChanged;
                _assetController.BoothItemFoldersChanged -= _window.OnBoothItemFoldersChanged;
                _assetController.ModeChanged -= _window.OnModeChanged;
                _assetController.OnHistoryChanged -= _window.OnControllerHistoryChanged;
                _assetController.Dispose();
            }

            if (_window.AssetView != null) {
                _window.AssetView.OnSelectionChange -= _window.OnSelectionChanged;
                _window.AssetView.OnItemsDroppedToFolder -= _window.OnItemsDroppedToFolder;
                if (_assetViewSortMenuHandler != null)
                    _window.AssetView.OnSortMenuRequested -= _assetViewSortMenuHandler;
            }

            if (_window.AssetInfo != null) {
                _window.AssetInfo.OnNameChanged -= _propertyPresenter.OnNameChanged;
                _window.AssetInfo.OnDescriptionChanged -= _propertyPresenter.OnDescriptionChanged;
                _window.AssetInfo.OnTagAdded -= _propertyPresenter.OnTagAdded;
                _window.AssetInfo.OnTagRemoved -= _propertyPresenter.OnTagRemoved;
                _window.AssetInfo.OnTagClicked -= _navigationPresenter.OnTagSelected;
                _window.AssetInfo.OnDependencyAdded -= _propertyPresenter.OnDependencyAdded;
                _window.AssetInfo.OnDependencyRemoved -= _propertyPresenter.OnDependencyRemoved;
                _window.AssetInfo.OnDependencyClicked -= _window.OnDependencyClicked;
                _window.AssetInfo.OnFolderClicked -= _navigationPresenter.OnFolderSelected;
                _window.AssetInfo.OnDownloadRequested -= _propertyPresenter.OnDownloadRequested;
            }

            if (_selectionModel != null) {
                if (_presenterSelectedAssetHandler != null)
                    _selectionModel.SelectedAsset.OnValueChanged -= _presenterSelectedAssetHandler;
                if (_presenterPreviewFolderHandler != null)
                    _selectionModel.PreviewFolderId.OnValueChanged -= _presenterPreviewFolderHandler;
            }

            _textureService?.ClearCache();
            _toastManager?.ClearAll();
        }

        public void Initialize() {
            if (_repository == null) return;

            var root = _window.rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Row;

            var navigation = new Navigation {
                style = {
                    width = 200,
                    minWidth = 150,
                    flexShrink = 0,
                    borderRightWidth = 1,
                    borderRightColor = ColorPreset.WindowBorder
                }
            };
            root.Add(navigation);

            var assetView = new AssetView();
            root.Add(assetView);

            var tagListView = new TagListView();
            root.Add(tagListView);

            var assetInfo = new AssetInfo {
                style = {
                    width = 300,
                    minWidth = 250,
                    flexShrink = 0,
                    borderLeftWidth = 1,
                    borderLeftColor = ColorPreset.WindowBorder
                }
            };
            root.Add(assetInfo);

            _toastManager = new ToastManager(root);

            _selectionModel = new AssetSelectionModel();
            _assetController = new AssetViewController(_repository);

            assetView.SetController(_assetController);
            assetView.Initialize(_textureService, _repository, _assetService, _folderService);
            tagListView.Initialize(_repository);
            tagListView.SetController(_assetController);
            tagListView.SetShowDialogCallback(ShowDialog);
            assetInfo.Initialize(_repository, _textureService, _folderService);

            var refreshUIAction = new Action<bool>(RefreshUI);
            var setNavFoldersAction = new Action<List<BaseFolder>>(folders => navigation.SetFolders(folders));
            var tagListRefreshAction = new Action(() => tagListView.Refresh());
            var getScreenPosAction = new Func<Vector2>(() =>
            {
                try {
                    return GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                }
                catch {
                    return new Vector2(_window.position.x + _window.position.width / 2,
                        _window.position.y + _window.position.height / 2);
                }
            });

            _navigationPresenter = new AssetNavigationPresenter(
                _repository, _assetService, _folderService, _assetController,
                ShowToast, refreshUIAction, setNavFoldersAction, tagListRefreshAction
            );

            _gridPresenter = new AssetGridPresenter(
                _repository, _assetService, _folderService, refreshUIAction
            );

            _propertyPresenter = new AssetPropertyPresenter(
                _repository, _assetService, _folderService, _assetController,
                _selectionModel,
                ShowToast, refreshUIAction, setNavFoldersAction, tagListRefreshAction,
                getScreenPosAction, ShowDialog, TagSelectorWindow.Show,
                (screenPos, repo, selectedId, cb) =>
                    AssetSelectorWindow.Show(screenPos, repo, selectedId ?? Ulid.Empty, cb)
            );

            navigation.NavigationChanged += _window.OnNavigationChanged;

            _window.SetViews(navigation, assetView, tagListView, assetInfo, _assetController, _navigationPresenter,
                _gridPresenter, _propertyPresenter, _toastManager);


            navigation.FolderSelected += _window.OnFolderSelected;
            navigation.TagListClicked += _window.OnTagListClicked;
            navigation.OnFolderRenamed += _navigationPresenter.OnFolderRenamed;
            navigation.OnFolderDeleted += _navigationPresenter.OnFolderDeleted;
            navigation.OnFolderMoved += _navigationPresenter.OnFolderMoved;
            navigation.OnFolderCreated += _navigationPresenter.OnFolderCreated;
            navigation.OnDropRequested += _window.OnNavigationDropRequested;

            _navigationContextMenuHandler = (id, folderName, target) =>
            {
                var menu = new GenericDropdownMenu();
                menu.AddItem("Rename", false, () => navigation.ShowRenameFolderDialog(id, folderName));
                menu.AddItem("Delete", false, () => _navigationPresenter.OnFolderDeleted(id));
                menu.DropDown(target.worldBound, target);
            };
            navigation.OnFolderContextMenuRequested += _navigationContextMenuHandler;
            navigation.OnFolderReordered += _navigationPresenter.OnFolderReordered;
            navigation.OnAssetCreated += _navigationPresenter.OnAssetCreated;
            navigation.SetShowDialogCallback(ShowDialog);
            navigation.SetRepository(_repository);

            tagListView.OnTagSelected += _window.OnTagSelected;
            tagListView.OnTagRenamed += _navigationPresenter.OnTagRenamed;
            tagListView.OnTagDeleted += _navigationPresenter.OnTagDeleted;

            _assetController.AssetSelected += _window.OnAssetSelected;
            _assetController.FolderPreviewSelected += _window.OnFolderPreviewSelected;
            _assetController.FolderUpdated += _window.OnFolderUpdated;
            _assetController.FoldersChanged += _window.OnFoldersChanged;
            _assetController.BoothItemFoldersChanged += _window.OnBoothItemFoldersChanged;
            _assetController.ModeChanged += _window.OnModeChanged;
            _assetController.OnHistoryChanged += _window.OnControllerHistoryChanged;

            assetView.OnSelectionChange += _window.OnSelectionChanged;
            assetView.OnItemsDroppedToFolder += _window.OnItemsDroppedToFolder;

            _assetViewSortMenuHandler = element =>
            {
                var menu = new GenericDropdownMenu();
                menu.AddItem("Name (A-Z)", false, () => assetView.ApplySortType(AssetSortType.NameAsc));
                menu.AddItem("Name (Z-A)", false, () => assetView.ApplySortType(AssetSortType.NameDesc));
                menu.AddSeparator("");
                menu.AddItem("Date Added (Newest)", false,
                    () => assetView.ApplySortType(AssetSortType.DateAddedNewest));
                menu.AddItem("Date Added (Oldest)", false,
                    () => assetView.ApplySortType(AssetSortType.DateAddedOldest));
                menu.AddSeparator("");
                menu.AddItem("Last Edit (Newest)", false, () => assetView.ApplySortType(AssetSortType.DateNewest));
                menu.AddItem("Last Edit (Oldest)", false, () => assetView.ApplySortType(AssetSortType.DateOldest));
                menu.AddSeparator("");
                menu.AddItem("Size (Smallest)", false, () => assetView.ApplySortType(AssetSortType.SizeSmallest));
                menu.AddItem("Size (Largest)", false, () => assetView.ApplySortType(AssetSortType.SizeLargest));
                menu.AddSeparator("");
                menu.AddItem("Filetype (A-Z)", false, () => assetView.ApplySortType(AssetSortType.ExtAsc));
                menu.AddItem("Filetype (Z-A)", false, () => assetView.ApplySortType(AssetSortType.ExtDesc));
                var targetElement = element ?? assetView;
                menu.DropDown(targetElement.worldBound, targetElement);
            };
            assetView.OnSortMenuRequested += _assetViewSortMenuHandler;

            assetInfo.OnNameChanged += _propertyPresenter.OnNameChanged;
            assetInfo.OnDescriptionChanged += _propertyPresenter.OnDescriptionChanged;
            assetInfo.OnTagAdded += _propertyPresenter.OnTagAdded;
            assetInfo.OnTagRemoved += _propertyPresenter.OnTagRemoved;
            assetInfo.OnTagClicked += _navigationPresenter.OnTagSelected;
            assetInfo.OnDependencyAdded += _propertyPresenter.OnDependencyAdded;
            assetInfo.OnDependencyRemoved += _propertyPresenter.OnDependencyRemoved;
            assetInfo.OnDependencyClicked += _window.OnDependencyClicked;
            assetInfo.OnFolderClicked += _navigationPresenter.OnFolderSelected;
            assetInfo.OnDownloadRequested += _propertyPresenter.OnDownloadRequested;

            navigation.SelectAll();
            _assetController.Refresh();

            _window.ShowAssetView();

            _presenterSelectedAssetHandler = asset =>
            {
                _window.SetSelectedAsset(asset);
                assetInfo.UpdateSelection(asset != null ? new List<object> { asset } : new List<object>());
            };
            _selectionModel.SelectedAsset.OnValueChanged += _presenterSelectedAssetHandler;

            _presenterPreviewFolderHandler = id =>
            {
                _window.SetCurrentPreviewFolderId(id);
                if (id != Ulid.Empty) {
                    var folder = _repository.GetLibraryMetadata()?.GetFolder(id);
                    assetInfo.UpdateSelection(new List<object> { folder });
                }
                else {
                    if (_window.GetSelectedAsset() == null)
                        assetInfo.UpdateSelection(new List<object>());
                }
            };
            _selectionModel.PreviewFolderId.OnValueChanged += _presenterPreviewFolderHandler;
        }

        private void RefreshUI(bool fullRefresh) {
            _window.RefreshUI(fullRefresh);
        }

        private void ShowToast(string message, float? duration, ToastType type) {
            _toastManager?.Show(message, duration, type);
        }

        private VisualElement ShowDialog(VisualElement content) {
            return _window.ShowDialog(content);
        }
    }
}