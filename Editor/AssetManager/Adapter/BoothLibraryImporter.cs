using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.Utility;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.Adapter {
    public static class BoothLibraryImporter {
        public static int Import(List<ShopDto> shops) {
            if (shops == null) return 0;

            var repository = AssetManagerContainer.Repository;

            BoothLibraryServerState.SetProcessing(true);

            try {
                repository.Load();
                var existingAssets = repository.GetAllAssets().ToList();
                var librariesClone = new LibraryMetadata(repository.GetLibraryMetadata() ?? new LibraryMetadata());

                var stagedAssets = new List<AssetMetadata>();

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
                                    stagedAssets.Any(a => a.BoothData?.DownloadID == downloadId))
                                    continue;

                                var filename = string.IsNullOrEmpty(f.filename) ? null : f.filename;
                                if (!string.IsNullOrEmpty(itemId) && !string.IsNullOrEmpty(filename)) {
                                    var dupExisting = existingAssets.Any(a =>
                                        a.BoothData?.ItemID == itemId && a.BoothData?.FileName == filename);
                                    var dupStaged = stagedAssets.Any(a =>
                                        a.BoothData?.ItemID == itemId && a.BoothData?.FileName == filename);
                                    if (dupExisting || dupStaged) continue;
                                }

                                var asset = new AssetMetadata();
                                var name = !string.IsNullOrEmpty(f.filename) ? f.filename :
                                    !string.IsNullOrEmpty(item.name) ? item.name :
                                    !string.IsNullOrEmpty(item.itemURL) ? item.itemURL : "Unnamed Booth Item";
                                name = NormalizePresentation(name);
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
                                booth.SetFileName(NormalizePresentation(filename));

                                asset.SetBoothData(booth);

                                var folderIdentifier = !string.IsNullOrEmpty(itemId) ? itemId :
                                    !string.IsNullOrEmpty(downloadId) ? downloadId :
                                    !string.IsNullOrEmpty(filename) ? filename : item.itemURL;

                                var folderNameForMeta = !string.IsNullOrEmpty(item.name)
                                    ? NormalizePresentation(item.name)
                                    : !string.IsNullOrEmpty(itemId)
                                        ? itemId
                                        : !string.IsNullOrEmpty(item.itemURL)
                                            ? item.itemURL
                                            : folderIdentifier;

                                var folderId = EnsureBoothItemFolderForMeta(librariesClone, booth.ShopDomain,
                                    shop.shopName,
                                    folderIdentifier, folderNameForMeta, item.description);

                                if (folderId != Ulid.Empty) asset.SetFolder(folderId);

                                stagedAssets.Add(asset);
                            }
                            catch (Exception e) {
                                Debug.LogError($"Failed staging booth downloadable: {e.Message}");
                            }
                    }
                }

                if (stagedAssets.Count == 0) {
                    Debug.Log("No new Booth items to import.");
                    return 0;
                }

                var oldLib = new LibraryMetadata(repository.GetLibraryMetadata() ?? new LibraryMetadata());

                try {
                    repository.SaveLibraryMetadata(librariesClone);

                    var saved = new List<Ulid>();
                    foreach (var a in stagedAssets) {
                        repository.SaveAsset(a);
                        saved.Add(a.ID);
                    }

                    Debug.Log($"Imported {saved.Count} Booth items into AssetLibrary");
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
                        // ignore
                    }

                    try {
                        repository.SaveLibraryMetadata(oldLib);
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
            return !string.IsNullOrEmpty(downloadId) && assets.Any(a => a.BoothData?.DownloadID == downloadId);
        }

        private static Ulid EnsureBoothItemFolder(IAssetRepository repository, string shopDomain, string shopName,
            string identifier,
            string folderName, string folderDescription = null, Ulid parentFolderId = default) {
            var libraries = repository.GetLibraryMetadata();
            if (libraries == null) {
                Debug.LogError("Library metadata is not loaded.");
                return Ulid.Empty;
            }

            foreach (var root in libraries.FolderList) {
                var found = FindBoothItemFolderRecursive(root, shopDomain ?? string.Empty, identifier);
                if (found == null) continue;
                var needsUpdate = false;
                var updated = new BoothItemFolder(found);
                if (!string.IsNullOrEmpty(shopName) && updated.ShopName != shopName) {
                    updated.SetShopName(shopName);
                    needsUpdate = true;
                }

                if (!string.IsNullOrEmpty(folderDescription) && updated.Description != folderDescription) {
                    updated.SetDescription(folderDescription);
                    needsUpdate = true;
                }

                if (needsUpdate)
                    AssetManagerContainer.FolderService.UpdateBoothItemFolder(updated);
                return found.ID;
            }

            var newFolder = new BoothItemFolder();
            newFolder.SetName(NormalizePresentation(folderName ?? identifier ?? "Booth Item"));
            newFolder.SetDescription(NormalizePresentation(folderDescription ?? shopName ?? string.Empty));
            newFolder.SetShopDomain(shopDomain);
            newFolder.SetShopName(shopName);
            if (!string.IsNullOrEmpty(identifier) && identifier.All(char.IsDigit)) newFolder.SetItemId(identifier);

            if (parentFolderId == default || parentFolderId == Ulid.Empty) {
                libraries.AddFolder(newFolder);
                Debug.Log(
                    $"Created BoothItemFolder '{newFolder.Name}' (Id: {newFolder.ID}) at root for shop {shopDomain}");
            }
            else {
                var parentBase = libraries.GetFolder(parentFolderId);
                if (parentBase is BoothItemFolder) {
                    Debug.LogError("Cannot create a BoothItemFolder under another BoothItemFolder.");
                    return Ulid.Empty;
                }

                if (parentBase is not Folder parentFolder) {
                    Debug.LogError($"Parent folder {parentFolderId} not found.");
                    return Ulid.Empty;
                }

                parentFolder.AddChild(newFolder);
                Debug.Log(
                    $"Created BoothItemFolder '{newFolder.Name}' (Id: {newFolder.ID}) under parent {parentFolder.ID}");
            }

            repository.SaveLibraryMetadata(libraries);
            return newFolder.ID;
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
            newFolder.SetName(NormalizePresentation(folderName ?? identifier ?? "Booth Item"));
            newFolder.SetDescription(NormalizePresentation(folderDescription ?? shopName ?? string.Empty));
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

        private static string NormalizePresentation(string input) {
            if (string.IsNullOrEmpty(input)) return input;

            var decomposed = input.Normalize(NormalizationForm.FormKD);
            var sb = new StringBuilder(decomposed.Length);

            foreach (var ch in from ch in decomposed
                     let category = CharUnicodeInfo.GetUnicodeCategory(ch)
                     where category != UnicodeCategory.NonSpacingMark
                     select ch) {
                if (ch >= 32 && ch <= 126) {
                    sb.Append(ch);
                    continue;
                }

                if (ch >= '\uFF01' && ch <= '\uFF5E') sb.Append((char)(ch - 0xFEE0));
            }

            var cleaned = sb.ToString().Trim();
            var collapsed = Regex.Replace(cleaned, "[ \t\r\n\\f]+", " ");
            return collapsed;
        }
    }
}