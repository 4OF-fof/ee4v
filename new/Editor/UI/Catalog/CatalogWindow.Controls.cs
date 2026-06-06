using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private static string FormatCatalogToastTitle(string title)
        {
            var normalized = (title ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(normalized))
            {
                return "[TEST]";
            }

            return normalized.StartsWith("[TEST]", StringComparison.Ordinal)
                ? normalized
                : "[TEST] " + normalized;
        }

        private ControlsSectionContext CreateTabbedControlsSection(VisualElement parent, string description)
        {
            var card = new InfoCard(new InfoCardState("コントロール", description));
            card.userData = "catalog-controls-section";
            var tabCard = new TabCard();
            tabCard.Content.AddToClassList("ee4v-ui-catalog-controls");
            card.Body.Add(tabCard);
            parent.Add(card);
            return new ControlsSectionContext(card, tabCard.Content, tabCard);
        }

        private ControlsSectionContext CreatePlainControlsSection(VisualElement parent, string description)
        {
            var card = new InfoCard(new InfoCardState("コントロール", description));
            card.userData = "catalog-controls-section";
            var content = new VisualElement();
            content.AddToClassList("ee4v-ui-catalog-controls");
            content.style.flexDirection = FlexDirection.Column;
            card.Body.Add(content);
            parent.Add(card);
            return new ControlsSectionContext(card, content, null);
        }
    }
}
