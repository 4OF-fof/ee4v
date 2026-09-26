using UnityEditor;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AvatarMaterialsWindow : AssetModificationEditorWindow
    {
        protected override ModificationEditorMode EditorMode =>
            ModificationEditorMode.Materials;
        protected override string TitleKey => "workflow.standalone.materialsTitle";

        [MenuItem("ee4v/Window/Avatar/Materials")]
        private static void ShowWindow()
        {
            GetWindow<AvatarMaterialsWindow>().Show();
        }
    }
}
