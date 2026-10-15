using System;
using System.Collections.Generic;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class WorkflowCategoryRail : VisualElement
    {
        private readonly Dictionary<WorkflowCategory, NavigationItem> _buttons =
            new Dictionary<WorkflowCategory, NavigationItem>();
        private readonly Action<WorkflowCategory> _selected;

        internal WorkflowCategoryRail(Action<WorkflowCategory> selected, bool playMode = false, bool avatar = true)
        {
            _selected = selected;
            AddToClassList("ee4v-modification-workflow__category-rail");
            Actions = new VisualElement();
            Actions.AddToClassList("ee4v-modification-workflow__category-actions");
            if (playMode)
            {
                if (avatar)
                    AddCategoryButton(WorkflowCategory.MenuAndGestures,
                        "workflow.category.menuAndGestures", "arrow_clockwise.png");
                AddCategoryButton(WorkflowCategory.Lighting,
                    "workflow.category.lighting", "weather_sunny.png");
                return;
            }
            AddCategoryButton(
                WorkflowCategory.Overview,
                "workflow.category.overview",
                "info.png");
            AddCategoryButton(
                WorkflowCategory.Appearance,
                "workflow.category.appearance",
                "cube.png");
            if (avatar)
            {
                AddCategoryButton(
                    WorkflowCategory.ExpressionAnimation,
                    "workflow.category.expressionAnimation",
                    "star.png");
                AddCategoryButton(WorkflowCategory.ExpressionMenu,
                    "workflow.category.expressionMenu", "arrow_clockwise.png");
            }
            Add(Actions);
        }

        internal VisualElement Actions { get; }

        private void AddCategoryButton(
            WorkflowCategory category,
            string labelKey,
            string icon)
        {
            var button = new NavigationItem(
                new NavigationItemState(I18N.Get(labelKey),
                icon: AssetManagerControls.LoadFluentIconState(
                    icon,
                    UiSizeTokens.Size18)),
                () => _selected?.Invoke(category));
            button.AddToClassList(
                "ee4v-modification-workflow__category-button");
            _buttons[category] = button;
            Add(button);
        }

        internal void SetSelected(WorkflowCategory category)
        {
            foreach (var pair in _buttons)
            {
                pair.Value.SetSelected(pair.Key == category);
            }
        }

        internal void SetPrefabScope(bool selected)
        {
            if (_buttons.TryGetValue(WorkflowCategory.ExpressionAnimation, out var expression))
            {
                expression.SetEnabled(!selected);
            }
            if (_buttons.TryGetValue(WorkflowCategory.ExpressionMenu, out var menu))
                menu.SetEnabled(!selected);
        }
    }
}
