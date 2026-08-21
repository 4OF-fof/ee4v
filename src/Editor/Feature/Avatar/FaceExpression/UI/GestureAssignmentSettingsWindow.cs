using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal sealed class GestureAssignmentSettingsWindow : EditorWindow
    {
        [MenuItem("ee4v/Window/Gesture Assignment/Gesture Assignment Settings")]
        internal static void ShowWindow()
        {
            var window = GetWindow<GestureAssignmentSettingsWindow>();
            window.ConfigureWindow();
            window.Show();
        }

        private void OnEnable()
        {
            I18N.Reloaded += Rebuild;
            ConfigureWindow();
        }

        private void OnDisable()
        {
            I18N.Reloaded -= Rebuild;
        }

        private void CreateGUI()
        {
            BuildContent();
        }

        private void Rebuild()
        {
            if (rootVisualElement.panel == null)
            {
                return;
            }

            BuildContent();
        }

        private void BuildContent()
        {
            ConfigureWindow();
            var root = rootVisualElement;
            root.Clear();
            UiComposition.Prepare(
                root,
                "Editor/UI/Components/Inputs/ui-button.uss",
                "Editor/Feature/Avatar/FaceExpression/UI/face-expression.uss");
            root.Add(new GestureAssignmentSettingsView(
                GestureAssignmentWindow.CreateText()));
        }

        private void ConfigureWindow()
        {
            titleContent = UiTextFactory.CreateGuiContent(
                I18N.Get("assignmentSettingsWindow.title"));
            minSize = new Vector2(360f, 120f);
        }
    }
}
