using NUnit.Framework;
using UnityEngine.UIElements;

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

        [Test]
        public void State_AppliesSharedVisualVariants()
        {
            var button = new UiButton(new UiButtonState(
                "Selected",
                iconState: IconState.FromBuiltinIcon(
                    UiBuiltinIcon.Package,
                    UiSizeTokens.Size12),
                selected: true,
                variant: UiButtonVariant.Ghost,
                size: UiButtonSize.Compact));

            Assert.That(button.Selected, Is.True);
            Assert.That(
                button.ClassListContains(
                    "ee4v-ui-button--ghost"),
                Is.True);
            Assert.That(
                button.ClassListContains(
                    "ee4v-ui-button--compact"),
                Is.True);
            Assert.That(
                button.ClassListContains(
                    "ee4v-ui-button--selected"),
                Is.True);
            Assert.That(
                button.IconElement.style.display.value,
                Is.EqualTo(DisplayStyle.Flex));
        }
    }
}
