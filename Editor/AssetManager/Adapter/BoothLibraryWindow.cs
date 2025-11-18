using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Service;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.Adapter {
    public class BoothLibraryWindow : EditorWindow {
        private const int Port = 58080;
        private readonly Dictionary<string, bool> _itemFoldouts = new();
        private readonly Dictionary<int, bool> _shopFoldouts = new();
        private Vector2 _scroll;
        private List<ShopDto> _shops = new();

        private void OnEnable() {
            try {
                if (HttpServer.IsRunning) return;
                HttpServer.Start(Port);
                Debug.Log($"BoothLibrary server started on http://localhost:{Port}");
            }
            catch (Exception ex) {
                Debug.LogError("BoothLibrary server failed to start: " + ex);
            }

            EditorApplication.update += PollPendingContents;
        }

        private void OnDisable() {
            try {
                if (!HttpServer.IsRunning) return;
                HttpServer.Stop();
                Debug.Log("BoothLibrary server stopped");
            }
            catch (Exception ex) {
                Debug.LogError("BoothLibrary server failed to stop: " + ex);
            }

            EditorApplication.update -= PollPendingContents;
        }

        private void OnGUI() {
            EditorGUILayout.LabelField("BoothLibrary Local Server", EditorStyles.boldLabel);
            var running = HttpServer.IsRunning;
            EditorGUILayout.LabelField("Status", running ? "Running" : "Stopped");
            EditorGUILayout.LabelField("URL", running ? $"http://localhost:{Port}/health" : "-",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();
            if (!running) {
                if (GUILayout.Button("Start Server")) OnEnable();
            }
            else {
                if (GUILayout.Button("Stop Server")) OnDisable();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Received Contents", EditorStyles.boldLabel);
            if (GUILayout.Button("Clear")) {
                BoothLibraryServerState.Clear();
                _shops.Clear();
            }

            if (GUILayout.Button("Populate Sample")) {
                var sample = new List<ShopDto> {
                    new() {
                        shopURL = "https://example.com", shopName = "Example Shop", items = new List<ItemDto> {
                            new() {
                                itemURL = "https://example.com/items/1", name = "Sample Item",
                                description = "This is a sample item.", imageURL = "https://example.com/img.png",
                                files = new List<FileDto> {
                                    new() { url = "https://example.com/files/file1.zip", filename = "file1.zip" }
                                }
                            }
                        }
                    }
                };
                BoothLibraryServerState.SetContents(sample);
            }

            if (GUILayout.Button("Refresh"))
                _shops = new List<ShopDto>(BoothLibraryServerState.LastContents ?? new List<ShopDto>());

            if (_shops.Count > 0)
                if (GUILayout.Button("Import into Asset Manager")) {
                    var created = ImportBoothShops(_shops);
                    AssetLibraryService.RefreshAssetLibrary();
                    EditorUtility.DisplayDialog("Import Complete",
                        created > 0 ? $"Imported {created} items into Asset Manager" : "No items were imported.", "OK");
                }

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(300));
            for (var si = 0; si < _shops.Count; si++) {
                var s = _shops[si];
                var sf = _shopFoldouts.GetValueOrDefault(si, false);
                sf = EditorGUILayout.Foldout(sf, $"{s.shopName ?? "(unnamed)"} ({s.shopURL ?? "-"})");
                _shopFoldouts[si] = sf;
                if (!sf) continue;
                if (s.items != null)
                    for (var ii = 0; ii < s.items.Count; ii++) {
                        var it = s.items[ii];
                        var itemKey = it.itemURL ?? it.name ?? $"item_{ii}";
                        var itf = _itemFoldouts.GetValueOrDefault(itemKey, false);
                        itf = EditorGUILayout.Foldout(itf, it.name ?? it.itemURL ?? "Item");
                        _itemFoldouts[itemKey] = itf;
                        if (!itf) continue;
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField("Item URL", it.itemURL ?? "");
                        if (!string.IsNullOrEmpty(it.name)) {
                            EditorGUILayout.LabelField("Name");
                            EditorGUILayout.SelectableLabel(it.name,
                                GUILayout.Height(EditorGUIUtility.singleLineHeight));
                        }

                        if (!string.IsNullOrEmpty(it.description)) {
                            EditorGUILayout.LabelField("Description");
                            EditorGUILayout.SelectableLabel(it.description,
                                GUILayout.Height(EditorGUIUtility.singleLineHeight));
                        }

                        if (!string.IsNullOrEmpty(it.imageURL)) {
                            EditorGUILayout.LabelField("Image");
                            EditorGUILayout.SelectableLabel(it.imageURL,
                                GUILayout.Height(EditorGUIUtility.singleLineHeight));
                        }

                        if (it.files != null) {
                            EditorGUILayout.LabelField("Files", EditorStyles.boldLabel);
                            foreach (var f in it.files) {
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField(f.filename ?? "(file)", GUILayout.Width(300));
                                EditorGUILayout.SelectableLabel(f.url ?? "",
                                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
                                EditorGUILayout.EndHorizontal();
                            }
                        }

                        EditorGUI.indentLevel--;
                    }

                EditorGUILayout.Space();
            }

            EditorGUILayout.EndScrollView();
        }

        [MenuItem("Tools/BoothLibrary Server/Open Window")]
        public static void ShowWindow() {
            var w = GetWindow<BoothLibraryWindow>("BoothLibrary Server");
            w.Show();
        }

        private void PollPendingContents() {
            if (!BoothLibraryServerState.TryTakePending(out var shops)) return;
            _shops = shops ?? new List<ShopDto>();
            Repaint();
        }

        private static int ImportBoothShops(List<ShopDto> shops) {
            if (shops == null) return 0;
            var created = 0;
            foreach (var shop in shops) {
                if (shop.items == null) continue;
                foreach (var item in shop.items) {
                    if (item.files == null || item.files.Count == 0) continue; // Only import actual downloadable files
                    foreach (var f in item.files)
                        try {
                            // Extract download ID if present
                            string downloadId = null;
                            if (!string.IsNullOrEmpty(f.url)) {
                                var regexDownload = new Regex(@"downloadables/(\d+)", RegexOptions.IgnoreCase);
                                var matchDownload = regexDownload.Match(f.url);
                                if (matchDownload.Success) downloadId = matchDownload.Groups[1].Value;
                            }

                            // Extract item id if present
                            string itemId = null;
                            if (!string.IsNullOrEmpty(item.itemURL)) {
                                var regexItem = new Regex(@"items/(\d+)");
                                var matchItem = regexItem.Match(item.itemURL);
                                if (matchItem.Success) itemId = matchItem.Groups[1].Value;
                            }

                            // Duplicate check by downloadId primarily, then fallback to itemId+filename
                            if (!string.IsNullOrEmpty(downloadId) &&
                                AssetLibrary.Instance.Assets.Any(a => a.BoothData?.DownloadID == downloadId)) continue;
                            var filename = string.IsNullOrEmpty(f.filename) ? null : f.filename;
                            if (!string.IsNullOrEmpty(itemId) && !string.IsNullOrEmpty(filename) &&
                                AssetLibrary.Instance.Assets.Any(a =>
                                    a.BoothData?.ItemID == itemId && a.BoothData?.FileName == filename)) continue;

                            var asset = AssetLibrarySerializer.CreateAssetWithoutFile();
                            // Name fallback priorities: filename -> item name -> item URL
                            var name = !string.IsNullOrEmpty(f.filename) ? f.filename :
                                !string.IsNullOrEmpty(item.name) ? item.name :
                                !string.IsNullOrEmpty(item.itemURL) ? item.itemURL : "Unnamed Booth Item";
                            asset.SetName(name);
                            // Note: store description and shop name on the BoothItemFolder instead of the asset

                            var booth = new BoothMetadata();
                            // shop domain from shop.shopURL or item.itemURL
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
                            // Ensure folder exists for this booth item and place the asset inside
                            var folderIdentifier = !string.IsNullOrEmpty(itemId) ? itemId :
                                !string.IsNullOrEmpty(downloadId) ? downloadId :
                                !string.IsNullOrEmpty(filename) ? filename : item.itemURL;
                            // Ensure the folder exists and store the shopName and description on the folder
                            var folderId = EnsureBoothItemFolder(booth.ShopDomain, shop.shopName, folderIdentifier,
                                item.name ?? item.itemURL ?? folderIdentifier, item.description);
                            if (folderId != Ulid.Empty) asset.SetFolder(folderId);
                            AssetLibraryService.UpdateAsset(asset);
                            created++;
                        }
                        catch (Exception e) {
                            Debug.LogError($"Failed import booth downloadable: {e.Message}");
                        }
                }
            }

            Debug.Log($"Imported {created} Booth items into AssetLibrary");
            return created;
        }

        private static BoothItemFolder FindBoothItemFolderRecursive(BaseFolder root, string shopDomain,
            string identifier) {
            if (root == null) return null;
            if (root is BoothItemFolder bf) {
                // If shop domain is provided, require matching shops
                if (!string.IsNullOrEmpty(shopDomain) && !string.IsNullOrEmpty(bf.ShopDomain) &&
                    bf.ShopDomain != shopDomain) {
                    // skip
                }
                else {
                    // match by numeric item id, or by folder/name equality
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

        public static Ulid EnsureBoothItemFolder(string shopDomain, string shopName, string identifier,
            string folderName, string folderDescription = null, Ulid parentFolderId = default) {
            // identifier may be itemId or downloadId or filename, or null.
            var libraries = AssetLibrary.Instance.Libraries;
            if (libraries == null) {
                // Attempt to load library metadata automatically
                AssetLibrarySerializer.LoadLibrary();
                libraries = AssetLibrary.Instance.Libraries;
                if (libraries == null) {
                    Debug.LogError("Library metadata is not loaded.");
                    return Ulid.Empty;
                }
            }

            // Search existing
            foreach (var root in libraries.FolderList) {
                var found = FindBoothItemFolderRecursive(root, shopDomain ?? string.Empty, identifier);
                if (found != null) {
                    // If it's a BoothItemFolder, update shop name/description if provided and changed
                    if (found is BoothItemFolder bf) {
                        var needsUpdate = false;
                        var updated = new BoothItemFolder(bf);
                        if (!string.IsNullOrEmpty(shopName) && updated.ShopName != shopName) {
                            updated.SetShopName(shopName);
                            needsUpdate = true;
                        }

                        if (!string.IsNullOrEmpty(folderDescription) && updated.Description != folderDescription) {
                            updated.SetDescription(folderDescription);
                            needsUpdate = true;
                        }

                        if (needsUpdate) AssetLibraryService.UpdateBoothItemFolder(updated);
                    }

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

            AssetLibrarySerializer.SaveLibrary();
            return newFolder.ID;
        }
    }
}