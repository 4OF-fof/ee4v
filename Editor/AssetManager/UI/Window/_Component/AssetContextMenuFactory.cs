using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Service;
using _4OF.ee4v.AssetManager.UI.Window._Component.Dialog;
using _4OF.ee4v.ProjectExtension.API;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
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

            if (singleAsset != null) {
                var hasImportAction = false;
                var canImport = repository.HasAssetFile(singleAsset.ID);
                var isZip = singleAsset.Ext.Equals(".zip", StringComparison.OrdinalIgnoreCase);

                if (canImport && isZip)
                    canImport = repository.HasImportItems(singleAsset.ID);

                if (canImport) {
                    menu.AddItem("インポート", false, () => { assetService.ImportAsset(singleAsset.ID); });
                    height += ItemHeight;
                    hasImportAction = true;
                }

                if (isZip) {
                    menu.AddItem("インポート対象を選択", false, () =>
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
                    hasImportAction = true;
                }

                if (hasImportAction) {
                    menu.AddSeparator("");
                    height += SeparatorHeight;
                }
            }

            if (singleAsset != null && singleAsset.UnityData.AssetGuidList.Count > 0) {
                menu.AddItem("プロジェクトでハイライト", false, () =>
                {
                    var guids = singleAsset.UnityData.AssetGuidList.Select(g => g.ToString("N")).ToList();
                    ProjectExtensionAPI.ClearHighlights();
                    ProjectExtensionAPI.SetHighlights(guids);
                });
                menu.AddSeparator("");
                height += ItemHeight + SeparatorHeight;
            }

            var singleFolder = folderTargets.Count == 1 ? folderTargets[0] : null;
            if (singleFolder is BoothItemFolder) {
                var guids = repository.GetAllAssets()
                    .Where(a => a.Folder == singleFolder.ID && !a.IsDeleted)
                    .SelectMany(a => a.UnityData.AssetGuidList)
                    .Select(g => g.ToString("N"))
                    .ToList();

                if (guids.Count > 0) {
                    menu.AddItem("プロジェクトでハイライト", false, () =>
                    {
                        ProjectExtensionAPI.ClearHighlights();
                        ProjectExtensionAPI.SetHighlights(guids);
                    });
                    menu.AddSeparator("");
                    height += ItemHeight + SeparatorHeight;
                }
            }

            if (singleAsset != null) {
                menu.AddItem("エクスプローラーで開く", false, () =>
                {
                    var files = repository.GetAssetFiles(singleAsset.ID);
                    if (files.Count > 0) EditorUtility.RevealInFinder(files[0]);
                });
                menu.AddSeparator("");
                height += ItemHeight + SeparatorHeight;
            }

            if (singleAsset != null) {
                menu.AddItem("Booth情報を編集", false, () =>
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

                menu.AddItem(plural ? $"{deletedAssetTargets.Count} 個を復元" : "復元", false, () =>
                    ExecuteRestore(deletedAssetTargets));
                menu.AddItem(plural ? $"{deletedAssetTargets.Count} 個を完全に削除" : "完全に削除", false, () =>
                    ExecuteHardDelete(deletedAssetTargets));

                height += ItemHeight * 2;
                estimatedHeight = height;
                return menu;
            }

            if (activeAssetTargets.Count > 0 || folderTargets.Count > 0) {
                menu.AddItem("サムネイルを設定", false, () =>
                {
                    var path = EditorUtility.OpenFilePanel("Select Thumbnail", "", "png,jpg,jpeg");
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
                menu.AddItem("サムネイルを削除", false, () =>
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
                menu.AddItem(plural ? $"{activeAssetTargets.Count} 個を削除" : "アセットを削除", false, () =>
                {
                    foreach (var a in activeAssetTargets) assetService.RemoveAsset(a.ID);
                    onRefresh?.Invoke();
                });
                height += ItemHeight;
            }

            if (folderTargets.Count > 0) {
                var plural = folderTargets.Count > 1;
                menu.AddItem(plural ? $"{folderTargets.Count} 個のフォルダを削除" : "フォルダを削除", false, () =>
                {
                    foreach (var f in folderTargets) folderService.DeleteFolder(f.ID);
                    onRefresh?.Invoke();
                });
                height += ItemHeight;
            }

            if (deletedAssetTargets.Count > 0) {
                menu.AddItem("復元", false, () => ExecuteRestore(deletedAssetTargets));
                menu.AddItem("完全に削除", false, () => ExecuteHardDelete(deletedAssetTargets));
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
                    ? $"選択した {count} 個のアセットを完全に削除しますか？この操作は取り消せません。"
                    : "アセットを完全に削除しますか？この操作は取り消せません。";

                if (!EditorUtility.DisplayDialog("確認", message, "削除", "キャンセル")) return;

                foreach (var a in targetAssets) assetService.DeleteAsset(a.ID);
                onRefresh?.Invoke();
            }
        }
    }
}