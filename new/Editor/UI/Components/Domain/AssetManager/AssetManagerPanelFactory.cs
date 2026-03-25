using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal static class AssetManagerPanelFactory
    {
        private const string PanelRootClassName = "ee4v-asset-manager-panel";
        private const string PanelScrollClassName = "ee4v-asset-manager-panel__scroll";
        private const string PanelListClassName = "ee4v-asset-manager-panel__list";
        private const string PanelCardClassName = "ee4v-asset-manager-panel__card";
        private const string PanelRowClassName = "ee4v-asset-manager-panel__row";

        public static void Populate(VisualElement host, string modifierClassName, params InfoCard[] cards)
        {
            if (host == null)
            {
                return;
            }

            host.Clear();
            host.AddToClassList(PanelRootClassName);

            if (!string.IsNullOrWhiteSpace(modifierClassName))
            {
                host.AddToClassList(modifierClassName);
            }

            var scroll = new ScrollView();
            scroll.AddToClassList(PanelScrollClassName);

            var list = new VisualElement();
            list.AddToClassList(PanelListClassName);
            scroll.Add(list);
            host.Add(scroll);

            if (cards == null)
            {
                return;
            }

            for (var i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null)
                {
                    continue;
                }

                cards[i].AddToClassList(PanelCardClassName);
                list.Add(cards[i]);
            }
        }

        public static InfoCard CreateCard(string eyebrow, string title, string description, params string[] rows)
        {
            var card = new InfoCard(new InfoCardState(title, description, eyebrow));
            for (var i = 0; i < rows.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(rows[i]))
                {
                    continue;
                }

                var row = UiTextFactory.Create(rows[i]);
                row.AddToClassList(PanelRowClassName);
                row.SetWhiteSpace(WhiteSpace.Normal);
                card.Body.Add(row);
            }

            return card;
        }
    }
}
