using System.Collections.Generic;
using System.Linq;
using Ee4v.Testing.Contracts;
using NUnit.Framework;

namespace Ee4v.HiddenObjects.Tests
{
    public sealed class HiddenObjectsControllerTests
    {
        [Test]
        [FeatureTestCase(
            "表示中の非表示オブジェクトだけを一括選択できる",
            "検索と Scene filter を適用した結果だけが選択され、選択解除も state に反映されることを確認します。",
            order: 50)]
        public void SelectAllVisible_UsesCurrentFilters()
        {
            var repository = new FakeRepository(CreateSnapshot());
            var controller = new HiddenObjectsController(
                repository,
                new FakeNavigator());
            controller.Refresh();

            controller.SetQuery("Camera");
            controller.SelectAllVisible();

            Assert.That(
                controller.State.SelectedInstanceIds,
                Is.EquivalentTo(new[] { 2 }));

            controller.SetQuery(string.Empty);
            controller.SetSceneFilter(20);
            controller.SelectAllVisible();

            Assert.That(
                controller.State.SelectedInstanceIds,
                Is.EquivalentTo(new[] { 2, 3 }));

            controller.ClearSelection();
            Assert.That(
                controller.State.SelectedInstanceIds,
                Is.Empty);
        }

        [Test]
        [FeatureTestCase(
            "選択した object を Undo 名付きで再表示できる",
            "repository へ選択 ID と Undo operation 名を渡し、再走査後に選択と非表示件数が更新されることを確認します。",
            order: 60)]
        public void RevealSelected_RefreshesStateAfterRepositoryUpdate()
        {
            var repository = new FakeRepository(CreateSnapshot());
            var controller = new HiddenObjectsController(
                repository,
                new FakeNavigator());
            controller.Refresh();
            controller.SetSelected(2, true);

            var revealed = controller.RevealSelected(
                "Reveal hidden object");

            Assert.That(revealed, Is.EqualTo(1));
            Assert.That(
                repository.RevealedIds,
                Is.EquivalentTo(new[] { 2 }));
            Assert.That(
                repository.UndoOperationName,
                Is.EqualTo("Reveal hidden object"));
            Assert.That(
                controller.State.SelectedInstanceIds,
                Is.Empty);
            Assert.That(
                controller.State.TotalHiddenCount,
                Is.EqualTo(1));
        }

        [Test]
        [FeatureTestCase(
            "Tree row から対象 object へフォーカスできる",
            "controller が instance ID を navigator へそのまま通知することを確認します。",
            order: 70)]
        public void Focus_ForwardsInstanceIdToNavigator()
        {
            var navigator = new FakeNavigator();
            var controller = new HiddenObjectsController(
                new FakeRepository(CreateSnapshot()),
                navigator);

            controller.Focus(3);

            Assert.That(navigator.FocusedInstanceId, Is.EqualTo(3));
        }

        [Test]
        [FeatureTestCase(
            "controllerは除外済みsnapshotだけを表示stateへ反映する",
            "exclusion sourceのScene・object patternが件数とScene選択肢へ反映されることを確認します。",
            order: 75)]
        public void Refresh_AppliesExclusionSource()
        {
            var snapshot = CreateSnapshot()
                .Concat(new[]
                {
                    new HiddenObjectSnapshotItem(
                        4,
                        0,
                        30,
                        "NDMF Preview",
                        "Preview",
                        true,
                        3)
                })
                .ToArray();
            var controller = new HiddenObjectsController(
                new FakeRepository(snapshot),
                new FakeNavigator(),
                new FakeExclusionSource(
                    new HiddenObjectExclusionRules(
                        new[] { "*NDMF*" },
                        new[] { "*Camera" })));

            controller.Refresh();

            Assert.That(
                controller.State.TotalHiddenCount,
                Is.EqualTo(1));
            Assert.That(
                controller.State.SceneOptions
                    .Select(option => option.Label),
                Is.EqualTo(new[] { "Main", "UI" }));
            Assert.That(
                controller.State.SceneGroups
                    .SelectMany(group => group.Roots)
                    .Select(node => node.Name),
                Does.Not.Contain("Hidden Camera"));
        }

        private static IReadOnlyList<HiddenObjectSnapshotItem>
            CreateSnapshot()
        {
            return new[]
            {
                new HiddenObjectSnapshotItem(
                    1,
                    0,
                    10,
                    "Main",
                    "Root",
                    false,
                    0),
                new HiddenObjectSnapshotItem(
                    2,
                    1,
                    10,
                    "Main",
                    "Hidden Camera",
                    true,
                    1),
                new HiddenObjectSnapshotItem(
                    3,
                    0,
                    20,
                    "UI",
                    "Hidden Canvas",
                    true,
                    2)
            };
        }

        private sealed class FakeRepository : IHiddenObjectRepository
        {
            private IReadOnlyList<HiddenObjectSnapshotItem> _items;

            public FakeRepository(
                IReadOnlyList<HiddenObjectSnapshotItem> items)
            {
                _items = items;
            }

            public IReadOnlyCollection<int> RevealedIds { get; private set; }

            public string UndoOperationName { get; private set; }

            public IReadOnlyList<HiddenObjectSnapshotItem> Load()
            {
                return _items;
            }

            public int Reveal(
                IReadOnlyCollection<int> instanceIds,
                string undoOperationName)
            {
                RevealedIds = instanceIds.ToArray();
                UndoOperationName = undoOperationName;
                var revealed = new HashSet<int>(instanceIds);
                var count = _items.Count(item =>
                    item.IsHidden &&
                    revealed.Contains(item.InstanceId));
                _items = _items
                    .Select(item =>
                        new HiddenObjectSnapshotItem(
                            item.InstanceId,
                            item.ParentInstanceId,
                            item.SceneHandle,
                            item.SceneName,
                            item.Name,
                            item.IsHidden &&
                            !revealed.Contains(item.InstanceId),
                            item.Order))
                    .ToArray();
                return count;
            }
        }

        private sealed class FakeNavigator : IHiddenObjectNavigator
        {
            public int FocusedInstanceId { get; private set; }

            public void Focus(int instanceId)
            {
                FocusedInstanceId = instanceId;
            }
        }

        private sealed class FakeExclusionSource
            : IHiddenObjectExclusionSource
        {
            private readonly HiddenObjectExclusionRules _rules;

            public FakeExclusionSource(HiddenObjectExclusionRules rules)
            {
                _rules = rules;
            }

            public HiddenObjectExclusionRules Load()
            {
                return _rules;
            }
        }
    }
}
