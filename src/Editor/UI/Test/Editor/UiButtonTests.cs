using NUnit.Framework;

namespace Ee4v.UI.Tests
{
    public sealed class UiButtonTests
    {
        [Test]
        public void Text_UsesImguiFontCacheWorkaround()
        {
            var button = new UiButton(new UiButtonState(
                "Label",
                "Meta"));

            Assert.That(button.text, Is.Empty);
            Assert.That(
                button.LabelElement.GetType().Name,
                Is.EqualTo("ImguiUiTextElement"));
            Assert.That(
                button.MetaElement.GetType().Name,
                Is.EqualTo("ImguiUiTextElement"));
        }

    }
}
