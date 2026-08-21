using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public static class UiComposition
    {
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
