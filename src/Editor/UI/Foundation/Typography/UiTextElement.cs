using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public abstract class UiTextElement : VisualElement
    {
        private string _text;

        internal UiTextElement(
            TypographyStyleDefinition style,
            params string[] classNames)
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

        internal TypographyStyleDefinition StyleDefinition { get; }

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

        public abstract void SetColor(Color color);

        public abstract void SetTextAlign(TextAnchor alignment);

        public abstract void SetFontSize(int fontSize);

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
