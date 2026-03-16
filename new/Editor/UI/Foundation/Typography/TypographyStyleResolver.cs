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
            new Color32(230, 230, 230, 255),
            TextAnchor.UpperLeft,
            WhiteSpace.NoWrap);

        public TypographyStyleDefinition(
            bool requiresImgui,
            int fontSize,
            Color color,
            TextAnchor alignment,
            WhiteSpace whiteSpace,
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
                { UiClassNames.PageTitle, Create(true, 18, 230, 230, 230, 255, TextAnchor.UpperLeft, WhiteSpace.Normal, marginBottom: 4f) },
                { UiClassNames.PageDescription, Create(false, 12, 210, 210, 210, 209, TextAnchor.UpperLeft, WhiteSpace.Normal) },
                { UiClassNames.CardEyebrow, Create(true, 11, 220, 220, 220, 235, TextAnchor.UpperLeft, WhiteSpace.NoWrap) },
                { UiClassNames.CardTitle, Create(true, 14, 230, 230, 230, 255, TextAnchor.UpperLeft, WhiteSpace.Normal) },
                { UiClassNames.CardDescription, Create(false, 12, 210, 210, 210, 209, TextAnchor.UpperLeft, WhiteSpace.Normal) },
                { UiClassNames.CardBadge, Create(true, 11, 230, 230, 230, 255, TextAnchor.MiddleCenter, WhiteSpace.NoWrap) },
                { UiClassNames.BannerTitle, Create(true, 12, 230, 230, 230, 255, TextAnchor.UpperLeft, WhiteSpace.Normal, marginBottom: 2f) },
                { UiClassNames.BannerMessage, Create(false, 12, 210, 210, 210, 209, TextAnchor.UpperLeft, WhiteSpace.Normal) },
                { UiClassNames.StatusBadge, Create(true, 11, 230, 230, 230, 255, TextAnchor.MiddleCenter, WhiteSpace.NoWrap) },
                { UiClassNames.GroupTitle, Create(true, 11, 220, 220, 220, 235, TextAnchor.UpperLeft, WhiteSpace.NoWrap, marginBottom: 2f) },
                { UiClassNames.GroupDescription, Create(false, 12, 210, 210, 210, 209, TextAnchor.UpperLeft, WhiteSpace.Normal) },
                { UiClassNames.ReferencePrimary, Create(true, 11, 220, 220, 220, 235, TextAnchor.UpperLeft, WhiteSpace.Normal) },
                { UiClassNames.ReferenceSecondary, Create(false, 12, 210, 210, 210, 209, TextAnchor.UpperLeft, WhiteSpace.Normal) },
                { UiClassNames.CatalogNavigatorTitle, Create(true, 16, 230, 230, 230, 255, TextAnchor.UpperLeft, WhiteSpace.NoWrap, marginBottom: 4f) },
                { UiClassNames.CatalogNavigatorSubtitle, Create(false, 12, 210, 210, 210, 199, TextAnchor.UpperLeft, WhiteSpace.Normal, marginBottom: 10f) },
                { UiClassNames.CatalogTreeTitle, Create(false, 12, 230, 230, 230, 255, TextAnchor.MiddleLeft, WhiteSpace.NoWrap) },
                { UiClassNames.CatalogTreeImplementation, Create(false, 10, 210, 210, 210, 184, TextAnchor.MiddleRight, WhiteSpace.NoWrap, marginLeft: 8f) },
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
            byte red,
            byte green,
            byte blue,
            byte alpha,
            TextAnchor alignment,
            WhiteSpace whiteSpace,
            float marginBottom = 0f,
            float marginTop = 0f,
            float marginLeft = 0f,
            float marginRight = 0f)
        {
            return new TypographyStyleDefinition(
                requiresImgui,
                fontSize,
                new Color32(red, green, blue, alpha),
                alignment,
                whiteSpace,
                marginBottom,
                marginTop,
                marginLeft,
                marginRight);
        }
    }
}
