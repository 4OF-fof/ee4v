using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class WorkflowEditorLayout : VisualElement
    {
        internal WorkflowEditorLayout(VisualElement navigation,
            VisualElement preview)
        {
            AddToClassList("ee4v-modification-workflow__body");
            if (navigation != null) { Add(navigation); }
            AppearanceHost = new VisualElement();
            AppearanceHost.AddToClassList("ee4v-modification-workflow__customizer-host");
            AppearanceHost.Add(preview);
            var controlsColumn = new VisualElement();
            controlsColumn.AddToClassList("ee4v-modification-workflow__controls-column");
            AppearanceHeader = new VisualElement();
            AppearanceHeader.AddToClassList("ee4v-modification-workflow__appearance-header");
            controlsColumn.Add(AppearanceHeader);
            Controls = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible
            };
            Controls.AddToClassList("ee4v-modification-workflow__controls");
            controlsColumn.Add(Controls);
            AppearanceHost.Add(controlsColumn);

            FaceExpressionHost = new VisualElement();
            FaceExpressionHost.AddToClassList("ee4v-modification-workflow__face-expression-host");
            FaceExpressionHost.AddToClassList("ee4v-modification-workflow__hidden");
            Add(FaceExpressionHost);
            ExpressionMenuHost = new VisualElement();
            ExpressionMenuHost.AddToClassList("ee4v-modification-workflow__expression-menu-host");
            ExpressionMenuHost.AddToClassList("ee4v-modification-workflow__hidden");
            Add(ExpressionMenuHost);
            ExecutionHost = new VisualElement();
            ExecutionHost.AddToClassList("ee4v-execution-host");
            ExecutionHost.AddToClassList("ee4v-modification-workflow__hidden");
            Add(ExecutionHost);
            Add(AppearanceHost);
        }

        internal VisualElement AppearanceHost { get; }
        internal VisualElement AppearanceHeader { get; }
        internal ScrollView Controls { get; }
        internal VisualElement FaceExpressionHost { get; }
        internal VisualElement ExpressionMenuHost { get; }
        internal VisualElement ExecutionHost { get; }

        internal void ShowCategory(WorkflowCategory category)
        {
            AppearanceHost.EnableInClassList("ee4v-modification-workflow__hidden",
                category == WorkflowCategory.ExpressionAnimation || category == WorkflowCategory.ExpressionMenu || category.IsPlayMode());
            FaceExpressionHost.EnableInClassList("ee4v-modification-workflow__hidden",
                category != WorkflowCategory.ExpressionAnimation);
            ExpressionMenuHost.EnableInClassList("ee4v-modification-workflow__hidden",
                category != WorkflowCategory.ExpressionMenu);
            ExecutionHost.EnableInClassList("ee4v-modification-workflow__hidden",
                !category.IsPlayMode());
        }
    }
}
