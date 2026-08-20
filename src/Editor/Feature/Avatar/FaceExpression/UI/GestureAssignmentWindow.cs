using System.Collections.Generic;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal sealed class GestureAssignmentWindow : EditorWindow
    {
        private readonly VrchatFaceExpressionGateway _gateway =
            new VrchatFaceExpressionGateway();
        private GestureAssignmentView _view;
        private GameObject _avatar;

        [MenuItem("ee4v/Avatar/Gesture Assignments")]
        private static void Open()
        {
            ShowFor(Selection.activeGameObject);
        }

        internal static void ShowFor(GameObject avatar)
        {
            var window = GetWindow<GestureAssignmentWindow>();
            window.titleContent = UiTextFactory.CreateGuiContent(
                I18N.Get("assignmentWindow.title"));
            window.minSize = new Vector2(520f, 340f);
            window.Show();
            window.SetAvatar(avatar);
        }

        private void OnEnable()
        {
            I18N.Reloaded += Rebuild;
        }

        private void OnDisable()
        {
            I18N.Reloaded -= Rebuild;
        }

        private void CreateGUI()
        {
            BuildContent();
            if (_avatar == null && Selection.activeGameObject != null)
            {
                SetAvatar(Selection.activeGameObject);
            }
            else
            {
                RefreshAssignments();
            }
        }

        private void Rebuild()
        {
            if (rootVisualElement.panel == null)
            {
                return;
            }

            BuildContent();
            RefreshAssignments();
        }

        private void BuildContent()
        {
            titleContent = UiTextFactory.CreateGuiContent(
                I18N.Get("assignmentWindow.title"));
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

            _view = new GestureAssignmentView(new GestureAssignmentViewText
            {
                Avatar = I18N.Get("field.avatar"),
                Hint = I18N.Get("assignments.hint"),
                Apply = I18N.Get("action.apply"),
                GestureName = gesture => I18N.Get("gesture." + gesture)
            });
            _view.AvatarChanged += SetAvatar;
            _view.ApplyRequested += Apply;
            root.Add(_view);
        }

        private void SetAvatar(GameObject avatar)
        {
            _avatar = avatar;
            RefreshAssignments();
        }

        private void RefreshAssignments()
        {
            _view?.SetAvatar(_avatar);
            if (_gateway.TryRead(_avatar, out var assignments))
            {
                _view?.SetAssignments(assignments);
                _view?.SetStatus(string.Empty);
                return;
            }

            _view?.SetAssignments(
                new Dictionary<FaceGesture, AnimationClip>());
            _view?.SetStatus(I18N.Get("status.descriptorMissing"));
        }

        private void Apply(
            IReadOnlyDictionary<FaceGesture, AnimationClip> assignments)
        {
            if (_gateway.TryApply(
                    _avatar,
                    assignments,
                    out var controller,
                    out var error))
            {
                _view.SetStatus(I18N.Get("status.applied"));
                EditorGUIUtility.PingObject(controller);
                _gateway.TryRead(_avatar, out var saved);
                _view.SetAssignments(saved);
                return;
            }

            _view.SetStatus(I18N.Get(
                "status." + (error ?? "applyFailed")));
        }
    }
}
