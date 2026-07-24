using System.Linq;
using Ee4v.Testing.Contracts;
using NUnit.Framework;

namespace Ee4v.HiddenObjects.Tests
{
    public sealed class HiddenObjectTreeBuilderTests
    {
        [Test]
        [FeatureTestCase(
            "非表示オブジェクトの祖先だけを含む Tree を構築できる",
            "表示中の無関係な branch を除外し、非表示オブジェクトまでの親子関係を保持することを確認します。",
            order: 10)]
        public void Build_PrunesBranchesWithoutHiddenObjects()
        {
            var snapshot = new[]
            {
                Item(1, 0, 10, "Scene A", "Root", false, 0),
                Item(2, 1, 10, "Scene A", "Visible", false, 1),
                Item(3, 1, 10, "Scene A", "Hidden", true, 2),
                Item(4, 0, 10, "Scene A", "Other", false, 3)
            };

            var groups = HiddenObjectTreeBuilder.Build(
                snapshot,
                0,
                string.Empty);

            Assert.That(groups, Has.Count.EqualTo(1));
            Assert.That(groups[0].Roots, Has.Count.EqualTo(1));
            Assert.That(groups[0].Roots[0].Name, Is.EqualTo("Root"));
            Assert.That(
                groups[0].Roots[0].Children.Select(node => node.Name),
                Is.EqualTo(new[] { "Hidden" }));
        }

        [Test]
        [FeatureTestCase(
            "検索時も一致した非表示オブジェクトの祖先を保持する",
            "名前検索で一致した node だけでなく、到達に必要な祖先も Tree に残ることを確認します。",
            order: 20)]
        public void Build_SearchKeepsAncestorPath()
        {
            var snapshot = new[]
            {
                Item(1, 0, 10, "Scene A", "Root", false, 0),
                Item(2, 1, 10, "Scene A", "Camera Secret", true, 1),
                Item(3, 1, 10, "Scene A", "Light Secret", true, 2)
            };

            var groups = HiddenObjectTreeBuilder.Build(
                snapshot,
                0,
                "camera");

            Assert.That(groups[0].Roots[0].Name, Is.EqualTo("Root"));
            Assert.That(groups[0].Roots[0].Children, Has.Count.EqualTo(1));
            Assert.That(
                groups[0].Roots[0].Children[0].Name,
                Is.EqualTo("Camera Secret"));
        }

        [Test]
        [FeatureTestCase(
            "Scene filter で対象 Scene だけを表示できる",
            "複数 Scene の snapshot から指定 Scene の非表示オブジェクトだけを構築することを確認します。",
            order: 30)]
        public void Build_FiltersBySceneHandle()
        {
            var snapshot = new[]
            {
                Item(1, 0, 10, "Scene A", "Hidden A", true, 0),
                Item(2, 0, 20, "Scene B", "Hidden B", true, 1)
            };

            var groups = HiddenObjectTreeBuilder.Build(
                snapshot,
                20,
                string.Empty);

            Assert.That(groups, Has.Count.EqualTo(1));
            Assert.That(groups[0].SceneName, Is.EqualTo("Scene B"));
            Assert.That(groups[0].Roots[0].Name, Is.EqualTo("Hidden B"));
        }

        [Test]
        [FeatureTestCase(
            "Hierarchy button は既定で有効",
            "Hidden Objects への入口が初回起動時に表示されることを確認します。",
            order: 40)]
        public void Definition_HierarchyButtonIsEnabledByDefault()
        {
            Assert.That(
                HiddenObjectsDefinitions
                    .HierarchyButtonEnabled.DefaultValue,
                Is.True);
        }

        private static HiddenObjectSnapshotItem Item(
            int instanceId,
            int parentInstanceId,
            int sceneHandle,
            string sceneName,
            string name,
            bool hidden,
            int order)
        {
            return new HiddenObjectSnapshotItem(
                instanceId,
                parentInstanceId,
                sceneHandle,
                sceneName,
                name,
                hidden,
                order);
        }
    }
}
