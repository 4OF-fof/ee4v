using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI.Tests
{
    internal sealed class AssetItemGridViewTests
    {
        [Test]
        public void Selection_CtrlTogglesItems()
        {
            var grid = CreateSelectionGrid();
            try
            {
                grid.SelectItem("b", toggle: false, range: false);
                grid.SelectItem("d", toggle: true, range: false);

                Assert.That(grid.SelectedItemIds, Is.EqualTo(new[] { "b", "d" }));
                Assert.That(grid.PrimarySelectedItemId, Is.EqualTo("d"));

                grid.SelectItem("b", toggle: true, range: false);

                Assert.That(grid.SelectedItemIds, Is.EqualTo(new[] { "d" }));
                Assert.That(grid.PrimarySelectedItemId, Is.EqualTo("d"));
            }
            finally
            {
                grid.Dispose();
            }
        }

        [Test]
        public void Selection_ShiftUsesAnchorAndCtrlShiftAddsRange()
        {
            var grid = CreateSelectionGrid();
            try
            {
                grid.SelectItem("b", toggle: false, range: false);
                grid.SetSelectedItemIds(
                    grid.SelectedItemIds,
                    grid.PrimarySelectedItemId);
                grid.SelectItem("d", toggle: false, range: true);

                Assert.That(
                    grid.SelectedItemIds,
                    Is.EqualTo(new[] { "b", "c", "d" }));
                Assert.That(grid.PrimarySelectedItemId, Is.EqualTo("d"));

                grid.SelectItem("a", toggle: true, range: true);

                Assert.That(
                    grid.SelectedItemIds,
                    Is.EqualTo(new[] { "a", "b", "c", "d" }));
                Assert.That(grid.PrimarySelectedItemId, Is.EqualTo("a"));
            }
            finally
            {
                grid.Dispose();
            }
        }

        [Test]
        public void Selection_EscapeReturnsGridToUnselectedState()
        {
            var grid = CreateSelectionGrid();
            try
            {
                IReadOnlyList<string> changedItems = null;
                string changedPrimary = null;
                grid.SelectionChanged += (items, primary) =>
                {
                    changedItems = items;
                    changedPrimary = primary;
                };
                grid.SelectItem("b", toggle: false, range: false);

                using (var evt = KeyDownEvent.GetPooled(
                           '\0',
                           KeyCode.Escape,
                           EventModifiers.None))
                {
                    grid.OnGridKeyDown(evt);
                }

                Assert.That(grid.SelectedItemIds, Is.Empty);
                Assert.That(grid.PrimarySelectedItemId, Is.Empty);
                Assert.That(changedItems, Is.Empty);
                Assert.That(changedPrimary, Is.Empty);
            }
            finally
            {
                grid.Dispose();
            }
        }

        private static AssetItemGridView CreateSelectionGrid()
        {
            var grid = new AssetItemGridView();
            grid.SetItems(new[]
            {
                new AssetItemGridEntry("a", "A"),
                new AssetItemGridEntry("b", "B"),
                new AssetItemGridEntry("c", "C"),
                new AssetItemGridEntry("d", "D"),
                new AssetItemGridEntry("e", "E")
            });
            return grid;
        }
    }
}
