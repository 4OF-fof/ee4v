using UnityEditor;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AvatarShapePartsWindow : AssetModificationEditorWindow
    {
        protected override ModificationEditorMode EditorMode =>
            ModificationEditorMode.ShapeParts;
        protected override string TitleKey => "workflow.standalone.shapePartsTitle";

        [MenuItem("ee4v/Window/Avatar/Shape and Parts", false, 200)]
        private static void ShowWindow()
        {
            GetWindow<AvatarShapePartsWindow>().Show();
        }
    }
}
