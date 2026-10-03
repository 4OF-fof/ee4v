using System.Collections.Generic;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.Simulation
{
    internal sealed class AvatarExecutionStoryProvider : IUiStoryProvider
    {
        public int Order => 202;

        public IReadOnlyList<UiStory> GetStories()
        {
            return new[]
            {
                new UiStory("avatar-execution-view", "Domain/AssetManager/Containers",
                    "AvatarExecutionView", "実行アバターの描画とGestureManagerの入力をまとめます。",
                    "対象がない状態の実Viewを表示します。StoryからPlay ModeやSceneを変更しないよう操作を無効にします。",
                    Build, dependencies: new[] { "PreviewPane", "GestureManager" },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetModificationWorkflowView.Workspace.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-modification-workflow.uss",
                        "Editor/AssetManager/UI/Components/Workflow/workflow-story.uss"
                    })
            };
        }

        private static void Build(VisualElement parent)
        {
            var surface = new VisualElement();
            surface.AddToClassList("ee4v-workflow-story");
            surface.AddToClassList("ee4v-workflow-story--avatar-execution-view");
            var view = new AvatarExecutionView(null, () => { });
            view.SetEnabled(false);
            view.RegisterCallback<DetachFromPanelEvent>(_ => view.Dispose());
            surface.Add(view);
            parent.Add(surface);
        }
    }
}
