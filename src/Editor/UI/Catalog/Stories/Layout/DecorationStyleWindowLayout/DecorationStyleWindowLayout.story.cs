using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class
            DecorationStyleWindowLayoutCatalogRegistrar
            : ICatalogRegistrar
        {
            public int Order => 75;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Content/Icon/icon.uss");
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Inputs/Button/ui-button.uss");
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Inputs/DecorationStyleEditor/decoration-style-editor.uss");
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Layout/DecorationStyleWindowLayout/decoration-style-window-layout.uss");
                registry.RegisterStory(new StoryRegistration(
                    "decoration-style-window-layout",
                    "Layout",
                    "DecorationStyleWindowLayout",
                    CatalogCoveragePreview.ComponentDescription(
                        "DecorationStyleWindowLayout"),
                    CatalogCoveragePreview.ComponentDetails(
                        "DecorationStyleWindowLayout"),
                    new[] { "DecorationStyleEditor", "UiButton" },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) =>
                        window.BuildDecorationStyleWindowLayoutStory(
                            parent)));
            }
        }

        private void BuildDecorationStyleWindowLayoutStory(
            VisualElement parent)
        {
            UiFluentIconResolver.TryResolve(
                UiFluentIcon.Folder,
                out var folderIcon);
            var layout = new DecorationStyleWindowLayout(
                new DecorationStyleWindowLayoutState(
                    CatalogCoveragePreview.SampleTitle,
                    CatalogCoveragePreview.SampleSubtitle,
                    CatalogCoveragePreview.SampleDescription,
                    CatalogCoveragePreview.SampleClearSelection,
                    new DecorationStyleEditorText(
                        CatalogCoveragePreview.SampleTitle,
                        CatalogCoveragePreview.SampleDescription,
                        CatalogCoveragePreview.SampleSubtitle,
                        CatalogCoveragePreview.SampleClearSelection,
                        CatalogCoveragePreview.SampleFolder,
                        CatalogCoveragePreview.SampleDescription,
                        CatalogCoveragePreview.SampleFavorite,
                        CatalogCoveragePreview.SampleOpen,
                        CatalogCoveragePreview.SampleClearSelection),
                    new DecorationStyleEditorState(
                        new Color(
                            0.35f,
                            0.65f,
                            0.95f,
                            0.8f),
                        folderIcon,
                        new[]
                        {
                            new DecorationColorPresetState(
                                new Color(
                                    0.95f,
                                    0.45f,
                                    0.45f,
                                    0.8f)),
                            new DecorationColorPresetState(
                                new Color(
                                    0.35f,
                                    0.65f,
                                    0.95f,
                                    0.8f))
                        }),
                    folderIcon,
                    Color.white,
                    CatalogCoveragePreview.SampleReveal,
                    CatalogCoveragePreview.SampleDescription,
                    IconState.FromBuiltinIcon(
                        UiBuiltinIcon.VisibilityHidden,
                        UiSizeTokens.Size16)),
                () => { },
                () => { });
            layout.SetPreviewBackground(
                new Color(0.35f, 0.65f, 0.95f, 0.4f));

            CatalogCoveragePreview.CreateSurface(
                this,
                parent,
                430f).Add(layout);
        }
    }
}
