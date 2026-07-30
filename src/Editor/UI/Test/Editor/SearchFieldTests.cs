using NUnit.Framework;
using System.Linq;
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

        [Test]
        public void SearchableTreeEmptyText_UsesImguiTypography()
        {
            var tree = new SearchableTreeView<string>(
                () => new VisualElement(),
                (_, __) => { },
                emptyText: "No files");

            var empty = tree.Query<UiTextElement>()
                .ToList()
                .Single(element =>
                    element.Text == "No files");

            Assert.That(
                empty.GetType().Name,
                Is.EqualTo("ImguiUiTextElement"));
        }

    }
}
