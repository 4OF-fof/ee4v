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
            UiTypographyTokens.BodyFontSize,
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
                { UiClassNames.WindowTitle, Create(true, UiTypographyTokens.TitleFontSize, UiColorTokens.TextPrimary, TextAnchor.MiddleLeft, WhiteSpace.NoWrap, fontStyle: FontStyle.Bold) },
                { UiClassNames.SectionTitle, Create(true, UiTypographyTokens.BodyFontSize, UiColorTokens.TextPrimary, TextAnchor.MiddleLeft, WhiteSpace.NoWrap, fontStyle: FontStyle.Bold) },
                { UiClassNames.SecondaryText, Create(false, UiTypographyTokens.SmallFontSize, UiColorTokens.TextMuted, TextAnchor.MiddleLeft, WhiteSpace.Normal) },
                { UiClassNames.InfoCardEyebrow, Create(true, UiTypographyTokens.SmallFontSize, UiColorTokens.TextPrimary, TextAnchor.UpperLeft, WhiteSpace.NoWrap, fontStyle: FontStyle.Bold) },
                { UiClassNames.InfoCardTitle, Create(true, UiTypographyTokens.SubtitleFontSize, UiColorTokens.TextPrimary, TextAnchor.UpperLeft, WhiteSpace.Normal, fontStyle: FontStyle.Bold) },
                { UiClassNames.InfoCardDescription, Create(true, UiTypographyTokens.BodyFontSize, UiColorTokens.TextSecondary, TextAnchor.UpperLeft, WhiteSpace.Normal) },
                { UiClassNames.BannerTitle, Create(true, UiTypographyTokens.BodyFontSize, UiColorTokens.TextPrimary, TextAnchor.UpperLeft, WhiteSpace.Normal, fontStyle: FontStyle.Bold, marginBottom: UiSpacingTokens.Xxs) },
                { UiClassNames.BannerMessage, Create(false, UiTypographyTokens.BodyFontSize, UiColorTokens.TextSecondary, TextAnchor.UpperLeft, WhiteSpace.Normal) },
                { UiClassNames.StatusBadge, Create(true, UiTypographyTokens.SmallFontSize, UiColorTokens.TextPrimary, TextAnchor.MiddleCenter, WhiteSpace.NoWrap, fontStyle: FontStyle.Bold) },
                { UiClassNames.InputPlaceholder, Create(true, UiTypographyTokens.BodyFontSize, UiColorTokens.TextMuted, TextAnchor.MiddleLeft, WhiteSpace.NoWrap) },
                { UiClassNames.FormLabel, Create(true, UiTypographyTokens.BodyFontSize, UiColorTokens.TextSecondary, TextAnchor.MiddleLeft, WhiteSpace.NoWrap) },
                { UiClassNames.FormError, Create(true, UiTypographyTokens.BodyFontSize, UiColorTokens.Error, TextAnchor.MiddleLeft, WhiteSpace.Normal) },
                { UiClassNames.TestResultGroupCasesTitle, Create(true, UiTypographyTokens.BodyFontSize, UiColorTokens.TextPrimary, TextAnchor.MiddleLeft, WhiteSpace.NoWrap, fontStyle: FontStyle.Bold) },
                { UiClassNames.TestResultGroupCasesMeta, Create(false, UiTypographyTokens.SmallFontSize, UiColorTokens.TextMuted, TextAnchor.MiddleRight, WhiteSpace.NoWrap) },
                { UiClassNames.NavigationItemLabel, Create(true, UiTypographyTokens.BodyFontSize, UiColorTokens.TextPrimary, TextAnchor.MiddleLeft, WhiteSpace.NoWrap) },
                { UiClassNames.ButtonLabel, Create(true, UiTypographyTokens.BodyFontSize, UiColorTokens.TextPrimary, TextAnchor.MiddleLeft, WhiteSpace.NoWrap) },
                { UiClassNames.ButtonMeta, Create(true, UiTypographyTokens.CaptionFontSize, UiColorTokens.TextMuted, TextAnchor.MiddleRight, WhiteSpace.NoWrap, marginLeft: UiSpacingTokens.Medium) },
                { UiClassNames.ItemCardName, Create(false, UiTypographyTokens.BodyFontSize, UiColorTokens.TextPrimary, TextAnchor.MiddleCenter, WhiteSpace.NoWrap) },
                { UiClassNames.ContextMenuLabel, Create(true, UiTypographyTokens.BodyFontSize, UiColorTokens.TextPrimary, TextAnchor.MiddleLeft, WhiteSpace.NoWrap) },
                { UiClassNames.ContextMenuShortcut, Create(true, UiTypographyTokens.BodyFontSize, UiColorTokens.TextMuted, TextAnchor.MiddleLeft, WhiteSpace.NoWrap) },
                { UiClassNames.ImageTooltipFileName, Create(false, UiTypographyTokens.BodyFontSize, UiColorTokens.TextPrimary, TextAnchor.MiddleCenter, WhiteSpace.NoWrap) },
                { UiClassNames.HistoryNavigationBreadcrumbItemLabel, Create(false, UiTypographyTokens.BodyFontSize, UiColorTokens.TextPrimary, TextAnchor.MiddleLeft, WhiteSpace.NoWrap) },
                { UiClassNames.HistoryNavigationOverlayRow, Create(true, UiTypographyTokens.BodyFontSize, UiColorTokens.TextPrimary, TextAnchor.MiddleLeft, WhiteSpace.NoWrap) },
                { UiClassNames.HistoryNavigationOverlaySeparator, Create(false, UiTypographyTokens.BodyFontSize, UiColorTokens.TextMuted, TextAnchor.MiddleCenter, WhiteSpace.NoWrap) },
                { UiClassNames.InfomationPanelSelectionCount, Create(true, UiTypographyTokens.TitleFontSize, UiColorTokens.Focus, TextAnchor.MiddleCenter, WhiteSpace.NoWrap, fontStyle: FontStyle.Bold) },
                { UiClassNames.InfomationPanelSelectionCountSuffix, Create(false, UiTypographyTokens.LargeBodyFontSize, UiColorTokens.TextSoft, TextAnchor.MiddleCenter, WhiteSpace.NoWrap) },
                { UiClassNames.CatalogPageTitle, Create(true, UiTypographyTokens.HeadingFontSize, UiColorTokens.TextPrimary, TextAnchor.UpperLeft, WhiteSpace.Normal, fontStyle: FontStyle.Bold, marginBottom: UiSpacingTokens.Xs) },
                { UiClassNames.CatalogPageDescription, Create(false, UiTypographyTokens.BodyFontSize, UiColorTokens.TextSecondary, TextAnchor.UpperLeft, WhiteSpace.Normal) },
                { UiClassNames.CatalogDetailLabel, Create(true, UiTypographyTokens.SmallFontSize, UiColorTokens.TextPrimary, TextAnchor.UpperLeft, WhiteSpace.NoWrap, fontStyle: FontStyle.Bold, marginBottom: UiSpacingTokens.Xxs) },
                { UiClassNames.CatalogDetailValue, Create(false, UiTypographyTokens.BodyFontSize, UiColorTokens.TextPrimary, TextAnchor.UpperLeft, WhiteSpace.Normal) },
                { UiClassNames.CatalogNavigatorTitle, Create(false, UiTypographyTokens.TitleFontSize, UiColorTokens.TextPrimary, TextAnchor.MiddleCenter, WhiteSpace.NoWrap, marginBottom: UiSpacingTokens.Large) },
                { UiClassNames.CatalogTreeTitle, Create(false, UiTypographyTokens.BodyFontSize, UiColorTokens.TextPrimary, TextAnchor.MiddleLeft, WhiteSpace.NoWrap) },
                { UiClassNames.CatalogTreeImplementation, Create(false, UiTypographyTokens.CaptionFontSize, UiColorTokens.TextMuted, TextAnchor.MiddleRight, WhiteSpace.NoWrap, marginLeft: UiSpacingTokens.Medium) },
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
