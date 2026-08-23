using NUnit.Framework;

namespace Ee4v.UI.Tests
{
    public sealed class UiTextFactoryTests
    {
        [Test]
        public void FactoryButton_UpdatesDisplayedText()
        {
            var button = UiTextFactory.CreateButton("Run");

            Assert.That(button.TextElement.Text, Is.EqualTo("Run"));
            button.SetText("Stop");

            Assert.That(button.TextElement.Text, Is.EqualTo("Stop"));
        }

    }
}
