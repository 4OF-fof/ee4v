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
