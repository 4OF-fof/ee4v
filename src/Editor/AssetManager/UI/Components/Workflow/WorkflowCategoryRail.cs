using System;
using System.Collections.Generic;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class WorkflowCategoryRail : VisualElement
    {
        private readonly Dictionary<WorkflowCategory, UiButton> _buttons =
            new Dictionary<WorkflowCategory, UiButton>();
        private readonly Action<WorkflowCategory> _selected;

        internal WorkflowCategoryRail(Action<WorkflowCategory> selected, bool playMode = false)
        {
            _selected = selected;
            AddToClassList("ee4v-modification-workflow__category-rail");
            if (playMode)
            {
                AddCategoryButton(WorkflowCategory.MenuAndGestures,
                    "workflow.category.menuAndGestures", "arrow_clockwise.png");
                return;
            }
            AddCategoryButton(
                WorkflowCategory.Overview,
                "workflow.category.overview",
                "info.png");
            AddCategoryButton(
                WorkflowCategory.ShapeParts,
                "workflow.category.shapeParts",
                "cube.png");
            AddCategoryButton(
                WorkflowCategory.Material,
                "workflow.category.material",
                "image.png");
            AddCategoryButton(
                WorkflowCategory.ExpressionAnimation,
                "workflow.category.expressionAnimation",
                "star.png");
        }

        private void AddCategoryButton(
            WorkflowCategory category,
            string labelKey,
            string icon)
        {
            var button = new UiButton(
                I18N.Get(labelKey),
                () => _selected?.Invoke(category),
                icon: AssetManagerControls.LoadFluentIconState(
                    icon,
                    UiSizeTokens.Size18),
                variant: UiButtonVariant.Ghost,
                labelTypographyClassName:
                    UiClassNames.NavigationItemLabel);
            button.AddToClassList(
                "ee4v-modification-workflow__category-button");
            _buttons[category] = button;
            Add(button);
        }

        internal void SetSelected(WorkflowCategory category)
        {
            foreach (var pair in _buttons)
            {
                pair.Value.EnableInClassList(
                    "ee4v-modification-workflow__category-button--active",
                    pair.Key == category);
            }
        }

        internal void SetPrefabScope(bool selected)
        {
            if (_buttons.TryGetValue(WorkflowCategory.ExpressionAnimation, out var expression))
            {
                expression.SetEnabled(!selected);
            }
        }
    }
}
