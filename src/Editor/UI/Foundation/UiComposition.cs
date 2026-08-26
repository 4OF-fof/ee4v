using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public static class UiComposition
    {
        private static readonly string[] ComponentStyleSheetPaths =
        {
            "Editor/UI/Components/Overlays/StatusOverlay/status-overlay.uss",
            "Editor/UI/Components/Overlays/CustomPopup/custom-popup.uss",
            "Editor/UI/Components/Inputs/ui-button.uss",
            "Editor/UI/Components/Inputs/SearchField/search-field.uss",
            "Editor/UI/Components/Inputs/InputField/input-field.uss",
            "Editor/UI/Components/Inputs/CommaSeparatedListField/comma-separated-list-field.uss",
            "Editor/UI/Components/Collections/SearchableTreeView/searchable-tree-view.uss",
            "Editor/UI/Components/Content/Icon/icon.uss",
            "Editor/UI/Components/Content/InfoCard/info-card.uss",
            "Editor/UI/Components/Content/StatusBadge/status-badge.uss",
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
    }
}
