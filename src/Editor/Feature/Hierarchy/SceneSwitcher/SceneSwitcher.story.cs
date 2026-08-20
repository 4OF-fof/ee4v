using System.Collections.Generic;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.SceneSwitcher
{
    internal sealed class SceneSwitcherStoryProvider : IUiStoryProvider
    {
        public int Order => 130;

        public IReadOnlyList<UiStory> GetStories()
        {
            return new[]
            {
                new UiStory(
                    "scene-switcher-view",
                    "Domain/SceneSwitcher",
                    "SceneSwitcherView",
                    "シーンの検索、切り替え、お気に入り操作を行う画面です。",
                    "SceneSwitcherWindow が使用する実際の一覧と検索欄を表示します。",
                    Build,
                    dependencies: new[] { "SearchField", "UiButton" },
                    usageLocations: new[]
                    {
                        "Editor/Feature/Hierarchy/SceneSwitcher/UI/SceneSwitcherWindow.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/UI/Components/Inputs/ui-button.uss",
                        "Editor/UI/Components/Inputs/SearchField/search-field.uss",
                        "Editor/Feature/Hierarchy/SceneSwitcher/UI/scene-switcher-window.uss"
                    })
            };
        }

        private static void Build(VisualElement parent)
        {
            var view = new SceneSwitcherView(
                new SceneSwitcherViewText
                {
                    SearchPlaceholder = "Search scenes or folders",
                    SearchTooltip = "Filter scenes by name or folder",
                    ClearSearchTooltip = "Clear search",
                    Empty = "No Scene assets were found under Assets.",
                    NoMatches = "No scenes match this search.",
                    Open = "OPEN",
                    OpenTooltip = "This Scene is currently open.",
                    FavoriteTooltip = "Add to favorites",
                    UnfavoriteTooltip = "Remove from favorites",
                    CreateFormat = "Create \"{0}\""
                },
                UiBuiltinIconResolver.LoadTexture(
                    UiBuiltinIcon.Scene),
                FluentUiIcons.LoadTexture("star.png"));
            view.SetState(new SceneSwitcherViewState(
                string.Empty,
                new[]
                {
                    new SceneSwitcherItem(
                        "Assets/Scenes/Main.unity",
                        true,
                        true),
                    new SceneSwitcherItem(
                        "Assets/Scenes/Gameplay.unity",
                        false,
                        false)
                },
                false));
            var surface = new VisualElement();
            surface.AddToClassList("ee4v-scene-switcher-window");
            surface.style.width = 200f;
            surface.style.height = 300f;
            surface.Add(view);
            parent.Add(surface);
        }
    }
}
