using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class DecorationStyleEditorCatalogRegistrar
            : ICatalogRegistrar
        {
            public int Order => 35;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Inputs/DecorationStyleEditor/decoration-style-editor.uss");
                registry.RegisterStory(new StoryRegistration(
                    "decoration-style-editor",
                    "Inputs",
                    "DecorationStyleEditor",
                    CatalogCoveragePreview.ComponentDescription(
                        "DecorationStyleEditor"),
                    CatalogCoveragePreview.ComponentDetails(
                        "DecorationStyleEditor"),
                    new[] { "UiButton", "ImageTooltip" },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) =>
                        window.BuildDecorationStyleEditorStory(parent)));
            }
        }

        private void BuildDecorationStyleEditorStory(
            VisualElement parent)
        {
            UiFluentIconResolver.TryResolve(
                UiFluentIcon.Folder,
                out var folderIcon);
            var editor = new DecorationStyleEditor(
                new DecorationStyleEditorText(
                    CatalogCoveragePreview.SampleTitle,
                    CatalogCoveragePreview.SampleDescription,
                    CatalogCoveragePreview.SampleSubtitle,
                    CatalogCoveragePreview.SampleClearSelection,
                    CatalogCoveragePreview.SampleCollection,
                    CatalogCoveragePreview.SampleDescription,
                    CatalogCoveragePreview.SampleFavorite,
                    CatalogCoveragePreview.SampleOpen,
                    CatalogCoveragePreview.SampleClearSelection),
                new DecorationStyleEditorState(
                    new Color(0.25f, 0.55f, 0.95f, 0.8f),
                    folderIcon,
                    new[]
                    {
                        new DecorationColorPresetState(
                            new Color(0.95f, 0.35f, 0.35f, 0.8f)),
                        new DecorationColorPresetState(
                            new Color(0.25f, 0.55f, 0.95f, 0.8f)),
                        new DecorationColorPresetState(
                            new Color(0.35f, 0.8f, 0.5f, 0.8f))
                    },
                    folderIcon == null
                        ? Array.Empty<DecorationIconCandidateState>()
                        : new[]
                        {
                            new DecorationIconCandidateState(
                                folderIcon,
                                CatalogCoveragePreview.SampleFolder,
                                true)
                        }));
            var surface = CatalogCoveragePreview.CreateSurface(
                this,
                parent,
                420f);
            surface.style.height = StyleKeyword.Auto;
            surface.style.minHeight = 420f;
            surface.Add(editor);
        }
    }
}
