using NUnit.Framework;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI.Tests
{
    internal sealed class AssetItemGridViewTests
    {
        [Test]
        public void ItemsPerRow_UpdatesVisibleGridImmediatelyWithoutReplacingRow()
        {
            var grid = new AssetItemGridView();
            try
            {
                var items = new AssetItemGridEntry[12];
                for (var index = 0; index < items.Length; index++)
                {
                    items[index] = new AssetItemGridEntry(
                        index.ToString(),
                        $"Item {index}");
                }

                grid.SetItems(items);
                var visibleRow = grid.Q<VisualElement>(
                    className: "ee4v-asset-grid__row");
                Assert.That(visibleRow, Is.Not.Null);
                Assert.That(visibleRow.childCount, Is.EqualTo(6));

                grid.SetItemsPerRow(8);

                Assert.That(
                    grid.Q<VisualElement>(
                        className: "ee4v-asset-grid__row"),
                    Is.SameAs(visibleRow));
                Assert.That(visibleRow.childCount, Is.EqualTo(8));
            }
            finally
            {
                grid.Dispose();
            }
        }
    }
}
