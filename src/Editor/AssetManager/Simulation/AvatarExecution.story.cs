using System.Collections.Generic;
using Ee4v.UI;
using UnityEngine;
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
                    "専用Preview Sceneのダミーアバターで、手の入力状態をメモリ内だけで切り替えます。",
                    Build, dependencies: new[] { "PreviewPane", "GestureManager" },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetModificationWorkflowView.Workspace.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/Simulation/avatar-execution.uss",
                        "Editor/AssetManager/UI/asset-modification-workflow.uss",
                        "Editor/AssetManager/UI/Components/Workflow/workflow-story.uss"
                    }),
                new UiStory("avatar-lighting-view", "Domain/AssetManager/Containers", "AvatarLightingView",
                    "プレビューを追加・削除し、右ペインのライト設定と保存したプリセットで比較します。", "専用Preview Sceneのダミーアバターと、メモリ内のプリセットで比較します。",
                    BuildLighting, usageLocations: new[]
                    { "Editor/AssetManager/UI/AssetModificationWorkflowView.Workspace.cs" },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/Simulation/avatar-execution.uss",
                        "Editor/AssetManager/UI/Components/Workflow/workflow-story.uss"
                    })
            };
        }

        private static void Build(VisualElement parent)
        {
            var surface = new VisualElement();
            surface.AddToClassList("ee4v-workflow-story");
            surface.AddToClassList("ee4v-workflow-story--avatar-execution-view");
            surface.style.width = 1360;
            var avatar = CreateAvatar(surface);
            var view = new AvatarExecutionView(avatar, parent.MarkDirtyRepaint, previewOnly: true);
            view.RegisterCallback<DetachFromPanelEvent>(evt => { if (evt.target == view) { view.Dispose(); } });
            surface.Add(view);
            parent.Add(surface);
        }

        private static void BuildLighting(VisualElement parent)
        {
            var surface = new VisualElement();
            surface.AddToClassList("ee4v-workflow-story");
            surface.AddToClassList("ee4v-workflow-story--avatar-lighting-view");
            surface.style.width = 1360;
            var avatar = CreateAvatar(surface);
            var view = new AvatarLightingView(avatar, parent.MarkDirtyRepaint, previewOnly: true);
            view.RegisterCallback<DetachFromPanelEvent>(evt => { if (evt.target == view) { view.Dispose(); } });
            surface.Add(view);
            parent.Add(surface);
        }

        private static GameObject CreateAvatar(VisualElement surface)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var avatar = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            avatar.name = "Sample Avatar";
            avatar.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(avatar, scene);
            surface.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                if (evt.target != surface) { return; }
                UnityEngine.Object.DestroyImmediate(avatar);
                if (scene.IsValid()) { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
            });
            return avatar;
        }
    }
}
