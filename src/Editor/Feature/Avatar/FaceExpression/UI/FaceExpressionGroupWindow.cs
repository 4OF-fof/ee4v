using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal sealed class FaceExpressionGroupWindow : EditorWindow
    {
        private FaceExpressionGroupView _view;
        private string _removeMeshText;

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
            FaceExpressionGroupSession.MeshesChanged += Refresh;
            I18N.Reloaded += Rebuild;
            ConfigureWindow();
        }

        private void OnDisable()
        {
            FaceExpressionGroupSession.Changed -= Refresh;
            FaceExpressionGroupSession.MeshesChanged -= Refresh;
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
                "Editor/UI/Components/Inputs/ui-button.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/Feature/Avatar/FaceExpression/UI/face-expression.uss");
            _view = new FaceExpressionGroupView(
                new FaceExpressionGroupViewText
                {
                    Groups = I18N.Get("section.groups"),
                    All = I18N.Get("group.all"),
                    Meshes = I18N.Get("section.meshes"),
                    AddMesh = I18N.Get("action.addMesh"),
                    MeshGroupSection = I18N.Get("group.sectionMeshes"),
                    BlendShapeGroupSection = I18N.Get("group.sectionBlendShapes")
                });
            _removeMeshText = I18N.Get("action.removeMesh");
            _view.GroupSelected += FaceExpressionGroupSession.SelectGroup;
            _view.AddMeshRequested += ShowMeshMenu;
            _view.RemoveMeshRequested += FaceExpressionGroupSession.RemoveMesh;
            root.Add(_view);
        }

        private void Refresh()
        {
            _view?.SetGroups(
                FaceExpressionGroupSession.Groups,
                FaceExpressionGroupSession.TotalCount,
                FaceExpressionGroupSession.SelectedGroupKey);
            _view?.SetMeshes(
                FaceExpressionGroupSession.SelectedMeshes,
                _removeMeshText);
        }

        private void ShowMeshMenu()
        {
            var selected = FaceExpressionGroupSession.RendererPaths;
            var options = FaceExpressionGroupSession.AvailableMeshes
                .Where(mesh => !selected.Contains(mesh.Path))
                .ToArray();
            var menu = new GenericMenu();
            if (options.Length == 0)
            {
                menu.AddDisabledItem(UiTextFactory.CreateGuiContent(
                    I18N.Get("group.noMeshes")));
            }
            else
            {
                for (var index = 0; index < options.Length; index++)
                {
                    var option = options[index];
                    menu.AddItem(
                        UiTextFactory.CreateGuiContent(option.DisplayName),
                        false,
                        () => FaceExpressionGroupSession.AddMesh(option.Path));
                }
            }

            menu.ShowAsContext();
        }

        private void ConfigureWindow()
        {
            titleContent = UiTextFactory.CreateGuiContent(
                I18N.Get("groupWindow.title"));
            minSize = new Vector2(280f, 420f);
        }
    }
}
