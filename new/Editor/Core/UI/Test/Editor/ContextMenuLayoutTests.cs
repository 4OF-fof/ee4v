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
    }
}
