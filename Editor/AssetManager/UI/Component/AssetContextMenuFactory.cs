using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _4OF.ee4v.AssetManager.Booth;
using _4OF.ee4v.AssetManager.Booth.Dialog;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.AssetManager.UI.Window;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Utility;
using _4OF.ee4v.ProjectExtension.ItemStyle.API;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Component {
    public static class AssetContextMenuFactory {
        private const float ItemHeight = 19f;
        private const float SeparatorHeight = 6f;
        private const float MenuPadding = 10f;

        public static GenericDropdownMenu Create(
            List<object> targets,
            IAssetRepository repository,
            AssetService assetService,
            FolderService folderService,
            TextureService textureService,
            Action onRefresh,
            Action<VisualElement> showDialog,
            out float estimatedHeight
        ) {
            var menu = new GenericDropdownMenu();
            var height = MenuPadding;

            var assetTargets = targets.OfType<AssetMetadata>().ToList();
            var folderTargets = targets.OfType<BaseFolder>().ToList();

            var deletedAssetTargets = assetTargets.Where(a => a.IsDeleted).ToList();
            var activeAssetTargets = assetTargets.Where(a => !a.IsDeleted).ToList();

            var singleAsset = activeAssetTargets.Count == 1 ? activeAssetTargets[0] : null;

            var assetsWithFiles = activeAssetTargets.Where(a => repository.HasAssetFile(a.ID)).ToList();
            var importableAssets = assetsWithFiles.Where(a =>
                !a.Ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) || repository.HasImportItems(a.ID)).ToList();

            var showZipImport = singleAsset != null &&
                singleAsset.Ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) &&
                repository.HasAssetFile(singleAsset.ID);

            if (importableAssets.Count > 0 || showZipImport) {
                if (importableAssets.Count > 0) {
                    var label = importableAssets.Count > 1
                        ? I18N.Get("UI.AssetManager.ContextMenu.ImportPluralFmt", importableAssets.Count)
                        : I18N.Get("UI.AssetManager.ContextMenu.Import");

                    menu.AddItem(label, false,
                        () => { assetService.ImportAssetList(importableAssets.Select(a => a.ID)); });
                    height += ItemHeight;
                }

                if (showZipImport) {
                    menu.AddItem(I18N.Get("UI.AssetManager.ContextMenu.ImportSelectFromZip"), false, () =>
                    {
                        var mousePos = Event.current != null ? Event.current.mousePosition : Vector2.zero;
                        ZipImportWindow.Open(
                            GUIUtility.GUIToScreenPoint(mousePos),
                            singleAsset.ID,
                            repository,
                            assetService
                        );
                    });
                    height += ItemHeight;
                }

                menu.AddSeparator("");
                height += SeparatorHeight;
            }

            var highlightGuids = new List<string>();

            foreach (var asset in activeAssetTargets)
                highlightGuids.AddRange(asset.UnityData.AssetGuidList.Select(g => g.ToString("N")));

            foreach (var folder in folderTargets.OfType<BoothItemFolder>()) {
                var guidsInFolder = repository.GetAllAssets()
                    .Where(a => a.Folder == folder.ID && !a.IsDeleted)
                    .SelectMany(a => a.UnityData.AssetGuidList)
                    .Select(g => g.ToString("N"));
                highlightGuids.AddRange(guidsInFolder);
            }

            if (highlightGuids.Count > 0) {
                highlightGuids = highlightGuids.Distinct().ToList();

                var label = activeAssetTargets.Count + folderTargets.Count > 1
                    ? I18N.Get("UI.AssetManager.ContextMenu.HighlightProjectPlural")
                    : I18N.Get("UI.AssetManager.ContextMenu.HighlightProject");

                menu.AddItem(label, false, () =>
                {
                    ProjectExtensionAPI.ClearHighlights();
                    ProjectExtensionAPI.SetHighlights(highlightGuids);
                });
                menu.AddSeparator("");
                height += ItemHeight + SeparatorHeight;
            }

            if (singleAsset != null) {
                menu.AddItem(I18N.Get("UI.AssetManager.ContextMenu.OpenInExplorer"), false, () =>
                {
                    var files = repository.GetAssetFiles(singleAsset.ID);
                    if (files.Count > 0) EditorUtility.RevealInFinder(files[0]);
                });
                menu.AddSeparator("");
                height += ItemHeight + SeparatorHeight;
            }

            if (singleAsset != null) {
                menu.AddItem(I18N.Get("UI.AssetManager.ContextMenu.EditBoothInfo"), false, () =>
                {
                    var dialog = new EditBoothInfoDialog();
                    dialog.OnBoothInfoUpdated += (domain, itemId) =>
                    {
                        var newAsset = new AssetMetadata(singleAsset);
                        newAsset.BoothData.SetShopDomain(domain);
                        newAsset.BoothData.SetItemID(itemId);
                        assetService.SaveAsset(newAsset);
                        onRefresh?.Invoke();
                    };
                    showDialog?.Invoke(dialog.CreateContent(singleAsset.BoothData?.ItemUrl ?? ""));
                });
                menu.AddSeparator("");
                height += ItemHeight + SeparatorHeight;
            }

            if (assetTargets.Count > 0 && assetTargets.Count == deletedAssetTargets.Count && folderTargets.Count == 0) {
                var plural = deletedAssetTargets.Count > 1;

                menu.AddItem(
                    plural
                        ? I18N.Get("UI.AssetManager.ContextMenu.RestorePluralFmt", deletedAssetTargets.Count)
                        : I18N.Get("UI.AssetManager.ContextMenu.Restore"), false, () =>
                        ExecuteRestore(deletedAssetTargets));
                menu.AddItem(
                    plural
                        ? I18N.Get("UI.AssetManager.ContextMenu.DeletePermanentlyPluralFmt", deletedAssetTargets.Count)
                        : I18N.Get("UI.AssetManager.ContextMenu.DeletePermanently"), false, () =>
                        ExecuteHardDelete(deletedAssetTargets));

                height += ItemHeight * 2;
                estimatedHeight = height;
                return menu;
            }
            
            var boothFolders = folderTargets.OfType<BoothItemFolder>().ToList();
            if (boothFolders.Count > 0) {
                var label = boothFolders.Count > 1
                    ? I18N.Get("UI.AssetManager.ContextMenu.RefetchThumbnailPlural")
                    : I18N.Get("UI.AssetManager.ContextMenu.RefetchThumbnail");

                menu.AddItem(label, false,
                    () => { BoothUtility.FetchAndDownloadThumbnails(boothFolders, repository, showDialog); });

                height += ItemHeight + SeparatorHeight;
            }
            
            var assetsWithParentThumb = activeAssetTargets.Where(a =>
            {
                if (a.Folder == Ulid.Empty) return false;
                var parent = repository.GetLibraryMetadata()?.GetFolder(a.Folder);

                if (parent == null) return false;
                var p = repository.GetFolderThumbnailPath(parent.ID);
                return !string.IsNullOrEmpty(p) && File.Exists(p);
            }).ToList();

            if (assetsWithParentThumb.Count > 0) {
                var label = assetsWithParentThumb.Count > 1
                    ? I18N.Get("UI.AssetManager.ContextMenu.UseParentThumbnailPlural")
                    : I18N.Get("UI.AssetManager.ContextMenu.UseParentThumbnail");

                menu.AddItem(label, false, () =>
                {
                    foreach (var asset in assetsWithParentThumb) {
                        var parent = repository.GetLibraryMetadata().GetFolder(asset.Folder);
                        var p = repository.GetFolderThumbnailPath(parent.ID);
                        repository.SetThumbnail(asset.ID, p);
                    }

                    onRefresh?.Invoke();
                });
                height += ItemHeight;
            }

            if (activeAssetTargets.Count > 0 || folderTargets.Count > 0) {
                menu.AddItem(I18N.Get("UI.AssetManager.ContextMenu.SetThumbnail"), false, () =>
                {
                    var path = EditorUtility.OpenFilePanel(
                        I18N.Get("UI.AssetManager.ContextMenu.SelectThumbnailDialogTitle"), "", "png,jpg,jpeg");
                    if (string.IsNullOrEmpty(path)) return;

                    foreach (var a in activeAssetTargets) repository?.SetThumbnail(a.ID, path);
                    foreach (var f in folderTargets) folderService.SetFolderThumbnail(f.ID, path);

                    onRefresh?.Invoke();
                });
                height += ItemHeight;
            }

            var anyRemovableThumb = activeAssetTargets.Any(a =>
            {
                var p = repository?.GetThumbnailPath(a.ID);
                return !string.IsNullOrEmpty(p) && File.Exists(p);
            }) || folderTargets.Any(f =>
            {
                var p = repository?.GetFolderThumbnailPath(f.ID);
                return !string.IsNullOrEmpty(p) && File.Exists(p);
            });

            if (anyRemovableThumb) {
                menu.AddItem(I18N.Get("UI.AssetManager.ContextMenu.RemoveThumbnail"), false, () =>
                {
                    foreach (var a in activeAssetTargets) {
                        repository?.RemoveThumbnail(a.ID);
                        textureService?.RemoveAssetFromCache(a.ID);
                    }

                    foreach (var f in from f in folderTargets
                             let thumbPath = repository?.GetFolderThumbnailPath(f.ID)
                             where !string.IsNullOrEmpty(thumbPath) && File.Exists(thumbPath)
                             select f) {
                        folderService.RemoveFolderThumbnail(f.ID);
                        textureService?.RemoveFolderFromCache(f.ID);
                    }

                    onRefresh?.Invoke();
                });
                height += ItemHeight;
            }

            menu.AddSeparator("");
            height += SeparatorHeight;

            if (activeAssetTargets.Count > 0) {
                var plural = activeAssetTargets.Count > 1;
                menu.AddItem(
                    plural
                        ? I18N.Get("UI.AssetManager.ContextMenu.DeleteAssetsPluralFmt", activeAssetTargets.Count)
                        : I18N.Get("UI.AssetManager.ContextMenu.DeleteAsset"), false, () =>
                    {
                        foreach (var a in activeAssetTargets) assetService.RemoveAsset(a.ID);
                        onRefresh?.Invoke();
                    });
                height += ItemHeight;
            }

            if (folderTargets.Count > 0) {
                var plural = folderTargets.Count > 1;
                menu.AddItem(
                    plural
                        ? I18N.Get("UI.AssetManager.ContextMenu.DeleteFoldersPluralFmt", folderTargets.Count)
                        : I18N.Get("UI.AssetManager.ContextMenu.DeleteFolder"), false, () =>
                    {
                        foreach (var f in folderTargets) folderService.DeleteFolder(f.ID);
                        onRefresh?.Invoke();
                    });
                height += ItemHeight;
            }

            if (deletedAssetTargets.Count > 0) {
                menu.AddItem(I18N.Get("UI.AssetManager.ContextMenu.Restore"), false,
                    () => ExecuteRestore(deletedAssetTargets));
                menu.AddItem(I18N.Get("UI.AssetManager.ContextMenu.DeletePermanently"), false,
                    () => ExecuteHardDelete(deletedAssetTargets));
                height += ItemHeight * 2;
            }

            estimatedHeight = height;
            return menu;

            void ExecuteRestore(List<AssetMetadata> targetAssets) {
                foreach (var a in targetAssets) assetService.RestoreAsset(a.ID);
                onRefresh?.Invoke();
            }

            void ExecuteHardDelete(List<AssetMetadata> targetAssets) {
                var count = targetAssets.Count;
                var plural = count > 1;
                var message = plural
                    ? I18N.Get("UI.AssetManager.ContextMenu.DeleteAssetsConfirmPluralFmt", count)
                    : I18N.Get("UI.AssetManager.ContextMenu.DeleteAssetConfirm");

                if (!EditorUtility.DisplayDialog(I18N.Get("UI.Core.ConfirmTitle"), message, I18N.Get("UI.Core.Delete"),
                        I18N.Get("UI.Core.Cancel"))) return;

                foreach (var a in targetAssets) assetService.DeleteAsset(a.ID);
                onRefresh?.Invoke();
            }
        }
    }
}