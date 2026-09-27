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

        [MenuItem("ee4v/Window/Avatar/Face Expression/Groups", false, 221)]
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
            UiComposition.Prepare(
                root,
                "Editor/Feature/Avatar/FaceExpression/UI/face-expression.uss");
            _view = new FaceExpressionGroupView(
                new FaceExpressionGroupViewText
                {
                    Groups = I18N.Get("section.groups"),
                    All = I18N.Get("group.all"),
                    AddMesh = I18N.Get("action.addMesh"),
                    RemoveMesh = I18N.Get("action.remove"),
                    CopyBlendShapes = I18N.Get("action.copyBlendShapes"),
                    MeshGroupSection = I18N.Get("group.sectionMeshes"),
                    BodySection = I18N.Get("group.sectionBody")
                });
            _view.GroupSelected += FaceExpressionGroupSession.SelectGroup;
            _view.AddMeshRequested += ShowMeshMenu;
            _view.RemoveMeshRequested += FaceExpressionGroupSession.RemoveMesh;
            _view.CopyBodyBlendShapesRequested += () =>
                BlendShapeClipboard.Copy(
                    FaceExpressionGroupSession.BodyChannels);
            root.Add(_view);
        }

        private void Refresh()
        {
            _view?.SetGroups(
                FaceExpressionGroupSession.Groups,
                FaceExpressionGroupSession.TotalCount,
                FaceExpressionGroupSession.SelectedGroupKey);
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
