using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.AssetManager.Services;
using _4OF.ee4v.AssetManager.Views.Toast;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Utility;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.Presenter {
    public class AssetNavigationPresenter {
        private readonly AssetListService _assetController;
        private readonly AssetService _assetService;
        private readonly FolderService _folderService;
        private readonly bool _isInitialized;
        private readonly Action<bool> _refreshUI;
        private readonly IAssetRepository _repository;
        private readonly Action<List<BaseFolder>> _setNavigationFolders;

        private readonly Action<string, float?, ToastType> _showToast;
        private readonly Action _tagListRefresh;

        public AssetNavigationPresenter(
            IAssetRepository repository,
            AssetService assetService,
            FolderService folderService,
            AssetListService controller,
            Action<string, float?, ToastType> showToast,
            Action<bool> refreshUI,
            Action<List<BaseFolder>> setNavigationFolders,
            Action tagListRefresh
        ) {
            _repository = repository;
            _assetService = assetService;
            _folderService = folderService;
            _assetController = controller;
            _showToast = showToast;
            _refreshUI = refreshUI;
            _setNavigationFolders = setNavigationFolders;
            _tagListRefresh = tagListRefresh;
            _isInitialized = true;
        }

        public void OnNavigationChanged(NavigationMode mode, string contextName, Func<AssetMetadata, bool> filter) {
            _assetController.SetMode(mode, contextName, filter, _isInitialized);
        }

        public void OnFolderSelected(Ulid folderId) {
            _assetController.SetFolder(folderId, _isInitialized);
        }

        public void OnTagListClicked() {
            _assetController.SetMode(NavigationMode.TagList, I18N.Get("UI.AssetManager.Navigation.TagList"), _ => false,
                _isInitialized);
        }

        public void OnTagSelected(string tag) {
            _assetController.SetMode(
                NavigationMode.Tag,
                $"{I18N.Get("UI.AssetManager.Navigation.TagPrefix")}{tag}",
                a => !a.IsDeleted && a.Tags.Contains(tag),
                _isInitialized
            );
        }

        public void OnFolderCreated(string folderName) {
            var success = _folderService.CreateFolder(Ulid.Empty, folderName);
            var folders = _folderService.GetRootFolders();
            _setNavigationFolders?.Invoke(folders);
            _refreshUI(false);
            if (!success)
                _showToast?.Invoke(I18N.Get("UI.AssetManager.Toast.FolderCreateFailedInvalid", folderName), 10,
                    ToastType.Error);
        }

        public void OnFolderRenamed(Ulid folderId, string newName) {
            var libMetadata = _repository.GetLibraryMetadata();
            var oldFolder = libMetadata?.GetFolder(folderId);
            var oldName = oldFolder?.Name ?? I18N.Get("UI.AssetManager.Folder.UnknownName");

            var success = _folderService.SetFolderName(folderId, newName);
            var folders = _folderService.GetRootFolders();
            _setNavigationFolders?.Invoke(folders);
            _refreshUI(false);
            if (!success)
                _showToast?.Invoke(I18N.Get("UI.AssetManager.Toast.FolderRenameFailedInvalid", oldName), 10,
                    ToastType.Error);
        }

        public void OnFolderDeleted(Ulid folderId) {
            var libMetadata = _repository.GetLibraryMetadata();
            var folder = libMetadata?.GetFolder(folderId);
            var folderName = folder?.Name ?? I18N.Get("UI.AssetManager.Folder.UnknownName");

            _folderService.DeleteFolder(folderId);
            var folders = _folderService.GetRootFolders();
            _setNavigationFolders?.Invoke(folders);
            _refreshUI(false);
            _showToast?.Invoke(I18N.Get("UI.AssetManager.Toast.FolderDeleted", folderName), 3, ToastType.Success);
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

        public void OnAssetCreated(string assetName, string description, string fileOrUrl, List<string> tags,
            string shopDomain, string itemId) {
            if (string.IsNullOrWhiteSpace(assetName)) {
                _showToast?.Invoke(I18N.Get("UI.AssetManager.Toast.AssetNameRequired"), 5, ToastType.Error);
                return;
            }

            try {
                AssetMetadata asset;

                if (!string.IsNullOrWhiteSpace(fileOrUrl)) {
                    if (File.Exists(fileOrUrl)) {
                        _repository.CreateAssetFromFile(fileOrUrl);
                        asset = _repository.GetAllAssets().OrderByDescending(a => a.ModificationTime).FirstOrDefault();
                        if (asset == null) {
                            _showToast?.Invoke(I18N.Get("UI.AssetManager.Toast.CreateFromFileFailed"), 5,
                                ToastType.Error);
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
                _showToast?.Invoke(I18N.Get("UI.AssetManager.Toast.AssetCreatedFmt", assetName), 3, ToastType.Success);
            }
            catch (Exception ex) {
                _showToast?.Invoke(I18N.Get("UI.AssetManager.Toast.AssetCreateFailedFmt", ex.Message), 5,
                    ToastType.Error);
                Debug.LogError($"Failed to create asset: {ex}");
            }
        }

        public void OnTagRenamed(string oldTag, string newTag) {
            _assetService.RenameTag(oldTag, newTag);
            _tagListRefresh?.Invoke();
            _refreshUI(false);
            _showToast?.Invoke(I18N.Get("UI.AssetManager.Toast.TagRenamedFmt", oldTag, newTag), 3, ToastType.Success);
        }

        public void OnTagDeleted(string tag) {
            _assetService.DeleteTag(tag);
            _tagListRefresh?.Invoke();
            _refreshUI(false);
            _showToast?.Invoke(I18N.Get("UI.AssetManager.Toast.TagDeleted", tag), 3, ToastType.Success);
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
    }
}