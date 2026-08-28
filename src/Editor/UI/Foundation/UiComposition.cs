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
            "Editor/UI/Components/Inputs/StringListField/string-list-field.uss",
            "Editor/UI/Components/Collections/SearchableTreeView/searchable-tree-view.uss",
            "Editor/UI/Components/Content/Icon/icon.uss",
            "Editor/UI/Components/Content/InfoCard/info-card.uss",
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
