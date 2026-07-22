using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class TypographyStyleDefinition
    {
        public static readonly TypographyStyleDefinition Default = new TypographyStyleDefinition(
            false,
            12,
            UiColorTokens.TextPrimary,
            TextAnchor.UpperLeft,
            WhiteSpace.NoWrap);

        public TypographyStyleDefinition(
            bool requiresImgui,
            int fontSize,
            Color color,
            TextAnchor alignment,
            WhiteSpace whiteSpace,
            FontStyle fontStyle = FontStyle.Normal,
            float marginBottom = 0f,
            float marginTop = 0f,
            float marginLeft = 0f,
            float marginRight = 0f)
        {
            RequiresImgui = requiresImgui;
            FontSize = fontSize;
            Color = color;
            Alignment = alignment;
            WhiteSpace = whiteSpace;
            FontStyle = fontStyle;
            MarginBottom = marginBottom;
            MarginTop = marginTop;
            MarginLeft = marginLeft;
            MarginRight = marginRight;
        }

        public bool RequiresImgui { get; }

        public int FontSize { get; }

        public Color Color { get; }

        public TextAnchor Alignment { get; }

        public WhiteSpace WhiteSpace { get; }

        public FontStyle FontStyle { get; }

        public float MarginBottom { get; }

        public float MarginTop { get; }

        public float MarginLeft { get; }

        public float MarginRight { get; }
    }

    internal sealed class TypographyResolution
    {
        public TypographyResolution(string typographyClassName, TypographyStyleDefinition style)
        {
            TypographyClassName = typographyClassName ?? string.Empty;
            Style = style ?? TypographyStyleDefinition.Default;
        }

        public string TypographyClassName { get; }

        public TypographyStyleDefinition Style { get; }
    }

    internal static class TypographyStyleResolver
    {
        private static readonly Dictionary<string, TypographyStyleDefinition> Styles =
            new Dictionary<string, TypographyStyleDefinition>(StringComparer.Ordinal)
            {
                { UiClassNames.InfoCardEyebrow, Create(true, 11, UiColorTokens.TextPrimary, TextAnchor.UpperLeft, WhiteSpace.NoWrap, fontStyle: FontStyle.Bold) },
                { UiClassNames.InfoCardTitle, Create(true, 14, UiColorTokens.TextPrimary, TextAnchor.UpperLeft, WhiteSpace.Normal, fontStyle: FontStyle.Bold) },
                { UiClassNames.InfoCardDescription, Create(false, 12, UiColorTokens.TextSecondary, TextAnchor.UpperLeft, WhiteSpace.Normal) },
                { UiClassNames.BannerTitle, Create(true, 12, UiColorTokens.TextPrimary, TextAnchor.UpperLeft, WhiteSpace.Normal, fontStyle: FontStyle.Bold, marginBottom: 2f) },
                { UiClassNames.BannerMessage, Create(false, 12, UiColorTokens.TextSecondary, TextAnchor.UpperLeft, WhiteSpace.Normal) },
                { UiClassNames.StatusBadge, Create(true, 11, UiColorTokens.TextPrimary, TextAnchor.MiddleCenter, WhiteSpace.NoWrap, fontStyle: FontStyle.Bold) },
                { UiClassNames.SearchFieldPlaceholder, Create(false, 12, UiColorTokens.TextMuted, TextAnchor.MiddleLeft, WhiteSpace.NoWrap) },
                { UiClassNames.TestResultGroupCasesTitle, Create(true, 12, UiColorTokens.TextPrimary, TextAnchor.MiddleLeft, WhiteSpace.NoWrap, fontStyle: FontStyle.Bold) },
                { UiClassNames.TestResultGroupCasesMeta, Create(false, 11, UiColorTokens.TextMuted, TextAnchor.MiddleRight, WhiteSpace.NoWrap) },
                { UiClassNames.SingleSelectButtonGroupMeta, Create(false, 10, UiColorTokens.TextMuted, TextAnchor.MiddleRight, WhiteSpace.NoWrap, marginLeft: 8f) },
                { UiClassNames.ItemCardName, Create(false, 12, UiColorTokens.TextPrimary, TextAnchor.MiddleCenter, WhiteSpace.NoWrap) },
                { UiClassNames.ContextMenuLabel, Create(true, 12, UiColorTokens.TextPrimary, TextAnchor.MiddleLeft, WhiteSpace.NoWrap) },
                { UiClassNames.ContextMenuShortcut, Create(true, 12, UiColorTokens.TextMuted, TextAnchor.MiddleLeft, WhiteSpace.NoWrap) },
                { UiClassNames.InfomationPanelSelectionCount, Create(true, 16, UiColorTokens.Focus, TextAnchor.MiddleCenter, WhiteSpace.NoWrap, fontStyle: FontStyle.Bold) },
                { UiClassNames.InfomationPanelSelectionCountSuffix, Create(false, 13, UiColorTokens.TextSoft, TextAnchor.MiddleCenter, WhiteSpace.NoWrap) },
                { UiClassNames.CatalogPageTitle, Create(true, 18, UiColorTokens.TextPrimary, TextAnchor.UpperLeft, WhiteSpace.Normal, fontStyle: FontStyle.Bold, marginBottom: 4f) },
                { UiClassNames.CatalogPageDescription, Create(false, 12, UiColorTokens.TextSecondary, TextAnchor.UpperLeft, WhiteSpace.Normal) },
                { UiClassNames.CatalogDetailLabel, Create(true, 11, UiColorTokens.TextPrimary, TextAnchor.UpperLeft, WhiteSpace.NoWrap, fontStyle: FontStyle.Bold, marginBottom: 2f) },
                { UiClassNames.CatalogDetailValue, Create(false, 12, UiColorTokens.TextPrimary, TextAnchor.UpperLeft, WhiteSpace.Normal) },
                { UiClassNames.CatalogNavigatorTitle, Create(false, 16, UiColorTokens.TextPrimary, TextAnchor.MiddleCenter, WhiteSpace.NoWrap, marginBottom: 10f) },
                { UiClassNames.CatalogTreeTitle, Create(false, 12, UiColorTokens.TextPrimary, TextAnchor.MiddleLeft, WhiteSpace.NoWrap) },
                { UiClassNames.CatalogTreeImplementation, Create(false, 10, UiColorTokens.TextMuted, TextAnchor.MiddleRight, WhiteSpace.NoWrap, marginLeft: 8f) },
                { UiClassNames.Phase1StubLabel, Create(true, 12, UiColorTokens.TextOnState, TextAnchor.MiddleLeft, WhiteSpace.NoWrap, fontStyle: FontStyle.Bold) },
            };

        public static TypographyResolution Resolve(params string[] classNames)
        {
            TypographyStyleDefinition matchedStyle = null;
            string matchedClassName = string.Empty;
            var typographyClassCount = 0;

            if (classNames != null)
            {
                for (var i = 0; i < classNames.Length; i++)
                {
                    var className = classNames[i];
                    if (string.IsNullOrWhiteSpace(className))
                    {
                        continue;
                    }

                    TypographyStyleDefinition style;
                    if (!Styles.TryGetValue(className, out style))
                    {
                        continue;
                    }

                    matchedStyle = style;
                    matchedClassName = className;
                    typographyClassCount++;
                }
            }

            if (typographyClassCount > 1)
            {
                throw new InvalidOperationException("UiTextFactory received multiple typography classes.");
            }

            return new TypographyResolution(matchedClassName, matchedStyle ?? TypographyStyleDefinition.Default);
        }

        private static TypographyStyleDefinition Create(
            bool requiresImgui,
            int fontSize,
            Color color,
            TextAnchor alignment,
            WhiteSpace whiteSpace,
            FontStyle fontStyle = FontStyle.Normal,
            float marginBottom = 0f,
            float marginTop = 0f,
            float marginLeft = 0f,
            float marginRight = 0f)
        {
            return new TypographyStyleDefinition(
                requiresImgui,
                fontSize,
                color,
                alignment,
                whiteSpace,
                fontStyle,
                marginBottom,
                marginTop,
                marginLeft,
                marginRight);
        }
    }
}
