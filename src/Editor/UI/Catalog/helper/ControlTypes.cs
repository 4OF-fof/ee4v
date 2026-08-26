using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        internal sealed class ControlsSectionContext
        {
            public ControlsSectionContext(
                InfoCard card,
                VisualElement content)
            {
                Card = card;
                Content = content;
            }

            public InfoCard Card { get; }

            public VisualElement Content { get; }
        }
    }
}
