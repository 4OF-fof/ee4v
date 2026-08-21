using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    internal sealed class BlendShapePresetWindow : EditorWindow
    {
        private BlendShapePresetView _view;

        [MenuItem("ee4v/Window/Face Expression/BlendShape Presets")]
        private static void Open()
        {
            ShowWindow();
        }

        internal static void ShowWindow()
        {
            var window = GetWindow<BlendShapePresetWindow>();
            window.ConfigureWindow();
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            I18N.Reloaded += Rebuild;
            ConfigureWindow();
        }

        private void OnDisable()
        {
            I18N.Reloaded -= Rebuild;
            _view?.Dispose();
            _view = null;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            UiComposition.Prepare(
                root,
                "Editor/UI/Components/Inputs/ui-button.uss",
                "Editor/Feature/Avatar/FaceExpression/UI/face-expression.uss");
            _view?.Dispose();
            _view = new BlendShapePresetView(CoreSettings.Current);
            root.Add(_view);

            var selection = Selection.activeGameObject;
            if (BlendShapeNamePresetSetting.ResolveSourceFbx(selection) !=
                null)
            {
                _view.SelectAvatar(selection);
            }
        }

        private void Rebuild()
        {
            if (rootVisualElement.panel == null)
            {
                return;
            }

            ConfigureWindow();
            _view?.Rebuild();
        }

        private void ConfigureWindow()
        {
            titleContent = UiTextFactory.CreateGuiContent(
                I18N.Get("presetWindow.title"));
            minSize = new Vector2(840f, 480f);
        }
    }
}
