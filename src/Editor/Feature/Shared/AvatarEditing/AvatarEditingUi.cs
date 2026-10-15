using System;
using System.Collections.Generic;
using Ee4v.Core.EditorIntegration;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AvatarEditing
{
    public static class AvatarEditingUi
    {
        private sealed class SelectionRevealEvent : EventBase<SelectionRevealEvent>
        {
            public SelectionRevealEvent() { }

            protected override void Init()
            {
                base.Init();
                bubbles = true;
            }
        }

        public static void RegisterSelectionReveal(VisualElement content, Action expand)
        {
            content.RegisterCallback<SelectionRevealEvent>(_ => expand());
        }

        public static void RevealSelection(VisualElement row)
        {
            using (var evt = SelectionRevealEvent.GetPooled())
            {
                evt.target = row;
                row.SendEvent(evt);
            }
        }

        public static bool IsRowActionTarget(VisualElement row, VisualElement target)
        {
            for (var element = target; element != null && element != row; element = element.parent)
            {
                if (element is Button || element is Toggle) { return true; }
            }
            return false;
        }

        public static ExclusiveButtonGroup<BodyPartCategory?> CreateBodyPartSelector(
            BodyPartCategory? selected, Func<BodyPartCategory, bool> available, Action<BodyPartCategory?> select)
        {
            var options = new List<ExclusiveButtonOption<BodyPartCategory?>>
            { new ExclusiveButtonOption<BodyPartCategory?>(null, I18N.Get("avatarEditor.bodyPart.wholeBody")) };
            foreach (var part in new[] { BodyPartCategory.Head, BodyPartCategory.Shoulders, BodyPartCategory.Hands,
                BodyPartCategory.Chest, BodyPartCategory.Waist, BodyPartCategory.Legs, BodyPartCategory.Feet })
            {
                options.Add(new ExclusiveButtonOption<BodyPartCategory?>(part,
                    I18N.Get("avatarEditor.bodyPart." + part.ToString().ToLowerInvariant())));
            }
            return new ExclusiveButtonGroup<BodyPartCategory?>(options, selected, select,
                part => !part.HasValue || available == null || available(part.Value));
        }

        public static IconState CreateBlendShapeFavoriteIcon(bool selected)
        {
            return FluentUiIcons.CreateState(selected ? "star_20.png" : "star_regular_20.png",
                UiSizeTokens.Size12, tintColor: selected
                    ? new UnityEngine.Color32(255, 205, 64, 255)
                    : UiColorTokens.TextPrimary);
        }

        public static void AddFeedback(AvatarEditingContext context, VisualElement panel)
        {
            if (string.IsNullOrWhiteSpace(context.Feedback))
            {
                return;
            }
            panel.Add(UiTextFactory.CreateMessagePanel(
                context.Feedback,
                context.FeedbackType,
                "ee4v-modification-workflow__feedback"));
        }

        public static VisualElement CreateEmptyState(
            string title,
            string description)
        {
            var empty = new EmptyState(new EmptyStateState(title, description));
            empty.AddToClassList(
                "ee4v-modification-workflow__empty-state");
            empty.Actions.style.display = DisplayStyle.None;
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
