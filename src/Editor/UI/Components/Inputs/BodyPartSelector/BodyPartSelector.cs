using System;
using System.Collections.Generic;
using Ee4v.Core.I18n;
using Ee4v.Core.EditorIntegration;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class BodyPartSelector : VisualElement
    {
        private static readonly BodyPartCategory[] Parts =
        {
            BodyPartCategory.Head, BodyPartCategory.Shoulders, BodyPartCategory.Hands,
            BodyPartCategory.Chest, BodyPartCategory.Waist, BodyPartCategory.Legs,
            BodyPartCategory.Feet
        };
        private readonly List<(BodyPartCategory? Part, UiButton Button)> _buttons =
            new List<(BodyPartCategory?, UiButton)>();

        public BodyPartSelector(BodyPartCategory? selected,
            Func<BodyPartCategory, bool> available, Action<BodyPartCategory?> select)
        {
            AddToClassList("ee4v-ui-body-part-selector");
            AddPart(null, UiLocalization.Get("ui.bodyPart.wholeBody"), true, select);
            foreach (var part in Parts)
            {
                AddPart(part, UiLocalization.Get("ui.bodyPart." +
                    part.ToString().ToLowerInvariant()), available == null || available(part), select);
            }
            SetSelected(selected);
        }

        public void SetSelected(BodyPartCategory? selected)
        {
            foreach (var pair in _buttons)
            {
                pair.Button.EnableInClassList("ee4v-ui-body-part-selector__button--active",
                    pair.Part == selected);
            }
        }

        private void AddPart(BodyPartCategory? part, string label,
            bool enabled, Action<BodyPartCategory?> select)
        {
            var button = new UiButton(label, () => select?.Invoke(part),
                variant: UiButtonVariant.Ghost);
            button.AddToClassList("ee4v-ui-body-part-selector__button");
            button.SetEnabled(enabled);
            _buttons.Add((part, button));
            Add(button);
        }
    }
}
