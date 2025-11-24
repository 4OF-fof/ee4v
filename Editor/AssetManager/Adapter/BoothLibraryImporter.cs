using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.Utility;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.Adapter {
    public static class BoothLibraryImporter {
        // Import shops into the Asset Manager. Returns number of created assets.
        public static int Import(List<ShopDto> shops) {
            if (shops == null) return 0;

            var repository = AssetManagerContainer.Repository;

            // mark processing so health/status reflects ongoing work
            BoothLibraryServerState.SetProcessing(true);

            try {
                // Load repository and take snapshots
                repository.Load();
                var existingAssets = repository.GetAllAssets().ToList();
                var librariesClone = new LibraryMetadata(repository.GetLibraryMetadata() ?? new LibraryMetadata());

                // staging buffers
                var stagedAssets = new List<AssetMetadata>();

                foreach (var shop in shops) {
                    if (shop.items == null) continue;
                    foreach (var item in shop.items) {
                        if (item.files == null || item.files.Count == 0) continue;
                        foreach (var f in item.files) {
                            try {
                                // Extract download and item ids
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

                                // Duplicate check against existing assets and staged assets
                                if (AlreadyImportedDownloadable(existingAssets, downloadId) ||
                                    stagedAssets.Any(a => a.BoothData?.DownloadID == downloadId))
                                    continue;

                                var filename = string.IsNullOrEmpty(f.filename) ? null : f.filename;
                                if (!string.IsNullOrEmpty(itemId) && !string.IsNullOrEmpty(filename)) {
                                    var dupExisting = existingAssets.Any(a => a.BoothData?.ItemID == itemId && a.BoothData?.FileName == filename);
                                    var dupStaged = stagedAssets.Any(a => a.BoothData?.ItemID == itemId && a.BoothData?.FileName == filename);
                                    if (dupExisting || dupStaged) continue;
                                }

                                // Create in-memory asset and set metadata
                                var asset = new AssetMetadata();
                                var name = !string.IsNullOrEmpty(f.filename) ? f.filename :
                                    !string.IsNullOrEmpty(item.name) ? item.name :
                                    !string.IsNullOrEmpty(item.itemURL) ? item.itemURL : "Unnamed Booth Item";
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

                                // allocate folder within cloned metadata
                                var folderIdentifier = !string.IsNullOrEmpty(itemId) ? itemId :
                                    !string.IsNullOrEmpty(downloadId) ? downloadId :
                                    !string.IsNullOrEmpty(filename) ? filename : item.itemURL;

                                // Use item.name when available; otherwise fallback to itemId, then itemURL, then identifier
                                var folderNameForMeta = !string.IsNullOrEmpty(item.name)
                                    ? item.name
                                    : !string.IsNullOrEmpty(itemId)
                                        ? itemId
                                        : !string.IsNullOrEmpty(item.itemURL)
                                            ? item.itemURL
                                            : folderIdentifier;

                                var folderId = EnsureBoothItemFolderForMeta(librariesClone, booth.ShopDomain, shop.shopName,
                                    folderIdentifier, folderNameForMeta, item.description);

                                if (folderId != Ulid.Empty) asset.SetFolder(folderId);

                                stagedAssets.Add(asset);
                            }
                            catch (Exception e) {
                                UnityEngine.Debug.LogError($"Failed staging booth downloadable: {e.Message}");
                            }
                        }
                    }
                }

                // commit: save library metadata and staged assets atomically where possible
                if (stagedAssets.Count == 0) {
                    UnityEngine.Debug.Log("No new Booth items to import.");
                    return 0;
                }

                // snapshot current library metadata so we can roll back on failure
                var oldLib = new LibraryMetadata(repository.GetLibraryMetadata() ?? new LibraryMetadata());

                try {
                    // save library metadata first (contains newly created folders)
                    repository.SaveLibraryMetadata(librariesClone);

                    var saved = new List<Ulid>();
                    foreach (var a in stagedAssets) {
                        repository.SaveAsset(a);
                        saved.Add(a.ID);
                    }

                    UnityEngine.Debug.Log($"Imported {saved.Count} Booth items into AssetLibrary");
                    return saved.Count;
                }
                catch (Exception commitEx) {
                    UnityEngine.Debug.LogError($"Failed committing staged Booth imports: {commitEx.Message}");
                    // try to roll back saved assets
                    try {
                        foreach (var id in repository.GetAllAssets().Select(x => x.ID).Where(id => stagedAssets.Any(s => s.ID == id))) {
                            try { repository.DeleteAsset(id); } catch { /* ignore */ }
                        }
                    }
                    catch {
                        // ignore
                    }

                    // restore old library metadata
                    try { repository.SaveLibraryMetadata(oldLib); } catch { /* ignore */ }

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
                UnityEngine.Debug.LogError("Library metadata is not loaded.");
                return Ulid.Empty;
            }

            // Search existing
            foreach (var root in libraries.FolderList) {
                var found = FindBoothItemFolderRecursive(root, shopDomain ?? string.Empty, identifier);
                if (found != null) {
                    // Update shop name/description on the found BoothItemFolder if provided and they differ
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
            }

            // Create new BoothItemFolder
            var newFolder = new BoothItemFolder();
            newFolder.SetName(folderName ?? identifier ?? "Booth Item");
            newFolder.SetDescription(folderDescription ?? shopName ?? string.Empty);
            newFolder.SetShopDomain(shopDomain);
            newFolder.SetShopName(shopName);
            if (!string.IsNullOrEmpty(identifier) && identifier.All(char.IsDigit)) newFolder.SetItemId(identifier);

            if (parentFolderId == default || parentFolderId == Ulid.Empty) {
                libraries.AddFolder(newFolder);
                UnityEngine.Debug.Log(
                    $"Created BoothItemFolder '{newFolder.Name}' (Id: {newFolder.ID}) at root for shop {shopDomain}");
            }
            else {
                var parentBase = libraries.GetFolder(parentFolderId);
                if (parentBase is BoothItemFolder) {
                    UnityEngine.Debug.LogError("Cannot create a BoothItemFolder under another BoothItemFolder.");
                    return Ulid.Empty;
                }

                if (parentBase is not Folder parentFolder) {
                    UnityEngine.Debug.LogError($"Parent folder {parentFolderId} not found.");
                    return Ulid.Empty;
                }

                parentFolder.AddChild(newFolder);
                UnityEngine.Debug.Log(
                    $"Created BoothItemFolder '{newFolder.Name}' (Id: {newFolder.ID}) under parent {parentFolder.ID}");
            }

            repository.SaveLibraryMetadata(libraries);
            return newFolder.ID;
        }

        // Ensure folder on a cloned LibraryMetadata instance (no repository side-effects).
        private static Ulid EnsureBoothItemFolderForMeta(LibraryMetadata libraries, string shopDomain, string shopName,
            string identifier,
            string folderName, string folderDescription = null, Ulid parentFolderId = default) {
            if (libraries == null) return Ulid.Empty;

            // Search existing in cloned metadata
            foreach (var root in libraries.FolderList) {
                var found = FindBoothItemFolderRecursive(root, shopDomain ?? string.Empty, identifier);
                if (found != null) {
                    // Update shop name/description on found BoothItemFolder within the cloned metadata
                    if (found is BoothItemFolder bf) {
                        if (!string.IsNullOrEmpty(shopName) && bf.ShopName != shopName) bf.SetShopName(shopName);
                        if (!string.IsNullOrEmpty(folderDescription) && bf.Description != folderDescription) bf.SetDescription(folderDescription);
                    }

                    return found.ID;
                }
            }

            // Create new BoothItemFolder in the clone
            var newFolder = new BoothItemFolder();
            newFolder.SetName(folderName ?? identifier ?? "Booth Item");
            newFolder.SetDescription(folderDescription ?? shopName ?? string.Empty);
            newFolder.SetShopDomain(shopDomain);
            newFolder.SetShopName(shopName);
            if (!string.IsNullOrEmpty(identifier) && identifier.All(char.IsDigit)) newFolder.SetItemId(identifier);

            if (parentFolderId == default || parentFolderId == Ulid.Empty) {
                libraries.AddFolder(newFolder);
            }
            else {
                var parentBase = libraries.GetFolder(parentFolderId);
                if (parentBase is BoothItemFolder) {
                    return Ulid.Empty;
                }

                if (parentBase is not Folder parentFolder) {
                    return Ulid.Empty;
                }

                parentFolder.AddChild(newFolder);
            }

            return newFolder.ID;
        }

        private static BoothItemFolder FindBoothItemFolderRecursive(BaseFolder root, string shopDomain, string identifier) {
            if (root == null) return null;
            if (root is BoothItemFolder bf) {
                if (!string.IsNullOrEmpty(shopDomain) && !string.IsNullOrEmpty(bf.ShopDomain) &&
                    bf.ShopDomain != shopDomain) {
                    // skip
                }
                else {
                    if (!string.IsNullOrEmpty(identifier))
                        if ((!string.IsNullOrEmpty(bf.ItemId) && bf.ItemId == identifier) || bf.Name == identifier)
                            return bf;
                }
            }

            if (root is Folder f && f.Children != null)
                foreach (var c in f.Children) {
                    var found = FindBoothItemFolderRecursive(c, shopDomain, identifier);
                    if (found != null) return found;
                }

            return null;
        }
    }
}
