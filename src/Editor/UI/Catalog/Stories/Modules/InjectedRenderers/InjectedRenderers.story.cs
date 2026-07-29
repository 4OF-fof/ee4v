using Ee4v.Core.Injector;
using Ee4v.DepthIndicator;
using Ee4v.FolderContentOverlay;
using Ee4v.FolderStyle;
using Ee4v.HiddenObjects;
using Ee4v.HierarchyStyle;
using Ee4v.SceneSwitcher;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal static class InjectedRenderersCatalogStory
    {
        private enum PreviewKind
        {
            DepthIndicator,
            FolderContentOverlay,
            FolderStyle,
            HierarchyStyle,
            HiddenObjectsButton,
            SceneSwitcherTrigger
        }

        private sealed class Registrar : CatalogWindow.ICatalogRegistrar
        {
            public int Order => 180;

            public void Register(
                CatalogWindow.CatalogRegistry registry)
            {
                Register(
                    registry,
                    "depth-indicator-renderer",
                    "Domain/DepthIndicator/Injected UI",
                    "DepthIndicatorRenderer",
                    PreviewKind.DepthIndicator);
                Register(
                    registry,
                    "folder-content-overlay-renderer",
                    "Domain/FolderContentOverlay/Injected UI",
                    "FolderContentOverlayRenderer",
                    PreviewKind.FolderContentOverlay);
                Register(
                    registry,
                    "folder-style-renderer",
                    "Domain/FolderStyle/Injected UI",
                    "FolderStyleRenderer",
                    PreviewKind.FolderStyle);
                Register(
                    registry,
                    "hierarchy-style-renderer",
                    "Domain/HierarchyStyle/Injected UI",
                    "HierarchyStyleRenderer",
                    PreviewKind.HierarchyStyle);
                Register(
                    registry,
                    "hidden-object-hierarchy-button",
                    "Domain/HiddenObjects/Injected UI",
                    "HiddenObjectHierarchyButtonRenderer",
                    PreviewKind.HiddenObjectsButton);
                Register(
                    registry,
                    "scene-switcher-hierarchy-trigger",
                    "Domain/SceneSwitcher/Injected UI",
                    "SceneSwitcherHierarchyTrigger",
                    PreviewKind.SceneSwitcherTrigger);
            }

            private static void Register(
                CatalogWindow.CatalogRegistry registry,
                string id,
                string group,
                string title,
                PreviewKind kind)
            {
                registry.RegisterStory(
                    new CatalogWindow.StoryRegistration(
                        id,
                        group,
                        title,
                        CatalogCoveragePreview.ComponentDescription(title),
                        CatalogCoveragePreview.ComponentDetails(title),
                        null,
                        CatalogWindow.ComponentImplementationKind.Imgui,
                        (window, parent) =>
                            Build(window, parent, kind)));
            }
        }

        private static void Build(
            CatalogWindow window,
            VisualElement parent,
            PreviewKind kind)
        {
            var container = new IMGUIContainer(() => Draw(kind));
            container.style.flexGrow = 1f;
            CatalogCoveragePreview.CreateSurface(
                window,
                parent,
                150f).Add(container);
        }

        private static void Draw(PreviewKind kind)
        {
            var row = GUILayoutUtility.GetRect(
                320f,
                42f,
                GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(
                row,
                EditorGUIUtility.isProSkin
                    ? new Color32(56, 56, 56, 255)
                    : new Color32(200, 200, 200, 255));
            GUI.Label(
                new Rect(
                    row.x + 52f,
                    row.y,
                    row.width - 56f,
                    row.height),
                CatalogCoveragePreview.SampleTitle);

            switch (kind)
            {
                case PreviewKind.DepthIndicator:
                    DrawDepthIndicator(row);
                    break;
                case PreviewKind.FolderContentOverlay:
                    DrawFolder(row, true, false);
                    break;
                case PreviewKind.FolderStyle:
                    DrawFolder(row, false, true);
                    break;
                case PreviewKind.HierarchyStyle:
                    DrawHierarchyStyle(row);
                    break;
                case PreviewKind.HiddenObjectsButton:
                    DrawHiddenObjectsButton(row);
                    break;
                case PreviewKind.SceneSwitcherTrigger:
                    DrawSceneSwitcherTrigger(row);
                    break;
            }
        }

        private static void DrawDepthIndicator(Rect row)
        {
            var cell = DepthIndicatorGeometry.GetFirstCell(
                new Rect(row.x + 6f, row.y, 48f, row.height));
            var color = new Color32(110, 110, 110, 255);
            EditorGUI.DrawRect(
                DepthIndicatorGeometry.GetBranchHorizontalLine(cell),
                color);
            EditorGUI.DrawRect(
                DepthIndicatorGeometry.GetBranchEndVerticalLine(cell),
                color);
            var parentCell =
                DepthIndicatorGeometry.MoveToParentCell(cell);
            EditorGUI.DrawRect(
                DepthIndicatorGeometry.GetVerticalLine(parentCell),
                color);
        }

        private static void DrawFolder(
            Rect row,
            bool overlay,
            bool tinted)
        {
            var iconRect = FolderContentOverlayLayout.GetFolderIconRect(
                new Rect(row.x + 8f, row.y + 4f, 34f, 34f),
                ProjectItemViewMode.TwoColumns,
                ProjectItemOrientation.Vertical);
            UiBuiltinIconResolver.TryResolve(
                UiBuiltinIcon.Folder,
                out var folder);
            if (folder != null)
            {
                if (tinted)
                {
                    EditorGUI.DrawRect(
                        iconRect,
                        FolderStyleRenderer.ResolveBackgroundColor(
                            ProjectItemViewMode.TwoColumns,
                            ProjectItemOrientation.Vertical,
                            EditorGUIUtility.isProSkin));
                }

                var previous = GUI.color;
                GUI.color = tinted
                    ? new Color(0.35f, 0.65f, 0.95f, 0.9f)
                    : Color.white;
                GUI.DrawTexture(
                    iconRect,
                    folder,
                    ScaleMode.ScaleToFit,
                    true);
                GUI.color = previous;
            }

            if (!overlay)
            {
                return;
            }

            UiBuiltinIconResolver.TryResolve(
                UiBuiltinIcon.Star,
                out var star);
            if (star != null)
            {
                GUI.DrawTexture(
                    FolderContentOverlayLayout.GetOverlayRect(iconRect),
                    star,
                    ScaleMode.ScaleToFit,
                    true);
            }
        }

        private static void DrawHierarchyStyle(Rect row)
        {
            EditorGUI.DrawRect(
                HierarchyStyleRenderer.GetBackgroundRect(
                    row,
                    row.xMax),
                new Color(0.2f, 0.45f, 0.8f, 0.3f));
            UiBuiltinIconResolver.TryResolve(
                UiBuiltinIcon.GenericFile,
                out var icon);
            if (icon != null)
            {
                GUI.DrawTexture(
                    HierarchyStyleRenderer.GetIconRect(row),
                    icon,
                    ScaleMode.ScaleToFit,
                    true);
            }
        }

        private static void DrawHiddenObjectsButton(Rect row)
        {
            UiBuiltinIconResolver.TryResolve(
                UiBuiltinIcon.VisibilityHidden,
                out var icon);
            if (icon == null)
            {
                return;
            }

            var buttonRect =
                HiddenObjectHierarchyButtonRenderer.GetButtonRect(row);
            var iconRect =
                HiddenObjectHierarchyButtonRenderer.GetIconRect(
                    buttonRect);
            GUI.DrawTexture(
                iconRect,
                icon,
                ScaleMode.ScaleToFit,
                true);
        }

        private static void DrawSceneSwitcherTrigger(Rect row)
        {
            var trigger = SceneSwitcherHierarchyTrigger.GetAnchorRect(
                row,
                row.xMax);
            EditorGUI.DrawRect(
                trigger,
                new Color(0.25f, 0.55f, 0.95f, 0.18f));
            GUI.Label(
                trigger,
                CatalogCoveragePreview.SampleOpenTooltip);
        }
    }
}
