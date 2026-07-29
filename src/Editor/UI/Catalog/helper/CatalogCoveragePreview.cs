using Ee4v.Core.I18n;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal static class CatalogCoveragePreview
    {
        public static string ComponentDescription(string name)
        {
            return I18N.Get(
                "catalog.coverage.componentDescription",
                name ?? string.Empty);
        }

        public static string ComponentDetails(string name)
        {
            return I18N.Get(
                "catalog.coverage.componentDetails",
                name ?? string.Empty);
        }

        public static string ScreenDescription(string name)
        {
            return I18N.Get(
                "catalog.coverage.screenDescription",
                name ?? string.Empty);
        }

        public static string ScreenDetails(string name)
        {
            return I18N.Get(
                "catalog.coverage.screenDetails",
                name ?? string.Empty);
        }

        public static VisualElement CreateSurface(
            CatalogWindow window,
            VisualElement parent,
            float height = 320f,
            bool compact = false)
        {
            var preview = window.CreatePreviewSection(parent);
            var surface = window.CreatePreviewSurface(compact);
            surface.style.height = height;
            surface.style.minHeight = height;
            preview.Body.Add(surface);
            return surface;
        }

        public static string SampleTitle =>
            I18N.Get("catalog.coverage.sample.title");
        public static string SampleSubtitle =>
            I18N.Get("catalog.coverage.sample.subtitle");
        public static string SampleSearch =>
            I18N.Get("catalog.coverage.sample.search");
        public static string SampleEmpty =>
            I18N.Get("catalog.coverage.sample.empty");
        public static string SampleNoMatches =>
            I18N.Get("catalog.coverage.sample.noMatches");
        public static string SampleSelectAll =>
            I18N.Get("catalog.coverage.sample.selectAll");
        public static string SampleClearSelection =>
            I18N.Get("catalog.coverage.sample.clearSelection");
        public static string SampleReveal =>
            I18N.Get("catalog.coverage.sample.reveal");
        public static string SampleRefresh =>
            I18N.Get("catalog.coverage.sample.refresh");
        public static string SampleRefreshTooltip =>
            I18N.Get("catalog.coverage.sample.refreshTooltip");
        public static string SampleSceneAll =>
            I18N.Get("catalog.coverage.sample.sceneAll");
        public static string SampleSceneMain =>
            I18N.Get("catalog.coverage.sample.sceneMain");
        public static string SampleOpen =>
            I18N.Get("catalog.coverage.sample.open");
        public static string SampleOpenTooltip =>
            I18N.Get("catalog.coverage.sample.openTooltip");
        public static string SampleFavorite =>
            I18N.Get("catalog.coverage.sample.favorite");
        public static string SampleUnfavorite =>
            I18N.Get("catalog.coverage.sample.unfavorite");
        public static string SampleCreateFormat =>
            I18N.Get("catalog.coverage.sample.createFormat");
        public static string SampleCollection =>
            I18N.Get("catalog.coverage.sample.collection");
        public static string SampleSmartCollection =>
            I18N.Get("catalog.coverage.sample.smartCollection");
        public static string SampleTagOne =>
            I18N.Get("catalog.coverage.sample.tagOne");
        public static string SampleTagTwo =>
            I18N.Get("catalog.coverage.sample.tagTwo");
        public static string SampleFile =>
            I18N.Get("catalog.coverage.sample.file");
        public static string SampleFolder =>
            I18N.Get("catalog.coverage.sample.folder");
        public static string SampleStatus =>
            I18N.Get("catalog.coverage.sample.status");
        public static string SampleRun =>
            I18N.Get("catalog.coverage.sample.run");
        public static string SamplePassed =>
            I18N.Get("catalog.coverage.sample.passed");
        public static string SampleDescription =>
            I18N.Get("catalog.coverage.sample.description");
    }
}
