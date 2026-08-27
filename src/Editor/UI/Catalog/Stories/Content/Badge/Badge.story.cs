using System;
using Ee4v.Core.I18n;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private enum BadgeStoryTone
        {
            Neutral,
            Idle,
            Running,
            Passed,
            Failed,
            Skipped,
            Inconclusive
        }

        private sealed class BadgeCatalogRegistrar : ICatalogRegistrar
        {
            public int Order => 10;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStory(new StoryRegistration(
                    "badge", "Labels", "Badge",
                    "件数、短い分類値、処理状態を表示するバッジです。",
                    "中立表示とUiStatusToneによる状態色を一つの部品で切り替えます。",
                    new string[0], ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildBadgeStory(parent),
                    new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.cs",
                        "Editor/AssetManager/UI/AssetDetailComponents.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/BlendShapePresetView.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionGroupView.cs",
                        "Editor/UI/Components/Content/InfoCard/InfoCard.cs"
                    }));
            }
        }

        private void BuildBadgeStory(VisualElement parent)
        {
            var text = "12";
            var tone = BadgeStoryTone.Neutral;
            Action refresh = null;
            Action<BadgeStoryTone> applyPreset = selectedTone =>
            {
                tone = selectedTone;
                switch (selectedTone)
                {
                    case BadgeStoryTone.Idle:
                        text = I18N.Get("catalog.status.idle");
                        break;
                    case BadgeStoryTone.Running:
                        text = I18N.Get("catalog.status.running");
                        break;
                    case BadgeStoryTone.Passed:
                        text = I18N.Get("catalog.status.passed");
                        break;
                    case BadgeStoryTone.Failed:
                        text = I18N.Get("catalog.status.failed");
                        break;
                    case BadgeStoryTone.Skipped:
                        text = I18N.Get("catalog.status.skipped");
                        break;
                    case BadgeStoryTone.Inconclusive:
                        text = I18N.Get("catalog.status.inconclusive");
                        break;
                    default:
                        text = "12";
                        break;
                }

                refresh?.Invoke();
            };
            var controls = CreatePlainControlsSection(
                parent,
                "表示文字と中立・状態色を切り替えます。");
            var textField = AddTextField(
                controls.Content,
                "テキスト",
                text,
                value =>
                {
                    text = value;
                    refresh();
                });
            var toneField = AddEnumField(
                controls.Content,
                "種類",
                tone,
                applyPreset);

            var preview = CreatePreviewSection(parent);
            var badge = new Badge();
            preview.Body.Add(CreatePreviewArea(badge, true));

            refresh = () =>
            {
                textField.SetValueWithoutNotify(text);
                toneField.SetValueWithoutNotify((Enum)(object)tone);
                badge.SetState(new BadgeState(text, ToStatusTone(tone)));
            };

            applyPreset(tone);
            FinalizeControlsSection(parent, controls);
        }

        private static UiStatusTone? ToStatusTone(BadgeStoryTone tone)
        {
            switch (tone)
            {
                case BadgeStoryTone.Idle:
                    return UiStatusTone.Idle;
                case BadgeStoryTone.Running:
                    return UiStatusTone.Running;
                case BadgeStoryTone.Passed:
                    return UiStatusTone.Passed;
                case BadgeStoryTone.Failed:
                    return UiStatusTone.Failed;
                case BadgeStoryTone.Skipped:
                    return UiStatusTone.Skipped;
                case BadgeStoryTone.Inconclusive:
                    return UiStatusTone.Inconclusive;
                default:
                    return null;
            }
        }
    }
}
