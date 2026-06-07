using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private InfoCard CreatePreviewSection(VisualElement parent)
        {
            var card = new InfoCard(new InfoCardState("プレビュー", "コントロールの変更はすぐにプレビューへ反映されます。"));
            var inserted = false;
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.ElementAt(i);
                if (!Equals(child.userData, "catalog-controls-section"))
                {
                    continue;
                }

                parent.Insert(i, card);
                inserted = true;
                break;
            }

            if (!inserted)
            {
                parent.Add(card);
            }

            return card;
        }

        private VisualElement CreatePreviewSurface(bool compact = false)
        {
            var surface = new VisualElement();
            surface.AddToClassList("ee4v-ui-catalog-preview-surface");
            if (compact)
            {
                surface.AddToClassList("ee4v-ui-catalog-preview-surface--compact");
            }

            return surface;
        }

        private VisualElement CreatePreviewSurface(VisualElement content, bool compact = false)
        {
            var surface = CreatePreviewSurface(compact);
            surface.Add(content);
            return surface;
        }
    }
}
