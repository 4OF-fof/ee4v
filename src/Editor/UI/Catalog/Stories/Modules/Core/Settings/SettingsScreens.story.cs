using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal static class SettingsScreensCatalogStory
    {
        private sealed class Registrar : CatalogWindow.ICatalogRegistrar
        {
            public int Order => 160;

            public void Register(
                CatalogWindow.CatalogRegistry registry)
            {
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Inputs/InputField/input-field.uss");
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Inputs/CommaSeparatedListField/comma-separated-list-field.uss");
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Inputs/ReorderableListField/reorderable-list-field.uss");
                registry.RegisterStyleSheet(
                    "Editor/Core/Presentation/Settings/settings-ui.uss");
                Register(
                    registry,
                    "user-settings-screen",
                    "User Settings",
                    SettingScope.User);
                Register(
                    registry,
                    "project-settings-screen",
                    "Project Settings",
                    SettingScope.Project);
            }

            private static void Register(
                CatalogWindow.CatalogRegistry registry,
                string id,
                string title,
                SettingScope scope)
            {
                registry.RegisterStory(
                    new CatalogWindow.StoryRegistration(
                        id,
                        "Domain/Core/Settings",
                        title,
                        CatalogCoveragePreview.ScreenDescription(title),
                        CatalogCoveragePreview.ScreenDetails(title),
                        null,
                        CatalogWindow.ComponentImplementationKind.UiToolkit,
                        (window, parent) =>
                            Build(window, parent, scope)));
            }
        }

        private static void Build(
            CatalogWindow window,
            VisualElement parent,
            SettingScope scope)
        {
            CoreLocalizationDefinitions.RegisterAll(
                CoreSettings.Current);
            var host = new VisualElement();
            SettingsUiRenderer.BuildScope(
                host,
                CoreSettings.Current,
                scope,
                string.Empty);
            CatalogCoveragePreview.CreateSurface(
                window,
                parent,
                480f).Add(host);
        }
    }
}
