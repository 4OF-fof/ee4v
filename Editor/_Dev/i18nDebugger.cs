using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace _4OF.ee4v._Dev {
    public class I18NDebugger : EditorWindow {
        private readonly List<string> _codeKeys = new();

        private readonly Dictionary<string, string> _jsonFilePaths = new();
        private readonly Dictionary<string, List<string>> _jsonKeys = new();

        private readonly Dictionary<string, List<string>> _missingKeys = new();
        private readonly Dictionary<string, List<string>> _unusedKeys = new();

        private bool _foldoutMissing = true;
        private bool _foldoutUnused = true;
        private Vector2 _scrollPosition;

        private void OnGUI() {
            GUILayout.Label("I18n Analysis Tool", EditorStyles.boldLabel);

            if (GUILayout.Button("Analyze Project", GUILayout.Height(30))) Analyze();

            EditorGUILayout.Space();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            if (_codeKeys.Count > 0) {
                GUILayout.Label($"Code References Found: {_codeKeys.Count}", EditorStyles.miniLabel);

                _foldoutMissing =
                    EditorGUILayout.Foldout(_foldoutMissing, "Missing Keys (In Code but NOT in JSON)", true);
                if (_foldoutMissing)
                    foreach (var lang in _missingKeys.Keys.Where(lang => _missingKeys[lang].Count != 0)) {
                        GUI.color = new Color(1f, 0.7f, 0.7f);
                        GUILayout.BeginVertical(EditorStyles.helpBox);
                        GUI.color = Color.white;

                        using (new EditorGUILayout.HorizontalScope()) {
                            GUILayout.Label($"[{lang}] Missing: {_missingKeys[lang].Count} items",
                                EditorStyles.boldLabel);

                            if (GUILayout.Button("Add All to JSON", GUILayout.Width(120))) AddMissingKeys(lang);
                        }

                        foreach (var key in _missingKeys[lang]) {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.SelectableLabel(key, GUILayout.Height(18));
                            if (GUILayout.Button("Copy", GUILayout.Width(50))) {
                                GUIUtility.systemCopyBuffer = key;
                                Debug.Log($"Copied: {key}");
                            }

                            EditorGUILayout.EndHorizontal();
                        }

                        GUILayout.EndVertical();
                        EditorGUILayout.Space();
                    }

                EditorGUILayout.Space();

                _foldoutUnused = EditorGUILayout.Foldout(_foldoutUnused, "Unused Keys (In JSON but NOT in Code)", true);
                if (_foldoutUnused)
                    foreach (var lang in _unusedKeys.Keys.Where(lang => _unusedKeys[lang].Count != 0)) {
                        GUI.color = new Color(0.7f, 0.8f, 1f);
                        GUILayout.BeginVertical(EditorStyles.helpBox);
                        GUI.color = Color.white;

                        using (new EditorGUILayout.HorizontalScope()) {
                            GUILayout.Label($"[{lang}] Unused: {_unusedKeys[lang].Count} items",
                                EditorStyles.boldLabel);

                            if (GUILayout.Button("Remove All from JSON", GUILayout.Width(150)))
                                if (EditorUtility.DisplayDialog("Confirm Delete",
                                        $"Are you sure you want to remove {_unusedKeys[lang].Count} keys from {lang}.json?\n\nMake sure these are not used dynamically (e.g. string concatenation).",
                                        "Yes, Remove", "Cancel"))
                                    RemoveUnusedKeys(lang);
                        }

                        foreach (var key in _unusedKeys[lang])
                            EditorGUILayout.SelectableLabel(key, GUILayout.Height(18));
                        GUILayout.EndVertical();
                        EditorGUILayout.Space();
                    }
            }
            else {
                GUILayout.Label("Click 'Analyze Project' to start.");
            }

            EditorGUILayout.EndScrollView();
        }

        [MenuItem("Debug/I18n Debugger")]
        public static void ShowWindow() {
            GetWindow<I18NDebugger>("I18n Debugger");
        }

        private void Analyze() {
            _codeKeys.Clear();
            _jsonKeys.Clear();
            _jsonFilePaths.Clear();
            _missingKeys.Clear();
            _unusedKeys.Clear();

            ScanCodeFiles();
            ScanJsonFiles();
            Compare();
        }

        private void ScanCodeFiles() {
            var scripts = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);

            var regex = new Regex(@"I18N\.Get\s*\(\s*""([^""]+)""", RegexOptions.Compiled);

            foreach (var path in scripts) {
                var content = File.ReadAllText(path);
                var matches = regex.Matches(content);

                foreach (Match match in matches)
                    if (match.Success && match.Groups.Count > 1) {
                        var key = match.Groups[1].Value;
                        if (!_codeKeys.Contains(key)) _codeKeys.Add(key);
                    }
            }

            _codeKeys.Sort();
            Debug.Log($"Found {_codeKeys.Count} I18N keys in C# scripts.");
        }

        private void ScanJsonFiles() {
            var guids = AssetDatabase.FindAssets("t:TextAsset");

            foreach (var guid in guids) {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("/i18n/") || !path.EndsWith(".json")) continue;

                var fileName = Path.GetFileNameWithoutExtension(path);
                if (!Regex.IsMatch(fileName, @"^[a-z]{2}-[A-Z]{2}$")) continue;

                try {
                    var jsonContent = File.ReadAllText(path);
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent);

                    if (dict != null) {
                        _jsonKeys[fileName] = dict.Keys.ToList();
                        _jsonKeys[fileName].Sort();

                        _jsonFilePaths[fileName] = path;
                    }
                }
                catch (Exception e) {
                    Debug.LogWarning($"Failed to parse JSON at {path}: {e.Message}");
                }
            }
        }

        private void Compare() {
            foreach (var lang in _jsonKeys.Keys) {
                var jsonKeyList = _jsonKeys[lang];

                var missing = _codeKeys.Except(jsonKeyList).ToList();
                _missingKeys[lang] = missing;

                var unused = jsonKeyList.Except(_codeKeys).ToList();
                _unusedKeys[lang] = unused;
            }
        }

        private void AddMissingKeys(string lang) {
            if (!_jsonFilePaths.TryGetValue(lang, out var path)) return;
            if (!_missingKeys.TryGetValue(lang, out var missingList) || missingList.Count == 0) return;

            try {
                var jsonContent = File.ReadAllText(path);
                var dict = JsonConvert.DeserializeObject<SortedDictionary<string, string>>(jsonContent)
                    ?? new SortedDictionary<string, string>();

                var count = 0;
                foreach (var key in missingList.Where(key => !dict.ContainsKey(key))) {
                    dict.Add(key, key);
                    count++;
                }

                if (count <= 0) return;
                SaveJsonFile(path, dict);
                Debug.Log($"Added {count} keys to {lang}.json");
                Analyze();
            }
            catch (Exception e) {
                Debug.LogError($"Failed to add keys to {path}: {e.Message}");
            }
        }

        private void RemoveUnusedKeys(string lang) {
            if (!_jsonFilePaths.TryGetValue(lang, out var path)) return;
            if (!_unusedKeys.TryGetValue(lang, out var unusedList) || unusedList.Count == 0) return;

            try {
                var jsonContent = File.ReadAllText(path);
                var dict = JsonConvert.DeserializeObject<SortedDictionary<string, string>>(jsonContent);

                if (dict == null) return;

                var count = unusedList.Count(key => dict.Remove(key));

                if (count <= 0) return;
                SaveJsonFile(path, dict);
                Debug.Log($"Removed {count} keys from {lang}.json");
                Analyze();
            }
            catch (Exception e) {
                Debug.LogError($"Failed to remove keys from {path}: {e.Message}");
            }
        }

        private static void SaveJsonFile(string path, object data) {
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(path, json);
            AssetDatabase.Refresh();
        }
    }
}