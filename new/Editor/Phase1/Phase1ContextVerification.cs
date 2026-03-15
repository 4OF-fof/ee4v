using Ee4v.Injector;

namespace Ee4v.Phase1
{
    internal static class Phase1ContextVerification
    {
        public static string GetHierarchyBadge(ItemInjectionContext context, string baseLabel)
        {
            var kindLabel = "UNK";
            if (context.IsHierarchySceneHeader)
            {
                kindLabel = string.IsNullOrEmpty(context.HierarchyScene.name)
                    ? "SCENE"
                    : "SCENE:" + context.HierarchyScene.name;
            }
            else if (context.IsHierarchyGameObject)
            {
                kindLabel = "GO";
            }

            return string.IsNullOrWhiteSpace(baseLabel)
                ? kindLabel
                : baseLabel + " " + kindLabel;
        }

        public static string GetProjectBadge(ItemInjectionContext context)
        {
            switch (context.ProjectViewMode)
            {
                case ProjectItemViewMode.OneColumn:
                    return "1C";
                case ProjectItemViewMode.TwoColumns:
                    return context.ProjectOrientation == ProjectItemOrientation.Vertical
                        ? "2C-V"
                        : context.ProjectOrientation == ProjectItemOrientation.Horizontal
                            ? "2C-H"
                            : "2C";
                default:
                    return "UNK";
            }
        }
    }
}
