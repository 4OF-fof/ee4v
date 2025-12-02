using System.IO;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Setting;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.UI.Window;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AvatarModifyUtility.Variant {
    public class VariantConverterWindow : BaseWindow {
        private Label _errorLabel;
        private TextField _nameField;
        private GameObject _targetObject;

        [MenuItem("GameObject/ee4v/Create Variant", false, -1)]
        private static void CreateVariant(MenuCommand menuCommand) {
            var obj = menuCommand.context as GameObject;
            if (obj == null) return;

            if (AssetDatabase.Contains(obj)) return;

            Vector2 mousePos;
            if (Event.current != null) {
                mousePos = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            }
            else {
                var win = focusedWindow;
                if (win != null)
                    mousePos = win.position.position + new Vector2(100, 100);
                else
                    mousePos = new Vector2(200, 200);
            }

            Show(obj, mousePos);
        }

        [MenuItem("GameObject/ee4v/Create Variant", true, -1)]
        private static bool ValidateCreateVariant() {
            var obj = Selection.activeGameObject;
            if (obj == null || AssetDatabase.Contains(obj)) return false;
            return obj != null && PrefabUtility.IsAnyPrefabInstanceRoot(obj);
        }

        private static void Show(GameObject target, Vector2 position) {
            var window = OpenSetup<VariantConverterWindow>(position);
            window.IsLocked = true;
            window._targetObject = target;
            window.titleContent = new GUIContent(I18N.Get("UI.HierarchyExtension.CreateVariantWindow.Title"));
            window.position = new Rect(position.x, position.y, 340, 180);
            window.ShowPopup();
        }

        protected override VisualElement HeaderContent() {
            var label = new Label(I18N.Get("UI.HierarchyExtension.CreateVariantWindow.Title")) {
                style = {
                    unityTextAlign = TextAnchor.MiddleCenter,
                    fontSize = 12,
                    color = ColorPreset.TextColor,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            return label;
        }

        protected override VisualElement Content() {
            var container = new VisualElement {
                style = {
                    flexGrow = 1,
                    paddingTop = 16,
                    paddingBottom = 16,
                    paddingLeft = 16,
                    paddingRight = 16,
                    backgroundColor = ColorPreset.DefaultBackground
                }
            };

            var inputLabel = new Label(I18N.Get("UI.HierarchyExtension.CreateVariantWindow.NameLabel")) {
                style = {
                    fontSize = 11,
                    marginBottom = 4,
                    color = ColorPreset.TextColor
                }
            };
            container.Add(inputLabel);

            var defaultName = _targetObject != null ? _targetObject.name + "_Variant" : "NewVariant";
            _nameField = new TextField {
                value = defaultName,
                style = {
                    marginBottom = 8,
                    height = 24
                }
            };

            _nameField.RegisterCallback<KeyDownEvent>(evt =>
            {
                switch (evt.keyCode) {
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        OnCreateClicked();
                        evt.StopPropagation();
                        break;
                    case KeyCode.Escape:
                        Close();
                        evt.StopPropagation();
                        break;
                }
            });
            _nameField.RegisterCallback<InputEvent>(_ => { _errorLabel.text = string.Empty; });
            container.Add(_nameField);

            _errorLabel = new Label {
                style = {
                    color = ColorPreset.WarningText,
                    fontSize = 10,
                    marginBottom = 8,
                    minHeight = 20,
                    unityTextAlign = TextAnchor.UpperLeft,
                    whiteSpace = WhiteSpace.Normal
                }
            };
            container.Add(_errorLabel);

            var buttonContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexEnd,
                    marginTop = 4
                }
            };

            var cancelButton = new Button(Close) {
                text = I18N.Get("UI.Core.Cancel"),
                style = {
                    width = 80,
                    height = 26,
                    marginRight = 8
                }
            };
            buttonContainer.Add(cancelButton);

            var createButton = new Button(OnCreateClicked) {
                text = I18N.Get("UI.ProjectExtension.Create"),
                style = {
                    width = 80,
                    height = 26,
                    backgroundColor = ColorPreset.SuccessButtonStyle,
                    color = ColorPreset.TextColor
                }
            };
            buttonContainer.Add(createButton);

            container.Add(buttonContainer);

            _nameField.schedule.Execute(() =>
            {
                _nameField.Focus();
                _nameField.SelectAll();
            }).ExecuteLater(100);

            return container;
        }

        private void OnCreateClicked() {
            var variantName = _nameField.value?.Trim();

            if (string.IsNullOrEmpty(variantName)) {
                _errorLabel.text = I18N.Get("UI.ProjectExtension.WorkspaceNameEmpty");
                return;
            }

            if (variantName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) {
                _errorLabel.text = I18N.Get("UI.AssetManager.Dialog.RenameFolder.Error.InvalidName");
                return;
            }

            var rootFolder = Settings.I.variantCreateFolderPath;
            var targetPath = Path.Combine(rootFolder, variantName).Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(targetPath)) {
                _errorLabel.text = I18N.Get("UI.HierarchyExtension.CreateVariantWindow.Error.AlreadyExists");
                return;
            }

            VariantConverter.CreateVariantWithMaterials(_targetObject, variantName);
            Close();
        }
    }
}