using Ee4v.AvatarEditing;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Ee4v.UI;

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
        protected override void RenderFeature()
        {
            minSize = new Vector2(480f, 560f);
            UiComposition.Prepare(Context.ControlsHost, "Editor/Feature/Avatar/ExpressionMenu/expression-menu.uss");
            Context.ControlsHost.AddToClassList("ee4v-expression-menu__standalone-host");
            Context.ControlsHost.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            Context.ControlsHost.Add(new ExpressionMenuView(Context, showsEditSource: true));
        }
    }
}
