using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal static class UiTextFactory
    {
        public static UiTextElement Create(string text = "", params string[] classNames)
        {
            var resolution = TypographyStyleResolver.Resolve(classNames);
            if (resolution.Style.RequiresImgui)
            {
                return new ImguiUiTextElement(text, resolution.Style, classNames);
            }

            return new LabelUiTextElement(text, resolution.Style, classNames);
        }

        private sealed class LabelUiTextElement : UiTextElement
        {
            private readonly Label _label;

            public LabelUiTextElement(string text, TypographyStyleDefinition style, params string[] classNames)
                : base(style, classNames)
            {
                _label = new Label();
                _label.pickingMode = PickingMode.Ignore;
                _label.style.fontSize = StyleDefinition.FontSize;
                _label.style.color = StyleDefinition.Color;
                _label.style.unityTextAlign = StyleDefinition.Alignment;
                _label.style.whiteSpace = StyleDefinition.WhiteSpace;
                _label.style.flexShrink = 1f;
                Add(_label);
                SetText(text);
            }

            protected override void ApplyText(string text)
            {
                _label.text = text;
                _label.style.display = string.IsNullOrWhiteSpace(text) ? DisplayStyle.None : DisplayStyle.Flex;
                style.display = string.IsNullOrWhiteSpace(text) ? DisplayStyle.None : DisplayStyle.Flex;
            }

            public override void SetWhiteSpace(WhiteSpace whiteSpace)
            {
                _label.style.whiteSpace = whiteSpace;
            }
        }

        private sealed class ImguiUiTextElement : UiTextElement
        {
            private readonly IMGUIContainer _container;
            private readonly GUIStyle _guiStyle;
            private WhiteSpace _whiteSpace;

            public ImguiUiTextElement(string text, TypographyStyleDefinition style, params string[] classNames)
                : base(style, classNames)
            {
                _whiteSpace = StyleDefinition.WhiteSpace;
                _guiStyle = new GUIStyle
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = StyleDefinition.FontSize,
                    alignment = StyleDefinition.Alignment,
                    wordWrap = _whiteSpace == WhiteSpace.Normal,
                    richText = false,
                    clipping = _whiteSpace == WhiteSpace.Normal ? TextClipping.Clip : TextClipping.Overflow
                };
                _guiStyle.normal.textColor = StyleDefinition.Color;

                _container = new IMGUIContainer(Draw)
                {
                    pickingMode = PickingMode.Ignore
                };
                _container.style.flexShrink = 1f;
                Add(_container);
                RegisterCallback<GeometryChangedEvent>(_ => UpdateMeasure());
                SetText(text);
            }

            protected override void ApplyText(string text)
            {
                var visible = !string.IsNullOrWhiteSpace(text);
                _container.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                UpdateMeasure();
                _container.MarkDirtyRepaint();
            }

            public override void SetWhiteSpace(WhiteSpace whiteSpace)
            {
                _whiteSpace = whiteSpace;
                _guiStyle.wordWrap = whiteSpace == WhiteSpace.Normal;
                _guiStyle.clipping = whiteSpace == WhiteSpace.Normal ? TextClipping.Clip : TextClipping.Overflow;
                UpdateMeasure();
                _container.MarkDirtyRepaint();
            }

            private void Draw()
            {
                if (string.IsNullOrWhiteSpace(Text))
                {
                    return;
                }

                GUI.Label(_container.contentRect, Text, _guiStyle);
            }

            private void UpdateMeasure()
            {
                if (string.IsNullOrWhiteSpace(Text))
                {
                    return;
                }

                var content = new GUIContent(Text);
                if (_whiteSpace == WhiteSpace.Normal)
                {
                    var width = resolvedStyle.width;
                    if (float.IsNaN(width) || width <= 0f)
                    {
                        return;
                    }

                    var height = Mathf.Max(18f, Mathf.Ceil(_guiStyle.CalcHeight(content, width)));
                    _container.style.height = height;
                    return;
                }

                var size = _guiStyle.CalcSize(content);
                _container.style.width = Mathf.Ceil(size.x);
                _container.style.height = Mathf.Max(18f, Mathf.Ceil(size.y));
            }
        }
    }
}
