namespace Ee4v.AssetManager.UI
{
    internal enum WorkflowCategory
    {
        Overview,
        MenuAndGestures,
        Appearance,
        ExpressionAnimation = 4,
        Lighting,
        ExpressionMenu
    }

    internal static class WorkflowCategories
    {
        internal static bool IsPlayMode(this WorkflowCategory category) =>
            category == WorkflowCategory.MenuAndGestures || category == WorkflowCategory.Lighting;
    }
}
