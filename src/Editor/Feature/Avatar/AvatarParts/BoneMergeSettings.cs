using System;
using Ee4v.Core.I18n;
using Ee4v.UI;
using nadena.dev.modular_avatar.core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AvatarParts
{
    internal sealed class BoneMergeSettings : VisualElement
    {
        internal BoneMergeSettings(ModularAvatarMergeArmature merge, string sourcePath,
            Func<GameObject, bool> targetChanged)
        {
            AddToClassList("ee4v-bone-merge-settings");
            Add(UiTextFactory.Create(I18N.Get("workflow.attachment.mergeTitle"), UiClassNames.SectionTitle,
                "ee4v-bone-merge-settings__title"));

            var source = UiTextFactory.Create(sourcePath, "ee4v-bone-merge-settings__source");
            source.SetWhiteSpace(WhiteSpace.Normal);
            source.tooltip = sourcePath;
            AddField(this, I18N.Get("workflow.attachment.mergeSource"), source);

            var target = UiTextFactory.CreateObjectField();
            target.objectType = typeof(GameObject);
            target.allowSceneObjects = true;
            target.SetValueWithoutNotify(merge.mergeTargetObject);
            target.RegisterValueChangedCallback(evt =>
            {
                if (!targetChanged(evt.newValue as GameObject))
                { target.SetValueWithoutNotify(merge.mergeTargetObject); }
            });
            AddField(this, I18N.Get("workflow.attachment.mergeTarget"), target)
                .AddToClassList("ee4v-bone-merge-settings__field--last");
        }

        private static FormInput AddField(VisualElement parent, string label, VisualElement value)
        {
            var field = new FormInput(label, value);
            field.AddToClassList("ee4v-bone-merge-settings__field");
            parent.Add(field);
            return field;
        }
    }
}
