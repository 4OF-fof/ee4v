using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public static class UiComposition
    {
        private const string DarkThemeClassName = "ee4v-ui--dark";
        private const string LightThemeClassName = "ee4v-ui--light";
        private static readonly string[] ComponentStyleSheetPaths =
        {
            "Editor/UI/Components/Overlays/StatusOverlay/status-overlay.uss",
            "Editor/UI/Components/Overlays/CustomPopup/custom-popup.uss",
            "Editor/UI/Components/Inputs/ui-button.uss",
            "Editor/UI/Components/Inputs/SearchField/search-field.uss",
            "Editor/UI/Components/Inputs/InputField/input-field.uss",
            "Editor/UI/Components/Inputs/PathField/path-field.uss",
            "Editor/UI/Components/Inputs/ListField/list-field.uss",
            "Editor/UI/Components/Collections/SearchableTreeView/searchable-tree-view.uss",
            "Editor/UI/Components/Content/Icon/icon.uss",
            "Editor/UI/Components/Content/InfoCard/info-card.uss",
            "Editor/UI/Components/Content/MessagePanel/message-panel.uss",
            "Editor/UI/Components/Content/PreviewPane/preview-pane.uss",
            "Editor/UI/Components/Inputs/BodyPartSelector/body-part-selector.uss",
            "Editor/UI/Components/Inputs/PrefabSelector/prefab-selector.uss",
            "Editor/UI/Components/Inputs/SelectionTab/selection-tab.uss",
            "Editor/UI/Components/Content/SelectionTabBar/selection-tab-bar.uss",
            "Editor/UI/Components/Content/PrefabThumbnail/prefab-thumbnail.uss",
            "Editor/UI/Components/Content/PrefabScenePreview/prefab-scene-preview.uss",
            "Editor/UI/Components/Content/ScenePreviewViewport/scene-preview-viewport.uss",
            "Editor/UI/Components/Content/TagPill/tag-pill.uss"
        };

        public static void Prepare(VisualElement root)
        {
            Prepare(root, Array.Empty<string>());
        }

        public static void Prepare(
            VisualElement root,
            params string[] styleSheetPaths)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            root.AddToClassList("ee4v-ui");
            ApplyTheme(root);
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/common.uss");
            for (var index = 0;
                 index < ComponentStyleSheetPaths.Length;
                 index++)
            {
                UiStyleUtility.AddPackageStyleSheet(
                    root,
                    ComponentStyleSheetPaths[index]);
            }

            for (var index = 0;
                 index < (styleSheetPaths?.Length ?? 0);
                 index++)
            {
                UiStyleUtility.AddPackageStyleSheet(
                    root,
                    styleSheetPaths[index]);
            }
        }

        internal static void ApplyTheme(VisualElement root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var useLightTheme = object.ReferenceEquals(
                UiColorPalettes.Current,
                UiColorPalettes.UnityLight);
            root.EnableInClassList(DarkThemeClassName, !useLightTheme);
            root.EnableInClassList(LightThemeClassName, useLightTheme);
        }
    }
}
