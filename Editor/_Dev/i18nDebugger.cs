using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace _4OF.ee4v._Dev {
    public class I18NDebugger : EditorWindow {
        private const string TargetFolderName = "4of/ee4v";
        private readonly List<string> _codeKeys = new();

        private readonly List<HardcodedStringInfo> _hardcodedStrings = new();
        private readonly Dictionary<string, string> _jsonFilePaths = new();
        private readonly Dictionary<string, List<string>> _jsonKeys = new();
        private readonly Dictionary<string, List<string>> _missingKeys = new();
        private readonly Dictionary<string, List<string>> _unusedKeys = new();
        private bool _foldoutHardcoded = true;

        private bool _foldoutMissing = true;
        private bool _foldoutUnused = true;
        private Vector2 _scrollPosition;

        private void OnGUI() {
            GUILayout.Label("I18n Analysis Tool", EditorStyles.boldLabel);

            GUILayout.Label($"Target Directory: Assets/{TargetFolderName}", EditorStyles.miniLabel);

            if (GUILayout.Button("Analyze Project", GUILayout.Height(30))) Analyze();

            EditorGUILayout.Space();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            if (_codeKeys.Count > 0 || _hardcodedStrings.Count > 0) {
                if (_hardcodedStrings.Count > 0) {
                    GUI.color = new Color(1f, 0.9f, 0.6f);
                    _foldoutHardcoded = EditorGUILayout.Foldout(_foldoutHardcoded,
                        $"Potential Hardcoded Strings: {_hardcodedStrings.Count} items", true);
                    GUI.color = Color.white;

                    if (_foldoutHardcoded) {
                        GUILayout.BeginVertical(EditorStyles.helpBox);
                        GUILayout.Label("Strings found in code NOT wrapped in I18N.Get()", EditorStyles.miniLabel);

                        foreach (var info in _hardcodedStrings)
                            using (new EditorGUILayout.HorizontalScope()) {
                                if (GUILayout.Button("Jump", GUILayout.Width(45)))
                                    OpenScriptAtLine(info.FilePath, info.LineNumber);

                                EditorGUILayout.LabelField($"[{info.FileName}:{info.LineNumber}]",
                                    GUILayout.Width(140));
                                EditorGUILayout.SelectableLabel($"\"{info.Text}\"", GUILayout.Height(18));
                            }

                        GUILayout.EndVertical();
                        EditorGUILayout.Space();
                    }
                }
                else {
                    if (_codeKeys.Count > 0) {
                        GUILayout.Label("No hardcoded strings found (checked filters applied).",
                            EditorStyles.miniLabel);
                        EditorGUILayout.Space();
                    }
                }

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
            _hardcodedStrings.Clear();

            ScanCodeFiles();
            ScanJsonFiles();
            Compare();
        }

        private void ScanCodeFiles() {
            var targetPath = Path.Combine(Application.dataPath, TargetFolderName);

            if (!Directory.Exists(targetPath)) {
                Debug.LogError($"Target directory not found: {targetPath}");
                return;
            }

            var scripts = Directory.GetFiles(targetPath, "*.cs", SearchOption.AllDirectories);

            var i18NRegex = new Regex(@"I18N\.Get\s*\(\s*""([^""]+)""", RegexOptions.Compiled);
            var stringLiteralRegex = new Regex(@"(I18N\.Get\s*\(\s*)|(Debug\.\w+\s*\(\s*)|""((?:[^""\\]|\\.)*)""",
                RegexOptions.Compiled);

            foreach (var path in scripts) {
                if (path.Contains("I18NDebugger.cs") || path.Contains("AllowedTypesBinder.cs")) continue;

                var lines = File.ReadAllLines(path);
                for (var i = 0; i < lines.Length; i++) {
                    var line = lines[i].Trim();

                    var i18NMatches = i18NRegex.Matches(line);
                    foreach (Match match in i18NMatches)
                        if (match.Success && match.Groups.Count > 1) {
                            var key = match.Groups[1].Value;
                            if (!_codeKeys.Contains(key)) _codeKeys.Add(key);
                        }

                    if (string.IsNullOrEmpty(line)) continue;
                    if (line.StartsWith("//")) continue;
                    if (line.StartsWith("using ")) continue;
                    if (line.StartsWith("[")) continue;
                    if (line.StartsWith("#")) continue;
                    if (line.StartsWith("const string")) continue;

                    var matches = stringLiteralRegex.Matches(line);
                    var skipNextString = false;

                    foreach (Match m in matches) {
                        if (m.Groups[1].Success || m.Groups[2].Success) {
                            skipNextString = true;
                            continue;
                        }

                        if (!m.Groups[3].Success) continue;
                        if (skipNextString) {
                            skipNextString = false;
                            continue;
                        }

                        var content = m.Groups[3].Value;
                        if (IsNoiseString(content, line)) continue;

                        _hardcodedStrings.Add(new HardcodedStringInfo {
                            FilePath = path,
                            LineNumber = i + 1,
                            Text = content
                        });
                    }
                }
            }

            _codeKeys.Sort();
            Debug.Log($"Found {_codeKeys.Count} I18N keys in C# scripts under {TargetFolderName}.");
            Debug.Log($"Found {_hardcodedStrings.Count} potential hardcoded strings under {TargetFolderName}.");
        }

        private static bool IsNoiseString(string text, string lineContext) {
            if (string.IsNullOrWhiteSpace(text)) return true;
            if (text.Length <= 1) return true;

            if (Regex.IsMatch(text, @"^[\d\W]+$")) return true;
            if (Regex.IsMatch(text, @"^\.[a-z0-9]+$", RegexOptions.IgnoreCase)) return true;
            if (text.Contains("/") || text.Contains("\\")) return true;

            if (lineContext.StartsWith("case \"")) return true;
            if (lineContext.Contains("GetComponent")) return true;
            if (lineContext.Contains("Find")) return true;
            if (lineContext.Contains("const string")) return true;

            if (Regex.IsMatch(text, @"^[a-fA-F0-9]{32}$")) return true;

            return false;
        }

        private void ScanJsonFiles() {
            var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { $"Assets/{TargetFolderName}" });

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

        private static void OpenScriptAtLine(string filePath, int lineNumber) {
            var scriptAsset = AssetDatabase.LoadAssetAtPath<MonoScript>(GetRelativePath(filePath));
            if (scriptAsset)
                AssetDatabase.OpenAsset(scriptAsset, lineNumber);
            else
                InternalEditorUtility.OpenFileAtLineExternal(filePath, lineNumber);
        }

        private static string GetRelativePath(string fullPath) {
            if (fullPath.StartsWith(Application.dataPath))
                return "Assets" + fullPath[Application.dataPath.Length..];
            return fullPath;
        }

        private class HardcodedStringInfo {
            public string FilePath;
            public int LineNumber;
            public string Text;
            public string FileName => Path.GetFileName(FilePath);
        }
    }
}