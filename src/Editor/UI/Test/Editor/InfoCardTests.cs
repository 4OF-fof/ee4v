using System.Linq;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Ee4v.UI.Tests
{
    public sealed class InfoCardTests
    {
        [Test]
        public void HeaderTypography_UsesImguiFontCacheWorkaround()
        {
            var card = new InfoCard(new InfoCardState(
                "Preview",
                "Control changes are reflected immediately.",
                "Catalog"));

            var textElements = card.Query<UiTextElement>().ToList();

            Assert.That(textElements, Is.Not.Empty);
            Assert.That(
                textElements.All(element =>
                    element.GetType().Name == "ImguiUiTextElement"),
                Is.True);
        }
    }
}
