using System.Linq;
using Ee4v.Testing.Contracts;
using NUnit.Framework;

namespace Ee4v.HiddenObjects.Tests
{
    public sealed class HiddenObjectExclusionPolicyTests
    {
        [Test]
        [FeatureTestCase(
            "NDMF の Scene と activator object を既定で除外する",
            "既定patternを適用すると通常の非表示objectだけがsnapshotへ残ることを確認します。",
            order: 80)]
        public void Apply_DefaultPatternsExcludeNdmfContent()
        {
            var snapshot = new[]
            {
                Item(1, 0, 10, "Main", "Root", false, 0),
                Item(
                    2,
                    1,
                    10,
                    "Main",
                    "nadena.dev.ndmf.__Activator",
                    true,
                    1),
                Item(3, 0, 20, "NDMF Preview", "Preview", true, 2),
                Item(
                    4,
                    0,
                    30,
                    "___NDMF Preview___",
                    "Preview",
                    true,
                    3),
                Item(5, 1, 10, "Main", "Hidden Camera", true, 4)
            };
            var rules = new HiddenObjectExclusionRules(
                HiddenObjectExclusionPolicy.ParsePatterns(
                    HiddenObjectsDefinitions
                        .DefaultExcludedScenePatterns),
                HiddenObjectExclusionPolicy.ParsePatterns(
                    HiddenObjectsDefinitions
                        .DefaultExcludedObjectPatterns));

            var filtered = HiddenObjectExclusionPolicy.Apply(
                snapshot,
                rules);

            Assert.That(
                filtered.Select(item => item.InstanceId),
                Is.EqualTo(new[] { 1, 5 }));
        }

        [Test]
        [FeatureTestCase(
            "除外objectの子階層も一覧から除外する",
            "除外対象を親に持つobjectが単独のrootとして残らないことを確認します。",
            order: 90)]
        public void Apply_ExcludesMatchingObjectSubtree()
        {
            var snapshot = new[]
            {
                Item(1, 0, 10, "Main", "Root", false, 0),
                Item(2, 1, 10, "Main", "Generated", true, 1),
                Item(3, 2, 10, "Main", "Generated Child", true, 2),
                Item(4, 1, 10, "Main", "Keep", true, 3)
            };
            var rules = new HiddenObjectExclusionRules(
                null,
                new[] { "Generated" });

            var filtered = HiddenObjectExclusionPolicy.Apply(
                snapshot,
                rules);

            Assert.That(
                filtered.Select(item => item.InstanceId),
                Is.EqualTo(new[] { 1, 4 }));
        }

        [Test]
        [FeatureTestCase(
            "除外patternでwildcardと大文字小文字無視を利用できる",
            "1行・カンマ・セミコロン区切りを解釈し、* と ? で名前を照合できることを確認します。",
            order: 100)]
        public void ParseAndMatch_SupportsSeparatorsAndWildcards()
        {
            var patterns = HiddenObjectExclusionPolicy.ParsePatterns(
                " *ndmf* \n Preview?;Generated,Object ");

            Assert.That(patterns.Count, Is.EqualTo(4));
            Assert.That(
                HiddenObjectExclusionPolicy.Matches(
                    "NDMF Preview",
                    patterns[0]),
                Is.True);
            Assert.That(
                HiddenObjectExclusionPolicy.Matches(
                    "Preview1",
                    patterns[1]),
                Is.True);
            Assert.That(
                HiddenObjectExclusionPolicy.Matches(
                    "Preview12",
                    patterns[1]),
                Is.False);
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
