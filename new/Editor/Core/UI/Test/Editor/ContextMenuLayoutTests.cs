using Ee4v.Core.Testing;
using NUnit.Framework;
using UnityEngine;

namespace Ee4v.UI.Tests
{
    public sealed class ContextMenuLayoutTests
    {
        [Test]
        [FeatureTestCase(
            "コンテキストメニューの上下余白を等しく確保する",
            "項目と separator に加えて上下の border と padding を含むウィンドウ高さになることを確認します。",
            order: 220,
            category: FeatureTestCategory.Ui)]
        public void CalculateSize_IncludesTopAndBottomBorderAndPadding()
        {
            var state = new ContextMenuState(
                new[]
                {
                    new ContextMenuItemState("open", "Open"),
                    ContextMenuItemState.Separator(),
                    new ContextMenuItemState("delete", "Delete")
                },
                width: 140f);

            var size = ContextMenuLayout.CalculateSize(state);

            Assert.That(size, Is.EqualTo(new Vector2(140f, 70f)));
        }

        [Test]
        [FeatureTestCase(
            "空のコンテキストメニューでも外周寸法を確保する",
            "項目がない場合も上下の border と padding が欠けないウィンドウ高さになることを確認します。",
            order: 221,
            category: FeatureTestCategory.Ui)]
        public void CalculateSize_EmptyMenuStillIncludesSymmetricChrome()
        {
            var size = ContextMenuLayout.CalculateSize(new ContextMenuState(null, width: 140f));

            Assert.That(size, Is.EqualTo(new Vector2(140f, 10f)));
        }

        [Test]
        [FeatureTestCase(
            "短い1項目のコンテキストメニューを詰めて表示する",
            "Import だけのメニューが不要な右余白を持たず、80px の最小幅で表示されることを確認します。",
            order: 222,
            category: FeatureTestCategory.Ui)]
        public void CalculateSize_SingleShortItemUsesCompactMinimumWidth()
        {
            var state = new ContextMenuState(
                new[]
                {
                    new ContextMenuItemState("import", "Import")
                });

            var size = ContextMenuLayout.CalculateSize(state);

            Assert.That(size, Is.EqualTo(new Vector2(80f, 36f)));
        }

        [Test]
        public void CalculateSize_AutoWidthIsNotLimitedToLegacyFixedMaximum()
        {
            var state = new ContextMenuState(
                new[]
                {
                    new ContextMenuItemState(
                        "long",
                        "This context menu label is deliberately long enough to exceed the old fixed maximum width")
                });

            var size = ContextMenuLayout.CalculateSize(state);

            Assert.That(size.x, Is.GreaterThan(360f));
        }

        [Test]
        public void CalculateSize_ClampsDesiredWidthToDesktopWidth()
        {
            var state = new ContextMenuState(
                new[]
                {
                    new ContextMenuItemState(
                        "long",
                        "This context menu label is deliberately long enough to exceed the available desktop width")
                });

            var size = ContextMenuLayout.CalculateSize(state, 240f);

            Assert.That(size.x, Is.EqualTo(240f));
        }

        [Test]
        public void CalculateSize_AddsAllowanceForJapaneseLabel()
        {
            const string label = "Import 対象に追加";
            var editorSkin = UnityEditor.EditorGUIUtility.GetBuiltinSkin(UnityEditor.EditorSkin.Inspector);
            var textWidth = editorSkin.label.CalcSize(new GUIContent(label)).x;
            var state = new ContextMenuState(
                new[]
                {
                    new ContextMenuItemState("mark", label)
                });

            var size = ContextMenuLayout.CalculateSize(state);

            Assert.That(size.x, Is.GreaterThanOrEqualTo(Mathf.Ceil(textWidth + 42f)));
        }

        [Test]
        public void CalculateWindowRect_ShiftsPopupInsideDesktopBounds()
        {
            var desktopBounds = new Rect(-800f, 40f, 800f, 600f);

            var rect = ContextMenuLayout.CalculateWindowRect(
                new Vector2(-120f, 620f),
                new Vector2(260f, 100f),
                desktopBounds);

            Assert.That(rect, Is.EqualTo(new Rect(-260f, 540f, 260f, 100f)));
        }

        [Test]
        public void ContextMenuTypography_UsesImguiWithNormalFontStyle()
        {
            var labelStyle = TypographyStyleResolver.Resolve(UiClassNames.ContextMenuLabel).Style;
            var shortcutStyle = TypographyStyleResolver.Resolve(UiClassNames.ContextMenuShortcut).Style;

            Assert.That(labelStyle.RequiresImgui, Is.True);
            Assert.That(labelStyle.FontStyle, Is.EqualTo(FontStyle.Normal));
            Assert.That(shortcutStyle.RequiresImgui, Is.True);
            Assert.That(shortcutStyle.FontStyle, Is.EqualTo(FontStyle.Normal));
        }
    }
}
