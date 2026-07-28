using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Ee4v.UI.Tests
{
    public sealed class ItemGridTests
    {
        [TestCase(132f, 480f, 1)]
        [TestCase(281f, 480f, 1)]
        [TestCase(480f, 480f, 2)]
        [TestCase(1000f, 480f, 3)]
        [TestCase(1240f, 540f, 3)]
        public void RecommendedMinimumItemsPerRowAvoidsUnusedHorizontalSpace(
            float viewportWidth,
            float viewportHeight,
            int expected)
        {
            Assert.That(
                ItemGrid.CalculateRecommendedMinimumItemsPerRow(
                    viewportWidth,
                    viewportHeight),
                Is.EqualTo(expected));
        }

        [Test]
        public void ItemCard_ShowsImageStackArtwork()
        {
            var card = new ItemCard(
                new ItemCardState(
                    "collection",
                    "Collection",
                    new ItemImageState(),
                    null,
                    string.Empty,
                    new[]
                    {
                        new ItemCardState("back"),
                        new ItemCardState(
                            string.Empty,
                            string.Empty,
                            new ItemImageState(),
                            IconState.FromBuiltinIcon(
                                UiBuiltinIcon.Folder))
                    }));

            var stack = card.Q<ImageStack>(
                className:
                "ee4v-ui-item-card__image-stack");

            Assert.That(stack, Is.Not.Null);
            Assert.That(
                stack.style.display.value,
                Is.EqualTo(DisplayStyle.Flex));
            Assert.That(
                card.Q<VisualElement>(
                    className:
                    "ee4v-ui-item-card__badge"),
                Is.Null);
        }

        [Test]
        public void ItemCard_ShowsTypeIconBeforeName()
        {
            var nameIcon =
                IconState.FromBuiltinIcon(
                    UiBuiltinIcon.Folder);
            var card = new ItemCard(
                new ItemCardState(
                    "collection",
                    "Collection",
                    new ItemImageState(),
                    null,
                    string.Empty,
                    null,
                    nameIcon));

            var nameRow = card.Q<VisualElement>(
                className:
                "ee4v-ui-item-card__name-row");
            Assert.That(nameRow, Is.Not.Null);

            var icon = nameRow.Q<Icon>(
                className:
                "ee4v-ui-item-card__name-icon");
            var label = nameRow.Q<UiTextElement>(
                className:
                UiClassNames.ItemCardName);

            Assert.That(icon, Is.Not.Null);
            Assert.That(label, Is.Not.Null);
            Assert.That(
                nameRow.IndexOf(icon),
                Is.LessThan(nameRow.IndexOf(label)));
            Assert.That(
                icon.style.display.value,
                Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void ItemCard_UsesRequestedMainIconSize()
        {
            var card = new ItemCard(
                new ItemCardState(
                    "group",
                    "Group",
                    new ItemImageState(),
                    IconState.FromFluentIcon(
                        UiFluentIcon.FolderBranchFork,
                        size: 88f)));

            var icon = card.Q<Icon>(
                className:
                "ee4v-ui-item-card__icon");

            Assert.That(icon, Is.Not.Null);
            Assert.That(
                icon.style.width.value.value,
                Is.EqualTo(88f));
            Assert.That(
                icon.style.height.value.value,
                Is.EqualTo(88f));
        }

        [UnityTest]
        public IEnumerator LayoutFallbackKeepsItemsVisibleAtBothRangeEdges()
        {
            var window = ScriptableObject.CreateInstance<ItemGridTestWindow>();
            window.position = new Rect(0f, 0f, 600f, 420f);
            window.Show();

            try
            {
                var items = new List<ItemCardState>();
                for (var i = 0; i < 24; i++)
                {
                    items.Add(new ItemCardState("Item " + i));
                }

                var grid = new TestItemGrid(new ItemGridState(items));
                grid.style.flexGrow = 1f;
                window.rootVisualElement.Add(grid);

                grid.SetItemsPerRow(1);
                yield return null;
                yield return null;
                Assert.That(grid.RealizedItemCount, Is.GreaterThan(0));
                Assert.That(grid.FixedItemHeight, Is.LessThan(grid.ListHeight));

                grid.SetItemsPerRow(12);
                yield return null;
                yield return null;
                Assert.That(grid.Items, Is.SameAs(items));
                Assert.That(grid.RealizedItemCount, Is.GreaterThan(0));
                Assert.That(grid.RealizedColumnCount, Is.EqualTo(12));
            }
            finally
            {
                window.Close();
            }
        }

        private sealed class ItemGridTestWindow : EditorWindow
        {
        }

        private sealed class TestItemGrid : ItemGrid
        {
            public TestItemGrid(ItemGridState state)
                : base(state)
            {
            }

            public float FixedItemHeight
            {
                get { return ListView.fixedItemHeight; }
            }

            public float ListHeight
            {
                get { return ListView.resolvedStyle.height; }
            }

            public int RealizedItemCount
            {
                get
                {
                    var slots = ListView.Query<VisualElement>(className: RowSlotClassName).ToList();
                    var count = 0;
                    for (var i = 0; i < slots.Count; i++)
                    {
                        if (slots[i].userData is int && (int)slots[i].userData >= 0)
                        {
                            count++;
                        }
                    }

                    return count;
                }
            }

            public int RealizedColumnCount
            {
                get
                {
                    var slots = ListView.Query<VisualElement>(className: RowSlotClassName).ToList();
                    return slots.Count > 0 && slots[0].parent != null
                        ? slots[0].parent.childCount
                        : 0;
                }
            }
        }
    }
}
