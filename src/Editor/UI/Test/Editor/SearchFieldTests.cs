using NUnit.Framework;
using UnityEngine.UIElements;

namespace Ee4v.UI.Tests
{
    public sealed class SearchFieldTests
    {
        [Test]
        public void Placeholder_UsesSearchFieldLayoutAndImguiTypography()
        {
            var field = new SearchField(new SearchFieldState(
                placeholder: "Search files"));

            var placeholder = field.Q<UiTextElement>(
                className:
                "ee4v-ui-search-field__placeholder");

            Assert.That(placeholder, Is.Not.Null);
            Assert.That(
                placeholder.ClassListContains(
                    UiClassNames.InputPlaceholder),
                Is.True);
            Assert.That(
                placeholder.GetType().Name,
                Is.EqualTo("ImguiUiTextElement"));
        }

    }
}
