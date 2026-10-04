using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.UI;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class WorkflowUiStoryProvider : IUiStoryProvider
    {
        public int Order => 201;

        private static readonly string[] Styles =
        {
            "Editor/AssetManager/UI/asset-manager.uss",
            "Editor/AssetManager/UI/asset-detail.uss",
            "Editor/AssetManager/UI/asset-modification-workflow.uss",
            "Editor/AssetManager/UI/Components/Workflow/workflow-story.uss"
        };

        public IReadOnlyList<UiStory> GetStories()
        {
            return new[]
            {
                Story("workflow-editor-layout", "Containers", "WorkflowEditorLayout",
                    "改変画面のカテゴリ、プレビュー、編集ペインと専用画面の配置を管理します。",
                    BuildLayout, "AssetModificationWorkflowView.Workspace.cs"),
                Story("workflow-category-rail", "Inputs", "WorkflowCategoryRail",
                    "編集時とPlayMode時のカテゴリ、選択表示とPrefab選択中の操作可否を管理します。",
                    BuildCategoryRail, "AssetModificationWorkflowView.Workspace.cs"),

            };
        }

        private static UiStory Story(string id, string group, string title,
            string description, Action<VisualElement> build, params string[] usages)
        {
            return new UiStory(id, "Domain/AssetManager/" + group, title,
                description, description, parent =>
                {
                    var surface = new VisualElement();
                    surface.AddToClassList("ee4v-workflow-story");
                    surface.AddToClassList("ee4v-workflow-story--" + id);
                    build(surface);
                    parent.Add(surface);
                },
                usageLocations: usages.Select(path => "Editor/AssetManager/UI/" + path).ToArray(),
                styleSheetPaths: Styles);
        }

        private static void BuildLayout(VisualElement parent)
        {
            WorkflowEditorLayout layout = null;
            WorkflowCategoryRail rail = null;
            rail = new WorkflowCategoryRail(category =>
            {
                rail.SetSelected(category);
                layout.ShowCategory(category);
            });
            rail.SetSelected(WorkflowCategory.Overview);
            var pane = new PreviewPane("Preview");
            pane.AddToClassList("ee4v-modification-workflow__preview-pane");
            pane.Content.Add(new EmptyState(new EmptyStateState("プレビュー領域", "描画する部品を配置します。")));
            layout = new WorkflowEditorLayout(rail, pane);
            layout.AppearanceHeader.Add(new BodyPartSelector(null, _ => true, _ => { }));
            layout.Controls.Add(UiTextFactory.Create("編集コントロール領域"));
            layout.FaceExpressionHost.Add(new EmptyState(new EmptyStateState("表情・アニメーション", "専用Viewの領域です。")));
            layout.ExecutionHost.Add(new EmptyState(new EmptyStateState("操作確認", "PlayMode専用Viewの領域です。")));
            parent.Add(layout);
        }

        private static void BuildCategoryRail(VisualElement parent)
        {
            WorkflowCategoryRail rail = null;
            rail = new WorkflowCategoryRail(category => rail.SetSelected(category));
            rail.SetSelected(WorkflowCategory.Overview);
            var scopeSelected = false;
            parent.Add(new UiButton("Prefab選択状態を切り替え",
                () => rail.SetPrefabScope(scopeSelected = !scopeSelected)));
            parent.Add(rail);
            parent.Add(UiTextFactory.Create("PlayMode"));
            WorkflowCategoryRail playRail = null;
            playRail = new WorkflowCategoryRail(category => playRail.SetSelected(category), true);
            playRail.SetSelected(WorkflowCategory.MenuAndGestures);
            playRail.SetPrefabScope(true);
            parent.Add(playRail);
        }

    }
}
