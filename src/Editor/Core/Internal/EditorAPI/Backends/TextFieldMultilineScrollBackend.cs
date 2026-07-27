using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.Core.Internal.EditorAPI.Backends
{
    internal static class TextFieldMultilineScrollBackend
    {
        private static readonly FieldInfo TextInputBaseField = FindField(typeof(TextField), "m_TextInputBase");
        private static readonly FieldInfo TextInputBaseScrollViewField = FindNestedField("scrollView");
        private static readonly MethodInfo SetScrollViewModeMethod = FindNestedMethod("SetScrollViewMode");
        private static readonly MethodInfo SetVerticalScrollerVisibilityMethod = FindNestedMethod("SetVerticalScrollerVisibility");

        public static bool Configure(TextField textField, bool useVerticalScroll, float maxHeight)
        {
            if (textField == null)
            {
                return false;
            }

            var textInputBase = TextInputBaseField == null ? null : TextInputBaseField.GetValue(textField);
            if (textInputBase == null)
            {
                return false;
            }

            var visibility = useVerticalScroll ? ScrollerVisibility.Auto : ScrollerVisibility.Hidden;
            if (SetScrollViewModeMethod != null)
            {
                SetScrollViewModeMethod.Invoke(textInputBase, null);
            }

            if (SetVerticalScrollerVisibilityMethod != null)
            {
                SetVerticalScrollerVisibilityMethod.Invoke(textInputBase, new object[] { visibility });
            }

            var scrollView = TextInputBaseScrollViewField == null
                ? null
                : TextInputBaseScrollViewField.GetValue(textInputBase) as ScrollView;
            if (scrollView == null)
            {
                return false;
            }

            scrollView.verticalScrollerVisibility = visibility;
            scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scrollView.style.maxHeight = useVerticalScroll
                ? new StyleLength(Mathf.Max(0f, maxHeight))
                : new StyleLength(StyleKeyword.Null);
            StyleVerticalScroller(scrollView.verticalScroller, useVerticalScroll);
            return true;
        }

        private static void StyleVerticalScroller(Scroller scroller, bool visible)
        {
            if (scroller == null)
            {
                return;
            }

            scroller.style.width = visible ? 8f : 0f;
            scroller.style.minWidth = visible ? 8f : 0f;
            scroller.style.marginTop = 0f;
            scroller.style.marginBottom = 0f;
            scroller.style.paddingTop = 0f;
            scroller.style.paddingBottom = 0f;
            scroller.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

            HideScrollerButton(scroller.lowButton);
            HideScrollerButton(scroller.highButton);
            StyleScrollerSlider(scroller.slider);
        }

        private static void StyleScrollerSlider(Slider slider)
        {
            if (slider == null)
            {
                return;
            }

            slider.style.flexGrow = 1f;
            slider.style.flexShrink = 1f;
            slider.style.height = new StyleLength(StyleKeyword.Auto);
            slider.style.minHeight = 0f;
            slider.style.marginTop = 0f;
            slider.style.marginBottom = 0f;
            slider.style.paddingTop = 0f;
            slider.style.paddingBottom = 0f;
            slider.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            ClearVerticalSpacing(slider);
        }

        private static void ClearVerticalSpacing(VisualElement element)
        {
            var children = element.Query<VisualElement>().ToList();
            for (var i = 0; i < children.Count; i++)
            {
                children[i].style.marginTop = 0f;
                children[i].style.marginBottom = 0f;
                children[i].style.paddingTop = 0f;
                children[i].style.paddingBottom = 0f;
            }
        }

        private static void HideScrollerButton(VisualElement button)
        {
            if (button == null)
            {
                return;
            }

            button.style.display = DisplayStyle.None;
            button.style.visibility = Visibility.Hidden;
            button.style.height = 0f;
            button.style.minHeight = 0f;
            button.style.maxHeight = 0f;
            button.style.marginTop = 0f;
            button.style.marginBottom = 0f;
            button.style.paddingTop = 0f;
            button.style.paddingBottom = 0f;
            button.pickingMode = PickingMode.Ignore;
        }

        private static FieldInfo FindField(Type type, string fieldName)
        {
            while (type != null)
            {
                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static FieldInfo FindNestedField(string fieldName)
        {
            var textInputBaseType = FindTextInputBaseType();
            return textInputBaseType == null
                ? null
                : textInputBaseType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static MethodInfo FindNestedMethod(string methodName)
        {
            var textInputBaseType = FindTextInputBaseType();
            return textInputBaseType == null
                ? null
                : textInputBaseType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static Type FindTextInputBaseType()
        {
            var textInputBaseField = TextInputBaseField ?? FindField(typeof(TextField), "m_TextInputBase");
            return textInputBaseField == null ? null : textInputBaseField.FieldType;
        }
    }
}
