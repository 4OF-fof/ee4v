using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Service;
using _4OF.ee4v.AssetManager.UI.Window;
using _4OF.ee4v.AssetManager.UI.Window._Component;
using _4OF.ee4v.AssetManager.UI.Window._Component.Dialog;
using _4OF.ee4v.Core.Utility;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Presenter {
    public class AssetManagerPresenter {
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

        public AssetManagerPresenter(
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

        public AssetMetadata SelectedAsset { get; private set; }

        public Ulid CurrentPreviewFolderId { get; private set; } = Ulid.Empty;

        public void UpdateSelection(List<object> selectedItems) {
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

        public void OnTagRenamed(string oldTag, string newTag) {
            _assetService.RenameTag(oldTag, newTag);
            _tagListRefresh?.Invoke();
            _refreshUI(false);
            _showToast?.Invoke($"タグ '{oldTag}' を '{newTag}' にリネームしました", 3, ToastType.Success);
        }

        public void OnTagDeleted(string tag) {
            _assetService.DeleteTag(tag);
            _tagListRefresh?.Invoke();
            _refreshUI(false);
            _showToast?.Invoke($"タグ '{tag}' を削除しました", 3, ToastType.Success);
        }

        public void OnFolderRenamed(Ulid folderId, string newName) {
            var libMetadata = _repository.GetLibraryMetadata();
            var oldFolder = libMetadata?.GetFolder(folderId);
            var oldName = oldFolder?.Name ?? "フォルダ";

            var success = _folderService.SetFolderName(folderId, newName);
            var folders = _folderService.GetRootFolders();
            _setNavigationFolders?.Invoke(folders);
            _refreshUI(false);
            if (!success)
                _showToast?.Invoke($"フォルダ '{oldName}' のリネームに失敗しました: 名前が無効です", 10, ToastType.Error);
        }

        public void OnFolderDeleted(Ulid folderId) {
            var libMetadata = _repository.GetLibraryMetadata();
            var folder = libMetadata?.GetFolder(folderId);
            var folderName = folder?.Name ?? "フォルダ";

            _folderService.DeleteFolder(folderId);
            var folders = _folderService.GetRootFolders();
            _setNavigationFolders?.Invoke(folders);
            _refreshUI(false);
            _showToast?.Invoke($"フォルダ '{folderName}' を削除しました", 3, ToastType.Success);
        }

        public void OnFolderCreated(string folderName) {
            var success = _folderService.CreateFolder(Ulid.Empty, folderName);
            var folders = _folderService.GetRootFolders();
            _setNavigationFolders?.Invoke(folders);
            _refreshUI(false);
            if (!success)
                _showToast?.Invoke($"フォルダ '{folderName}' の作成に失敗しました: 名前が無効です", 10, ToastType.Error);
        }

        public void OnAssetCreated(string assetName, string description, string fileOrUrl, List<string> tags,
            string shopDomain, string itemId) {
            if (string.IsNullOrWhiteSpace(assetName)) {
                _showToast?.Invoke("アセット名を入力してください", 5, ToastType.Error);
                return;
            }

            try {
                AssetMetadata asset;

                if (!string.IsNullOrWhiteSpace(fileOrUrl)) {
                    if (File.Exists(fileOrUrl)) {
                        _repository.CreateAssetFromFile(fileOrUrl);
                        asset = _repository.GetAllAssets().OrderByDescending(a => a.ModificationTime).FirstOrDefault();
                        if (asset == null) {
                            _showToast?.Invoke("ファイルからのアセット作成に失敗しました", 5, ToastType.Error);
                            return;
                        }
                    }
                    else {
                        asset = _repository.CreateEmptyAsset();
                    }
                }
                else {
                    asset = _repository.CreateEmptyAsset();
                }

                asset.SetName(assetName);
                if (!string.IsNullOrWhiteSpace(description))
                    asset.SetDescription(description);

                if (!string.IsNullOrWhiteSpace(shopDomain) || !string.IsNullOrWhiteSpace(itemId)) {
                    var boothData = new BoothMetadata();
                    if (!string.IsNullOrWhiteSpace(shopDomain))
                        boothData.SetShopDomain(shopDomain);
                    if (!string.IsNullOrWhiteSpace(itemId))
                        boothData.SetItemID(itemId);
                    asset.SetBoothData(boothData);
                }

                if (tags is { Count: > 0 })
                    foreach (var tag in tags.Where(tag => !string.IsNullOrWhiteSpace(tag)))
                        asset.AddTag(tag);

                _assetService.SaveAsset(asset);
                _assetController.Refresh();
                _showToast?.Invoke($"アセット '{assetName}' を作成しました", 3, ToastType.Success);
            }
            catch (Exception ex) {
                _showToast?.Invoke($"アセットの作成に失敗しました: {ex.Message}", 5, ToastType.Error);
                Debug.LogError($"Failed to create asset: {ex}");
            }
        }

        public void OnFolderMoved(Ulid sourceFolderId, Ulid targetFolderId) {
            _folderService.MoveFolder(sourceFolderId, targetFolderId);
            var folders = _folderService.GetRootFolders();
            _setNavigationFolders?.Invoke(folders);
            _refreshUI(false);
        }

        public void OnFolderReordered(Ulid parentFolderId, Ulid folderId, int newIndex) {
            if (parentFolderId == Ulid.Empty) {
                var libraries = _repository.GetLibraryMetadata();
                if (libraries != null) {
                    var full = libraries.FolderList;
                    var mappedIndex = MapVisibleRootIndexToFullIndex(full, newIndex);
                    _folderService.ReorderFolder(parentFolderId, folderId, mappedIndex);
                    var mappedRootFolders = _folderService.GetRootFolders();
                    _setNavigationFolders?.Invoke(mappedRootFolders);
                    _refreshUI(false);
                    return;
                }
            }

            _folderService.ReorderFolder(parentFolderId, folderId, newIndex);
            var rootFolders = _folderService.GetRootFolders();
            _setNavigationFolders?.Invoke(rootFolders);
            _refreshUI(false);
        }

        private static int MapVisibleRootIndexToFullIndex(IReadOnlyList<BaseFolder> fullList, int visibleIndex) {
            if (visibleIndex < 0) return 0;

            var visibleCount = 0;
            for (var i = 0; i < fullList.Count; i++) {
                if (fullList[i] is BoothItemFolder) continue;

                if (visibleCount == visibleIndex) return i;
                visibleCount++;
            }

            if (visibleCount == 0) return 0;

            for (var i = fullList.Count - 1; i >= 0; i--)
                if (fullList[i] is not BoothItemFolder)
                    return i + 1;

            return fullList.Count;
        }

        public List<AssetMetadata> FindAssetsFromBoothItemFolder(List<Ulid> assetIds) {
            var libMetadata = _repository.GetLibraryMetadata();
            var assetsFromBoothItemFolder = (from assetId in assetIds
                select _repository.GetAsset(assetId)
                into asset
                where asset != null
                where asset.Folder != Ulid.Empty && libMetadata != null
                let currentFolder = libMetadata.GetFolder(asset.Folder)
                where currentFolder is BoothItemFolder
                select asset).ToList();

            return assetsFromBoothItemFolder;
        }

        public void PerformSetFolderForAssets(List<Ulid> assetIds, Ulid targetFolderId) {
            foreach (var assetId in assetIds) _assetService.SetFolder(assetId, targetFolderId);
            _refreshUI(true);
        }

        public void PerformItemsDroppedToFolder(List<Ulid> assetIds, List<Ulid> folderIds, Ulid targetFolderId) {
            if (assetIds.Count > 0)
                foreach (var assetId in assetIds)
                    _assetService.SetFolder(assetId, targetFolderId);

            if (folderIds.Count > 0)
                foreach (var folderId in folderIds)
                    _folderService.MoveFolder(folderId, targetFolderId);

            _refreshUI(true);
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