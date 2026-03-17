using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace _4OF.ee4v.Core.Setting {
    [FilePath("ee4v/Settings.asset", FilePathAttribute.Location.PreferencesFolder)]
    public class SettingSingleton : ScriptableSingleton<SettingSingleton> {
        // Core  
        public string language = "en-US";
        public string contentFolderPath;
        public string sceneCreateFolderPath = "Assets/";

        // Hierarchy Extension  
        public bool enableHierarchyExtension = true;
        public bool enableSceneSwitcher = true;
        public bool enableCustomStyleItem = true;
        public bool showComponentIcons = true;
        public bool showMenuIcon = true;
        public bool showDepthLine = true;
        public string headingPrefix = ":HEADING";
        public string separatorPrefix = ":SEPARATOR";

        public List<string> hiddenItemList = new() { "nadena.dev.ndmf__Activator" };
        public List<string> ignoreComponentNameList = new() { "Transform" };

        // Project Extension  
        public bool enableProjectExtension = true;
        public bool enableProjectTab = true;
        public bool enableStyledFolder = true;
        public bool showFolderOverlayIcon = true;

        // Avatar Modify Utility  
        public bool enableAutoBackup = true;
        public string variantCreateFolderPath = "Assets/Variant";

        // Compatibility  
        public bool compatLilEditorToolbox;
        public bool compatFaceEmo;

        // Icon List  
        public List<string> iconList = new() {
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

        public static SettingSingleton I => instance;

        private void OnEnable() {
            if (string.IsNullOrEmpty(contentFolderPath))
                contentFolderPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "ee4v"
                );
        }

        public void Save() {
            Save(true);
        }

        // Default Value Resets  
        public void ResetHiddenItemList() {
            hiddenItemList = new List<string> { "nadena.dev.ndmf__Activator" };
        }

        public void ResetIgnoreComponentNameList() {
            ignoreComponentNameList = new List<string> { "Transform" };
        }

        public void ResetIconList() {
            iconList = new List<string> {
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
        }
    }
}