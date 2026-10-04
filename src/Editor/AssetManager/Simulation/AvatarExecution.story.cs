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
                    "AvatarExecutionView", "PlayModeの操作確認用にアバター描画とGestureManagerの入力をまとめます。",
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
                    }),
                new UiStory("avatar-lighting-view", "Domain/AssetManager/Containers", "AvatarLightingView",
                    "通常照明とVRCLVのプリセットを選び、分割プレビューで比較します。", "対象がない実Viewのプリセットと閉じた詳細設定を表示します。",
                    BuildLighting, usageLocations: new[]
                    { "Editor/AssetManager/UI/AssetModificationWorkflowView.Workspace.cs" },
                    styleSheetPaths: new[] { "Editor/AssetManager/Simulation/avatar-execution.uss" })
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

        private static void BuildLighting(VisualElement parent)
        {
            var view = new AvatarLightingView(null, () => { });
            view.SetEnabled(false);
            view.RegisterCallback<DetachFromPanelEvent>(_ => view.Dispose());
            parent.Add(view);
        }
    }
}
