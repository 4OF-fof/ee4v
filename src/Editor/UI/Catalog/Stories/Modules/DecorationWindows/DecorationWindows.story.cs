using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal static class DecorationWindowsCatalogStory
    {
        private sealed class Registrar : CatalogWindow.ICatalogRegistrar
        {
            public int Order => 170;

            public void Register(
                CatalogWindow.CatalogRegistry registry)
            {
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Inputs/Button/ui-button.uss");
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Inputs/DecorationStyleEditor/decoration-style-editor.uss");
                registry.RegisterStyleSheet(
                    "Editor/UI/Components/Layout/DecorationStyleWindowLayout/decoration-style-window-layout.uss");
                Register(
                    registry,
                    "folder-style-window-screen",
                    "Domain/FolderStyle/Screens",
                    "Folder Style Window",
                    false);
                Register(
                    registry,
                    "hierarchy-style-window-screen",
                    "Domain/HierarchyStyle/Screens",
                    "Hierarchy Style Window",
                    true);
            }

            private static void Register(
                CatalogWindow.CatalogRegistry registry,
                string id,
                string group,
                string title,
                bool includeHide)
            {
                registry.RegisterStory(
                    new CatalogWindow.StoryRegistration(
                        id,
                        group,
                        title,
                        CatalogCoveragePreview.ScreenDescription(title),
                        CatalogCoveragePreview.ScreenDetails(title),
                        new[]
                        {
                            "DecorationStyleWindowLayout",
                            "DecorationStyleEditor",
                            "UiButton"
                        },
                        CatalogWindow.ComponentImplementationKind.UiToolkit,
                        (window, parent) => Build(
                            window,
                            parent,
                            includeHide)));
            }
        }

        private static void Build(
            CatalogWindow window,
            VisualElement parent,
            bool includeHide)
        {
            UiFluentIconResolver.TryResolve(
                UiFluentIcon.Folder,
                out var folderIcon);
            var content = new DecorationStyleWindowLayout(
                new DecorationStyleWindowLayoutState(
                    CatalogCoveragePreview.SampleTitle,
                    CatalogCoveragePreview.SampleSubtitle,
                    CatalogCoveragePreview.SampleDescription,
                    CatalogCoveragePreview.SampleClearSelection,
                    CreateEditorText(),
                    CreateEditorState(folderIcon),
                    folderIcon,
                    includeHide
                        ? Color.white
                        : new Color(
                            0.35f,
                            0.65f,
                            0.95f,
                            0.8f),
                    includeHide
                        ? CatalogCoveragePreview.SampleReveal
                        : null,
                    includeHide
                        ? CatalogCoveragePreview.SampleDescription
                        : null,
                    includeHide
                        ? IconState.FromBuiltinIcon(
                            UiBuiltinIcon.VisibilityHidden,
                            UiSizeTokens.Size16)
                        : null),
                () => { },
                includeHide ? (System.Action)(() => { }) : null);
            if (includeHide)
            {
                content.SetPreviewBackground(
                    new Color(0.35f, 0.65f, 0.95f, 0.4f));
            }

            CatalogCoveragePreview.CreateSurface(
                window,
                parent,
                includeHide ? 430f : 380f).Add(content);
        }

        private static DecorationStyleEditorText
            CreateEditorText()
        {
            return new DecorationStyleEditorText(
                CatalogCoveragePreview.SampleTitle,
                CatalogCoveragePreview.SampleDescription,
                CatalogCoveragePreview.SampleSubtitle,
                CatalogCoveragePreview.SampleClearSelection,
                CatalogCoveragePreview.SampleFolder,
                CatalogCoveragePreview.SampleDescription,
                CatalogCoveragePreview.SampleFavorite,
                CatalogCoveragePreview.SampleOpen,
                CatalogCoveragePreview.SampleClearSelection);
        }

        private static DecorationStyleEditorState CreateEditorState(
            Texture iconTexture)
        {
            return new DecorationStyleEditorState(
                new Color(0.35f, 0.65f, 0.95f, 0.8f),
                iconTexture,
                new[]
                {
                    new DecorationColorPresetState(
                        new Color(0.95f, 0.45f, 0.45f, 0.8f)),
                    new DecorationColorPresetState(
                        new Color(0.35f, 0.65f, 0.95f, 0.8f))
                });
        }
    }
}
