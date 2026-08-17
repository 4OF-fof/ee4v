using NUnit.Framework;
using UnityEngine.UIElements;

namespace Ee4v.UI.Tests
{
    public sealed class UiTextFactoryTests
    {
        [Test]
        public void FactoryButton_RoutesTextThroughUiTextElement()
        {
            var button = UiTextFactory.CreateButton("Run");

            Assert.That(((Button)button).text, Is.Empty);
            Assert.That(button.text, Is.EqualTo("Run"));
            Assert.That(
                button.TextElement.GetType().Name,
                Is.EqualTo("ImguiUiTextElement"));

            button.SetText("Stop");

            Assert.That(button.text, Is.EqualTo("Stop"));
        }

    }
}
