namespace Ee4v.AssetManager.UI
{
    internal enum WorkflowCategory
    {
        Overview,
        MenuAndGestures,
        ShapeParts,
        Material,
        ExpressionAnimation,
        Lighting,
        ExpressionMenu
    }

    internal static class WorkflowCategories
    {
        internal static bool IsPlayMode(this WorkflowCategory category) =>
            category == WorkflowCategory.MenuAndGestures || category == WorkflowCategory.Lighting;
    }
}
