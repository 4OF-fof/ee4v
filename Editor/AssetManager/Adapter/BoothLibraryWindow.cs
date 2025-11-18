using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using _4OF.ee4v.AssetManager.Data;
using UnityEditor;
using _4OF.ee4v.AssetManager.Service;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.Adapter {
    public class BoothLibraryWindow : EditorWindow {
        private const int Port = 58080;
        private List<ShopDto> _shops = new();
        private Vector2 _scroll;
        private readonly Dictionary<int, bool> _shopFoldouts = new();
        private readonly Dictionary<string, bool> _itemFoldouts = new();

        [MenuItem("Tools/BoothLibrary Server/Open Window")]
        public static void ShowWindow() {
            var w = GetWindow<BoothLibraryWindow>("BoothLibrary Server");
            w.Show();
        }

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
            EditorGUILayout.LabelField("URL", running ? $"http://localhost:{Port}/health" : "-", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();
            if (!running) {
                if (GUILayout.Button("Start Server")) {
                    OnEnable();
                }
            }
            else {
                if (GUILayout.Button("Stop Server")) {
                    OnDisable();
                }
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Received Contents", EditorStyles.boldLabel);
            if (GUILayout.Button("Clear")) {
                BoothLibraryServerState.Clear();
                _shops.Clear();
            }
            if (GUILayout.Button("Populate Sample")) {
                var sample = new List<ShopDto>() {
                    new() { shopURL = "https://example.com", shopName = "Example Shop", items = new List<ItemDto>() {
                        new() { itemURL = "https://example.com/items/1", name = "Sample Item", description = "This is a sample item.", imageURL = "https://example.com/img.png", files = new List<FileDto>() {
                            new() { url = "https://example.com/files/file1.zip", filename = "file1.zip" }
                        } }
                    } }
                };
                BoothLibraryServerState.SetContents(sample);
            }
            if (GUILayout.Button("Refresh")) {
                _shops = new List<ShopDto>(BoothLibraryServerState.LastContents ?? new List<ShopDto>());
            }

            if (_shops.Count > 0) {
                if (GUILayout.Button("Import into Asset Manager")) {
                    var created = ImportBoothShops(_shops);
                    AssetLibraryService.RefreshAssetLibrary();
                    EditorUtility.DisplayDialog("Import Complete",
                        created > 0 ? $"Imported {created} items into Asset Manager" : "No items were imported.", "OK");
                }
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(300));
            for (var si = 0; si < _shops.Count; si++) {
                var s = _shops[si];
                var sf = _shopFoldouts.GetValueOrDefault(si, false);
                sf = EditorGUILayout.Foldout(sf, $"{(s.shopName ?? "(unnamed)")} ({(s.shopURL ?? "-")})");
                _shopFoldouts[si] = sf;
                if (!sf) continue;
                if (s.items != null) {
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
                            EditorGUILayout.SelectableLabel(it.name, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                        }
                        if (!string.IsNullOrEmpty(it.description)) {
                            EditorGUILayout.LabelField("Description");
                            EditorGUILayout.SelectableLabel(it.description, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                        }
                        if (!string.IsNullOrEmpty(it.imageURL)) {
                            EditorGUILayout.LabelField("Image");
                            EditorGUILayout.SelectableLabel(it.imageURL, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                        }
                        if (it.files != null) {
                            EditorGUILayout.LabelField("Files", EditorStyles.boldLabel);
                            foreach (var f in it.files) {
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField(f.filename ?? "(file)", GUILayout.Width(300));
                                EditorGUILayout.SelectableLabel(f.url ?? "", GUILayout.Height(EditorGUIUtility.singleLineHeight));
                                EditorGUILayout.EndHorizontal();
                            }
                        }
                        EditorGUI.indentLevel--;
                    }
                }
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndScrollView();
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
                    foreach (var f in item.files) {
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
                            if (!string.IsNullOrEmpty(downloadId) && AssetLibrary.Instance.Assets.Any(a => a.BoothData?.DownloadID == downloadId)) continue;
                            var filename = f.filename ?? string.Empty;
                            if (!string.IsNullOrEmpty(itemId) && !string.IsNullOrEmpty(filename) &&
                                AssetLibrary.Instance.Assets.Any(a => a.BoothData?.ItemID == itemId && a.BoothData?.FileName == filename)) continue;

                            var asset = AssetLibrarySerializer.CreateAssetWithoutFile();
                            // Name fallback priorities: filename -> item name -> item URL
                            var name = !string.IsNullOrEmpty(f.filename) ? f.filename : (!string.IsNullOrEmpty(item.name) ? item.name : (!string.IsNullOrEmpty(item.itemURL) ? item.itemURL : "Unnamed Booth Item"));
                            asset.SetName(name);
                            if (!string.IsNullOrEmpty(item.description)) asset.SetDescription(item.description);
                            if (!string.IsNullOrEmpty(shop.shopName)) asset.AddTag(shop.shopName);
                            asset.AddTag("booth");

                            var booth = new BoothMetadata();
                            // shop domain from shop.shopURL or item.itemURL
                            var shopUrl = shop.shopURL ?? item.itemURL;
                            if (!string.IsNullOrEmpty(shopUrl)) {
                                var regexShop = new Regex(@"https?://([^\.]+)\.booth\.pm", RegexOptions.IgnoreCase);
                                var matchShop = regexShop.Match(shopUrl);
                                if (matchShop.Success) {
                                    booth.SetShopDomain(matchShop.Groups[1].Value);
                                    booth.SetShopName(shop.shopName ?? matchShop.Groups[1].Value);
                                }
                            }
                            if (!string.IsNullOrEmpty(itemId)) booth.SetItemID(itemId);
                            if (!string.IsNullOrEmpty(downloadId)) booth.SetDownloadID(downloadId);
                            booth.SetFileName(filename);

                            asset.SetBoothData(booth);
                            AssetLibraryService.UpdateAsset(asset);
                            created++;
                        }
                        catch (Exception e) {
                            Debug.LogError($"Failed import booth downloadable: {e.Message}");
                        }
                    }
                }
            }
            Debug.Log($"Imported {created} Booth items into AssetLibrary");
            return created;
        }
    }
}
