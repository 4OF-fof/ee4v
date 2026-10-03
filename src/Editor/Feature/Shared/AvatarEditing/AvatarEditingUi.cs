using System;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AvatarEditing
{
    public static class AvatarEditingUi
    {
        public static void AddFeedback(AvatarEditingContext context, VisualElement panel)
        {
            if (string.IsNullOrWhiteSpace(context.Feedback))
            {
                return;
            }
            panel.Add(UiTextFactory.CreateHelpBox(
                context.Feedback,
                context.FeedbackType,
                "ee4v-modification-workflow__feedback"));
        }

        public static VisualElement CreateEmptyState(
            string titleKey,
            string descriptionKey)
        {
            var empty = new VisualElement();
            empty.AddToClassList("ee4v-ui-empty-state");
            empty.AddToClassList(
                "ee4v-modification-workflow__empty-state");
            empty.Add(UiTextFactory.Create(
                I18N.Get(titleKey),
                UiClassNames.SectionTitle,
                "ee4v-ui-empty-state__title"));
            var description = UiTextFactory.Create(
                I18N.Get(descriptionKey),
                UiClassNames.SecondaryText,
                "ee4v-ui-empty-state__description");
            description.SetWhiteSpace(WhiteSpace.Normal);
            empty.Add(description);
            return empty;
        }

        public static void MarkLastListItem(VisualElement list)
        {
            if (list.childCount == 0)
            {
                return;
            }

            var last = list[list.childCount - 1];
            var isPrefabGroup = last.ClassListContains(
                "ee4v-modification-workflow__prefab-group");
            var isObjectGroup = last.ClassListContains(
                "ee4v-modification-workflow__object-group");
            if (!isPrefabGroup && !isObjectGroup)
            {
                last.AddToClassList(
                    "ee4v-modification-workflow__list-last-row");
                return;
            }

            last.AddToClassList(
                isPrefabGroup
                    ? "ee4v-modification-workflow__prefab-group--last"
                    : "ee4v-modification-workflow__object-group--last");
            MarkLastListItem(last[last.childCount - 1]);
        }

        public static UiButton CreatePrefabVisibilityButton(
            bool isVisible,
            string tooltip,
            Action onClick,
            string className)
        {
            var button = new UiButton(
                string.Empty,
                onClick,
                variant: UiButtonVariant.Ghost);
            button.AddToClassList(className);
            button.tooltip = tooltip;
            button.SetIcon(FluentUiIcons.CreateState(
                isVisible
                    ? "eye.png"
                    : "eye_off.png",
                UiSizeTokens.Size18,
                tooltip));
            return button;
        }

        public static UiButton CreateIconButton(string tooltip, string iconFileName,
            float iconSize, UiButtonVariant variant, Action onClick = null, params string[] classNames)
        {
            var icon = FluentUiIcons.CreateState(iconFileName, iconSize, tooltip);
            var button = new UiButton(icon == null ? tooltip : string.Empty, onClick, tooltip, icon, variant);
            foreach (var className in classNames) { button.AddToClassList(className); }
            return button;
        }
    }
}
