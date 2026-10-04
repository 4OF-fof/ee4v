using Ee4v.Core.I18n;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        internal InfoCard CreatePreviewSection(VisualElement parent)
        {
            var card = new InfoCard(new InfoCardState(
                I18N.Get("catalog.common.preview"),
                I18N.Get("catalog.common.previewDescription")));
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

        internal VisualElement CreatePreviewArea(bool compact = false)
        {
            var surface = new VisualElement();
            surface.AddToClassList("ee4v-ui-catalog-preview-area");
            if (compact)
            {
                surface.AddToClassList(
                    "ee4v-ui-catalog-preview-area--compact");
            }

            return surface;
        }

        internal VisualElement CreatePreviewArea(
            VisualElement content,
            bool compact = false)
        {
            var surface = CreatePreviewArea(compact);
            surface.Add(content);
            return surface;
        }
    }
}
