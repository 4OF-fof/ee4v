using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class PreviewPaneStoryProvider : IUiStoryProvider
    {
        public int Order => 24;

        public IReadOnlyList<UiStory> GetStories()
        {
            return new[]
            {
                new UiStory("preview-pane", "Containers", "PreviewPane",
                    "プレビューの見出し、操作と描画内容を配置する外枠です。",
                    "描画方法に依存しないContentとActionsを、編集用・実行用プレビューで共有します。",
                    Build, dependencies: new[] { "UiTextFactory", "UiButton" },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetModificationWorkflowView.Workspace.cs",
                        "Editor/AssetManager/Simulation/AvatarExecutionView.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/UI/Components/Content/PreviewPane/preview-pane.uss"
                    })
            };
        }

        private static void Build(VisualElement parent)
        {
            var surface = new VisualElement();
            surface.AddToClassList("ee4v-ui-catalog-preview-pane-story");
            var pane = new PreviewPane("Preview");
            pane.Content.Add(new EmptyState(new EmptyStateState(
                "プレビュー内容", "描画する部品をこの領域へ配置します。")));
            pane.Actions.Add(new UiButton("見出しを変更", () => pane.SetTitle("Updated preview"),
                variant: UiButtonVariant.Ghost));
            surface.Add(pane);
            var controls = new InfoCard(new InfoCardState("コントロール"));
            var enabled = UiTextFactory.CreateToggle();
            enabled.SetValueWithoutNotify(true);
            enabled.RegisterValueChangedCallback(evt => pane.SetEnabled(evt.newValue));
            controls.Body.Add(new FormInput("有効", enabled));
            parent.Add(controls);
            var preview = new InfoCard(new InfoCardState("プレビュー"));
            preview.Body.Add(surface);
            parent.Add(preview);
        }
    }
}
