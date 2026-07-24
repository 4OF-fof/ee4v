using System.Collections.Generic;
using System.Linq;
using Ee4v.Testing.Contracts;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Ee4v.UI.Tests
{
    public sealed class ReorderableListFieldTests
    {
        [Test]
        [FeatureTestCase(
            "固定項目の並び順だけを変更する",
            "値編集や項目追加を持たず、ドラッグ対象の順序変更だけを通知することを確認します。",
            order: 270,
            category: FeatureTestCategory.Ui)]
        public void MoveItem_ChangesOnlyItemOrder()
        {
            IReadOnlyList<string> notifiedOrder = null;
            var field = new ReorderableListField(
                new ReorderableListFieldState(
                    new[]
                    {
                        new ReorderableListItemState("ee4v", "ee4v"),
                        new ReorderableListItemState("eagle", "Eagle"),
                        new ReorderableListItemState(
                            "blm",
                            "Booth Library Manager")
                    },
                    reorderTooltip: "Reorder"));
            field.OrderChanged += order =>
                notifiedOrder = order;

            field.MoveItem(2, 0);

            Assert.That(
                field.Order,
                Is.EqualTo(new[] { "blm", "ee4v", "eagle" }));
            Assert.That(notifiedOrder, Is.EqualTo(field.Order));
            Assert.That(
                field.Query<InputField>().ToList(),
                Is.Empty);
            Assert.That(
                field.Query<UiTextElement>()
                    .ToList()
                    .Count(label => label.Text == "\u2261"),
                Is.EqualTo(3));
        }

        [Test]
        [FeatureTestCase(
            "アニメーション中も安定したスロットを選択する",
            "行の表示順と古いレイアウト座標が一致しない瞬間でも、座標を並べ直して最寄りスロットを一意に判定することを確認します。",
            order: 280,
            category: FeatureTestCategory.Ui)]
        public void FindClosestSlotIndex_IgnoresItemOrder()
        {
            Assert.That(
                ReorderableListField.FindClosestSlotIndex(
                    new[] { 48f, 0f, 24f },
                    23f),
                Is.EqualTo(1));
            Assert.That(
                ReorderableListField.FindClosestSlotIndex(
                    new[] { 48f, 0f, 24f },
                    47f),
                Is.EqualTo(2));
        }
    }
}
