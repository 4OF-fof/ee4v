using Ee4v.Core.Testing;
using NUnit.Framework;
using UnityEngine;

namespace Ee4v.UI.Tests
{
    public sealed class ImageTooltipTests
    {
        [Test]
        [FeatureTestCase(
            "画像 tooltip の縦横比を維持する",
            "大きな画像が最大表示領域へ収まり、元の縦横比を維持することを確認します。",
            order: 223,
            category: FeatureTestCategory.Ui)]
        public void CalculateImageSize_FitsLargeImageWithoutChangingAspectRatio()
        {
            var texture = new Texture2D(1200, 600);
            try
            {
                Assert.That(ImageTooltipLayout.CalculateImageSize(texture), Is.EqualTo(new Vector2(300f, 150f)));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void CalculateWindowRect_MovesTooltipAwayFromDesktopEdges()
        {
            var desktop = new Rect(0f, 0f, 800f, 600f);
            var rect = ImageTooltipLayout.CalculateWindowRect(
                new Vector2(790f, 590f),
                new Vector2(316f, 200f),
                desktop);

            Assert.That(rect.xMax, Is.LessThanOrEqualTo(desktop.xMax));
            Assert.That(rect.yMax, Is.LessThanOrEqualTo(desktop.yMax));
            Assert.That(rect.Contains(new Vector2(rect.xMin, rect.yMin)), Is.True);
        }

        [Test]
        public void FileNameTypography_IsCentered()
        {
            var style = TypographyStyleResolver.Resolve(UiClassNames.ImageTooltipFileName).Style;

            Assert.That(style.Alignment, Is.EqualTo(TextAnchor.MiddleCenter));
            Assert.That(style.WhiteSpace, Is.EqualTo(UnityEngine.UIElements.WhiteSpace.NoWrap));
        }

        [Test]
        public void HistoryNavigationOverlayTypography_IsVerticallyCentered()
        {
            var breadcrumbStyle = TypographyStyleResolver.Resolve(UiClassNames.HistoryNavigationBreadcrumbItemLabel).Style;
            var rowStyle = TypographyStyleResolver.Resolve(UiClassNames.HistoryNavigationOverlayRow).Style;
            var separatorStyle = TypographyStyleResolver.Resolve(UiClassNames.HistoryNavigationOverlaySeparator).Style;

            Assert.That(breadcrumbStyle.Alignment, Is.EqualTo(TextAnchor.MiddleLeft));
            Assert.That(rowStyle.Alignment, Is.EqualTo(TextAnchor.MiddleLeft));
            Assert.That(separatorStyle.Alignment, Is.EqualTo(TextAnchor.MiddleCenter));
        }
    }
}
