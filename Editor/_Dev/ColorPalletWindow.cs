using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using _4OF.ee4v.Core.UI;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v._Dev {
    public class ColorPaletteWindow : EditorWindow {
        private Vector2 _scrollPosition;
        private List<ColorDefinition> _colorDefinitions = new();
        private List<ColorDefinition> _styleColorDefinitions = new();
        private bool _isAnalyzing = false;

        [MenuItem("Debug/Color Palette Window", false, 201)]
        public static void ShowWindow() {
            var window = GetWindow<ColorPaletteWindow>("Color Palette");
            window.minSize = new Vector2(400, 600);
            window.Show();
        }

        private void OnEnable() {
            Analyze();
        }

        private void OnGUI() {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Refresh / Analyze", EditorStyles.toolbarButton, GUILayout.Width(120))) {
                    Analyze();
                }
            }

            if (_isAnalyzing) {
                EditorGUILayout.HelpBox("Analyzing project files...", MessageType.Info);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // 1. Colors Section
            if (_colorDefinitions.Count > 0) {
                EditorGUILayout.LabelField("Colors", EditorStyles.boldLabel);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                    foreach (var def in _colorDefinitions) {
                        DrawColorItem(def);
                    }
                }
                EditorGUILayout.Space(10);
            }

            // 2. StyleColors Section
            if (_styleColorDefinitions.Count > 0) {
                EditorGUILayout.LabelField("StyleColors", EditorStyles.boldLabel);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                    foreach (var def in _styleColorDefinitions) {
                        DrawColorItem(def);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawColorItem(ColorDefinition colorDef) {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
                using (new EditorGUILayout.HorizontalScope()) {
                    // Color Preview
                    var rect = EditorGUILayout.GetControlRect(GUILayout.Width(40), GUILayout.Height(40));
                    EditorGUI.DrawRect(rect, colorDef.Color);
                    
                    // Info
                    using (new EditorGUILayout.VerticalScope()) {
                        using (new EditorGUILayout.HorizontalScope()) {
                            EditorGUILayout.SelectableLabel(colorDef.Name, EditorStyles.boldLabel, GUILayout.Height(18));
                            GUILayout.FlexibleSpace();
                            // Hex Value
                            var hex = ColorUtility.ToHtmlStringRGBA(colorDef.Color);
                            EditorGUILayout.LabelField($"#{hex}", GUILayout.Width(80));
                        }

                        // Usage Foldout
                        var usageCount = colorDef.Usages.Count;
                        var label = $"Usages: {usageCount}";
                        
                        // Usage count coloring (warn if 0)
                        var prevColor = GUI.contentColor;
                        if (usageCount == 0) GUI.contentColor = new Color(1f, 0.6f, 0.6f);
                        
                        colorDef.IsExpanded = EditorGUILayout.Foldout(colorDef.IsExpanded, label, true);
                        GUI.contentColor = prevColor;
                    }
                }

                if (colorDef.IsExpanded) {
                    if (colorDef.Usages.Count == 0) {
                        EditorGUILayout.LabelField("  No usages found.", EditorStyles.miniLabel);
                    }
                    else {
                        foreach (var usage in colorDef.Usages) {
                            using (new EditorGUILayout.HorizontalScope()) {
                                if (GUILayout.Button("Jump", GUILayout.Width(45))) {
                                    OpenScriptAtLine(usage.FilePath, usage.LineNumber);
                                }
                                
                                var fileName = Path.GetFileName(usage.FilePath);
                                // Show "Internal" for ColorPreset.cs usages
                                if (fileName == "ColorPreset.cs") {
                                    var style = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.gray } };
                                    EditorGUILayout.LabelField("[Internal]", style, GUILayout.Width(60));
                                }
                                else {
                                    EditorGUILayout.LabelField(fileName, GUILayout.Width(140));
                                }

                                EditorGUILayout.LabelField(usage.LineContent.Trim(), EditorStyles.miniLabel);
                            }
                        }
                    }
                    EditorGUILayout.Space(4);
                }
            }
        }

        private void Analyze() {
            _isAnalyzing = true;
            _colorDefinitions.Clear();
            _styleColorDefinitions.Clear();

            try {
                // 1. Reflection: Get definitions
                var type = typeof(ColorPreset);
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);

                foreach (var field in fields) {
                    if (field.FieldType == typeof(Color)) {
                        var val = (Color)field.GetValue(null);
                        _colorDefinitions.Add(new ColorDefinition { Name = field.Name, Color = val });
                    }
                    else if (field.FieldType == typeof(StyleColor)) {
                        var val = (StyleColor)field.GetValue(null);
                        _colorDefinitions.Add(new ColorDefinition { Name = field.Name, Color = val.value });
                        // Move to style list later or sort
                    }
                }

                // Split lists
                var allDefs = new List<ColorDefinition>(_colorDefinitions);
                _colorDefinitions = allDefs.Where(d => fields.First(f => f.Name == d.Name).FieldType == typeof(Color)).ToList();
                _styleColorDefinitions = allDefs.Where(d => fields.First(f => f.Name == d.Name).FieldType == typeof(StyleColor)).ToList();

                // 2. Scan project files
                var scriptFiles = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories);
                
                // Exclude this debugger window itself
                var excludeFiles = new[] { "ColorPaletteWindow.cs" };

                var totalFiles = scriptFiles.Length;
                for (int i = 0; i < totalFiles; i++) {
                    var file = scriptFiles[i];
                    if (excludeFiles.Any(ex => file.EndsWith(ex))) continue;

                    var fileName = Path.GetFileName(file);
                    var isColorPresetFile = fileName == "ColorPreset.cs";

                    EditorUtility.DisplayProgressBar("Analyzing Colors", $"Scanning {fileName}...", (float)i / totalFiles);

                    var lines = File.ReadAllLines(file);
                    for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++) {
                        var line = lines[lineIndex];
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        foreach (var def in allDefs) {
                            bool isMatch = false;

                            if (isColorPresetFile) {
                                // Inside ColorPreset.cs: 
                                // Find usage like "new(ActiveBackground)" but ignore definition "public static Color ActiveBackground ="
                                // Simple heuristic: contains name but doesn't contain "public static"
                                if (line.Contains(def.Name) && !line.Contains($"public static") && !line.Contains($"{def.Name} =")) {
                                    isMatch = true;
                                }
                            }
                            else {
                                // External files: Look for "ColorPreset.Name"
                                if (line.Contains($"ColorPreset.{def.Name}")) {
                                    isMatch = true;
                                }
                            }

                            if (isMatch) {
                                def.Usages.Add(new UsageInfo {
                                    FilePath = file,
                                    LineNumber = lineIndex + 1,
                                    LineContent = line
                                });
                            }
                        }
                    }
                }
            }
            finally {
                EditorUtility.ClearProgressBar();
                _isAnalyzing = false;
                Repaint();
            }
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
                return "Assets" + fullPath.Substring(Application.dataPath.Length);
            return fullPath;
        }

        private class ColorDefinition {
            public string Name;
            public Color Color;
            public List<UsageInfo> Usages = new();
            public bool IsExpanded = false;
        }

        private class UsageInfo {
            public string FilePath;
            public int LineNumber;
            public string LineContent;
        }
    }
}