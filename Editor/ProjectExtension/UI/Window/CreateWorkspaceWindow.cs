using System.Linq;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.UI.Window;
using _4OF.ee4v.ProjectExtension.Data;
using _4OF.ee4v.ProjectExtension.UI.ToolBar._Component.Tab;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.ProjectExtension.UI.Window {
    public class CreateWorkspaceWindow : BaseWindow {
        private Label _errorLabel;
        private TextField _nameField;

        public static void Show(Vector2 position) {
            var window = OpenSetup<CreateWorkspaceWindow>(position);
            window.position = new Rect(position.x, position.y, 340, 180);
            window.ShowPopup();
        }

        protected override VisualElement HeaderContent() {
            var label = new Label(I18N.Get("UI.ProjectExtension.CreateWorkspace")) {
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

            var inputLabel = new Label(I18N.Get("UI.ProjectExtension.WorkspaceName")) {
                style = {
                    fontSize = 11,
                    marginBottom = 4,
                    color = ColorPreset.TextColor
                }
            };
            container.Add(inputLabel);

            _nameField = new TextField {
                style = {
                    marginBottom = 8,
                    height = 24
                }
            };
            _nameField.RegisterCallback<KeyDownEvent>(evt =>
            {
                switch (evt.keyCode) {
                    case KeyCode.Return or KeyCode.KeypadEnter:
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
                    maxHeight = 20,
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
                text = I18N.Get("UI.ProjectExtension.Cancel"),
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
                    height = 26
                }
            };
            buttonContainer.Add(createButton);

            container.Add(buttonContainer);

            _nameField.schedule.Execute(() => _nameField.Focus()).ExecuteLater(100);

            return container;
        }

        private void OnCreateClicked() {
            var workspaceName = _nameField.value?.Trim();

            if (string.IsNullOrEmpty(workspaceName)) {
                _errorLabel.text = I18N.Get("UI.ProjectExtension.WorkspaceNameEmpty");
                return;
            }

            if (IsWorkspaceNameExists(workspaceName)) {
                _errorLabel.text = I18N.Get("UI.ProjectExtension.WorkspaceAlreadyExists");
                return;
            }

            CreateWorkspace(workspaceName);
            Close();
        }

        private static bool IsWorkspaceNameExists(string workspaceName) {
            if (string.IsNullOrEmpty(workspaceName)) return false;

            var tabAsset = TabListController.GetInstance();
            if (tabAsset == null || tabAsset.Contents == null) return false;
            return tabAsset.Contents.Any(t => t.isWorkspace && t.tabName == workspaceName);
        }

        private static void CreateWorkspace(string workspaceName) {
            var workspaceTab = WorkspaceTab.Element(workspaceName, workspaceName);
            TabListController.AddWorkspaceTab(workspaceTab);
            TabListController.SelectTab(workspaceTab);
        }
    }
}