using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Service;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public static class AssetContextMenuFactory {
        public static GenericDropdownMenu Create(
            List<object> targets,
            IAssetRepository repository,
            AssetService assetService,
            FolderService folderService,
            TextureService textureService,
            Action onRefresh
        ) {
            var menu = new GenericDropdownMenu();

            var assetTargets = targets.OfType<AssetMetadata>().ToList();
            var folderTargets = targets.OfType<BaseFolder>().ToList();

            var deletedAssetTargets = assetTargets.Where(a => a.IsDeleted).ToList();
            var activeAssetTargets = assetTargets.Where(a => !a.IsDeleted).ToList();

            var singleAsset = activeAssetTargets.Count == 1 ? activeAssetTargets[0] : null;

            if (singleAsset != null) {
                var canImport = true;

                if (singleAsset.Ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    canImport = repository.HasImportItems(singleAsset.ID);

                if (canImport) {
                    menu.AddItem("インポート", false, () => { assetService.ImportAsset(singleAsset.ID); });
                    menu.AddSeparator("");
                }
            }

            if (singleAsset != null && singleAsset.Ext.Equals(".zip", StringComparison.OrdinalIgnoreCase)) {
                menu.AddItem("インポート対象を選択...", false, () =>
                {
                    var mousePos = Event.current != null ? Event.current.mousePosition : Vector2.zero;
                    ZipImportWindow.Open(
                        GUIUtility.GUIToScreenPoint(mousePos),
                        singleAsset.ID,
                        repository,
                        assetService
                    );
                });
                menu.AddSeparator("");
            }

            if (singleAsset != null) {
                menu.AddItem("エクスプローラーで開く", false, () =>
                {
                    var files = repository.GetAssetFiles(singleAsset.ID);
                    if (files.Count > 0) EditorUtility.RevealInFinder(files[0]);
                });
                menu.AddSeparator("");
            }

            if (assetTargets.Count > 0 && assetTargets.Count == deletedAssetTargets.Count && folderTargets.Count == 0) {
                var plural = deletedAssetTargets.Count > 1;

                menu.AddItem(plural ? $"{deletedAssetTargets.Count} 個を完全に削除" : "完全に削除", false, () =>
                {
                    var message = plural
                        ? $"選択した {deletedAssetTargets.Count} 個のアセットを完全に削除しますか？この操作は取り消せません。"
                        : "アセットを完全に削除しますか？この操作は取り消せません。";
                    if (!EditorUtility.DisplayDialog("確認", message, "削除", "キャンセル")) return;
                    foreach (var a in deletedAssetTargets) assetService.DeleteAsset(a.ID);
                    onRefresh?.Invoke();
                });

                menu.AddItem(plural ? $"{deletedAssetTargets.Count} 個を復元" : "復元", false, () =>
                {
                    foreach (var a in deletedAssetTargets) assetService.RestoreAsset(a.ID);
                    onRefresh?.Invoke();
                });

                return menu;
            }

            if (activeAssetTargets.Count > 0 || folderTargets.Count > 0)
                menu.AddItem("サムネイルを設定...", false, () =>
                {
                    var path = EditorUtility.OpenFilePanel("Select Thumbnail", "", "png,jpg,jpeg");
                    if (string.IsNullOrEmpty(path)) return;

                    foreach (var a in activeAssetTargets) repository?.SetThumbnail(a.ID, path);
                    foreach (var f in folderTargets) folderService.SetFolderThumbnail(f.ID, path);

                    onRefresh?.Invoke();
                });

            var anyRemovableThumb = activeAssetTargets.Any(a =>
            {
                var p = repository?.GetThumbnailPath(a.ID);
                return !string.IsNullOrEmpty(p) && File.Exists(p);
            }) || folderTargets.Any(f =>
            {
                var p = repository?.GetFolderThumbnailPath(f.ID);
                return !string.IsNullOrEmpty(p) && File.Exists(p);
            });

            if (anyRemovableThumb)
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

            menu.AddSeparator("");

            if (activeAssetTargets.Count > 0) {
                var plural = activeAssetTargets.Count > 1;
                menu.AddItem(plural ? $"{activeAssetTargets.Count} 個を削除" : "アセットを削除", false, () =>
                {
                    foreach (var a in activeAssetTargets) assetService.RemoveAsset(a.ID);
                    onRefresh?.Invoke();
                });
            }

            if (folderTargets.Count > 0) {
                var plural = folderTargets.Count > 1;
                menu.AddItem(plural ? $"{folderTargets.Count} 個のフォルダを削除" : "フォルダを削除", false, () =>
                {
                    foreach (var f in folderTargets) folderService.DeleteFolder(f.ID);
                    onRefresh?.Invoke();
                });
            }

            if (deletedAssetTargets.Count <= 0) return menu;
            menu.AddItem("復元", false, () =>
            {
                foreach (var a in deletedAssetTargets) assetService.RestoreAsset(a.ID);
                onRefresh?.Invoke();
            });
            menu.AddItem("完全に削除", false, () =>
            {
                var plural2 = deletedAssetTargets.Count > 1;
                var message = plural2
                    ? $"選択した {deletedAssetTargets.Count} 個のアセットを完全に削除しますか？この操作は取り消せません。"
                    : "アセットを完全に削除しますか？この操作は取り消せません。";
                if (!EditorUtility.DisplayDialog("確認", message, "削除", "キャンセル")) return;
                foreach (var a in deletedAssetTargets) assetService.DeleteAsset(a.ID);
                onRefresh?.Invoke();
            });

            return menu;
        }
    }
}