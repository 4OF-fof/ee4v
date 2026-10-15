using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class PickerBrowser : VisualElement
    {
        private VisualElement _overview;
        private VisualElement _candidates;
        private VisualElement _preview;
        private VisualElement _cards;

        public PickerBrowser()
        {
            AddToClassList("ee4v-ui-picker-browser");
        }

        public void SetPanels(VisualElement overview, VisualElement candidates,
            VisualElement preview, VisualElement cards)
        {
            if (overview == null || candidates == null || preview == null || cards == null)
            { throw new ArgumentNullException("Picker browser panels must be provided."); }
            Clear();
            _overview = overview;
            _candidates = candidates;
            _preview = preview;
            _cards = cards;
            Add(overview);
            if (!ReferenceEquals(overview, candidates)) { Add(candidates); }
            Add(preview);
        }

        public void ShowDetails(bool visible)
        {
            EnableInClassList("ee4v-ui-picker-browser--details", visible);
            if (!ReferenceEquals(_overview, _candidates))
            {
                _overview.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
                _candidates.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
            _preview.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void RefreshCardSizes()
        {
            var width = _cards.contentRect.width;
            if (float.IsNaN(width) || width <= 0f) { return; }
            const float spacing = 12f;
            var columns = Mathf.Clamp(Mathf.FloorToInt((width + spacing) / 224f), 1, 5);
            var cardWidth = Mathf.Max(1f, (width - columns * spacing) / columns);
            foreach (var card in _cards.Children().OfType<NavigationItem>())
            {
                card.style.width = cardWidth;
                var frame = card.Leading.Q<PreviewContainer>();
                if (frame != null) { frame.style.height = Mathf.Clamp(cardWidth - 16f, 120f, 220f); }
            }
        }
    }

    public sealed class PickerBrowserCard : NavigationItem
    {
        public PickerBrowserCard(NavigationItemState state, Action activate) : base(state, activate)
        {
            AddToClassList("ee4v-ui-picker-browser-card");
            Row.TitleText.SetWhiteSpace(WhiteSpace.Normal);
            Preview = new PreviewContainer { pickingMode = PickingMode.Ignore };
            Preview.AddToClassList("ee4v-ui-picker-browser-card__preview");
            Leading.Add(Preview);
        }

        public PreviewContainer Preview { get; }
    }
}
