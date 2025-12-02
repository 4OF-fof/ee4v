using System;
using System.Collections.Generic;

namespace _4OF.ee4v.Core.Setting {
    [Obsolete("Use Ee4vSettings.I instead.")]
    public static class EditorPrefsManager {
        #region Core

        public static string Language {
            get => Settings.I.language;
            set {
                Settings.I.language = value;
                Settings.I.Save();
            }
        }

        public static bool EnableHierarchyExtension {
            get => Settings.I.enableHierarchyExtension;
            set {
                Settings.I.enableHierarchyExtension = value;
                Settings.I.Save();
            }
        }

        public static bool EnableProjectExtension {
            get => Settings.I.enableProjectExtension;
            set {
                Settings.I.enableProjectExtension = value;
                Settings.I.Save();
            }
        }

        public static string ContentFolderPath {
            get => Settings.I.contentFolderPath;
            set {
                Settings.I.contentFolderPath = value;
                Settings.I.Save();
            }
        }

        public static string SceneCreateFolderPath {
            get => Settings.I.sceneCreateFolderPath;
            set {
                Settings.I.sceneCreateFolderPath = value;
                Settings.I.Save();
            }
        }

        #endregion

        #region HierarchyExtension

        public static bool ShowComponentIcons {
            get => Settings.I.showComponentIcons;
            set {
                Settings.I.showComponentIcons = value;
                Settings.I.Save();
            }
        }

        public static bool ShowMenuIcon {
            get => Settings.I.showMenuIcon;
            set {
                Settings.I.showMenuIcon = value;
                Settings.I.Save();
            }
        }

        public static bool EnableSceneSwitcher {
            get => Settings.I.enableSceneSwitcher;
            set {
                Settings.I.enableSceneSwitcher = value;
                Settings.I.Save();
            }
        }

        public static bool ShowDepthLine {
            get => Settings.I.showDepthLine;
            set {
                Settings.I.showDepthLine = value;
                Settings.I.Save();
            }
        }

        public static bool EnableCustomStyleItem {
            get => Settings.I.enableCustomStyleItem;
            set {
                Settings.I.enableCustomStyleItem = value;
                Settings.I.Save();
            }
        }

        public static string HeadingPrefix {
            get => Settings.I.headingPrefix;
            set {
                Settings.I.headingPrefix = value;
                Settings.I.Save();
            }
        }

        public static string SeparatorPrefix {
            get => Settings.I.separatorPrefix;
            set {
                Settings.I.separatorPrefix = value;
                Settings.I.Save();
            }
        }

        #endregion

        #region ProjectExtension

        // 元のキーは ShowSceneIconKey でしたが、プロパティ名は EnableProjectTab
        public static bool EnableProjectTab {
            get => Settings.I.enableProjectTab;
            set {
                Settings.I.enableProjectTab = value;
                Settings.I.Save();
            }
        }

        // 元のキーは ShowFileColorKey でしたが、プロパティ名は EnableStyledFolder
        public static bool EnableStyledFolder {
            get => Settings.I.enableStyledFolder;
            set {
                Settings.I.enableStyledFolder = value;
                Settings.I.Save();
            }
        }

        // 元のキーは ShowFolderColorKey でしたが、プロパティ名は ShowFolderOverlayIcon
        public static bool ShowFolderOverlayIcon {
            get => Settings.I.showFolderOverlayIcon;
            set {
                Settings.I.showFolderOverlayIcon = value;
                Settings.I.Save();
            }
        }

        #endregion

        #region AvatarModifyUtility

        public static bool EnableAutoBackup {
            get => Settings.I.enableAutoBackup;
            set {
                Settings.I.enableAutoBackup = value;
                Settings.I.Save();
            }
        }

        public static string VariantCreateFolderPath {
            get => Settings.I.variantCreateFolderPath;
            set {
                Settings.I.variantCreateFolderPath = value;
                Settings.I.Save();
            }
        }

        #endregion

        #region Compatibility

        public static bool CompatLilEditorToolbox {
            get => Settings.I.compatLilEditorToolbox;
            set {
                Settings.I.compatLilEditorToolbox = value;
                Settings.I.Save();
            }
        }

        public static bool CompatFaceEmo {
            get => Settings.I.compatFaceEmo;
            set {
                Settings.I.compatFaceEmo = value;
                Settings.I.Save();
            }
        }

        #endregion

        #region Lists (Helpers)

        // リストは参照を返すため、取得側で操作してSaveを呼ばないと保存されない可能性がある点に注意が必要ですが、
        // 旧コードの利用箇所を見る限り、getして表示、setして保存というパターンが多いので
        // setterでSave()を呼ぶ形にしています。

        public static List<string> HiddenItemList {
            get => Settings.I.hiddenItemList;
            set {
                Settings.I.hiddenItemList = value;
                Settings.I.Save();
            }
        }

        public static void ResetHiddenItemList() {
            Settings.I.ResetHiddenItemList();
            Settings.I.Save();
        }

        public static List<string> IgnoreComponentNameList {
            get => Settings.I.ignoreComponentNameList;
            set {
                Settings.I.ignoreComponentNameList = value;
                Settings.I.Save();
            }
        }

        public static void ResetIgnoreComponentNameList() {
            Settings.I.ResetIgnoreComponentNameList();
            Settings.I.Save();
        }

        public static List<string> IconList {
            get => Settings.I.iconList;
            set {
                Settings.I.iconList = value;
                Settings.I.Save();
            }
        }

        public static void ResetIconList() {
            Settings.I.ResetIconList();
            Settings.I.Save();
        }

        #endregion
    }
}