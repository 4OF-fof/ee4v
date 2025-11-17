using System;
using System.Collections.Generic;
using UnityEditor;
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
    }
}
