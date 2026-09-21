using System.Collections.Generic;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.ProjectTabs
{
    internal sealed class ProjectTabsStoryProvider : IUiStoryProvider
    {
        public int Order => 176;

        public IReadOnlyList<UiStory> GetStories()
        {
            return new[]
            {
                new UiStory(
                    "project-tabs",
                    "Domain/ProjectTabs/Inputs",
                    "Project Tabs",
                    "Project Browser のフォルダー、履歴、固定状態をタブで操作する toolbar です。",
                    "実際の toolbar に、通常タブ、固定タブ、選択状態を表示します。",
                    Build,
                    dependencies: new[]
                    {
                        "UiButton",
                        "Icon",
                        "Fluent UI System Icons"
                    },
                    usageLocations: new[]
                    {
                        "Editor/Feature/Project/ProjectTabs/UI/ProjectTabsHost.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/UI/Components/Content/Icon/icon.uss",
                        "Editor/UI/Components/Inputs/ui-button.uss",
                        "Editor/Feature/Project/ProjectTabs/UI/project-tabs.uss"
                    })
            };
        }

        private static void Build(VisualElement parent)
        {
            var view = new ProjectTabsView();
            view.SetState(new ProjectTabsViewState(
                new[]
                {
                    new ProjectTabViewState(
                        "assets",
                        "Assets",
                        "Assets"),
                    new ProjectTabViewState(
                        "avatars",
                        "Avatars",
                        "Assets/Avatars"),
                    new ProjectTabViewState(
                        "favorites",
                        "Favorites",
                        "Assets/Favorites",
                        true),
                    new ProjectTabViewState(
                        "materials",
                        "Materials",
                        "Assets/Art/Materials")
                },
                "avatars",
                true,
                true,
                new[]
                {
                    new ProjectHistoryEntryViewState("Scenes", 1),
                    new ProjectHistoryEntryViewState("Assets", 2)
                },
                new[]
                {
                    new ProjectHistoryEntryViewState("Animations", 1)
                }));

            var surface = new VisualElement();
            surface.style.width = 820f;
            surface.style.height = UiSizeTokens.ControlHeightCompact;
            surface.Add(view);
            parent.Add(surface);
        }
    }
}
