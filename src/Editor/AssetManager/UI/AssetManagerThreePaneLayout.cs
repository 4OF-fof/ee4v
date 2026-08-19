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
                false,
                out _,
                out var leftContent);
            var mainPane = CreatePane(
                "ee4v-asset-manager-three-pane__pane--main",
                true,
                out var mainToolbar,
                out var mainContent);
            var rightPane = CreatePane(
                "ee4v-asset-manager-three-pane__pane--right",
                false,
                out _,
                out var rightContent);

            MainToolbarContent = mainToolbar;
            LeftContent = leftContent;
            MainContent = mainContent;
            RightContent = rightContent;

            Add(leftPane);
            Add(mainPane);
            Add(rightPane);
        }

        public VisualElement MainToolbarContent { get; }
        public VisualElement LeftContent { get; }
        public VisualElement MainContent { get; }
        public VisualElement RightContent { get; }

        private static VisualElement CreatePane(
            string modifierClass,
            bool hasToolbar,
            out VisualElement toolbarContent,
            out VisualElement body)
        {
            var pane = new VisualElement();
            pane.AddToClassList("ee4v-asset-manager-three-pane__pane");
            pane.AddToClassList(modifierClass);

            toolbarContent = null;
            if (hasToolbar)
            {
                var toolbar = new VisualElement();
                toolbar.AddToClassList(
                    "ee4v-asset-manager-three-pane__toolbar");
                toolbarContent = new VisualElement();
                toolbarContent.AddToClassList(
                    "ee4v-asset-manager-three-pane__toolbar-content");
                toolbar.Add(toolbarContent);
                pane.Add(toolbar);
            }

            body = new VisualElement();
            body.AddToClassList(
                "ee4v-asset-manager-three-pane__body");
            pane.Add(body);
            return pane;
        }
    }
}
