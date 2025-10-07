using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.Core.Data;
using _4OF.ee4v.Core.i18n;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace _4OF.ee4v.Core.UI {
    public class PreferencesProvider : SettingsProvider {
        private const string SettingPath = "Preferences/4OF/ee4v";

        private static List<string> _hiddenItemListCache;
        private static List<string> _iconListCache;
        private static List<bool> _iconUseAssetFlags;
        private static List<string> _ignoreComponentListCache;
        private static ReorderableList _hiddenItemReorderableList;
        private static ReorderableList _iconReorderableList;
        private static ReorderableList _ignoreComponentReorderableList;

        private PreferencesProvider(string path, SettingsScope scopes, IEnumerable<string> keywords) : base(path,
            scopes, keywords) {
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingProvider() {
            return new PreferencesProvider(SettingPath, SettingsScope.User, null);
        }

        public override void OnGUI(string searchContext) {
            //TODO: need restart
            EditorPrefsManager.EnableHierarchyExtension = EditorGUILayout.Toggle(
                new GUIContent("Enable Hierarchy Extension", "Enable or disable the Hierarchy Extension features."),
                EditorPrefsManager.EnableHierarchyExtension);

            EditorPrefsManager.EnableProjectExtension = EditorGUILayout.Toggle(
                new GUIContent("Enable Project Extension", "Enable or disable the Project Extension features."),
                EditorPrefsManager.EnableProjectExtension);

            EditorPrefsManager.EnableProjectTab = EditorGUILayout.Toggle(
                new GUIContent("Enable Project Tab", "Show or hide the Project Tab in the Project window."),
                EditorPrefsManager.EnableProjectTab);
            
            var languages = I18N.GetAvailableLanguages();
            EditorPrefsManager.Language = languages[
                EditorGUILayout.Popup(
                    "Language",
                    Array.IndexOf(languages, EditorPrefsManager.Language),
                    languages.ToArray()
                    )
            ];

            EditorGUILayout.Space(10);
            EditorGUI.BeginChangeCheck();
            EditorPrefsManager.ShowComponentIcons = EditorGUILayout.Toggle(
                new GUIContent("Show Component Icons", "Show or hide component icons in the hierarchy."),
                EditorPrefsManager.ShowComponentIcons);
            if (EditorGUI.EndChangeCheck()) EditorApplication.RepaintHierarchyWindow();

            EditorGUI.BeginChangeCheck();
            EditorPrefsManager.ShowMenuIcon = EditorGUILayout.Toggle(
                new GUIContent("Show Menu Icon", "Show or hide the menu icon in the hierarchy."),
                EditorPrefsManager.ShowMenuIcon);
            if (EditorGUI.EndChangeCheck()) EditorApplication.RepaintHierarchyWindow();

            EditorGUI.BeginChangeCheck();
            EditorPrefsManager.ShowDepthLine = EditorGUILayout.Toggle(
                new GUIContent("Show Depth Line", "Show or hide depth lines in the hierarchy."),
                EditorPrefsManager.ShowDepthLine);
            if (EditorGUI.EndChangeCheck()) EditorApplication.RepaintHierarchyWindow();

            EditorGUI.BeginChangeCheck();
            EditorPrefsManager.EnableCustomStyleItem = EditorGUILayout.Toggle(
                new GUIContent("Enable Custom Style Item", "Enable or disable custom styled items in the hierarchy."),
                EditorPrefsManager.EnableCustomStyleItem);
            if (EditorGUI.EndChangeCheck()) EditorApplication.RepaintHierarchyWindow();

            EditorGUI.BeginChangeCheck();
            EditorPrefsManager.HeadingPrefix = EditorGUILayout.TextField(
                new GUIContent("Heading Prefix", "Set the prefix string to identify heading items in the hierarchy."),
                EditorPrefsManager.HeadingPrefix);
            if (EditorGUI.EndChangeCheck()) EditorApplication.RepaintHierarchyWindow();

            EditorGUI.BeginChangeCheck();
            EditorPrefsManager.EnableStyledFolder = EditorGUILayout.Toggle(
                new GUIContent("Enable Styled Folder", "Enable or disable styled folders in the Project window."),
                EditorPrefsManager.EnableStyledFolder);
            if (EditorGUI.EndChangeCheck()) EditorApplication.RepaintProjectWindow();

            EditorGUI.BeginChangeCheck();
            EditorPrefsManager.ShowFolderOverlayIcon = EditorGUILayout.Toggle(
                new GUIContent("Show Folder Overlay Icon",
                    "Show or hide the folder overlay icon in the Project window."),
                EditorPrefsManager.ShowFolderOverlayIcon);
            if (EditorGUI.EndChangeCheck()) EditorApplication.RepaintProjectWindow();

            EditorPrefsManager.EnableSceneSwitcher = EditorGUILayout.Toggle(
                new GUIContent("Enable Scene Switcher", "Enable or disable the Scene Switcher feature."),
                EditorPrefsManager.EnableSceneSwitcher);

            EditorPrefsManager.CompatLilEditorToolbox = EditorGUILayout.Toggle(
                new GUIContent("Compatibility lilEditorToolbox",
                    "Enable this option if you are using lilEditorToolbox to avoid UI conflicts."),
                EditorPrefsManager.CompatLilEditorToolbox);

            EditorGUI.BeginChangeCheck();
            EditorPrefsManager.CompatFaceEmo = EditorGUILayout.Toggle(
                new GUIContent("Compatibility FaceEmo",
                    "Hide component icons when a VRChat Avatar Descriptor is present to avoid UI conflicts with FaceEmo."),
                EditorPrefsManager.CompatFaceEmo);
            if (EditorGUI.EndChangeCheck()) EditorApplication.RepaintHierarchyWindow();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(new GUIContent("Hidden Item List",
                "Comma-separated list of component type names to hide in the 'Add Component' menu (e.g. Transform, RectTransform)."));
            HiddenItemList();
            var hiddenResetStyle = new GUIStyle(GUI.skin.button) { fixedWidth = 120 };
            if (GUILayout.Button("Reset to Default", hiddenResetStyle)) {
                _hiddenItemListCache = new List<string>(EditorPrefsManager.HiddenItemList);
                EditorPrefsManager.ResetHiddenItemList();
                _hiddenItemListCache = new List<string>(EditorPrefsManager.HiddenItemList);
                if (_hiddenItemReorderableList != null) _hiddenItemReorderableList.list = _hiddenItemListCache;
                EditorApplication.RepaintHierarchyWindow();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(new GUIContent("Ignore Component Name List",
                "Comma-separated list of component type names to ignore when gathering component icons (e.g. Transform, RectTransform)."));
            EditorGUI.BeginChangeCheck();
            IgnoreComponentName();
            var ignoreResetStyle = new GUIStyle(GUI.skin.button) { fixedWidth = 120 };
            if (GUILayout.Button("Reset to Default", ignoreResetStyle)) {
                _ignoreComponentListCache = new List<string>(EditorPrefsManager.IgnoreComponentNameList);
                EditorPrefsManager.ResetIgnoreComponentNameList();
                _ignoreComponentListCache = new List<string>(EditorPrefsManager.IgnoreComponentNameList);
                if (_ignoreComponentReorderableList != null)
                    _ignoreComponentReorderableList.list = _ignoreComponentListCache;
                EditorApplication.RepaintHierarchyWindow();
            }

            if (EditorGUI.EndChangeCheck()) EditorApplication.RepaintHierarchyWindow();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(new GUIContent("Icon List",
                "List of icon keys used by the icon selector. Use '<SEP>' (without quotes) to insert a separator. Examples: d_Folder Icon, d_UnityEditor.SceneView."));
            IconListUi();
            var iconResetStyle = new GUIStyle(GUI.skin.button) { fixedWidth = 120 };
            if (GUILayout.Button("Reset to Default", iconResetStyle)) {
                _iconListCache = new List<string>(EditorPrefsManager.IconList);
                EditorPrefsManager.ResetIconList();
                _iconListCache = new List<string>(EditorPrefsManager.IconList);
                _iconUseAssetFlags = new List<bool>(_iconListCache.Select(s => s != null && s.StartsWith("asset:")));
                while (_iconUseAssetFlags.Count < _iconListCache.Count) _iconUseAssetFlags.Add(false);
                if (_iconReorderableList        != null) _iconReorderableList.list = _iconListCache;
            }
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
                    var tooltip = isAsset ? "Toggle input to Builtin Icon" : "Toggle input to Asset";
                    var btnContent = toggleIcon != null
                        ? new GUIContent(toggleIcon, tooltip)
                        : new GUIContent(isAsset ? "Icon" : "Texture", tooltip);
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