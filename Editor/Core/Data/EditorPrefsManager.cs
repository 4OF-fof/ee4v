using System.Collections.Generic;
using System.Linq;

namespace _4OF.ee4v.Core.Data {
    public static class EditorPrefsManager {
        #region Core
        private const string EnableHierarchyExtensionKey = "4OF.ee4v.EnableHierarchyExtension";
        public static bool EnableHierarchyExtension {
            get => UnityEditor.EditorPrefs.GetBool(EnableHierarchyExtensionKey, true);
            set => UnityEditor.EditorPrefs.SetBool(EnableHierarchyExtensionKey, value);
        }
        
        private const string EnableProjectExtensionKey = "4OF.ee4v.EnableInspectorExtension";
        public static bool EnableProjectExtension {
            get => UnityEditor.EditorPrefs.GetBool(EnableProjectExtensionKey, true);
            set => UnityEditor.EditorPrefs.SetBool(EnableProjectExtensionKey, value);
        }
        #endregion

        #region HierarchyExtension
        private const string ShowComponentIconsKey = "4OF.ee4v.ShowComponentIcons";
        public static bool ShowComponentIcons {
            get => UnityEditor.EditorPrefs.GetBool(ShowComponentIconsKey, true);
            set => UnityEditor.EditorPrefs.SetBool(ShowComponentIconsKey, value);
        }
        
        private const string ShowMenuIconKey = "4OF.ee4v.ShowMenuIcon";
        public static bool ShowMenuIcon {
            get => UnityEditor.EditorPrefs.GetBool(ShowMenuIconKey, true);
            set => UnityEditor.EditorPrefs.SetBool(ShowMenuIconKey, value);
        }
        
        private const string EnableSceneSwitcherKey = "4OF.ee4v.EnableSceneSwitcher";
        public static bool EnableSceneSwitcher {
            get => UnityEditor.EditorPrefs.GetBool(EnableSceneSwitcherKey, true);
            set => UnityEditor.EditorPrefs.SetBool(EnableSceneSwitcherKey, value);
        }
        
        private const string ShowDepthLineKey = "4OF.ee4v.ShowDepthLine";
        public static bool ShowDepthLine {
            get => UnityEditor.EditorPrefs.GetBool(ShowDepthLineKey, true);
            set => UnityEditor.EditorPrefs.SetBool(ShowDepthLineKey, value);
        }
        
        private const string EnableCustomStyleItemKey = "4OF.ee4v.EnableCustomStyleItem";
        public static bool EnableCustomStyleItem {
            get => UnityEditor.EditorPrefs.GetBool(EnableCustomStyleItemKey, true);
            set => UnityEditor.EditorPrefs.SetBool(EnableCustomStyleItemKey, value);
        }
        
        private const string HeadingPrefixKey = "4OF.ee4v.HeadingPrefix";
        public static string HeadingPrefix {
            get => UnityEditor.EditorPrefs.GetString(HeadingPrefixKey, "HEADING:");
            set => UnityEditor.EditorPrefs.SetString(HeadingPrefixKey, value ?? string.Empty);
        }
        #endregion
        
        #region ProjectExtension
        private const string ShowSceneIconKey = "4OF.ee4v.EnableProjectTab";
        public static bool EnableProjectTab {
            get => UnityEditor.EditorPrefs.GetBool(ShowSceneIconKey, true);
            set => UnityEditor.EditorPrefs.SetBool(ShowSceneIconKey, value);
        }
        
        private const string ShowFileColorKey = "4OF.ee4v.EnableStyledFolder";
        public static bool EnableStyledFolder {
            get => UnityEditor.EditorPrefs.GetBool(ShowFileColorKey, true);
            set => UnityEditor.EditorPrefs.SetBool(ShowFileColorKey, value);
        }
        
        
        private const string ShowFolderColorKey = "4OF.ee4v.ShowFolderOverlayIcon";
        public static bool ShowFolderOverlayIcon {
            get => UnityEditor.EditorPrefs.GetBool(ShowFolderColorKey, true);
            set => UnityEditor.EditorPrefs.SetBool(ShowFolderColorKey, value);
        }
        #endregion

        #region Compatibility
        private const string CompatLilEditorToolboxKey = "4OF.ee4v.compatLilEditorToolbox";
        public static bool CompatLilEditorToolbox {
            get => UnityEditor.EditorPrefs.GetBool(CompatLilEditorToolboxKey, false);
            set => UnityEditor.EditorPrefs.SetBool(CompatLilEditorToolboxKey, value);
        }
        
        private const string CompatFaceEmoKey = "4OF.ee4v.compatFaceEmo";
        public static bool CompatFaceEmo {
            get => UnityEditor.EditorPrefs.GetBool(CompatFaceEmoKey, false);
            set => UnityEditor.EditorPrefs.SetBool(CompatFaceEmoKey, value);
        }
        #endregion

        #region HiddenItemList

        public const string HiddenItemListKey = "4OF.ee4v.DefaultHiddenItemList";
        private static string HiddenItemListCsv {
            get => UnityEditor.EditorPrefs.GetString(HiddenItemListKey, "");
            set => UnityEditor.EditorPrefs.SetString(HiddenItemListKey, value ?? string.Empty);
        }
        
        private static readonly List<string> DefaultHiddenItemList = new() {
            "nadena.dev.ndmf__Activator",
        };
        
        public static List<string> HiddenItemList {
            get {
                var csv = HiddenItemListCsv;
                if (string.IsNullOrEmpty(csv)) {
                    return UnityEditor.EditorPrefs.HasKey(HiddenItemListKey)
                        ? new List<string> { string.Empty }
                        : new List<string>();
                }
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
            get => UnityEditor.EditorPrefs.GetString(IgnoreComponentNameListKey, "");
            set => UnityEditor.EditorPrefs.SetString(IgnoreComponentNameListKey, value ?? string.Empty);
        }

        public static List<string> IgnoreComponentNameList {
            get {
                var csv = IgnoreComponentNameListCsv;
                if (string.IsNullOrEmpty(csv)) {
                    return UnityEditor.EditorPrefs.HasKey(IgnoreComponentNameListKey)
                        ? new List<string> { string.Empty }
                        : new List<string>();
                }
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
            UnityEditor.EditorPrefs.DeleteKey(IgnoreComponentNameListKey);
        }
        #endregion

        #region IconList
        private const string IconListKey = "4OF.ee4v.IconListKeys";
        private static string IconListCsv {
            get => UnityEditor.EditorPrefs.GetString(IconListKey, string.Empty);
            set => UnityEditor.EditorPrefs.SetString(IconListKey, value ?? string.Empty);
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
            "UnityEditor.VersionControl",
        };

        public static List<string> IconList {
            get {
                var csv = IconListCsv;
                if (string.IsNullOrEmpty(csv)) {
                    return UnityEditor.EditorPrefs.HasKey(IconListKey)
                        ? new List<string> { string.Empty }
                        : new List<string>();
                }
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