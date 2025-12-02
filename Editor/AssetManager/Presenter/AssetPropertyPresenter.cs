using System;
using System.Collections.Generic;
using _4OF.ee4v.AssetManager.Component;
using _4OF.ee4v.AssetManager.Component.Dialog;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Utility;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Presenter {
    public class AssetPropertyPresenter {
        private readonly AssetViewController _assetController;
        private readonly AssetService _assetService;
        private readonly FolderService _folderService;
        private readonly Func<Vector2> _getScreenPosition;
        private readonly AssetSelectionModel _model;
        private readonly Action<bool> _refreshUI;
        private readonly IAssetRepository _repository;
        private readonly Action<List<BaseFolder>> _setNavigationFolders;
        private readonly Action<Vector2, IAssetRepository, Ulid?, Action<Ulid>> _showAssetSelector;
        private readonly Func<VisualElement, VisualElement> _showDialog;
        private readonly Action<Vector2, IAssetRepository, Action<string>> _showTagSelector;

        private readonly Action<string, float?, ToastType> _showToast;
        private readonly Action _tagListRefresh;
        private List<object> _currentSelection = new();

        public AssetPropertyPresenter(
            IAssetRepository repository,
            AssetService assetService,
            FolderService folderService,
            AssetViewController controller,
            AssetSelectionModel model,
            Action<string, float?, ToastType> showToast,
            Action<bool> refreshUI,
            Action<List<BaseFolder>> setNavigationFolders,
            Action tagListRefresh,
            Func<Vector2> getScreenPosition,
            Func<VisualElement, VisualElement> showDialog,
            Action<Vector2, IAssetRepository, Action<string>> showTagSelector,
            Action<Vector2, IAssetRepository, Ulid?, Action<Ulid>> showAssetSelector
        ) {
            _repository = repository;
            _assetService = assetService;
            _folderService = folderService;
            _assetController = controller;
            _model = model;
            _showToast = showToast;
            _refreshUI = refreshUI;
            _setNavigationFolders = setNavigationFolders;
            _tagListRefresh = tagListRefresh;
            _getScreenPosition = getScreenPosition;
            _showDialog = showDialog;
            _showTagSelector = showTagSelector;
            _showAssetSelector = showAssetSelector;
        }

        private AssetMetadata SelectedAsset => _model.SelectedAsset.Value;
        private Ulid CurrentPreviewFolderId => _model.PreviewFolderId.Value;

        public void UpdateSelection(List<object> selectedItems) {
            _currentSelection = selectedItems ?? new List<object>();
            _model.SetSelection(selectedItems);

            switch (selectedItems) {
                case { Count: 1 } when selectedItems[0] is AssetMetadata asset:
                    _model.SetSelectedAsset(asset);
                    _model.SetPreviewFolder(Ulid.Empty);
                    break;
                case { Count: 1 } when selectedItems[0] is BaseFolder folder:
                    _model.SetSelectedAsset(null);
                    _model.SetPreviewFolder(folder.ID);
                    break;
                default:
                    _model.SetSelectedAsset(null);
                    _model.SetPreviewFolder(Ulid.Empty);
                    break;
            }
        }

        public void ClearSelection() {
            _currentSelection.Clear();
            _model.Clear();
        }

        public void OnNameChanged(string newName) {
            if (SelectedAsset != null) {
                var oldName = SelectedAsset.Name;
                var success = _assetService.SetAssetName(SelectedAsset.ID, newName);
                _refreshUI(true);
                if (!success)
                    _showToast?.Invoke(I18N.Get("UI.AssetManager.Toast.AssetRenameFailedInvalidFmt", oldName), 10,
                        ToastType.Error);
                return;
            }

            var targetFolderId = _assetController?.SelectedFolderId ?? Ulid.Empty;
            if (targetFolderId == Ulid.Empty && CurrentPreviewFolderId != Ulid.Empty)
                targetFolderId = CurrentPreviewFolderId;

            if (targetFolderId == Ulid.Empty) return;

            var libMetadata = _repository?.GetLibraryMetadata();
            var oldFolder = libMetadata?.GetFolder(targetFolderId);
            var oldFolderName = oldFolder?.Name ?? I18N.Get("UI.AssetManager.Folder.UnknownName");

            var successFolder = _folderService.SetFolderName(targetFolderId, newName);
            var folders = _folderService.GetRootFolders();
            _setNavigationFolders?.Invoke(folders);
            _refreshUI(false);
            if (!successFolder)
                _showToast?.Invoke(I18N.Get("UI.AssetManager.Toast.FolderRenameFailedInvalid", oldFolderName), 10,
                    ToastType.Error);
        }

        public void OnDescriptionChanged(string newDesc) {
            if (SelectedAsset != null) {
                _assetService.SetDescription(SelectedAsset.ID, newDesc);
                _refreshUI(false);
                return;
            }

            var targetFolderId = _assetController?.SelectedFolderId ?? Ulid.Empty;
            if (targetFolderId == Ulid.Empty && CurrentPreviewFolderId != Ulid.Empty)
                targetFolderId = CurrentPreviewFolderId;

            if (targetFolderId == Ulid.Empty) return;

            _folderService.SetFolderDescription(targetFolderId, newDesc);
            _refreshUI(false);
        }

        public void OnTagAdded(string newTag) {
            if (string.IsNullOrWhiteSpace(newTag)) {
                Vector2 screenPosition;
                try {
                    screenPosition = _getScreenPosition();
                }
                catch {
                    screenPosition = Vector2.zero;
                }

                _showTagSelector?.Invoke(screenPosition, _repository, OnTagAdded);
                return;
            }

            if (_currentSelection.Count > 0) {
                foreach (var item in _currentSelection)
                    switch (item) {
                        case AssetMetadata asset:
                            _assetService.AddTag(asset.ID, newTag);
                            break;
                        case BaseFolder folder:
                            _folderService.AddTag(folder.ID, newTag);
                            break;
                    }
            }
            else {
                var targetFolderId = _assetController?.SelectedFolderId ?? Ulid.Empty;
                if (targetFolderId == Ulid.Empty && CurrentPreviewFolderId != Ulid.Empty)
                    targetFolderId = CurrentPreviewFolderId;

                if (targetFolderId != Ulid.Empty) {
                    _folderService.AddTag(targetFolderId, newTag);
                }
                else {
                    _showToast?.Invoke(I18N.Get("UI.AssetManager.Toast.TagAddNotFound"), 3, ToastType.Error);
                    return;
                }
            }

            _tagListRefresh?.Invoke();
            _refreshUI(false);
        }

        public void OnTagRemoved(string tagToRemove) {
            if (_currentSelection.Count > 0) {
                foreach (var item in _currentSelection)
                    switch (item) {
                        case AssetMetadata asset:
                            _assetService.RemoveTag(asset.ID, tagToRemove);
                            break;
                        case BaseFolder folder:
                            _folderService.RemoveTag(folder.ID, tagToRemove);
                            break;
                    }
            }
            else {
                var targetFolderId = _assetController?.SelectedFolderId ?? Ulid.Empty;
                if (targetFolderId == Ulid.Empty && CurrentPreviewFolderId != Ulid.Empty)
                    targetFolderId = CurrentPreviewFolderId;

                if (targetFolderId != Ulid.Empty) {
                    _folderService.RemoveTag(targetFolderId, tagToRemove);
                }
                else {
                    _showToast?.Invoke(I18N.Get("UI.AssetManager.Toast.TagDeleteNotFound"), 3, ToastType.Error);
                    return;
                }
            }

            _tagListRefresh?.Invoke();
            _refreshUI(false);
        }

        public void OnDependencyAdded(Ulid dependencyId) {
            if (SelectedAsset == null) {
                _showToast?.Invoke(I18N.Get("UI.AssetManager.Toast.DependencyAssetNotSelected"), 3, ToastType.Error);
                return;
            }

            if (dependencyId == Ulid.Empty) {
                Vector2 screenPosition;
                try {
                    screenPosition = _getScreenPosition();
                }
                catch {
                    screenPosition = Vector2.zero;
                }

                _showAssetSelector?.Invoke(screenPosition, _repository, SelectedAsset?.ID ?? Ulid.Empty,
                    OnDependencyAdded);
                return;
            }

            SelectedAsset.UnityData.AddDependenceItem(dependencyId);
            _assetService.SaveAsset(SelectedAsset);
            _refreshUI(false);
        }

        public void OnDependencyRemoved(Ulid dependencyId) {
            if (SelectedAsset == null) return;

            SelectedAsset.UnityData.RemoveDependenceItem(dependencyId);
            _assetService.SaveAsset(SelectedAsset);
            _refreshUI(false);
        }

        public void OnDownloadRequested(string downloadUrl) {
            if (string.IsNullOrEmpty(downloadUrl) || SelectedAsset == null) return;

            var fileName = SelectedAsset.BoothData?.FileName ?? "";
            if (string.IsNullOrEmpty(fileName)) {
                _showToast?.Invoke(I18N.Get("UI.AssetManager.Download.FileNameNotSet"), 3f, ToastType.Error);
                return;
            }

            var dialog = new DownloadDialog();
            var content = dialog.CreateContent(downloadUrl, SelectedAsset.ID, fileName, _assetService);

            dialog.OnDownloadCompleted += () =>
            {
                var dialogContainer = content.parent?.parent;
                dialogContainer?.RemoveFromHierarchy();
                _refreshUI(true);
                _showToast?.Invoke(I18N.Get("UI.AssetManager.Download.Completed"), 3f, ToastType.Success);
            };

            _showDialog?.Invoke(content);
        }
    }
}