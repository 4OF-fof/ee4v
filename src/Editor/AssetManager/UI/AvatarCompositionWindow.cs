using UnityEditor;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AvatarCompositionWindow : AssetModificationEditorWindow
    {
        protected override ModificationEditorMode EditorMode =>
            ModificationEditorMode.Composition;
        protected override string TitleKey => "workflow.standalone.compositionTitle";

        [MenuItem("ee4v/Window/Avatar/Prefab Composition", false, 202)]
        private static void ShowWindow()
        {
            GetWindow<AvatarCompositionWindow>().Show();
        }
    }
}
