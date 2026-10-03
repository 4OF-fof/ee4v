using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class WorkflowEditorLayout : VisualElement
    {
        internal WorkflowEditorLayout(VisualElement navigation,
            VisualElement preview, bool showControls = true)
        {
            AddToClassList("ee4v-modification-workflow__body");
            if (navigation != null) { Add(navigation); }
            if (!showControls)
            {
                Add(preview);
                return;
            }

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
        internal VisualElement ExecutionHost { get; }

        internal void ShowCategory(WorkflowCategory category)
        {
            if (AppearanceHost == null) { return; }
            AppearanceHost.EnableInClassList("ee4v-modification-workflow__hidden",
                category == WorkflowCategory.ExpressionAnimation || category == WorkflowCategory.Execution);
            FaceExpressionHost.EnableInClassList("ee4v-modification-workflow__hidden",
                category != WorkflowCategory.ExpressionAnimation);
            ExecutionHost.EnableInClassList("ee4v-modification-workflow__hidden",
                category != WorkflowCategory.Execution);
        }
    }
}
