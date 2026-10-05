using Ee4v.Core.I18n;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal sealed class GestureAssignmentWindow : EditorWindow
    {
        [MenuItem("ee4v/Window/Avatar/Gesture Assignment/Assignments", false, 240)]
        private static void Open()
        {
            ShowFor(Selection.activeGameObject);
        }

        internal static void ShowFor(GameObject avatar)
        {
            FaceExpressionWindow.ShowFor(avatar);
        }

        private void CreateGUI()
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null) { return; }
                ShowFor(Selection.activeGameObject);
                Close();
            };
        }

        internal static GestureAssignmentViewText CreateText()
        {
            return new GestureAssignmentViewText
            {
                Avatar = I18N.Get("field.avatar"),
                LeftHand = I18N.Get("assignments.leftHand"),
                RightHand = I18N.Get("assignments.rightHand"),
                Selection = I18N.Get("assignments.selection"),
                NoSelection = I18N.Get("assignments.noSelection"),
                ExpressionSettings = I18N.Get("assignments.expressionSettings"),
                Synchronization = I18N.Get("assignments.synchronization"),
                SynchronizeLeft = I18N.Get("assignments.synchronizeLeft"),
                SynchronizeRight = I18N.Get("assignments.synchronizeRight"),
                GlobalSettings = I18N.Get("assignments.globalSettings"),
                Clip = I18N.Get("field.clip"),
                EnableBlink = I18N.Get("assignments.enableBlink"),
                FixMouth = I18N.Get("assignments.fixMouth"),
                Unassigned = I18N.Get("assignments.unassigned"),
                MenuOnly = I18N.Get("assignments.menuOnly"),
                MenuOnlyHint = I18N.Get("assignments.menuOnlyHint"),
                MenuName = I18N.Get("assignments.menuName"),
                DisableMenuIcons = I18N.Get("assignments.disableMenuIcons"),
                AddMenuExpression = I18N.Get("action.addMenuExpression"),
                Remove = I18N.Get("action.remove"),
                GestureName = gesture => I18N.Get("gesture." + gesture)
            };
        }
    }
}
