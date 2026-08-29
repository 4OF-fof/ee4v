using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public static class UiTextFactory
    {
        private const string StandardInputFocusedClassName =
            "ee4v-ui-standard-input--focused";

        public static UiTextElement Create(string text = "", params string[] classNames)
        {
            var resolution = TypographyStyleResolver.Resolve(classNames);
            if (resolution.Style.RequiresImgui)
            {
                return new ImguiUiTextElement(text, resolution.Style, classNames);
            }

            return new LabelUiTextElement(text, resolution.Style, classNames);
        }

        public static Toggle CreateToggle(
            string text = "",
            params string[] classNames)
        {
            var toggle = ConfigureNativeInput(new Toggle(text), classNames);
            var checkmark = toggle.Q<VisualElement>(
                className: Toggle.checkmarkUssClassName);
            if (checkmark != null)
            {
                checkmark.style.backgroundImage = StyleKeyword.None;
                var checkmarkTexture =
                    FluentUiIcons.LoadTexture("checkmark.png");
                if (checkmarkTexture != null)
                {
                    checkmark.style.backgroundImage =
                        new StyleBackground(checkmarkTexture);
                }
            }

            return toggle;
        }

        internal static TextField CreateTextField(
            string label = "",
            params string[] classNames)
        {
            return ConfigureNativeInput(new TextField(label), classNames);
        }

        public static IntegerField CreateIntegerField(
            string label = "",
            params string[] classNames)
        {
            return ConfigureNativeInput(new IntegerField(label), classNames);
        }

        public static FloatField CreateFloatField(
            string label = "",
            params string[] classNames)
        {
            return ConfigureNativeInput(new FloatField(label), classNames);
        }

        public static DoubleField CreateDoubleField(
            string label = "",
            params string[] classNames)
        {
            return ConfigureNativeInput(new DoubleField(label), classNames);
        }

        public static ColorField CreateColorField(
            string label = "",
            params string[] classNames)
        {
            return ConfigureNativeInput(new ColorField(label), classNames);
        }

        public static EnumField CreateEnumField(
            string label,
            Enum value,
            params string[] classNames)
        {
            return ConfigureNativeInput(new EnumField(label, value), classNames);
        }

        public static ObjectField CreateObjectField(
            string label = "",
            params string[] classNames)
        {
            return ConfigureNativeInput(new ObjectField(label), classNames);
        }

        public static PopupField<T> CreatePopupField<T>(
            string label,
            List<T> choices,
            int defaultIndex,
            params string[] classNames)
        {
            return ConfigureNativeInput(
                new PopupField<T>(label, choices, defaultIndex),
                classNames);
        }

        public static PopupField<T> CreatePopupField<T>(
            string label,
            List<T> choices,
            int defaultIndex,
            Func<T, string> formatSelectedValue,
            Func<T, string> formatListItem,
            params string[] classNames)
        {
            return ConfigureNativeInput(
                new PopupField<T>(
                    label,
                    choices,
                    defaultIndex,
                    formatSelectedValue,
                    formatListItem),
                classNames);
        }

        public static HelpBox CreateHelpBox(
            string text,
            HelpBoxMessageType messageType,
            params string[] classNames)
        {
            return ConfigureNativeTextElement(
                new HelpBox(text ?? string.Empty, messageType),
                classNames);
        }

        public static GUIContent CreateGuiContent(string text = "")
        {
            return new GUIContent(text ?? string.Empty);
        }

        public static void SetText(HelpBox helpBox, string text)
        {
            if (helpBox == null)
            {
                throw new ArgumentNullException(nameof(helpBox));
            }

            helpBox.text = text ?? string.Empty;
        }

        private static T ConfigureNativeInput<T>(
            T element,
            params string[] classNames)
            where T : VisualElement
        {
            var configured = ConfigureNativeTextElement(element, classNames);
            configured.AddToClassList(UiClassNames.StandardInput);
            configured.RegisterCallback<FocusInEvent>(_ =>
                configured.AddToClassList(StandardInputFocusedClassName));
            configured.RegisterCallback<FocusOutEvent>(_ =>
                configured.RemoveFromClassList(StandardInputFocusedClassName));
            return configured;
        }

        private static T ConfigureNativeTextElement<T>(
            T element,
            params string[] classNames)
            where T : VisualElement
        {
            var resolution = TypographyStyleResolver.Resolve(classNames);
            AddClassNames(element, classNames);
            element.style.fontSize = resolution.Style.FontSize;
            element.style.color = resolution.Style.Color;
            element.style.unityTextAlign = resolution.Style.Alignment;
            element.style.whiteSpace = resolution.Style.WhiteSpace;
            return element;
        }

        private static void AddClassNames(
            VisualElement element,
            string[] classNames)
        {
            if (classNames == null)
            {
                return;
            }

            for (var i = 0; i < classNames.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(classNames[i]))
                {
                    element.AddToClassList(classNames[i]);
                }
            }
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

            public override void SetColor(Color color)
            {
                _label.style.color = color;
            }

            public override void SetTextAlign(TextAnchor alignment)
            {
                _label.style.unityTextAlign = alignment;
            }

            public override void SetFontSize(int fontSize)
            {
                _label.style.fontSize = Mathf.Max(1, fontSize);
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
                    fontStyle = StyleDefinition.FontStyle,
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

            public override void SetColor(Color color)
            {
                _guiStyle.normal.textColor = color;
                _container.MarkDirtyRepaint();
            }

            public override void SetTextAlign(TextAnchor alignment)
            {
                _guiStyle.alignment = alignment;
                _container.MarkDirtyRepaint();
            }

            public override void SetFontSize(int fontSize)
            {
                _guiStyle.fontSize = Mathf.Max(1, fontSize);
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
