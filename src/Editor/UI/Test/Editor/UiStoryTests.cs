using System.Linq;
using NUnit.Framework;

namespace Ee4v.UI.Tests
{
    public sealed class UiStoryTests
    {
        [Test]
        public void Catalog_DiscoversExternalStoryProviders()
        {
            Assert.That(
                CatalogWindow
                    .GetRegisteredStoriesForTests()
                    .Any(story => story.Id == "color-palette"),
                Is.True);
        }

        [Test]
        public void CatalogStories_DeclareUsageLocations()
        {
            var stories = CatalogWindow.GetRegisteredStoriesForTests();

            Assert.That(
                stories
                    .Where(story => story.Group != "Reference")
                    .All(story => story.UsageLocations.Count > 0),
                Is.True);
        }

    }
}
