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

        private static List<string> _hiddenItemListCache;
        private static List<string> _iconListCache;
        private static List<bool> _iconUseAssetFlags;
        private static List<string> _ignoreComponentListCache;
        private static ReorderableList _hiddenItemReorderableList;
        private static ReorderableList _iconReorderableList;
        private static ReorderableList _ignoreComponentReorderableList;
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
            EditorPrefsManager.Language = languages[
                EditorGUILayout.Popup(
                    I18N.Get("UI.Core.LanguageLabel"),
                    Array.IndexOf(languages, EditorPrefsManager.Language),
                    languages.ToArray()
                )
            ];
            if (EditorGUI.EndChangeCheck()) _needRestart = true;

            using (new EditorGUILayout.HorizontalScope()) {
                EditorPrefsManager.ContentFolderPath = EditorGUILayout.TextField(
                    new GUIContent(I18N.Get("UI.Core.ContentFolderPathLabel"),
                        I18N.Get("UI.Core.ContentFolderPathTooltip")),
                    EditorPrefsManager.ContentFolderPath);
                if (GUILayout.Button("...", GUILayout.Width(30))) {
                    var path = EditorUtility.OpenFolderPanel(
                        I18N.Get("UI.Core.ContentFolderPathWindow"),
                        EditorPrefsManager.ContentFolderPath,
                        ""
                    );
                    if (!string.IsNullOrEmpty(path)) {
                        EditorPrefsManager.ContentFolderPath = path;
                        GUIUtility.keyboardControl = 0;
                        EditorGUI.FocusTextInControl(null);
                    }
                }
            }

            #region HierarchyExtension

            EditorGUILayout.LabelField(I18N.Get("UI.Core.Preference.HierarchyExtension"), EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                EditorGUI.BeginChangeCheck();
                EditorPrefsManager.EnableHierarchyExtension = EditorGUILayout.Toggle(
                    new GUIContent(I18N.Get("UI.Core.EnableHierarchyExtensionLabel"),
                        I18N.Get("UI.Core.EnableHierarchyExtensionTooltip")),
                    EditorPrefsManager.EnableHierarchyExtension);
                if (EditorGUI.EndChangeCheck()) _needRestart = true;

                using (new EditorGUI.DisabledScope(!EditorPrefsManager.EnableHierarchyExtension)) {
                    EditorGUILayout.Space(3);
                    using (new EditorGUILayout.HorizontalScope()) {
                        GUILayout.Space(15);
                        using (new EditorGUILayout.VerticalScope()) {
                            EditorPrefsManager.EnableSceneSwitcher = EditorGUILayout.ToggleLeft(
                                new GUIContent(I18N.Get("UI.Core.EnableSceneSwitcherLabel"),
                                    I18N.Get("UI.Core.EnableSceneSwitcherTooltip")),
                                EditorPrefsManager.EnableSceneSwitcher);

                            using (new EditorGUILayout.HorizontalScope()) {
                                EditorPrefsManager.SceneCreateFolderPath = EditorGUILayout.TextField(
                                    new GUIContent(I18N.Get("UI.Core.SceneCreateFolderPathLabel"),
                                        I18N.Get("UI.Core.SceneCreateFolderPathTooltip")),
                                    EditorPrefsManager.SceneCreateFolderPath);
                                if (GUILayout.Button("...", GUILayout.Width(30))) {
                                    var path = EditorUtility.OpenFolderPanel(
                                        I18N.Get("UI.Core.SceneCreateFolderPathWindow"),
                                        EditorPrefsManager.SceneCreateFolderPath,
                                        ""
                                    );
                                    if (!string.IsNullOrEmpty(path)) {
                                        var dataPath = Application.dataPath.Replace('\\', '/');
                                        var normalized = path.Replace('\\', '/');

                                        if (normalized.StartsWith(dataPath)) {
                                            normalized = "Assets" + normalized.Substring(dataPath.Length);
                                            EditorPrefsManager.SceneCreateFolderPath = normalized;
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
                            EditorPrefsManager.EnableCustomStyleItem = EditorGUILayout.ToggleLeft(
                                new GUIContent(I18N.Get("UI.Core.EnableCustomStyleItemLabel"),
                                    I18N.Get("UI.Core.EnableCustomStyleItemTooltip")),
                                EditorPrefsManager.EnableCustomStyleItem);
                            if (EditorGUI.EndChangeCheck()) EditorApplication.RepaintHierarchyWindow();

                            EditorGUI.BeginChangeCheck();
                            EditorPrefsManager.ShowComponentIcons = EditorGUILayout.ToggleLeft(
                                new GUIContent(I18N.Get("UI.Core.ShowComponentIconsLabel"),
                                    I18N.Get("UI.Core.ShowComponentIconsTooltip")),
                                EditorPrefsManager.ShowComponentIcons);
                            if (EditorGUI.EndChangeCheck()) EditorApplication.RepaintHierarchyWindow();

                            EditorGUI.BeginChangeCheck();
                            EditorPrefsManager.ShowMenuIcon = EditorGUILayout.ToggleLeft(
                                new GUIContent(I18N.Get("UI.Core.ShowMenuIconLabel"),
                                    I18N.Get("UI.Core.ShowMenuIconTooltip")),
                                EditorPrefsManager.ShowMenuIcon);
                            if (EditorGUI.EndChangeCheck()) EditorApplication.RepaintHierarchyWindow();

                            EditorGUI.BeginChangeCheck();
                            using (new EditorGUILayout.HorizontalScope()) {
                                EditorGUILayout.PrefixLabel(new GUIContent(I18N.Get("UI.Core.HeadingPrefixLabel"),
                                    I18N.Get("UI.Core.HeadingPrefixTooltip")));
                                EditorPrefsManager.HeadingPrefix =
                                    EditorGUILayout.TextField(EditorPrefsManager.HeadingPrefix);
                            }

                            EditorGUI.BeginChangeCheck();
                            using (new EditorGUILayout.HorizontalScope()) {
                                EditorGUILayout.PrefixLabel(new GUIContent(I18N.Get("UI.Core.SeparatorPrefixLabel"),
                                    I18N.Get("UI.Core.SeparatorPrefixTooltip")));
                                EditorPrefsManager.SeparatorPrefix =
                                    EditorGUILayout.TextField(EditorPrefsManager.SeparatorPrefix);
                            }

                            if (EditorGUI.EndChangeCheck()) EditorApplication.RepaintHierarchyWindow();

                            if (EditorGUI.EndChangeCheck()) EditorApplication.RepaintHierarchyWindow();

                            EditorGUI.BeginChangeCheck();
                            EditorPrefsManager.ShowDepthLine = EditorGUILayout.ToggleLeft(
                                new GUIContent(I18N.Get("UI.Core.ShowDepthLineLabel"),
                                    I18N.Get("UI.Core.ShowDepthLineTooltip")),
                                EditorPrefsManager.ShowDepthLine);
                            if (EditorGUI.EndChangeCheck()) EditorApplication.RepaintHierarchyWindow();

                            EditorGUILayout.Space(5);
                            using (new EditorGUILayout.HorizontalScope()) {
                                EditorGUILayout.LabelField(new GUIContent(I18N.Get("UI.Core.HiddenItemListLabel"),
                                    I18N.Get("UI.Core.HiddenItemListTooltip")), EditorStyles.boldLabel);
                                GUILayout.FlexibleSpace();
                                if (GUILayout.Button(I18N.Get("UI.Core.ResetToDefault"), GUILayout.Width(120))) {
                                    _hiddenItemListCache = new List<string>(EditorPrefsManager.HiddenItemList);
                                    EditorPrefsManager.ResetHiddenItemList();
                                    _hiddenItemListCache = new List<string>(EditorPrefsManager.HiddenItemList);
                                    if (_hiddenItemReorderableList != null)
                                        _hiddenItemReorderableList.list = _hiddenItemListCache;
                                    EditorApplication.RepaintHierarchyWindow();
                                }
                            }

                            HiddenItemList();

                            EditorGUILayout.Space(5);
                            using (new EditorGUILayout.HorizontalScope()) {
                                EditorGUILayout.LabelField(new GUIContent(
                                    I18N.Get("UI.Core.IgnoreComponentNameListLabel"),
                                    I18N.Get("UI.Core.IgnoreComponentNameListTooltip")), EditorStyles.boldLabel);
                                GUILayout.FlexibleSpace();
                                if (GUILayout.Button(I18N.Get("UI.Core.ResetToDefault"), GUILayout.Width(120))) {
                                    _ignoreComponentListCache =
                                        new List<string>(EditorPrefsManager.IgnoreComponentNameList);
                                    EditorPrefsManager.ResetIgnoreComponentNameList();
                                    _ignoreComponentListCache =
                                        new List<string>(EditorPrefsManager.IgnoreComponentNameList);
                                    if (_ignoreComponentReorderableList != null)
                                        _ignoreComponentReorderableList.list = _ignoreComponentListCache;
                                    EditorApplication.RepaintHierarchyWindow();
                                }
                            }

                            EditorGUI.BeginChangeCheck();
                            IgnoreComponentName();
                            if (EditorGUI.EndChangeCheck()) EditorApplication.RepaintHierarchyWindow();

                            EditorGUILayout.Space(5);
                            using (new EditorGUILayout.HorizontalScope()) {
                                EditorGUILayout.LabelField(new GUIContent(I18N.Get("UI.Core.IconListLabel"),
                                    I18N.Get("UI.Core.IconListTooltip")), EditorStyles.boldLabel);
                                GUILayout.FlexibleSpace();
                                if (GUILayout.Button(I18N.Get("UI.Core.ResetToDefault"), GUILayout.Width(120))) {
                                    _iconListCache = new List<string>(EditorPrefsManager.IconList);
                                    EditorPrefsManager.ResetIconList();
                                    _iconListCache = new List<string>(EditorPrefsManager.IconList);
                                    _iconUseAssetFlags =
                                        new List<bool>(_iconListCache.Select(s => s != null && s.StartsWith("asset:")));
                                    while (_iconUseAssetFlags.Count < _iconListCache.Count)
                                        _iconUseAssetFlags.Add(false);
                                    if (_iconReorderableList != null) _iconReorderableList.list = _iconListCache;
                                }
                            }

                            IconListUi();
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
                EditorPrefsManager.EnableProjectExtension = EditorGUILayout.Toggle(
                    new GUIContent(I18N.Get("UI.Core.EnableProjectExtensionLabel"),
                        I18N.Get("UI.Core.EnableProjectExtensionTooltip")),
                    EditorPrefsManager.EnableProjectExtension);
                if (EditorGUI.EndChangeCheck()) _needRestart = true;

                using (new EditorGUI.DisabledScope(!EditorPrefsManager.EnableProjectExtension)) {
                    EditorGUILayout.Space(3);
                    using (new EditorGUILayout.HorizontalScope()) {
                        GUILayout.Space(15);
                        using (new EditorGUILayout.VerticalScope()) {
                            EditorGUI.BeginChangeCheck();
                            EditorPrefsManager.EnableProjectTab = EditorGUILayout.ToggleLeft(
                                new GUIContent(I18N.Get("UI.Core.EnableProjectTabLabel"),
                                    I18N.Get("UI.Core.EnableProjectTabTooltip")),
                                EditorPrefsManager.EnableProjectTab);
                            if (EditorGUI.EndChangeCheck()) _needRestart = true;

                            EditorGUI.BeginChangeCheck();
                            EditorPrefsManager.EnableStyledFolder = EditorGUILayout.ToggleLeft(
                                new GUIContent(I18N.Get("UI.Core.EnableStyledFolderLabel"),
                                    I18N.Get("UI.Core.EnableStyledFolderTooltip")),
                                EditorPrefsManager.EnableStyledFolder);
                            if (EditorGUI.EndChangeCheck()) EditorApplication.RepaintProjectWindow();

                            EditorGUI.BeginChangeCheck();
                            EditorPrefsManager.ShowFolderOverlayIcon = EditorGUILayout.ToggleLeft(
                                new GUIContent(I18N.Get("UI.Core.ShowFolderOverlayIconLabel"),
                                    I18N.Get("UI.Core.ShowFolderOverlayIconTooltip")),
                                EditorPrefsManager.ShowFolderOverlayIcon);
                            if (EditorGUI.EndChangeCheck()) EditorApplication.RepaintProjectWindow();
                        }
                    }
                }
            }

            EditorGUILayout.Space(10);

            #endregion

            #region AvatarModifyUtility

            EditorGUILayout.LabelField(I18N.Get("UI.Core.Preference.AvatarModifyUtility"), EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                EditorPrefsManager.EnableAutoBackup = EditorGUILayout.ToggleLeft(
                    new GUIContent(I18N.Get("UI.Core.EnableAutoBackupLabel"),
                        I18N.Get("UI.Core.EnableAutoBackupTooltip")),
                    EditorPrefsManager.EnableAutoBackup);
            }

            EditorGUILayout.Space(10);

            using (new EditorGUILayout.HorizontalScope()) {
                EditorPrefsManager.VariantCreateFolderPath = EditorGUILayout.TextField(
                    new GUIContent(I18N.Get("UI.Core.VariantCreateFolderPathLabel"),
                        I18N.Get("UI.Core.VariantCreateFolderPathTooltip")),
                    EditorPrefsManager.VariantCreateFolderPath);
                if (GUILayout.Button("...", GUILayout.Width(30))) {
                    var path = EditorUtility.OpenFolderPanel(
                        I18N.Get("UI.Core.VariantCreateFolderPathWindow"),
                        EditorPrefsManager.VariantCreateFolderPath,
                        ""
                    );
                    if (!string.IsNullOrEmpty(path)) {
                        var dataPath = Application.dataPath.Replace('\\', '/');
                        var normalized = path.Replace('\\', '/');

                        if (normalized.StartsWith(dataPath)) {
                            normalized = "Assets" + normalized.Substring(dataPath.Length);
                            EditorPrefsManager.VariantCreateFolderPath = normalized;
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
                EditorPrefsManager.CompatLilEditorToolbox = EditorGUILayout.ToggleLeft(
                    new GUIContent(I18N.Get("UI.Core.CompatLilEditorToolboxLabel"),
                        I18N.Get("UI.Core.CompatLilEditorToolboxTooltip")),
                    EditorPrefsManager.CompatLilEditorToolbox);
                if (EditorGUI.EndChangeCheck()) _needRestart = true;

                EditorGUI.BeginChangeCheck();
                EditorPrefsManager.CompatFaceEmo = EditorGUILayout.ToggleLeft(
                    new GUIContent(I18N.Get("UI.Core.CompatFaceEmoLabel"),
                        I18N.Get("UI.Core.CompatFaceEmoTooltip")),
                    EditorPrefsManager.CompatFaceEmo);
                if (EditorGUI.EndChangeCheck()) EditorApplication.RepaintHierarchyWindow();
            }

            #endregion

            EditorGUILayout.EndScrollView();
        }

        private static void HiddenItemList() {
            _hiddenItemListCache ??= new List<string>(EditorPrefsManager.HiddenItemList);
            if (_hiddenItemReorderableList == null) {
                _hiddenItemReorderableList = new ReorderableList(
                    _hiddenItemListCache,
                    typeof(string),
                    false, false, true, true);
                _hiddenItemReorderableList.drawElementCallback = (rect, index, _, _) =>
                {
                    if (_hiddenItemReorderableList.list is not List<string> list) return;
                    if (index < 0 || index >= list.Count) return;
                    rect.y += 2;
                    EditorGUI.BeginChangeCheck();
                    var newValue = EditorGUI.TextField(
                        new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                        list[index]);
                    if (!EditorGUI.EndChangeCheck()) return;
                    list[index] = newValue;
                    EditorPrefsManager.HiddenItemList = list;
                };

                _hiddenItemReorderableList.onAddCallback = rl =>
                {
                    if (rl.list is not List<string> list) return;
                    list.Add("");
                    EditorPrefsManager.HiddenItemList = list;
                };

                _hiddenItemReorderableList.onRemoveCallback = rl =>
                {
                    if (rl.list is not List<string> list) return;
                    if (rl.index < 0 || rl.index >= list.Count) return;
                    list.RemoveAt(rl.index);
                    EditorPrefsManager.HiddenItemList = list;
                };

                _hiddenItemReorderableList.onReorderCallback = rl =>
                {
                    if (rl.list is not List<string> list) return;
                    EditorPrefsManager.HiddenItemList = list;
                };
            }

            if (!AreListsEqual(_hiddenItemListCache, EditorPrefsManager.HiddenItemList)) {
                _hiddenItemListCache.Clear();
                _hiddenItemListCache.AddRange(EditorPrefsManager.HiddenItemList);
                if (_hiddenItemReorderableList != null) _hiddenItemReorderableList.list = _hiddenItemListCache;
            }

            _hiddenItemReorderableList.DoLayoutList();
        }

        private static void IgnoreComponentName() {
            _ignoreComponentListCache ??= new List<string>(EditorPrefsManager.IgnoreComponentNameList);
            if (_ignoreComponentReorderableList == null) {
                _ignoreComponentReorderableList = new ReorderableList(
                    _ignoreComponentListCache,
                    typeof(string),
                    false, false, true, true);

                _ignoreComponentReorderableList.drawElementCallback = (rect, index, _, _) =>
                {
                    if (_ignoreComponentReorderableList.list is not List<string> list) return;
                    if (index < 0 || index >= list.Count) return;
                    rect.y += 2;

                    EditorGUI.BeginChangeCheck();
                    var newValue = EditorGUI.TextField(
                        new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                        list[index]);
                    if (!EditorGUI.EndChangeCheck()) return;
                    list[index] = newValue;
                    EditorPrefsManager.IgnoreComponentNameList = list;
                };

                _ignoreComponentReorderableList.onAddCallback = rl =>
                {
                    if (rl.list is not List<string> list) return;
                    list.Add("");
                    EditorPrefsManager.IgnoreComponentNameList = list;
                };

                _ignoreComponentReorderableList.onRemoveCallback = rl =>
                {
                    if (rl.list is not List<string> list) return;
                    if (rl.index < 0 || rl.index >= list.Count) return;
                    list.RemoveAt(rl.index);
                    EditorPrefsManager.IgnoreComponentNameList = list;
                };

                _ignoreComponentReorderableList.onReorderCallback = rl =>
                {
                    if (rl.list is not List<string> list) return;
                    EditorPrefsManager.IgnoreComponentNameList = list;
                };
            }

            if (!AreListsEqual(_ignoreComponentListCache, EditorPrefsManager.IgnoreComponentNameList)) {
                _ignoreComponentListCache.Clear();
                _ignoreComponentListCache.AddRange(EditorPrefsManager.IgnoreComponentNameList);
                if (_ignoreComponentReorderableList != null)
                    _ignoreComponentReorderableList.list = _ignoreComponentListCache;
            }

            _ignoreComponentReorderableList.DoLayoutList();
        }

        private static void IconListUi() {
            _iconListCache ??= new List<string>(EditorPrefsManager.IconList);
            _iconUseAssetFlags ??= new List<bool>(_iconListCache.Select(s => s != null && s.StartsWith("asset:")));
            while (_iconUseAssetFlags.Count < _iconListCache.Count) _iconUseAssetFlags.Add(false);
            if (_iconUseAssetFlags.Count > _iconListCache.Count)
                _iconUseAssetFlags.RemoveRange(_iconListCache.Count, _iconUseAssetFlags.Count - _iconListCache.Count);
            if (_iconReorderableList == null) {
                _iconReorderableList = new ReorderableList(
                    _iconListCache,
                    typeof(string),
                    true, false, true, true);

                _iconReorderableList.drawElementCallback = (rect, index, _, _) =>
                {
                    if (_iconReorderableList.list is not List<string> list) return;
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
                        if (isAsset || list[index]?.StartsWith("asset:") == true) list[index] = string.Empty;
                    }

                    if (EditorGUI.EndChangeCheck()) EditorPrefsManager.IconList = list;
                };

                _iconReorderableList.onAddCallback = rl =>
                {
                    if (rl.list is not List<string> list) return;
                    list.Add("");
                    EditorPrefsManager.IconList = list;
                };

                _iconReorderableList.onRemoveCallback = rl =>
                {
                    if (rl.list is not List<string> list) return;
                    if (rl.index < 0 || rl.index >= list.Count) return;
                    list.RemoveAt(rl.index);
                    EditorPrefsManager.IconList = list;
                };

                _iconReorderableList.onReorderCallback = rl =>
                {
                    if (rl.list is not List<string> list) return;
                    EditorPrefsManager.IconList = list;
                };
            }

            if (!AreListsEqual(_iconListCache, EditorPrefsManager.IconList)) {
                _iconListCache.Clear();
                _iconListCache.AddRange(EditorPrefsManager.IconList);
                if (_iconReorderableList != null) _iconReorderableList.list = _iconListCache;
            }

            _iconReorderableList.DoLayoutList();
        }

        private static bool AreListsEqual(List<string> a, List<string> b) {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;
            return !a.Where((t, i) => t != b[i]).Any();
        }
    }
}