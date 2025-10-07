using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace _4OF.ee4v.Core.Data {
    public static class EditorPrefsManager {
        #region Core
        private const string LanguageKey = "4OF.ee4v.Language";
        public static string Language {
            get => EditorPrefs.GetString(LanguageKey, "ja-JP");
            set => EditorPrefs.SetString(LanguageKey, value ?? "ja-JP");
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
            get => EditorPrefs.GetString(HeadingPrefixKey, "HEADING:");
            set => EditorPrefs.SetString(HeadingPrefixKey, value ?? string.Empty);
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

        public const string HiddenItemListKey = "4OF.ee4v.DefaultHiddenItemList";

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

        public static List<string> IgnoreComponentNameList {
            get {
                var csv = IgnoreComponentNameListCsv;
                if (string.IsNullOrEmpty(csv))
                    return EditorPrefs.HasKey(IgnoreComponentNameListKey)
                        ? new List<string> { string.Empty }
                        : new List<string>();
                var parts = csv.Split(',');
                return parts.Select(p => p.Trim()).ToList();
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
            EditorPrefs.DeleteKey(IgnoreComponentNameListKey);
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