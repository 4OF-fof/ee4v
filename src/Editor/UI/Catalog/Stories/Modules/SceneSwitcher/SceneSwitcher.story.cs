using Ee4v.Core.Settings;
using Ee4v.SceneSwitcher;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal static class SceneSwitcherCatalogStory
    {
        private sealed class Registrar : CatalogWindow.ICatalogRegistrar
        {
            public int Order => 130;

            public void Register(
                CatalogWindow.CatalogRegistry registry)
            {
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Inputs/Button/ui-button.uss");
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Inputs/SearchField/search-field.uss");
                registry.RegisterStyleSheet(
                    "Editor/EditorEnhancements/SceneSwitcher/UI/scene-switcher-window.uss");
                Register(
                    registry,
                    "scene-switcher-view",
                    "SceneSwitcherView",
                    "Domain/SceneSwitcher/Components",
                    BuildView);
                Register(
                    registry,
                    "scene-switcher-row",
                    "SceneSwitcherRow",
                    "Domain/SceneSwitcher/Components",
                    BuildRow);
                Register(
                    registry,
                    "scene-switcher-setting-field",
                    "SceneSwitcherFolderSetting",
                    "Domain/SceneSwitcher/Components",
                    BuildSetting);
                registry.RegisterStory(
                    new CatalogWindow.StoryRegistration(
                        "scene-switcher-screen",
                        "Domain/SceneSwitcher/Screens",
                        "Scene Switcher Window",
                        CatalogCoveragePreview.ScreenDescription(
                            "Scene Switcher Window"),
                        CatalogCoveragePreview.ScreenDetails(
                            "Scene Switcher Window"),
                        new[] { "SceneSwitcherView" },
                        CatalogWindow.ComponentImplementationKind.UiToolkit,
                        BuildView));
            }

            private static void Register(
                CatalogWindow.CatalogRegistry registry,
                string id,
                string title,
                string group,
                System.Action<CatalogWindow, VisualElement> build)
            {
                registry.RegisterStory(
                    new CatalogWindow.StoryRegistration(
                        id,
                        group,
                        title,
                        CatalogCoveragePreview.ComponentDescription(title),
                        CatalogCoveragePreview.ComponentDetails(title),
                        null,
                        CatalogWindow.ComponentImplementationKind.UiToolkit,
                        build));
            }
        }

        private static void BuildView(
            CatalogWindow window,
            VisualElement parent)
        {
            ResolveIcons(out var sceneIcon, out var favoriteIcon);
            var view = new SceneSwitcherView(
                CreateText(),
                sceneIcon,
                favoriteIcon);
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
            CatalogCoveragePreview.CreateSurface(
                window,
                parent,
                340f).Add(view);
        }

        private static void BuildRow(
            CatalogWindow window,
            VisualElement parent)
        {
            ResolveIcons(out var sceneIcon, out var favoriteIcon);
            var row = new SceneSwitcherRow(
                CreateText(),
                sceneIcon,
                favoriteIcon);
            row.SetState(new SceneSwitcherItem(
                "Assets/Scenes/Main.unity",
                true,
                true));
            CatalogCoveragePreview.CreateSurface(
                window,
                parent,
                90f,
                true).Add(row);
        }

        private static void BuildSetting(
            CatalogWindow window,
            VisualElement parent)
        {
            var field = SceneSwitcherSettingDrawers.CreateFolderField(
                new SettingDrawerContext<string>(
                    CatalogCoveragePreview.SampleDescription,
                    "Assets/Scenes",
                    string.Empty,
                    _ => { }));
            CatalogCoveragePreview.CreateSurface(
                window,
                parent,
                100f,
                true).Add(field);
        }

        private static SceneSwitcherViewText CreateText()
        {
            return new SceneSwitcherViewText
            {
                SearchPlaceholder =
                    CatalogCoveragePreview.SampleSearch,
                SearchTooltip =
                    CatalogCoveragePreview.SampleSearch,
                ClearSearchTooltip =
                    CatalogCoveragePreview.SampleClearSelection,
                Empty = CatalogCoveragePreview.SampleEmpty,
                NoMatches =
                    CatalogCoveragePreview.SampleNoMatches,
                Open = CatalogCoveragePreview.SampleOpen,
                OpenTooltip =
                    CatalogCoveragePreview.SampleOpenTooltip,
                FavoriteTooltip =
                    CatalogCoveragePreview.SampleFavorite,
                UnfavoriteTooltip =
                    CatalogCoveragePreview.SampleUnfavorite,
                CreateFormat =
                    CatalogCoveragePreview.SampleCreateFormat
            };
        }

        private static void ResolveIcons(
            out Texture sceneIcon,
            out Texture favoriteIcon)
        {
            UiBuiltinIconResolver.TryResolve(
                UiBuiltinIcon.UnityFile,
                out sceneIcon);
            UiBuiltinIconResolver.TryResolve(
                UiBuiltinIcon.Star,
                out favoriteIcon);
        }
    }
}
