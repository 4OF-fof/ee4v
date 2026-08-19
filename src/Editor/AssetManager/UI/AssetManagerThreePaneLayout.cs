using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetManagerThreePaneLayout : VisualElement
    {
        public AssetManagerThreePaneLayout(
            AssetManagerViewMode mode = AssetManagerViewMode.Combined)
        {
            AddToClassList("ee4v-asset-manager-three-pane");
            if (mode != AssetManagerViewMode.Combined)
            {
                AddToClassList(
                    "ee4v-asset-manager-three-pane--" +
                    mode.ToString().ToLowerInvariant());
            }

            var leftPane = CreatePane(
                "ee4v-asset-manager-three-pane__pane--left",
                out var leftToolbar,
                out var leftContent);
            var mainPane = CreatePane(
                "ee4v-asset-manager-three-pane__pane--main",
                out var mainToolbar,
                out var mainContent);
            var rightPane = CreatePane(
                "ee4v-asset-manager-three-pane__pane--right",
                out var rightToolbar,
                out var rightContent);

            LeftToolbarContent = leftToolbar;
            MainToolbarContent = mainToolbar;
            RightToolbarContent = rightToolbar;
            LeftContent = leftContent;
            MainContent = mainContent;
            RightContent = rightContent;

            Add(leftPane);
            Add(mainPane);
            Add(rightPane);
        }

        public VisualElement LeftToolbarContent { get; }
        public VisualElement MainToolbarContent { get; }
        public VisualElement RightToolbarContent { get; }
        public VisualElement LeftContent { get; }
        public VisualElement MainContent { get; }
        public VisualElement RightContent { get; }

        private static VisualElement CreatePane(
            string modifierClass,
            out VisualElement toolbarContent,
            out VisualElement body)
        {
            var pane = new VisualElement();
            pane.AddToClassList("ee4v-asset-manager-three-pane__pane");
            pane.AddToClassList(modifierClass);

            var toolbar = new VisualElement();
            toolbar.AddToClassList(
                "ee4v-asset-manager-three-pane__toolbar");
            toolbarContent = new VisualElement();
            toolbarContent.AddToClassList(
                "ee4v-asset-manager-three-pane__toolbar-content");
            toolbar.Add(toolbarContent);

            body = new VisualElement();
            body.AddToClassList(
                "ee4v-asset-manager-three-pane__body");
            pane.Add(toolbar);
            pane.Add(body);
            return pane;
        }
    }
}
