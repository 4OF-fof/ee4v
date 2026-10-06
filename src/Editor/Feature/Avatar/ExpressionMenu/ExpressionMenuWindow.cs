using Ee4v.AvatarEditing;
using UnityEditor;

namespace Ee4v.ExpressionMenu
{
    internal sealed class ExpressionMenuWindow : AvatarPrefabEditorWindow
    {
        protected override string TitleKey => "avatarEditor.expressionMenuTitle";
        protected override bool UsesBodyPartSelector => false;
        protected override bool ShowsPreview => false;
        protected override bool ShowsRevertButton => false;
        [MenuItem("ee4v/Window/Avatar/Expression Menu", false, 204)]
        private static void ShowWindow() => GetWindow<ExpressionMenuWindow>().Show();
        protected override void CreateFeature() { }
        protected override void ClearFeatureData() { }
        protected override void DisposeFeature() { }
        protected override void RenderFeature() => Context.ControlsHost.Add(new ExpressionMenuView(Context, showsEditSource: true));
    }
}
