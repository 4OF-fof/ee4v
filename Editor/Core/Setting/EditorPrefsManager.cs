using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace _4OF.ee4v.Core.Setting {
    public static class EditorPrefsManager {
        #region Core

        private const string LanguageKey = "4OF.ee4v.Language";

        public static string Language {
            get => EditorPrefs.GetString(LanguageKey, "en-US");
            set => EditorPrefs.SetString(LanguageKey, value ?? "en-US");
        }

        private const string EnableHierarchyExtensionKey = "4OF.ee4v.EnableHierarchyExtension";

        public static bool EnableHierarchyExtension {
            get => EditorPrefs.GetBool(EnableHierarchyExtensionKey, true);
            set => EditorPrefs.SetBool(EnableHierarchyExtensionKey, value);
        }

        private const string EnableProjectExtensionKey = "4OF.ee4v.EnableInspectorExtension";

        public static bool EnableProjectExtension {
            get => EditorPrefs.GetBool(EnableProjectExtensionKey, true);
            set => EditorPrefs.SetBool(EnableProjectExtensionKey, value);
        }

        private const string ContentFolderPathKey = "4OF.ee4v.ee4vContentFolderPath";

        public static string ContentFolderPath {
            get {
                var defaultPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "ee4v"
                );
                return EditorPrefs.GetString(ContentFolderPathKey, defaultPath);
            }
            set => EditorPrefs.SetString(ContentFolderPathKey, value ?? "");
        }

        private const string SceneCreateFolderPathKey = "4OF.ee4v.SceneCreateFolderPath";

        public static string SceneCreateFolderPath {
            get {
                const string defaultPath = "Assets/";
                return EditorPrefs.GetString(SceneCreateFolderPathKey, defaultPath);
            }
            set => EditorPrefs.SetString(SceneCreateFolderPathKey, value ?? "Assets/");
        }

        #endregion

        #region HierarchyExtension

        private const string ShowComponentIconsKey = "4OF.ee4v.ShowComponentIcons";

        public static bool ShowComponentIcons {
            get => EditorPrefs.GetBool(ShowComponentIconsKey, true);
            set => EditorPrefs.SetBool(ShowComponentIconsKey, value);
        }

        private const string ShowMenuIconKey = "4OF.ee4v.ShowMenuIcon";

        public static bool ShowMenuIcon {
            get => EditorPrefs.GetBool(ShowMenuIconKey, true);
            set => EditorPrefs.SetBool(ShowMenuIconKey, value);
        }

        private const string EnableSceneSwitcherKey = "4OF.ee4v.EnableSceneSwitcher";

        public static bool EnableSceneSwitcher {
            get => EditorPrefs.GetBool(EnableSceneSwitcherKey, true);
            set => EditorPrefs.SetBool(EnableSceneSwitcherKey, value);
        }

        private const string ShowDepthLineKey = "4OF.ee4v.ShowDepthLine";

        public static bool ShowDepthLine {
            get => EditorPrefs.GetBool(ShowDepthLineKey, true);
            set => EditorPrefs.SetBool(ShowDepthLineKey, value);
        }

        private const string EnableCustomStyleItemKey = "4OF.ee4v.EnableCustomStyleItem";

        public static bool EnableCustomStyleItem {
            get => EditorPrefs.GetBool(EnableCustomStyleItemKey, true);
            set => EditorPrefs.SetBool(EnableCustomStyleItemKey, value);
        }

        private const string HeadingPrefixKey = "4OF.ee4v.HeadingPrefix";

        public static string HeadingPrefix {
            get => EditorPrefs.GetString(HeadingPrefixKey, ":HEADING");
            set => EditorPrefs.SetString(HeadingPrefixKey, value ?? string.Empty);
        }
        
        private const string SeparatorPrefixKey = "4OF.ee4v.SeparatorPrefix";

        public static string SeparatorPrefix {
            get => EditorPrefs.GetString(SeparatorPrefixKey, ":SEPARATOR");
            set => EditorPrefs.SetString(SeparatorPrefixKey, value ?? string.Empty);
        }

        #endregion

        #region ProjectExtension

        private const string ShowSceneIconKey = "4OF.ee4v.EnableProjectTab";

        public static bool EnableProjectTab {
            get => EditorPrefs.GetBool(ShowSceneIconKey, true);
            set => EditorPrefs.SetBool(ShowSceneIconKey, value);
        }

        private const string ShowFileColorKey = "4OF.ee4v.EnableStyledFolder";

        public static bool EnableStyledFolder {
            get => EditorPrefs.GetBool(ShowFileColorKey, true);
            set => EditorPrefs.SetBool(ShowFileColorKey, value);
        }


        private const string ShowFolderColorKey = "4OF.ee4v.ShowFolderOverlayIcon";

        public static bool ShowFolderOverlayIcon {
            get => EditorPrefs.GetBool(ShowFolderColorKey, true);
            set => EditorPrefs.SetBool(ShowFolderColorKey, value);
        }

        #endregion
        
        #region AvatarModifyUtility

        private const string EnableAutoBackupKey = "4OF.ee4v.EnableAutoBackup";

        public static bool EnableAutoBackup {
            get => EditorPrefs.GetBool(EnableAutoBackupKey, true);
            set => EditorPrefs.SetBool(EnableAutoBackupKey, value);
        }

        private const string VariantCreateFolderPathKey = "4OF.ee4v.VariantCreateFolderPath";

        public static string VariantCreateFolderPath {
            get {
                const string defaultPath = "Assets/Variant";
                return EditorPrefs.GetString(VariantCreateFolderPathKey, defaultPath);
            }
            set => EditorPrefs.SetString(VariantCreateFolderPathKey, value ?? "Assets/Variant");
        }
        #endregion

        #region Compatibility

        private const string CompatLilEditorToolboxKey = "4OF.ee4v.compatLilEditorToolbox";

        public static bool CompatLilEditorToolbox {
            get => EditorPrefs.GetBool(CompatLilEditorToolboxKey, false);
            set => EditorPrefs.SetBool(CompatLilEditorToolboxKey, value);
        }

        private const string CompatFaceEmoKey = "4OF.ee4v.compatFaceEmo";

        public static bool CompatFaceEmo {
            get => EditorPrefs.GetBool(CompatFaceEmoKey, false);
            set => EditorPrefs.SetBool(CompatFaceEmoKey, value);
        }

        #endregion

        #region HiddenItemList

        private const string HiddenItemListKey = "4OF.ee4v.DefaultHiddenItemList";

        private static string HiddenItemListCsv {
            get => EditorPrefs.GetString(HiddenItemListKey, "");
            set => EditorPrefs.SetString(HiddenItemListKey, value ?? string.Empty);
        }

        private static readonly List<string> DefaultHiddenItemList = new() {
            "nadena.dev.ndmf__Activator"
        };

        public static List<string> HiddenItemList {
            get {
                var csv = HiddenItemListCsv;
                if (string.IsNullOrEmpty(csv))
                    return EditorPrefs.HasKey(HiddenItemListKey)
                        ? new List<string> { string.Empty }
                        : new List<string>();
                var parts = csv.Split(',');
                var list = parts.Select(p => p.Trim()).ToList();
                return list.Count == 0 ? DefaultHiddenItemList : list;
            }
            set {
                if (value == null) {
                    HiddenItemListCsv = string.Empty;
                    return;
                }

                HiddenItemListCsv = string.Join(",", value);
            }
        }

        public static void ResetHiddenItemList() {
            HiddenItemList = DefaultHiddenItemList;
        }

        #endregion

        #region IgnoreComponentNameList

        private const string IgnoreComponentNameListKey = "4OF.ee4v.IgnoreComponentNameList";

        private static string IgnoreComponentNameListCsv {
            get => EditorPrefs.GetString(IgnoreComponentNameListKey, "");
            set => EditorPrefs.SetString(IgnoreComponentNameListKey, value ?? string.Empty);
        }

        private static readonly List<string> DefaultIgnoreComponentNameList = new() {
            "Transform"
        };

        public static List<string> IgnoreComponentNameList {
            get {
                var csv = IgnoreComponentNameListCsv;
                if (string.IsNullOrEmpty(csv))
                    return EditorPrefs.HasKey(IgnoreComponentNameListKey)
                        ? new List<string> { string.Empty }
                        : new List<string>();
                var parts = csv.Split(',');
                var list = parts.Select(p => p.Trim()).ToList();
                return list.Count == 0 ? DefaultIgnoreComponentNameList : list;
            }
            set {
                if (value == null) {
                    IgnoreComponentNameListCsv = string.Empty;
                    return;
                }

                IgnoreComponentNameListCsv = string.Join(",", value);
            }
        }

        public static void ResetIgnoreComponentNameList() {
            IgnoreComponentNameList = DefaultIgnoreComponentNameList;
        }

        #endregion

        #region IconList

        private const string IconListKey = "4OF.ee4v.IconListKeys";

        private static string IconListCsv {
            get => EditorPrefs.GetString(IconListKey, string.Empty);
            set => EditorPrefs.SetString(IconListKey, value ?? string.Empty);
        }

        private static readonly List<string> DefaultIconList = new() {
            "Folder Icon",
            "FolderOpened Icon",
            "FolderFavorite Icon",
            "Favorite Icon",
            "<SEP>",
            "UnityEditor.AnimationWindow",
            "UnityEditor.ConsoleWindow",
            "UnityEditor.FindDependencies",
            "UnityEditor.GameView",
            "UnityEditor.Graphs.AnimatorControllerTool",
            "UnityEditor.HierarchyWindow",
            "UnityEditor.InspectorWindow",
            "UnityEditor.ProfilerWindow",
            "UnityEditor.SceneHierarchyWindow",
            "UnityEditor.SceneView",
            "UnityEditor.Timeline.TimelineWindow",
            "UnityEditor.VersionControl"
        };

        public static List<string> IconList {
            get {
                var csv = IconListCsv;
                if (string.IsNullOrEmpty(csv))
                    return EditorPrefs.HasKey(IconListKey)
                        ? new List<string> { string.Empty }
                        : new List<string>();
                var parts = csv.Split(',');
                var list = parts.Select(p => p.Trim()).ToList();
                return list.Count == 0 ? DefaultIconList : list;
            }
            set {
                if (value == null) {
                    IconListCsv = string.Empty;
                    return;
                }

                IconListCsv = string.Join(",", value);
            }
        }

        public static void ResetIconList() {
            IconList = DefaultIconList;
        }

        #endregion
    }
}