using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal abstract class UiTextElement : VisualElement
    {
        private string _text;

        protected UiTextElement(TypographyStyleDefinition style, params string[] classNames)
        {
            StyleDefinition = style ?? TypographyStyleDefinition.Default;

            if (classNames != null)
            {
                for (var i = 0; i < classNames.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(classNames[i]))
                    {
                        AddToClassList(classNames[i]);
                    }
                }
            }

            ApplyRootStyle();
        }

        protected TypographyStyleDefinition StyleDefinition { get; }

        public string Text
        {
            get { return _text; }
        }

        public void SetText(string text)
        {
            _text = text ?? string.Empty;
            ApplyText(_text);
        }

        public abstract void SetWhiteSpace(WhiteSpace whiteSpace);

        private void ApplyRootStyle()
        {
            if (StyleDefinition.MarginBottom > 0f)
            {
                style.marginBottom = StyleDefinition.MarginBottom;
            }

            if (StyleDefinition.MarginTop > 0f)
            {
                style.marginTop = StyleDefinition.MarginTop;
            }

            if (StyleDefinition.MarginLeft > 0f)
            {
                style.marginLeft = StyleDefinition.MarginLeft;
            }

            if (StyleDefinition.MarginRight > 0f)
            {
                style.marginRight = StyleDefinition.MarginRight;
            }

            if (StyleDefinition.WhiteSpace == WhiteSpace.Normal)
            {
                style.flexShrink = 1f;
            }
        }

        protected abstract void ApplyText(string text);
    }
}
