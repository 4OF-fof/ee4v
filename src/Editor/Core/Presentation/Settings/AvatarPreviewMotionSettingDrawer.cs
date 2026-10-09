using Ee4v.Core.EditorIntegration;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.Core.Settings
{
    [InitializeOnLoad]
    internal static class AvatarPreviewMotionSettingDrawer
    {
        static AvatarPreviewMotionSettingDrawer()
        {
            foreach (var definition in AvatarPreviewMotionSettings.Definitions)
                SettingDrawerApi.Register(definition, Create);
        }

        private static VisualElement Create(SettingDrawerContext<PreviewMotionSelection> context)
        {
            var value = context.Value ?? new PreviewMotionSelection();
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.alignItems = Align.Center;
            row.style.width = 360f;
            row.style.maxWidth = Length.Percent(100f);
            row.style.minHeight = 28f;
            var clip = UiTextFactory.CreateObjectField();
            clip.objectType = typeof(AnimationClip);
            clip.allowSceneObjects = false;
            clip.tooltip = context.Tooltip;
            clip.style.flexGrow = 1f;
            clip.style.flexShrink = 1f;
            clip.style.minWidth = 180f;
            clip.SetValueWithoutNotify(AvatarPreviewMotionSettings.ResolveClip(value));
            row.Add(clip);
            var loopRow = new VisualElement();
            loopRow.style.flexDirection = FlexDirection.Row;
            loopRow.style.alignItems = Align.Center;
            loopRow.style.flexShrink = 0f;
            loopRow.style.marginLeft = UiSpacingTokens.Small;
            loopRow.tooltip = I18N.GetForScope("Core", "settings.previewMotion.loopTooltip");
            var loop = UiTextFactory.CreateToggle();
            loop.style.width = loop.style.minWidth = 20f;
            loop.SetValueWithoutNotify(value.Loop);
            loopRow.Add(loop);
            var label = UiTextFactory.Create(I18N.GetForScope("Core", "settings.previewMotion.loop"));
            label.RegisterCallback<ClickEvent>(_ => loop.value = !loop.value);
            loopRow.Add(label);
            row.Add(loopRow);
            void Save() => context.NotifyValueChanged(AvatarPreviewMotionSettings.Capture(clip.value as AnimationClip, loop.value));
            clip.RegisterValueChangedCallback(_ => Save());
            loop.RegisterValueChangedCallback(_ => Save());
            return row;
        }
    }
}
