using Ee4v.AssetManager.Contracts;
using NUnit.Framework;

namespace Ee4v.AssetManager.UI.Tests
{
    internal sealed class AssetManagerSearchTests
    {
        [TestCase(
            "avatar",
            AssetManagerSearchTarget.Name,
            true)]
        [TestCase(
            "warm",
            AssetManagerSearchTarget.Description,
            true)]
        [TestCase(
            "summ",
            AssetManagerSearchTarget.Tags,
            true)]
        [TestCase(
            "avatar",
            AssetManagerSearchTarget.Tags,
            false)]
        [TestCase(
            "summ",
            AssetManagerSearchTarget.None,
            false)]
        public void MatchesItem_UsesOnlyEnabledTargets(
            string search,
            AssetManagerSearchTarget targets,
            bool expected)
        {
            var item = new AssetItem
            {
                Name = "Summer Avatar",
                Description = "Warm weather outfit",
                Tags = new[]
                {
                    new AssetTag { Path = "season/summer" }
                }
            };

            Assert.That(
                AssetManagerSearch.MatchesItem(item, search, targets),
                Is.EqualTo(expected));
        }

        [TestCase(AssetManagerSearchTarget.Name, true)]
        [TestCase(AssetManagerSearchTarget.Description, false)]
        public void MatchesFile_UsesNameTarget(
            AssetManagerSearchTarget targets,
            bool expected)
        {
            var file = new AssetFile { FileName = "Avatar.unitypackage" };

            Assert.That(
                AssetManagerSearch.MatchesFile(file, "avatar", targets),
                Is.EqualTo(expected));
        }
    }
}
