using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public static class UiComposition
    {
        public static void Prepare(VisualElement root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            root.AddToClassList("ee4v-ui");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/common.uss");
        }
    }
}
