using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Service;
using _4OF.ee4v.AssetManager.UI.Presenter;
using _4OF.ee4v.AssetManager.UI.Window._Component;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window {
    public class AssetManagerWindow : EditorWindow {
        private AssetViewController _assetController;
        private AssetInfo _assetInfo;
        private AssetService _assetService;

        private AssetView _assetView;

        private Action<VisualElement> _assetViewSortMenuHandler;

        private Ulid _currentPreviewFolderId = Ulid.Empty;
        private FolderService _folderService;
        private bool _isInitialized;
        private Navigation _navigation;
        private Action<Ulid, string, VisualElement> _navigationContextMenuHandler;
        private AssetManagerPresenter _presenter;
        private IAssetRepository _repository;
        private AssetMetadata _selectedAsset;
        private TagListView _tagListView;
        private TextureService _textureService;
        private ToastManager _toastManager;

        private void OnEnable() {
            _repository = AssetManagerContainer.Repository;
            _assetService = AssetManagerContainer.AssetService;
            _folderService = AssetManagerContainer.FolderService;
            _textureService = AssetManagerContainer.TextureService;
        }

        private void OnDisable() {
            if (_navigation != null) {
                _navigation.NavigationChanged -= OnNavigationChanged;
                _navigation.FolderSelected -= OnFolderSelected;
                _navigation.TagListClicked -= OnTagListClicked;
                _navigation.OnFolderRenamed -= _presenter.OnFolderRenamed;
                _navigation.OnFolderDeleted -= _presenter.OnFolderDeleted;
                _navigation.OnFolderMoved -= _presenter.OnFolderMoved;
                _navigation.OnFolderCreated -= _presenter.OnFolderCreated;
                _navigation.OnDropRequested -= OnNavigationDropRequested;
                if (_navigationContextMenuHandler != null)
                    _navigation.OnFolderContextMenuRequested -= _navigationContextMenuHandler;
                _navigation.OnFolderReordered -= _presenter.OnFolderReordered;
                _navigation.OnAssetCreated -= _presenter.OnAssetCreated;
            }

            if (_tagListView != null) {
                _tagListView.OnTagSelected -= OnTagSelected;
                _tagListView.OnTagRenamed -= _presenter.OnTagRenamed;
                _tagListView.OnTagDeleted -= _presenter.OnTagDeleted;
            }

            if (_assetController != null) {
                _assetController.AssetSelected -= OnAssetSelected;
                _assetController.FolderPreviewSelected -= OnFolderPreviewSelected;
                _assetController.FolderUpdated -= OnFolderUpdated;
                _assetController.FoldersChanged -= OnFoldersChanged;
                _assetController.BoothItemFoldersChanged -= OnBoothItemFoldersChanged;
                _assetController.ModeChanged -= OnModeChanged;
                _assetController.OnHistoryChanged -= OnControllerHistoryChanged;
                _assetController.Dispose();
            }

            if (_assetView != null) {
                _assetView.OnSelectionChange -= OnSelectionChanged;
                _assetView.OnItemsDroppedToFolder -= OnItemsDroppedToFolder;
                if (_assetViewSortMenuHandler != null) _assetView.OnSortMenuRequested -= _assetViewSortMenuHandler;
            }

            if (_assetInfo != null) {
                _assetInfo.OnNameChanged -= _presenter.OnNameChanged;
                _assetInfo.OnDescriptionChanged -= _presenter.OnDescriptionChanged;
                _assetInfo.OnTagAdded -= _presenter.OnTagAdded;
                _assetInfo.OnTagRemoved -= _presenter.OnTagRemoved;
                _assetInfo.OnTagClicked -= OnTagSelected;
                _assetInfo.OnDependencyAdded -= _presenter.OnDependencyAdded;
                _assetInfo.OnDependencyRemoved -= _presenter.OnDependencyRemoved;
                _assetInfo.OnDependencyClicked -= OnDependencyClicked;
                _assetInfo.OnFolderClicked -= OnFolderSelected;
                _assetInfo.OnDownloadRequested -= _presenter.OnDownloadRequested;
            }

            _textureService?.ClearCache();
            _toastManager?.ClearAll();
        }

        private void CreateGUI() {
            if (_repository == null) OnEnable();

            _isInitialized = false;

            var root = rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Row;

            _navigation = new Navigation {
                style = {
                    width = 200,
                    minWidth = 150,
                    flexShrink = 0,
                    borderRightWidth = 1,
                    borderRightColor = ColorPreset.WindowBorder
                }
            };
            root.Add(_navigation);

            _assetView = new AssetView();
            root.Add(_assetView);

            _tagListView = new TagListView();
            root.Add(_tagListView);

            _assetInfo = new AssetInfo {
                style = {
                    width = 300,
                    minWidth = 250,
                    flexShrink = 0,
                    borderLeftWidth = 1,
                    borderLeftColor = ColorPreset.WindowBorder
                }
            };
            root.Add(_assetInfo);

            _assetController = new AssetViewController(_repository);

            _assetView.SetController(_assetController);
            _assetView.Initialize(_textureService, _repository);
            _tagListView.Initialize(_repository);
            _tagListView.SetController(_assetController);
            _tagListView.SetShowDialogCallback(ShowDialog);
            _assetInfo.Initialize(_repository, _textureService, _folderService);

            _presenter = new AssetManagerPresenter(
                _repository,
                _assetService,
                _folderService,
                _assetController,
                ShowToast,
                RefreshUI,
                folders => _navigation.SetFolders(folders),
                () => _tagListView.Refresh(),
                () =>
                {
                    try {
                        return GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                    }
                    catch {
                        return new Vector2(position.x + position.width / 2, position.y + position.height / 2);
                    }
                },
                ShowDialog,
                TagSelectorWindow.Show,
                (screenPos, repo, selectedId, cb) =>
                    AssetSelectorWindow.Show(screenPos, repo, selectedId ?? Ulid.Empty, cb)
            );

            _navigation.NavigationChanged += OnNavigationChanged;
            _navigation.FolderSelected += OnFolderSelected;
            _navigation.TagListClicked += OnTagListClicked;
            _navigation.OnFolderRenamed += _presenter.OnFolderRenamed;
            _navigation.OnFolderDeleted += _presenter.OnFolderDeleted;
            _navigation.OnFolderMoved += _presenter.OnFolderMoved;
            _navigation.OnFolderCreated += _presenter.OnFolderCreated;
            _navigation.OnDropRequested += OnNavigationDropRequested;
            _navigationContextMenuHandler = (id, folderName, target) =>
            {
                var menu = new GenericDropdownMenu();
                menu.AddItem("Rename", false, () => _navigation.ShowRenameFolderDialog(id, folderName));
                menu.AddItem("Delete", false, () => _presenter.OnFolderDeleted(id));
                menu.DropDown(target.worldBound, target);
            };
            _navigation.OnFolderContextMenuRequested += _navigationContextMenuHandler;
            _navigation.OnFolderReordered += _presenter.OnFolderReordered;
            _navigation.OnAssetCreated += _presenter.OnAssetCreated;
            _navigation.SetShowDialogCallback(ShowDialog);
            _navigation.SetRepository(_repository);

            _presenter = new AssetManagerPresenter(
                _repository,
                _assetService,
                _folderService,
                _assetController,
                ShowToast,
                RefreshUI,
                folders => _navigation.SetFolders(folders),
                () => _tagListView.Refresh(),
                () =>
                {
                    try {
                        return GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                    }
                    catch {
                        return new Vector2(position.x + position.width / 2, position.y + position.height / 2);
                    }
                },
                ShowDialog,
                TagSelectorWindow.Show,
                (screenPos, repo, selectedId, cb) =>
                    AssetSelectorWindow.Show(screenPos, repo, selectedId ?? Ulid.Empty, cb)
            );

            _tagListView.OnTagSelected += OnTagSelected;
            _tagListView.OnTagRenamed += _presenter.OnTagRenamed;
            _tagListView.OnTagDeleted += _presenter.OnTagDeleted;

            _assetController.AssetSelected += OnAssetSelected;
            _assetController.FolderPreviewSelected += OnFolderPreviewSelected;
            _assetController.FolderUpdated += OnFolderUpdated;
            _assetController.FoldersChanged += OnFoldersChanged;
            _assetController.BoothItemFoldersChanged += OnBoothItemFoldersChanged;
            _assetController.ModeChanged += OnModeChanged;
            _assetController.OnHistoryChanged += OnControllerHistoryChanged;
            _assetView.OnSelectionChange += OnSelectionChanged;
            _assetView.OnItemsDroppedToFolder += OnItemsDroppedToFolder;
            _assetViewSortMenuHandler = element =>
            {
                var menu = new GenericDropdownMenu();
                menu.AddItem("Name (A-Z)", false, () => _assetView.ApplySortType(AssetSortType.NameAsc));
                menu.AddItem("Name (Z-A)", false, () => _assetView.ApplySortType(AssetSortType.NameDesc));
                menu.AddSeparator("");
                menu.AddItem("Date Added (Newest)", false,
                    () => _assetView.ApplySortType(AssetSortType.DateAddedNewest));
                menu.AddItem("Date Added (Oldest)", false,
                    () => _assetView.ApplySortType(AssetSortType.DateAddedOldest));
                menu.AddSeparator("");
                menu.AddItem("Last Edit (Newest)", false, () => _assetView.ApplySortType(AssetSortType.DateNewest));
                menu.AddItem("Last Edit (Oldest)", false, () => _assetView.ApplySortType(AssetSortType.DateOldest));
                menu.AddSeparator("");
                menu.AddItem("Size (Smallest)", false, () => _assetView.ApplySortType(AssetSortType.SizeSmallest));
                menu.AddItem("Size (Largest)", false, () => _assetView.ApplySortType(AssetSortType.SizeLargest));
                menu.AddSeparator("");
                menu.AddItem("Filetype (A-Z)", false, () => _assetView.ApplySortType(AssetSortType.ExtAsc));
                menu.AddItem("Filetype (Z-A)", false, () => _assetView.ApplySortType(AssetSortType.ExtDesc));
                var targetElement = element ?? _assetView;
                menu.DropDown(targetElement.worldBound, targetElement);
            };
            _assetView.OnSortMenuRequested += _assetViewSortMenuHandler;

            _assetInfo.OnNameChanged += _presenter.OnNameChanged;
            _assetInfo.OnDescriptionChanged += _presenter.OnDescriptionChanged;
            _assetInfo.OnTagAdded += _presenter.OnTagAdded;
            _assetInfo.OnTagRemoved += _presenter.OnTagRemoved;
            _assetInfo.OnTagClicked += OnTagSelected;
            _assetInfo.OnDependencyAdded += _presenter.OnDependencyAdded;
            _assetInfo.OnDependencyRemoved += _presenter.OnDependencyRemoved;
            _assetInfo.OnDependencyClicked += OnDependencyClicked;
            _assetInfo.OnFolderClicked += OnFolderSelected;
            _assetInfo.OnDownloadRequested += _presenter.OnDownloadRequested;

            _toastManager = new ToastManager(root);

            _navigation.SelectAll();
            _assetController.Refresh();

            ShowAssetView();

            _isInitialized = true;
        }

        private void OnSelectionChanged(List<object> selectedItems) {
            _presenter.UpdateSelection(selectedItems);

            _selectedAsset = _presenter.SelectedAsset;
            _currentPreviewFolderId = _presenter.CurrentPreviewFolderId;

            _assetInfo.UpdateSelection(selectedItems);
        }

        private void OnNavigationChanged(NavigationMode mode, string contextName, Func<AssetMetadata, bool> filter) {
            _assetController.SetMode(mode, contextName, filter, _isInitialized);

            _assetView?.ClearSelection();
            _selectedAsset = null;
            _currentPreviewFolderId = Ulid.Empty;
            _assetInfo?.UpdateSelection(new List<object>());
        }

        private void OnFolderSelected(Ulid folderId) {
            _assetController.SetFolder(folderId, _isInitialized);

            if (folderId != Ulid.Empty && (_selectedAsset == null || _selectedAsset.Folder == folderId)) return;
            _assetView?.ClearSelection();
            _selectedAsset = null;
            _currentPreviewFolderId = Ulid.Empty;
            _assetInfo?.UpdateSelection(new List<object>());
        }

        private void OnTagListClicked() {
            _tagListView.Refresh();
            _assetController.SetMode(NavigationMode.TagList, "Tag List", _ => false);
        }

        private void OnTagSelected(string tag) {
            _assetController.SetMode(
                NavigationMode.Tag,
                $"Tag: {tag}",
                a => !a.IsDeleted && a.Tags.Contains(tag)
            );
        }

        private void OnModeChanged(NavigationMode mode) {
            if (mode == NavigationMode.TagList)
                ShowTagListView();
            else
                ShowAssetView();
        }

        private void OnControllerHistoryChanged() {
            _navigation?.SelectState(_assetController.CurrentMode, _assetController.SelectedFolderId);

            _assetView?.ClearSelection();
            _selectedAsset = null;
            _assetInfo?.UpdateSelection(new List<object>());
        }

        private void OnAssetSelected(AssetMetadata asset) {
            _currentPreviewFolderId = Ulid.Empty;
        }

        private void OnFolderPreviewSelected(BaseFolder folder) {
            _currentPreviewFolderId = folder?.ID ?? Ulid.Empty;
        }

        private void OnFoldersChanged(List<BaseFolder> folders) {
            _navigation.SetFolders(folders);
        }

        private void OnBoothItemFoldersChanged(List<BoothItemFolder> folders) {
            _assetView.ShowBoothItemFolders(folders);
        }

        private void OnFolderUpdated(BaseFolder folder) {
            var folders = _folderService.GetRootFolders();
            _navigation.SetFolders(folders);

            if (_currentPreviewFolderId != Ulid.Empty && _currentPreviewFolderId == folder.ID)
                _assetInfo.UpdateSelection(new List<object> { folder });
        }


        private void OnDependencyClicked(Ulid dependencyId) {
            var depAsset = _repository.GetAsset(dependencyId);
            if (depAsset == null || depAsset.IsDeleted) {
                ShowToast("依存関係先のアセットが見つかりません", 3, ToastType.Error);
                return;
            }

            var folder = depAsset.Folder;
            if (folder != Ulid.Empty) {
                _navigation.SelectState(NavigationMode.Folders, folder);
                OnFolderSelected(folder);
            }
            else {
                _navigation.SelectAll();
            }
        }

        private void OnNavigationDropRequested(Ulid targetFolderId, string[] assetIdsData, string[] folderIdsData) {
            if (assetIdsData is { Length: > 0 }) {
                var assetIds = assetIdsData.Select(Ulid.Parse).ToList();

                var assetsFromBoothItemFolder = _presenter.FindAssetsFromBoothItemFolder(assetIds);
                if (assetsFromBoothItemFolder.Count > 0)
                    ShowBoothItemFolderWarningDialog(assetIds, targetFolderId, assetsFromBoothItemFolder);
                else
                    _presenter.PerformSetFolderForAssets(assetIds, targetFolderId);
            }

            if (folderIdsData is not { Length: > 0 }) return;
            var folderIds = folderIdsData.Select(Ulid.Parse).ToList();
            foreach (var sourceFolderId in folderIds.Where(id => id != targetFolderId))
                _presenter.OnFolderMoved(sourceFolderId, targetFolderId);
        }

        private void OnItemsDroppedToFolder(List<Ulid> assetIds, List<Ulid> folderIds, Ulid targetFolderId) {
            if (assetIds.Count > 0) {
                var assetsFromBoothItemFolder = _presenter.FindAssetsFromBoothItemFolder(assetIds);

                if (assetsFromBoothItemFolder.Count > 0) {
                    ShowBoothItemFolderWarningDialog(assetIds, targetFolderId, assetsFromBoothItemFolder);
                    return;
                }
            }

            _presenter.PerformItemsDroppedToFolder(assetIds, folderIds, targetFolderId);
        }

        private void RefreshUI(bool fullRefresh = true) {
            if (fullRefresh) _assetController.Refresh();

            if (_selectedAsset == null) return;
            var freshAsset = _repository.GetAsset(_selectedAsset.ID);
            _selectedAsset = freshAsset;
            _assetInfo.UpdateSelection(new List<object> { freshAsset });
        }

        private void ShowAssetView() {
            _assetView.style.display = DisplayStyle.Flex;
            _tagListView.style.display = DisplayStyle.None;
        }

        private void ShowTagListView() {
            _assetView.style.display = DisplayStyle.None;
            _tagListView.style.display = DisplayStyle.Flex;
        }

        private void ShowBoothItemFolderWarningDialog(List<Ulid> assetIds, Ulid targetFolderId,
            List<AssetMetadata> assetsFromBoothItemFolder) {
            var content = new VisualElement();

            var titleLabel = new Label("Warning") {
                style = {
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 10
                }
            };
            content.Add(titleLabel);

            var messageText = assetsFromBoothItemFolder.Count == 1
                ? $"The asset '{assetsFromBoothItemFolder[0].Name}' is currently in a Booth Item folder.\n\nMoving it to a regular folder may cause issues with Booth item management. Are you sure you want to continue?"
                : $"{assetsFromBoothItemFolder.Count} assets are currently in Booth Item folders.\n\nMoving them to a regular folder may cause issues with Booth item management. Are you sure you want to continue?";

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
                text = "Cancel",
                style = { marginRight = 5 }
            };
            buttonRow.Add(cancelBtn);

            var continueBtn = new Button {
                text = "Continue",
                style = {
                    backgroundColor = new Color(0.8f, 0.4f, 0.4f)
                }
            };
            buttonRow.Add(continueBtn);

            content.Add(buttonRow);

            var dialogContainer = ShowDialog(content);

            cancelBtn.clicked += () => dialogContainer?.RemoveFromHierarchy();
            continueBtn.clicked += () =>
            {
                foreach (var assetId in assetIds) _assetService.SetFolder(assetId, targetFolderId);

                RefreshUI();
                dialogContainer?.RemoveFromHierarchy();
            };
        }

        private VisualElement ShowDialog(VisualElement dialogContent) {
            var container = new VisualElement {
                style = {
                    position = Position.Absolute,
                    left = 0, right = 0, top = 0, bottom = 0,
                    backgroundColor = new StyleColor(new Color(0, 0, 0, 0.5f)),
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
            var window = GetWindow<AssetManagerWindow>("Asset Manager");
            window.minSize = new Vector2(800, 400);
            AssetManagerContainer.Repository.Load();
            window.Show();
        }
    }
}