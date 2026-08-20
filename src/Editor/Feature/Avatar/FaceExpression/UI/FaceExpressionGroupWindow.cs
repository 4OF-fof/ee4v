using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal sealed class FaceExpressionGroupWindow : EditorWindow
    {
        private FaceExpressionGroupView _view;

        [MenuItem("ee4v/Avatar/Face Expression Groups")]
        internal static void ShowWindow()
        {
            var window = GetWindow<FaceExpressionGroupWindow>();
            window.ConfigureWindow();
            window.Show();
        }

        private void OnEnable()
        {
            FaceExpressionGroupSession.Changed += Refresh;
            I18N.Reloaded += Rebuild;
            ConfigureWindow();
        }

        private void OnDisable()
        {
            FaceExpressionGroupSession.Changed -= Refresh;
            I18N.Reloaded -= Rebuild;
        }

        private void CreateGUI()
        {
            BuildContent();
            Refresh();
        }

        private void Rebuild()
        {
            if (rootVisualElement.panel == null)
            {
                return;
            }

            BuildContent();
            Refresh();
        }

        private void BuildContent()
        {
            ConfigureWindow();
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList("ee4v-ui");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/Feature/Avatar/FaceExpression/UI/face-expression.uss");
            _view = new FaceExpressionGroupView(
                new FaceExpressionGroupViewText
                {
                    Groups = I18N.Get("section.groups"),
                    All = I18N.Get("group.all")
                });
            _view.GroupSelected += FaceExpressionGroupSession.SelectGroup;
            root.Add(_view);
        }

        private void Refresh()
        {
            _view?.SetGroups(
                FaceExpressionGroupSession.Groups,
                FaceExpressionGroupSession.TotalCount,
                FaceExpressionGroupSession.SelectedGroupName);
        }

        private void ConfigureWindow()
        {
            titleContent = UiTextFactory.CreateGuiContent(
                I18N.Get("groupWindow.title"));
            minSize = new Vector2(220f, 360f);
        }
    }
}
