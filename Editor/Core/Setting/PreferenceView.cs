using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.Core.i18n;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace _4OF.ee4v.Core.Setting {
    public class PreferencesProvider : SettingsProvider {
        private const string SettingPath = "Preferences/4OF/ee4v";

        private static ReorderableList _hiddenItemReorderableList;
        private static ReorderableList _iconReorderableList;
        private static ReorderableList _ignoreComponentReorderableList;

        private static List<bool> _iconUseAssetFlags;

        private static bool _needRestart;
        private static Vector2 _scrollPosition;

        private PreferencesProvider(string path, SettingsScope scopes, IEnumerable<string> keywords) : base(path,
            scopes, keywords) {
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingProvider() {
            return new PreferencesProvider(SettingPath, SettingsScope.User, null);
        }

        public override void OnGUI(string searchContext) {
            if (_needRestart)
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                    EditorGUILayout.HelpBox(I18N.Get("UI.Core.NeedRestartToApplyChanges"), MessageType.Warning);
                    if (GUILayout.Button(I18N.Get("UI.Core.RestartUnity"), GUILayout.Width(120), GUILayout.Height(38)))
                        EditorApplication.OpenProject(Environment.CurrentDirectory);
                }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            var languages = I18N.GetAvailableLanguages();
            EditorGUI.BeginChangeCheck();
            Settings.I.language = languages[
                EditorGUILayout.Popup(
                    I18N.Get("UI.Core.LanguageLabel"),
                    Array.IndexOf(languages, Settings.I.language),
                    languages.ToArray()
                )
            ];
            if (EditorGUI.EndChangeCheck()) {
                Settings.I.Save();
                _needRestart = true;
            }

            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUI.BeginChangeCheck();
                var newPath = EditorGUILayout.TextField(
                    new GUIContent(I18N.Get("UI.Core.ContentFolderPathLabel"),
                        I18N.Get("UI.Core.ContentFolderPathTooltip")),
                    Settings.I.contentFolderPath);
                if (EditorGUI.EndChangeCheck()) {
                    Settings.I.contentFolderPath = newPath;
                    Settings.I.Save();
                }

                if (GUILayout.Button("...", GUILayout.Width(30))) {
                    var path = EditorUtility.OpenFolderPanel(
                        I18N.Get("UI.Core.ContentFolderPathWindow"),
                        Settings.I.contentFolderPath,
                        ""
                    );
                    if (!string.IsNullOrEmpty(path)) {
                        Settings.I.contentFolderPath = path;
                        Settings.I.Save();
                        GUIUtility.keyboardControl = 0;
                        EditorGUI.FocusTextInControl(null);
                    }
                }
            }

            #region HierarchyExtension

            EditorGUILayout.LabelField(I18N.Get("UI.Core.Preference.HierarchyExtension"), EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                EditorGUI.BeginChangeCheck();
                Settings.I.enableHierarchyExtension = EditorGUILayout.Toggle(
                    new GUIContent(I18N.Get("UI.Core.EnableHierarchyExtensionLabel"),
                        I18N.Get("UI.Core.EnableHierarchyExtensionTooltip")),
                    Settings.I.enableHierarchyExtension);
                if (EditorGUI.EndChangeCheck()) {
                    Settings.I.Save();
                    _needRestart = true;
                }

                using (new EditorGUI.DisabledScope(!Settings.I.enableHierarchyExtension)) {
                    EditorGUILayout.Space(3);
                    using (new EditorGUILayout.HorizontalScope()) {
                        GUILayout.Space(15);
                        using (new EditorGUILayout.VerticalScope()) {
                            EditorGUI.BeginChangeCheck();
                            Settings.I.enableSceneSwitcher = EditorGUILayout.ToggleLeft(
                                new GUIContent(I18N.Get("UI.Core.EnableSceneSwitcherLabel"),
                                    I18N.Get("UI.Core.EnableSceneSwitcherTooltip")),
                                Settings.I.enableSceneSwitcher);
                            if (EditorGUI.EndChangeCheck()) Settings.I.Save();

                            using (new EditorGUILayout.HorizontalScope()) {
                                EditorGUI.BeginChangeCheck();
                                var newScenePath = EditorGUILayout.TextField(
                                    new GUIContent(I18N.Get("UI.Core.SceneCreateFolderPathLabel"),
                                        I18N.Get("UI.Core.SceneCreateFolderPathTooltip")),
                                    Settings.I.sceneCreateFolderPath);
                                if (EditorGUI.EndChangeCheck()) {
                                    Settings.I.sceneCreateFolderPath = newScenePath;
                                    Settings.I.Save();
                                }

                                if (GUILayout.Button("...", GUILayout.Width(30))) {
                                    var path = EditorUtility.OpenFolderPanel(
                                        I18N.Get("UI.Core.SceneCreateFolderPathWindow"),
                                        Settings.I.sceneCreateFolderPath,
                                        ""
                                    );
                                    if (!string.IsNullOrEmpty(path)) {
                                        var dataPath = Application.dataPath.Replace('\\', '/');
                                        var normalized = path.Replace('\\', '/');

                                        if (normalized.StartsWith(dataPath)) {
                                            normalized = "Assets" + normalized.Substring(dataPath.Length);
                                            Settings.I.sceneCreateFolderPath = normalized;
                                            Settings.I.Save();
                                            GUIUtility.keyboardControl = 0;
                                            EditorGUI.FocusTextInControl(null);
                                        }
                                        else {
                                            EditorUtility.DisplayDialog(
                                                I18N.Get("UI.Core.InvalidPathTitle"),
                                                I18N.Get("UI.Core.SceneCreateFolderPathError"),
                                                I18N.Get("UI.Core.OK")
                                            );
                                        }
                                    }
                                }
                            }

                            EditorGUI.BeginChangeCheck();
                            Settings.I.enableCustomStyleItem = EditorGUILayout.ToggleLeft(
                                new GUIContent(I18N.Get("UI.Core.EnableCustomStyleItemLabel"),
                                    I18N.Get("UI.Core.EnableCustomStyleItemTooltip")),
                                Settings.I.enableCustomStyleItem);
                            if (EditorGUI.EndChangeCheck()) {
                                Settings.I.Save();
                                EditorApplication.RepaintHierarchyWindow();
                            }

                            EditorGUI.BeginChangeCheck();
                            Settings.I.showComponentIcons = EditorGUILayout.ToggleLeft(
                                new GUIContent(I18N.Get("UI.Core.ShowComponentIconsLabel"),
                                    I18N.Get("UI.Core.ShowComponentIconsTooltip")),
                                Settings.I.showComponentIcons);
                            if (EditorGUI.EndChangeCheck()) {
                                Settings.I.Save();
                                EditorApplication.RepaintHierarchyWindow();
                            }

                            EditorGUI.BeginChangeCheck();
                            Settings.I.showMenuIcon = EditorGUILayout.ToggleLeft(
                                new GUIContent(I18N.Get("UI.Core.ShowMenuIconLabel"),
                                    I18N.Get("UI.Core.ShowMenuIconTooltip")),
                                Settings.I.showMenuIcon);
                            if (EditorGUI.EndChangeCheck()) {
                                Settings.I.Save();
                                EditorApplication.RepaintHierarchyWindow();
                            }

                            EditorGUI.BeginChangeCheck();
                            using (new EditorGUILayout.HorizontalScope()) {
                                EditorGUILayout.PrefixLabel(new GUIContent(I18N.Get("UI.Core.HeadingPrefixLabel"),
                                    I18N.Get("UI.Core.HeadingPrefixTooltip")));
                                Settings.I.headingPrefix = EditorGUILayout.TextField(Settings.I.headingPrefix);
                            }

                            using (new EditorGUILayout.HorizontalScope()) {
                                EditorGUILayout.PrefixLabel(new GUIContent(I18N.Get("UI.Core.SeparatorPrefixLabel"),
                                    I18N.Get("UI.Core.SeparatorPrefixTooltip")));
                                Settings.I.separatorPrefix = EditorGUILayout.TextField(Settings.I.separatorPrefix);
                            }

                            if (EditorGUI.EndChangeCheck()) {
                                Settings.I.Save();
                                EditorApplication.RepaintHierarchyWindow();
                            }

                            EditorGUI.BeginChangeCheck();
                            Settings.I.showDepthLine = EditorGUILayout.ToggleLeft(
                                new GUIContent(I18N.Get("UI.Core.ShowDepthLineLabel"),
                                    I18N.Get("UI.Core.ShowDepthLineTooltip")),
                                Settings.I.showDepthLine);
                            if (EditorGUI.EndChangeCheck()) {
                                Settings.I.Save();
                                EditorApplication.RepaintHierarchyWindow();
                            }

                            EditorGUILayout.Space(5);

                            using (new EditorGUILayout.HorizontalScope()) {
                                EditorGUILayout.LabelField(new GUIContent(I18N.Get("UI.Core.HiddenItemListLabel"),
                                    I18N.Get("UI.Core.HiddenItemListTooltip")), EditorStyles.boldLabel);
                                GUILayout.FlexibleSpace();
                                if (GUILayout.Button(I18N.Get("UI.Core.ResetToDefault"), GUILayout.Width(120))) {
                                    Settings.I.ResetHiddenItemList();
                                    Settings.I.Save();
                                    _hiddenItemReorderableList = null;
                                    EditorApplication.RepaintHierarchyWindow();
                                }
                            }

                            DrawHiddenItemList();

                            EditorGUILayout.Space(5);

                            using (new EditorGUILayout.HorizontalScope()) {
                                EditorGUILayout.LabelField(new GUIContent(
                                    I18N.Get("UI.Core.IgnoreComponentNameListLabel"),
                                    I18N.Get("UI.Core.IgnoreComponentNameListTooltip")), EditorStyles.boldLabel);
                                GUILayout.FlexibleSpace();
                                if (GUILayout.Button(I18N.Get("UI.Core.ResetToDefault"), GUILayout.Width(120))) {
                                    Settings.I.ResetIgnoreComponentNameList();
                                    Settings.I.Save();
                                    _ignoreComponentReorderableList = null;
                                    EditorApplication.RepaintHierarchyWindow();
                                }
                            }

                            DrawIgnoreComponentNameList();

                            EditorGUILayout.Space(5);

                            using (new EditorGUILayout.HorizontalScope()) {
                                EditorGUILayout.LabelField(new GUIContent(I18N.Get("UI.Core.IconListLabel"),
                                    I18N.Get("UI.Core.IconListTooltip")), EditorStyles.boldLabel);
                                GUILayout.FlexibleSpace();
                                if (GUILayout.Button(I18N.Get("UI.Core.ResetToDefault"), GUILayout.Width(120))) {
                                    Settings.I.ResetIconList();
                                    Settings.I.Save();
                                    _iconReorderableList = null;
                                    _iconUseAssetFlags = null;
                                }
                            }

                            DrawIconList();
                        }
                    }
                }
            }

            EditorGUILayout.Space(10);

            #endregion

            #region ProjectExtension

            EditorGUILayout.LabelField(I18N.Get("UI.Core.Preference.ProjectExtension"), EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                EditorGUI.BeginChangeCheck();
                Settings.I.enableProjectExtension = EditorGUILayout.Toggle(
                    new GUIContent(I18N.Get("UI.Core.EnableProjectExtensionLabel"),
                        I18N.Get("UI.Core.EnableProjectExtensionTooltip")),
                    Settings.I.enableProjectExtension);
                if (EditorGUI.EndChangeCheck()) {
                    Settings.I.Save();
                    _needRestart = true;
                }

                using (new EditorGUI.DisabledScope(!Settings.I.enableProjectExtension)) {
                    EditorGUILayout.Space(3);
                    using (new EditorGUILayout.HorizontalScope()) {
                        GUILayout.Space(15);
                        using (new EditorGUILayout.VerticalScope()) {
                            EditorGUI.BeginChangeCheck();
                            Settings.I.enableProjectTab = EditorGUILayout.ToggleLeft(
                                new GUIContent(I18N.Get("UI.Core.EnableProjectTabLabel"),
                                    I18N.Get("UI.Core.EnableProjectTabTooltip")),
                                Settings.I.enableProjectTab);
                            if (EditorGUI.EndChangeCheck()) {
                                Settings.I.Save();
                                _needRestart = true;
                            }

                            EditorGUI.BeginChangeCheck();
                            Settings.I.enableStyledFolder = EditorGUILayout.ToggleLeft(
                                new GUIContent(I18N.Get("UI.Core.EnableStyledFolderLabel"),
                                    I18N.Get("UI.Core.EnableStyledFolderTooltip")),
                                Settings.I.enableStyledFolder);
                            if (EditorGUI.EndChangeCheck()) {
                                Settings.I.Save();
                                EditorApplication.RepaintProjectWindow();
                            }

                            EditorGUI.BeginChangeCheck();
                            Settings.I.showFolderOverlayIcon = EditorGUILayout.ToggleLeft(
                                new GUIContent(I18N.Get("UI.Core.ShowFolderOverlayIconLabel"),
                                    I18N.Get("UI.Core.ShowFolderOverlayIconTooltip")),
                                Settings.I.showFolderOverlayIcon);
                            if (EditorGUI.EndChangeCheck()) {
                                Settings.I.Save();
                                EditorApplication.RepaintProjectWindow();
                            }
                        }
                    }
                }
            }

            EditorGUILayout.Space(10);

            #endregion

            #region AvatarModifyUtility

            EditorGUILayout.LabelField(I18N.Get("UI.Core.Preference.AvatarModifyUtility"), EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                EditorGUI.BeginChangeCheck();
                Settings.I.enableAutoBackup = EditorGUILayout.ToggleLeft(
                    new GUIContent(I18N.Get("UI.Core.EnableAutoBackupLabel"),
                        I18N.Get("UI.Core.EnableAutoBackupTooltip")),
                    Settings.I.enableAutoBackup);
                if (EditorGUI.EndChangeCheck()) Settings.I.Save();
            }

            EditorGUILayout.Space(10);

            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUI.BeginChangeCheck();
                var newVariantPath = EditorGUILayout.TextField(
                    new GUIContent(I18N.Get("UI.Core.VariantCreateFolderPathLabel"),
                        I18N.Get("UI.Core.VariantCreateFolderPathTooltip")),
                    Settings.I.variantCreateFolderPath);
                if (EditorGUI.EndChangeCheck()) {
                    Settings.I.variantCreateFolderPath = newVariantPath;
                    Settings.I.Save();
                }

                if (GUILayout.Button("...", GUILayout.Width(30))) {
                    var path = EditorUtility.OpenFolderPanel(
                        I18N.Get("UI.Core.VariantCreateFolderPathWindow"),
                        Settings.I.variantCreateFolderPath,
                        ""
                    );
                    if (!string.IsNullOrEmpty(path)) {
                        var dataPath = Application.dataPath.Replace('\\', '/');
                        var normalized = path.Replace('\\', '/');

                        if (normalized.StartsWith(dataPath)) {
                            normalized = "Assets" + normalized.Substring(dataPath.Length);
                            Settings.I.variantCreateFolderPath = normalized;
                            Settings.I.Save();
                            GUIUtility.keyboardControl = 0;
                            EditorGUI.FocusTextInControl(null);
                        }
                        else {
                            EditorUtility.DisplayDialog(
                                I18N.Get("UI.Core.InvalidPathTitle"),
                                I18N.Get("UI.Core.VariantCreateFolderPathError"),
                                I18N.Get("UI.Core.OK")
                            );
                        }
                    }
                }
            }

            #endregion

            #region Compatibility

            EditorGUILayout.LabelField(I18N.Get("UI.Core.Preference.Compatibility"), EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                EditorGUI.BeginChangeCheck();
                Settings.I.compatLilEditorToolbox = EditorGUILayout.ToggleLeft(
                    new GUIContent(I18N.Get("UI.Core.CompatLilEditorToolboxLabel"),
                        I18N.Get("UI.Core.CompatLilEditorToolboxTooltip")),
                    Settings.I.compatLilEditorToolbox);
                if (EditorGUI.EndChangeCheck()) {
                    Settings.I.Save();
                    _needRestart = true;
                }

                EditorGUI.BeginChangeCheck();
                Settings.I.compatFaceEmo = EditorGUILayout.ToggleLeft(
                    new GUIContent(I18N.Get("UI.Core.CompatFaceEmoLabel"),
                        I18N.Get("UI.Core.CompatFaceEmoTooltip")),
                    Settings.I.compatFaceEmo);
                if (EditorGUI.EndChangeCheck()) {
                    Settings.I.Save();
                    EditorApplication.RepaintHierarchyWindow();
                }
            }

            #endregion

            EditorGUILayout.EndScrollView();
        }

        private static void DrawHiddenItemList() {
            _hiddenItemReorderableList ??= new ReorderableList(
                Settings.I.hiddenItemList,
                typeof(string),
                false, false, true, true) {
                drawElementCallback = (rect, index, _, _) =>
                {
                    var list = Settings.I.hiddenItemList;
                    if (index < 0 || index >= list.Count) return;
                    rect.y += 2;
                    EditorGUI.BeginChangeCheck();
                    var newValue = EditorGUI.TextField(
                        new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                        list[index]);
                    if (!EditorGUI.EndChangeCheck()) return;
                    list[index] = newValue;
                    Settings.I.Save();
                },
                onAddCallback = _ =>
                {
                    Settings.I.hiddenItemList.Add("");
                    Settings.I.Save();
                },
                onRemoveCallback = rl =>
                {
                    if (rl.index < 0 || rl.index >= Settings.I.hiddenItemList.Count) return;
                    Settings.I.hiddenItemList.RemoveAt(rl.index);
                    Settings.I.Save();
                },
                onReorderCallback = _ => { Settings.I.Save(); }
            };

            _hiddenItemReorderableList.list = Settings.I.hiddenItemList;
            _hiddenItemReorderableList.DoLayoutList();
        }

        private static void DrawIgnoreComponentNameList() {
            _ignoreComponentReorderableList ??= new ReorderableList(
                Settings.I.ignoreComponentNameList,
                typeof(string),
                false, false, true, true) {
                drawElementCallback = (rect, index, _, _) =>
                {
                    var list = Settings.I.ignoreComponentNameList;
                    if (index < 0 || index >= list.Count) return;
                    rect.y += 2;
                    EditorGUI.BeginChangeCheck();
                    var newValue = EditorGUI.TextField(
                        new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                        list[index]);
                    if (!EditorGUI.EndChangeCheck()) return;
                    list[index] = newValue;
                    Settings.I.Save();
                },
                onAddCallback = _ =>
                {
                    Settings.I.ignoreComponentNameList.Add("");
                    Settings.I.Save();
                },
                onRemoveCallback = rl =>
                {
                    if (rl.index < 0 || rl.index >= Settings.I.ignoreComponentNameList.Count) return;
                    Settings.I.ignoreComponentNameList.RemoveAt(rl.index);
                    Settings.I.Save();
                },
                onReorderCallback = _ => { Settings.I.Save(); }
            };

            _ignoreComponentReorderableList.list = Settings.I.ignoreComponentNameList;
            _ignoreComponentReorderableList.DoLayoutList();
        }

        private static void DrawIconList() {
            var list = Settings.I.iconList;

            _iconUseAssetFlags ??= new List<bool>(list.Select(s => s != null && s.StartsWith("asset:")));
            while (_iconUseAssetFlags.Count < list.Count) _iconUseAssetFlags.Add(false);
            if (_iconUseAssetFlags.Count > list.Count)
                _iconUseAssetFlags.RemoveRange(list.Count, _iconUseAssetFlags.Count - list.Count);

            _iconReorderableList ??= new ReorderableList(
                list,
                typeof(string),
                true, false, true, true) {
                drawElementCallback = (rect, index, _, _) =>
                {
                    if (index < 0 || index >= list.Count) return;
                    rect.y += 2;

                    while (_iconUseAssetFlags.Count <= index) _iconUseAssetFlags.Add(false);

                    EditorGUI.BeginChangeCheck();
                    var keyOrPath = list[index] ?? string.Empty;
                    var isAsset = _iconUseAssetFlags[index] || keyOrPath.StartsWith("asset:");
                    _iconUseAssetFlags[index] = isAsset;

                    var fieldRect = new Rect(rect.x, rect.y, rect.width - 28, EditorGUIUtility.singleLineHeight);
                    var toggleRect = new Rect(rect.x + rect.width - 26, rect.y, 24, EditorGUIUtility.singleLineHeight);

                    if (isAsset) {
                        var currentPath = keyOrPath.StartsWith("asset:") ? keyOrPath["asset:".Length..] : string.Empty;
                        Texture currentTex = null;
                        if (!string.IsNullOrEmpty(currentPath))
                            currentTex = AssetDatabase.LoadAssetAtPath<Texture>(currentPath);
                        var newTex = (Texture)EditorGUI.ObjectField(fieldRect, currentTex, typeof(Texture), false);
                        if (newTex != currentTex) {
                            var path = newTex != null ? AssetDatabase.GetAssetPath(newTex) : string.Empty;
                            list[index] = string.IsNullOrEmpty(path) ? string.Empty : $"asset:{path}";
                        }
                    }
                    else {
                        var newValue = EditorGUI.TextField(fieldRect, keyOrPath);
                        if (newValue != keyOrPath) list[index] = newValue;
                    }

                    var toggleIcon = EditorGUIUtility.IconContent(isAsset ? "RawImage Icon" : "UnityLogo").image;
                    var tooltip = isAsset
                        ? I18N.Get("UI.Core.ToggleInputToBuiltinIcon")
                        : I18N.Get("UI.Core.ToggleInputToAsset");
                    var btnContent = toggleIcon != null
                        ? new GUIContent(toggleIcon, tooltip)
                        : new GUIContent(isAsset ? I18N.Get("UI.Core.Icon") : I18N.Get("UI.Core.Texture"), tooltip);

                    if (GUI.Button(toggleRect, btnContent)) {
                        isAsset = !isAsset;
                        _iconUseAssetFlags[index] = isAsset;
                        if (isAsset || (list[index]?.StartsWith("asset:") ?? false)) list[index] = string.Empty;
                    }

                    if (EditorGUI.EndChangeCheck()) Settings.I.Save();
                },
                onAddCallback = _ =>
                {
                    list.Add("");
                    _iconUseAssetFlags.Add(false);
                    Settings.I.Save();
                },
                onRemoveCallback = rl =>
                {
                    if (rl.index < 0 || rl.index >= list.Count) return;
                    list.RemoveAt(rl.index);
                    _iconUseAssetFlags.RemoveAt(rl.index);
                    Settings.I.Save();
                },
                onReorderCallback = _ =>
                {
                    _iconUseAssetFlags = new List<bool>(list.Select(s => s != null && s.StartsWith("asset:")));
                    Settings.I.Save();
                }
            };

            _iconReorderableList.list = list;
            _iconReorderableList.DoLayoutList();
        }
    }
}