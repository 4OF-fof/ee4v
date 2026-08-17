using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        internal sealed class ControlsSectionContext
        {
            public ControlsSectionContext(InfoCard card, VisualElement content, TabCard tabCard)
            {
                Card = card;
                Content = content;
                TabCard = tabCard;
            }

            public InfoCard Card { get; }

            public VisualElement Content { get; }

            public TabCard TabCard { get; }
        }
    }
}
