using System;
using System.Collections.Generic;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Service;
using _4OF.ee4v.AssetManager.UI.Window;
using _4OF.ee4v.AssetManager.UI.Window._Component;
using _4OF.ee4v.AssetManager.UI.Window._Component.Dialog;
using _4OF.ee4v.Core.Utility;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Presenter {
    public class AssetPropertyPresenter {
        private readonly AssetViewController _assetController;
        private readonly AssetService _assetService;
        private readonly FolderService _folderService;
        private readonly Func<Vector2> _getScreenPosition;
        private readonly Action<bool> _refreshUI;
        private readonly IAssetRepository _repository;
        private readonly Action<List<BaseFolder>> _setNavigationFolders;
        private readonly Action<Vector2, IAssetRepository, Ulid?, Action<Ulid>> _showAssetSelector;
        private readonly Func<VisualElement, VisualElement> _showDialog;
        private readonly Action<Vector2, IAssetRepository, Action<string>> _showTagSelector;

        private readonly Action<string, float?, ToastType> _showToast;
        private readonly Action _tagListRefresh;

        public AssetPropertyPresenter(
            IAssetRepository repository,
            AssetService assetService,
            FolderService folderService,
            AssetViewController controller,
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
            _showToast = showToast;
            _refreshUI = refreshUI;
            _setNavigationFolders = setNavigationFolders;
            _tagListRefresh = tagListRefresh;
            _getScreenPosition = getScreenPosition;
            _showDialog = showDialog;
            _showTagSelector = showTagSelector;
            _showAssetSelector = showAssetSelector;
        }

        private AssetMetadata SelectedAsset { get; set; }
        private Ulid CurrentPreviewFolderId { get; set; } = Ulid.Empty;

        public event Action<AssetMetadata> OnSelectedAssetChanged;
        public event Action<Ulid> OnPreviewFolderChanged;

        public void UpdateSelection(List<object> selectedItems) {
            var previousSelected = SelectedAsset;
            var previousPreviewFolder = CurrentPreviewFolderId;

            switch (selectedItems) {
                case { Count: 1 } when selectedItems[0] is AssetMetadata asset:
                    SelectedAsset = asset;
                    CurrentPreviewFolderId = Ulid.Empty;
                    break;
                case { Count: 1 } when selectedItems[0] is BaseFolder folder:
                    SelectedAsset = null;
                    CurrentPreviewFolderId = folder.ID;
                    break;
                default:
                    SelectedAsset = null;
                    CurrentPreviewFolderId = Ulid.Empty;
                    break;
            }

            if (!Equals(previousSelected, SelectedAsset)) OnSelectedAssetChanged?.Invoke(SelectedAsset);
            if (previousPreviewFolder != CurrentPreviewFolderId) OnPreviewFolderChanged?.Invoke(CurrentPreviewFolderId);
        }

        public void ClearSelection() {
            SelectedAsset = null;
            CurrentPreviewFolderId = Ulid.Empty;
            OnSelectedAssetChanged?.Invoke(null);
            OnPreviewFolderChanged?.Invoke(CurrentPreviewFolderId);
        }

        public void OnNameChanged(string newName) {
            if (SelectedAsset != null) {
                var oldName = SelectedAsset.Name;
                var success = _assetService.SetAssetName(SelectedAsset.ID, newName);
                _refreshUI(true);
                if (!success)
                    _showToast?.Invoke($"アセット '{oldName}' のリネームに失敗しました: 名前が無効です", 10, ToastType.Error);
                return;
            }

            var targetFolderId = _assetController?.SelectedFolderId ?? Ulid.Empty;
            if (targetFolderId == Ulid.Empty && CurrentPreviewFolderId != Ulid.Empty)
                targetFolderId = CurrentPreviewFolderId;

            if (targetFolderId == Ulid.Empty) return;

            var libMetadata = _repository?.GetLibraryMetadata();
            var oldFolder = libMetadata?.GetFolder(targetFolderId);
            var oldFolderName = oldFolder?.Name ?? "フォルダ";

            var successFolder = _folderService.SetFolderName(targetFolderId, newName);
            var folders = _folderService.GetRootFolders();
            _setNavigationFolders?.Invoke(folders);
            _refreshUI(false);
            if (!successFolder)
                _showToast?.Invoke($"フォルダ '{oldFolderName}' のリネームに失敗しました: 名前が無効です", 10, ToastType.Error);
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

            if (SelectedAsset != null) {
                _assetService.AddTag(SelectedAsset.ID, newTag);
            }
            else {
                var targetFolderId = _assetController?.SelectedFolderId ?? Ulid.Empty;
                if (targetFolderId == Ulid.Empty && CurrentPreviewFolderId != Ulid.Empty)
                    targetFolderId = CurrentPreviewFolderId;

                if (targetFolderId != Ulid.Empty) {
                    _folderService.AddTag(targetFolderId, newTag);
                }
                else {
                    _showToast?.Invoke("タグの追加対象が見つかりませんでした", 3, ToastType.Error);
                    return;
                }
            }

            _tagListRefresh?.Invoke();
            _refreshUI(false);
        }

        public void OnTagRemoved(string tagToRemove) {
            if (SelectedAsset != null) {
                _assetService.RemoveTag(SelectedAsset.ID, tagToRemove);
            }
            else {
                var targetFolderId = _assetController?.SelectedFolderId ?? Ulid.Empty;
                if (targetFolderId == Ulid.Empty && CurrentPreviewFolderId != Ulid.Empty)
                    targetFolderId = CurrentPreviewFolderId;

                if (targetFolderId != Ulid.Empty) {
                    _folderService.RemoveTag(targetFolderId, tagToRemove);
                }
                else {
                    _showToast?.Invoke("タグの削除対象が見つかりませんでした", 3, ToastType.Error);
                    return;
                }
            }

            _tagListRefresh?.Invoke();
            _refreshUI(false);
        }

        public void OnDependencyAdded(Ulid dependencyId) {
            if (SelectedAsset == null) {
                _showToast?.Invoke("依存関係を追加するアセットが選択されていません", 3, ToastType.Error);
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
                _showToast?.Invoke("ファイル名が設定されていません。", 3f, ToastType.Error);
                return;
            }

            var dialog = new DownloadDialog();
            var content = dialog.CreateContent(downloadUrl, SelectedAsset.ID, fileName, _assetService);

            dialog.OnDownloadCompleted += () =>
            {
                var dialogContainer = content.parent?.parent;
                dialogContainer?.RemoveFromHierarchy();
                _refreshUI(true);
                _showToast?.Invoke("ダウンロードが完了しました。", 3f, ToastType.Success);
            };

            _showDialog?.Invoke(content);
        }
    }
}