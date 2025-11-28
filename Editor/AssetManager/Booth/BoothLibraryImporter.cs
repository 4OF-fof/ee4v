using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Utility;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.Booth {
    public static class BoothLibraryImporter {
        public static event Action<int> OnImportCompleted;

        public static int Import(List<ShopDto> shops) {
            if (shops == null) return 0;

            var repository = AssetManagerContainer.Repository;

            BoothLibraryServerState.SetProcessing(true);

            try {
                repository.Load();
                var existingAssets = repository.GetAllAssets().ToList();
                var librariesClone = new LibraryMetadata(repository.GetLibraryMetadata() ?? new LibraryMetadata());

                var stagedAssets = new List<AssetMetadata>();
                var folderImageCandidates = new Dictionary<Ulid, string>();

                foreach (var shop in shops) {
                    if (shop.items == null) continue;
                    foreach (var item in shop.items) {
                        if (item.files == null || item.files.Count == 0) continue;
                        foreach (var f in item.files)
                            try {
                                string downloadId = null;
                                if (!string.IsNullOrEmpty(f.url)) {
                                    var regexDownload = new Regex(@"downloadables/(\d+)", RegexOptions.IgnoreCase);
                                    var matchDownload = regexDownload.Match(f.url);
                                    if (matchDownload.Success) downloadId = matchDownload.Groups[1].Value;
                                }

                                string itemId = null;
                                if (!string.IsNullOrEmpty(item.itemURL)) {
                                    var regexItem = new Regex(@"items/(\d+)");
                                    var matchItem = regexItem.Match(item.itemURL);
                                    if (matchItem.Success) itemId = matchItem.Groups[1].Value;
                                }

                                if (AlreadyImportedDownloadable(existingAssets, downloadId) ||
                                    stagedAssets.Any(a => a.BoothData?.DownloadId == downloadId))
                                    continue;

                                var filename = string.IsNullOrEmpty(f.filename) ? null : f.filename;
                                if (!string.IsNullOrEmpty(itemId) && !string.IsNullOrEmpty(filename)) {
                                    var dupExisting = existingAssets.Any(a =>
                                        a.BoothData?.ItemId == itemId && a.BoothData?.FileName == filename);
                                    var dupStaged = stagedAssets.Any(a =>
                                        a.BoothData?.ItemId == itemId && a.BoothData?.FileName == filename);
                                    if (dupExisting || dupStaged) continue;
                                }

                                var asset = new AssetMetadata();
                                var name = !string.IsNullOrEmpty(f.filename)
                                    ? Path.GetFileNameWithoutExtension(f.filename)
                                    : !string.IsNullOrEmpty(item.name)
                                        ? item.name
                                        : !string.IsNullOrEmpty(item.itemURL)
                                            ? item.itemURL
                                            : I18N.Get("UI.AssetManager.Default.UnnamedBoothItem");
                                asset.SetName(name);

                                var booth = new BoothMetadata();
                                var shopUrl = shop.shopURL ?? item.itemURL;
                                if (!string.IsNullOrEmpty(shopUrl)) {
                                    var regexShop = new Regex(@"https?://([^\.]+)\.booth\.pm", RegexOptions.IgnoreCase);
                                    var matchShop = regexShop.Match(shopUrl);
                                    if (matchShop.Success) booth.SetShopDomain(matchShop.Groups[1].Value);
                                }

                                if (!string.IsNullOrEmpty(itemId)) booth.SetItemID(itemId);
                                if (!string.IsNullOrEmpty(downloadId)) booth.SetDownloadID(downloadId);
                                booth.SetFileName(filename);

                                asset.SetBoothData(booth);

                                var folderIdentifier = !string.IsNullOrEmpty(itemId) ? itemId :
                                    !string.IsNullOrEmpty(downloadId) ? downloadId :
                                    !string.IsNullOrEmpty(filename) ? filename : item.itemURL;

                                var folderNameForMeta = !string.IsNullOrEmpty(item.name)
                                    ? item.name
                                    : !string.IsNullOrEmpty(itemId)
                                        ? itemId
                                        : !string.IsNullOrEmpty(item.itemURL)
                                            ? item.itemURL
                                            : folderIdentifier;

                                var folderId = EnsureBoothItemFolderForMeta(librariesClone, booth.ShopDomain,
                                    shop.shopName,
                                    folderIdentifier, folderNameForMeta, item.description);

                                if (folderId != Ulid.Empty) {
                                    asset.SetFolder(folderId);

                                    if (!string.IsNullOrEmpty(item.imageURL) &&
                                        !folderImageCandidates.ContainsKey(folderId))
                                        folderImageCandidates[folderId] = item.imageURL;
                                }

                                stagedAssets.Add(asset);
                            }
                            catch (Exception e) {
                                Debug.LogError(I18N.Get(
                                    "Debug.AssetManager.BoothLibraryImporter.FailedStagingDownloadableFmt", e.Message));
                            }
                    }
                }

                if (stagedAssets.Count == 0) {
                    Debug.Log(I18N.Get("Debug.AssetManager.BoothLibraryImporter.NoNewItems"));
                    try {
                        Debug.Log(I18N.Get("Debug.AssetManager.BoothLibraryImporter.NoItemsOnImportCompleted"));
                        OnImportCompleted?.Invoke(0);
                    }
                    catch {
                        /* ignore */
                    }

                    return 0;
                }

                var oldLib = new LibraryMetadata(repository.GetLibraryMetadata() ?? new LibraryMetadata());

                try {
                    repository.SaveLibraryMetadata(librariesClone);

                    var saved = stagedAssets.Select(a => a.ID).ToList();
                    if (stagedAssets.Count > 0) repository.SaveAssets(stagedAssets);

                    Debug.Log(I18N.Get("Debug.AssetManager.BoothLibraryImporter.ImportedItemsFmt", saved.Count));

                    try {
                        BoothThumbnailDownloader.Enqueue(repository, folderImageCandidates);
                    }
                    catch (Exception e) {
                        Debug.LogWarning(I18N.Get(
                            "Debug.AssetManager.BoothLibraryImporter.FailedToScheduleThumbnailDownloadFmt", e.Message));
                    }

                    try {
                        Debug.Log(I18N.Get("Debug.AssetManager.BoothLibraryImporter.InvokingOnImportCompletedFmt",
                            saved.Count));
                        OnImportCompleted?.Invoke(saved.Count);
                    }
                    catch {
                        /* ignore */
                    }

                    return saved.Count;
                }
                catch (Exception commitEx) {
                    Debug.LogError($"Failed committing staged Booth imports: {commitEx.Message}");
                    try {
                        foreach (var id in repository.GetAllAssets().Select(x => x.ID)
                                     .Where(id => stagedAssets.Any(s => s.ID == id)))
                            try {
                                repository.DeleteAsset(id);
                            }
                            catch {
                                /* ignore */
                            }
                    }
                    catch {
                        /* ignore */
                    }

                    try {
                        repository.SaveLibraryMetadata(oldLib);
                    }
                    catch {
                        /* ignore */
                    }

                    try {
                        Debug.Log(I18N.Get("Debug.AssetManager.BoothLibraryImporter.ImportFailedInvokeCompleted"));
                        OnImportCompleted?.Invoke(0);
                    }
                    catch {
                        /* ignore */
                    }

                    return 0;
                }
            }
            finally {
                BoothLibraryServerState.SetProcessing(false);
            }
        }

        private static bool AlreadyImportedDownloadable(IEnumerable<AssetMetadata> assets, string downloadId) {
            return !string.IsNullOrEmpty(downloadId) && assets.Any(a => a.BoothData?.DownloadId == downloadId);
        }

        private static Ulid EnsureBoothItemFolderForMeta(LibraryMetadata libraries, string shopDomain, string shopName,
            string identifier,
            string folderName, string folderDescription = null, Ulid parentFolderId = default) {
            if (libraries == null) return Ulid.Empty;

            foreach (var root in libraries.FolderList) {
                var found = FindBoothItemFolderRecursive(root, shopDomain ?? string.Empty, identifier);
                if (found == null) continue;
                if (!string.IsNullOrEmpty(shopName) && found.ShopName != shopName) found.SetShopName(shopName);
                if (!string.IsNullOrEmpty(folderDescription) && found.Description != folderDescription)
                    found.SetDescription(folderDescription);

                return found.ID;
            }

            var newFolder = new BoothItemFolder();
            var preferredName = !string.IsNullOrEmpty(folderName)
                ? folderName
                : !string.IsNullOrEmpty(identifier)
                    ? identifier
                    : I18N.Get("UI.AssetManager.Default.BoothItem");
            newFolder.SetName(preferredName);
            newFolder.SetDescription(folderDescription ?? shopName ?? string.Empty);
            newFolder.SetShopDomain(shopDomain);
            newFolder.SetShopName(shopName);
            if (!string.IsNullOrEmpty(identifier) && identifier.All(char.IsDigit)) newFolder.SetItemId(identifier);

            if (parentFolderId == default || parentFolderId == Ulid.Empty) {
                libraries.AddFolder(newFolder);
            }
            else {
                var parentBase = libraries.GetFolder(parentFolderId);
                if (parentBase is BoothItemFolder || parentBase is not Folder parentFolder) return Ulid.Empty;

                parentFolder.AddChild(newFolder);
            }

            return newFolder.ID;
        }

        private static BoothItemFolder FindBoothItemFolderRecursive(BaseFolder root, string shopDomain,
            string identifier) {
            switch (root) {
                case null:
                    break;
                case BoothItemFolder bf when !string.IsNullOrEmpty(shopDomain) &&
                    !string.IsNullOrEmpty(bf.ShopDomain) &&
                    bf.ShopDomain != shopDomain:
                    break;
                case BoothItemFolder bf: {
                    if (!string.IsNullOrEmpty(identifier))
                        if ((!string.IsNullOrEmpty(bf.ItemId) && bf.ItemId == identifier) || bf.Name == identifier)
                            return bf;
                    break;
                }
                case Folder { Children: not null } f: {
                    foreach (var c in f.Children) {
                        var found = FindBoothItemFolderRecursive(c, shopDomain, identifier);
                        if (found != null) return found;
                    }

                    break;
                }
            }

            return null;
        }
    }
}