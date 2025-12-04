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
        private bool _isAnalyzing;

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

            if (_colorDefinitions.Count > 0) {
                EditorGUILayout.LabelField("Colors", EditorStyles.boldLabel);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                    foreach (var def in _colorDefinitions) {
                        DrawColorItem(def);
                    }
                }
                EditorGUILayout.Space(10);
            }

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

        private static void DrawColorItem(ColorDefinition colorDef) {
            using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
                using (new EditorGUILayout.HorizontalScope()) {
                    var rect = EditorGUILayout.GetControlRect(GUILayout.Width(40), GUILayout.Height(40));
                    EditorGUI.DrawRect(rect, colorDef.Color);
                    
                    using (new EditorGUILayout.VerticalScope()) {
                        using (new EditorGUILayout.HorizontalScope()) {
                            EditorGUILayout.SelectableLabel(colorDef.Name, EditorStyles.boldLabel, GUILayout.Height(18));
                            GUILayout.FlexibleSpace();
                            var hex = ColorUtility.ToHtmlStringRGBA(colorDef.Color);
                            EditorGUILayout.LabelField($"#{hex}", GUILayout.Width(80));
                        }

                        var usageCount = colorDef.Usages.Count;
                        var label = $"Usages: {usageCount}";
                        
                        var prevColor = GUI.contentColor;
                        if (usageCount == 0) GUI.contentColor = new Color(1f, 0.6f, 0.6f);
                        
                        colorDef.IsExpanded = EditorGUILayout.Foldout(colorDef.IsExpanded, label, true);
                        GUI.contentColor = prevColor;
                    }
                }

                if (!colorDef.IsExpanded) return;
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

        private void Analyze() {
            _isAnalyzing = true;
            _colorDefinitions.Clear();
            _styleColorDefinitions.Clear();

            try {
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
                    }
                }

                var allDefs = new List<ColorDefinition>(_colorDefinitions);
                _colorDefinitions = allDefs.Where(d => fields.First(f => f.Name == d.Name).FieldType == typeof(Color)).ToList();
                _styleColorDefinitions = allDefs.Where(d => fields.First(f => f.Name == d.Name).FieldType == typeof(StyleColor)).ToList();

                var scriptFiles = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories);
                
                var excludeFiles = new[] { "ColorPaletteWindow.cs" };

                var totalFiles = scriptFiles.Length;
                for (var i = 0; i < totalFiles; i++) {
                    var file = scriptFiles[i];
                    if (excludeFiles.Any(ex => file.EndsWith(ex))) continue;

                    var fileName = Path.GetFileName(file);
                    var isColorPresetFile = fileName == "ColorPreset.cs";

                    EditorUtility.DisplayProgressBar("Analyzing Colors", $"Scanning {fileName}...", (float)i / totalFiles);

                    var lines = File.ReadAllLines(file);
                    for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++) {
                        var line = lines[lineIndex];
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        foreach (var def in allDefs) {
                            var isMatch = false;

                            if (isColorPresetFile) {
                                if (line.Contains(def.Name) && !line.Contains($"public static") && !line.Contains($"{def.Name} =")) {
                                    isMatch = true;
                                }
                            }
                            else {
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
                return "Assets" + fullPath[Application.dataPath.Length..];
            return fullPath;
        }

        private class ColorDefinition {
            public string Name;
            public Color Color;
            public readonly List<UsageInfo> Usages = new();
            public bool IsExpanded;
        }

        private class UsageInfo {
            public string FilePath;
            public int LineNumber;
            public string LineContent;
        }
    }
}