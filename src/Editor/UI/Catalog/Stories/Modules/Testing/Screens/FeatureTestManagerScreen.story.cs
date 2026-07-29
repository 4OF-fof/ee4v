using Ee4v.Testing.UI;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal static class FeatureTestManagerScreenCatalogStory
    {
        private sealed class Registrar : CatalogWindow.ICatalogRegistrar
        {
            public int Order => 150;

            public void Register(
                CatalogWindow.CatalogRegistry registry)
            {
                var styles = new[]
                {
                    "Editor/UI/Components/Inputs/Button/ui-button.uss",
                    "Editor/UI/Components/Inputs/SearchField/search-field.uss",
                    "Editor/UI/Components/Content/InfoCard/info-card.uss",
                    "Editor/UI/Components/Content/Alerts/alerts.uss",
                    "Editor/UI/Components/Content/StatusBadge/status-badge.uss",
                    "Editor/UI/Components/Content/CopyableTextArea/copyable-text-area.uss",
                    "Editor/Testing/UI/TestResultGroup/test-result-group.uss",
                    "Editor/Testing/UI/feature-test-manager-window.uss"
                };
                for (var i = 0; i < styles.Length; i++)
                {
                    registry.RegisterStyleSheet(styles[i]);
                }

                registry.RegisterStory(
                    new CatalogWindow.StoryRegistration(
                        "feature-test-manager-screen",
                        "Domain/Testing/Screens",
                        "Feature Test Manager Window",
                        CatalogCoveragePreview.ScreenDescription(
                            "Feature Test Manager Window"),
                        CatalogCoveragePreview.ScreenDetails(
                            "Feature Test Manager Window"),
                        new[]
                        {
                            "InfoCard",
                            "SearchField",
                            "Alerts",
                            "TestResultGroup"
                        },
                        CatalogWindow.ComponentImplementationKind.UiToolkit,
                        Build));
            }
        }

        private static void Build(
            CatalogWindow window,
            VisualElement parent)
        {
            var shell = new VisualElement();
            shell.AddToClassList("ee4v-test-manager__shell");

            var overall = new InfoCard(new InfoCardState(
                CatalogCoveragePreview.SampleTitle,
                CatalogCoveragePreview.SampleDescription));
            overall.AddToClassList("ee4v-test-manager__overall");
            overall.HeaderRight.Add(new UiButton(new UiButtonState(
                CatalogCoveragePreview.SampleRun,
                variant: UiButtonVariant.Solid)));
            overall.Body.Add(new Alerts(new AlertsState(
                UiBannerTone.Info,
                CatalogCoveragePreview.SamplePassed,
                CatalogCoveragePreview.SampleStatus)));

            var search = new SearchField(new SearchFieldState(
                placeholder: CatalogCoveragePreview.SampleSearch,
                searchTooltip: CatalogCoveragePreview.SampleSearch,
                clearTooltip:
                    CatalogCoveragePreview.SampleClearSelection));
            search.AddToClassList("ee4v-test-manager__search");

            var group = new TestResultGroup();
            group.AddToClassList("ee4v-test-manager__suite-card");
            group.SetState(new TestResultGroupState(
                new InfoCardState(
                    CatalogCoveragePreview.SampleSubtitle,
                    CatalogCoveragePreview.SampleDescription,
                    "UI",
                    new StatusBadgeState(
                        CatalogCoveragePreview.SamplePassed,
                        UiStatusTone.Passed)),
                CatalogCoveragePreview.SampleRun,
                true,
                CatalogCoveragePreview.SampleStatus,
                UiBannerTone.Info,
                CatalogCoveragePreview.SampleTitle,
                "2",
                true,
                new[]
                {
                    new TestResultGroupCaseState(
                        CatalogCoveragePreview.SampleTitle,
                        CatalogCoveragePreview.SampleDescription,
                        new StatusBadgeState(
                            CatalogCoveragePreview.SamplePassed,
                            UiStatusTone.Passed))
                }));

            var scroll = new ScrollView();
            scroll.AddToClassList("ee4v-test-manager__scroll");
            var list = new VisualElement();
            list.AddToClassList("ee4v-test-manager__list");
            list.Add(group);
            scroll.Add(list);

            shell.Add(overall);
            shell.Add(search);
            shell.Add(scroll);
            CatalogCoveragePreview.CreateSurface(
                window,
                parent,
                520f).Add(shell);
        }
    }
}
