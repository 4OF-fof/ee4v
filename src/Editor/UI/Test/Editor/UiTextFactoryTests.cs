using NUnit.Framework;
using UnityEngine.UIElements;

namespace Ee4v.UI.Tests
{
    public sealed class UiTextFactoryTests
    {
        [Test]
        public void UiButton_UpdatesDisplayedText()
        {
            var button = new UiButton("Run");
            var label = button.Content.Q<UiTextElement>();

            Assert.That(label.Text, Is.EqualTo("Run"));
            button.SetLabel("Stop");

            Assert.That(label.Text, Is.EqualTo("Stop"));
        }

    }
}
