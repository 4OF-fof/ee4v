using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Service;
using _4OF.ee4v.AssetManager.Utility;
using _4OF.ee4v.Core.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component.Dialog {
    public class CreateAssetDialog {
        private readonly List<string> _tempTags = new();
        private Label _errorLabel;
        private IAssetRepository _repository;

        public event Action<string, string, string, List<string>, string, string> OnAssetCreated;
        public event Action OnImportFromBoothRequested;

        public void SetRepository(IAssetRepository repository) {
            _repository = repository;
        }

        public VisualElement CreateContent() {
            _tempTags.Clear();
            var content = new VisualElement();


            var title = new Label("Create New Asset") {
                style = {
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 10
                }
            };
            var titleRow = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    position = Position.Relative
                }
            };

            titleRow.Add(title);

            var importBoothBtn = new Button {
                text = "Import from Booth",
                style = {
                    position = Position.Absolute,
                    right = 0,
                    top = 0,
                    marginRight = 0,
                    marginTop = 0,
                    marginBottom = 8,
                    paddingLeft = 8,
                    paddingRight = 8,
                    height = 22,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 11
                }
            };

            importBoothBtn.clicked += () =>
            {
                CloseDialog(content);
                OnImportFromBoothRequested?.Invoke();
            };

            titleRow.Add(importBoothBtn);
            content.Add(titleRow);

            var nameLabel = new Label("Asset Name:") {
                style = { marginBottom = 5 }
            };
            content.Add(nameLabel);

            var nameField = new TextField { value = "", style = { marginBottom = 10 } };
            content.Add(nameField);

            var descLabel = new Label("Description (optional):") {
                style = { marginBottom = 5 }
            };
            content.Add(descLabel);

            var descField = new TextField {
                multiline = true,
                value = "",
                style = {
                    marginBottom = 10,
                    minHeight = 60
                }
            };
            content.Add(descField);

            var fileUrlLabel = new Label("File Path or URL (optional):") {
                style = { marginBottom = 5 }
            };
            content.Add(fileUrlLabel);

            var fileUrlRow = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    marginBottom = 10
                }
            };

            var fileUrlField = new TextField {
                value = "",
                style = { flexGrow = 1, marginRight = 5 }
            };
            fileUrlRow.Add(fileUrlField);

            var browseBtn = new Button {
                text = "Browse",
                style = { width = 70 }
            };
            browseBtn.clicked += () =>
            {
                var path = EditorUtility.OpenFilePanel("Select Asset File", "", "");
                if (!string.IsNullOrEmpty(path))
                    fileUrlField.value = path;
            };
            fileUrlRow.Add(browseBtn);
            content.Add(fileUrlRow);

            var boothLabel = new Label("Booth URL (optional):") {
                style = {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 12,
                    marginTop = 10,
                    marginBottom = 5
                }
            };
            content.Add(boothLabel);

            var boothUrlHint = new Label("Format: [shopname].booth.pm/items/[itemid]") {
                style = {
                    fontSize = 10,
                    color = Color.gray,
                    marginBottom = 5
                }
            };
            content.Add(boothUrlHint);

            var boothUrlField = new TextField {
                value = "",
                style = { marginBottom = 10 }
            };
            content.Add(boothUrlField);

            var tagsLabel = new Label("Tags:") {
                style = {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 12,
                    marginBottom = 4
                }
            };
            content.Add(tagsLabel);

            var tagsContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    marginBottom = 10
                }
            };
            content.Add(tagsContainer);

            var addTagButton = new Button(() =>
            {
                if (_repository == null) return;
                var screenPosition = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                TagSelectorWindow.Show(screenPosition, _repository, tag =>
                {
                    if (string.IsNullOrEmpty(tag) || _tempTags.Contains(tag)) return;
                    _tempTags.Add(tag);
                    RefreshTagsUi();
                });
            }) {
                text = "+ Add Tag",
                style = {
                    backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f)),
                    borderTopLeftRadius = 10,
                    borderTopRightRadius = 10,
                    borderBottomLeftRadius = 10,
                    borderBottomRightRadius = 10,
                    paddingLeft = 10,
                    paddingRight = 10,
                    paddingTop = 4,
                    paddingBottom = 4,
                    height = 24,
                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    marginBottom = 10,
                    width = Length.Percent(100)
                }
            };
            addTagButton.RegisterCallback<MouseEnterEvent>(_ =>
            {
                addTagButton.style.backgroundColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
            });
            addTagButton.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                addTagButton.style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
            });
            content.Add(addTagButton);

            _errorLabel = new Label {
                style = {
                    color = ColorPreset.WarningText,
                    whiteSpace = WhiteSpace.Normal,
                    marginBottom = 5,
                    display = DisplayStyle.None
                }
            };
            content.Add(_errorLabel);

            var buttonRow = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexEnd,
                    marginTop = 10
                }
            };

            var cancelBtn = new Button {
                text = "Cancel",
                style = { marginRight = 5 }
            };
            buttonRow.Add(cancelBtn);

            var createBtn = new Button {
                text = "Create"
            };
            buttonRow.Add(createBtn);

            content.Add(buttonRow);

            content.userData = new DialogData {
                NameField = nameField,
                DescField = descField,
                FileUrlField = fileUrlField,
                BoothUrlField = boothUrlField
            };

            nameField.RegisterCallback<InputEvent>(_ => HideError());
            boothUrlField.RegisterCallback<InputEvent>(_ => HideError());

            cancelBtn.clicked += () => CloseDialog(content);
            createBtn.clicked += () => OnCreate(content);

            content.schedule.Execute(() => nameField.Focus());

            return content;

            void RefreshTagsUi() {
                tagsContainer.Clear();
                foreach (var pill in _tempTags.Select(tag => CreateTagPill(tag, () =>
                         {
                             _tempTags.Remove(tag);
                             RefreshTagsUi();
                         })))
                    tagsContainer.Add(pill);
            }
        }

        private void ShowError(string message) {
            if (_errorLabel == null) return;
            _errorLabel.text = message;
            _errorLabel.style.display = DisplayStyle.Flex;
        }

        private void HideError() {
            if (_errorLabel == null) return;
            _errorLabel.text = "";
            _errorLabel.style.display = DisplayStyle.None;
        }

        private void OnCreate(VisualElement content) {
            if (content.userData is not DialogData data) return;

            var assetName = data.NameField.value;
            var description = data.DescField.value;
            var fileOrUrl = data.FileUrlField.value;
            var boothUrl = data.BoothUrlField.value.Trim();

            if (!AssetValidationService.IsValidAssetName(assetName)) {
                ShowError("無効なアセット名です。");
                return;
            }

            var shopDomain = "";
            var itemId = "";
            if (!string.IsNullOrWhiteSpace(boothUrl))
                if (!BoothUtility.TryParseShopItemUrl(boothUrl, out shopDomain, out itemId)) {
                    ShowError("BOOTHのURL形式が正しくありません。\n例: https://[shop].booth.pm/items/[id]");
                    return;
                }

            OnAssetCreated?.Invoke(assetName, description, fileOrUrl, new List<string>(_tempTags), shopDomain, itemId);
            CloseDialog(content);
        }

        private static void CloseDialog(VisualElement content) {
            var dialog = content.parent;
            var container = dialog?.parent;
            container?.RemoveFromHierarchy();
        }

        private static VisualElement CreateTagPill(string tag, Action onRemove) {
            var pill = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f)),
                    borderTopLeftRadius = 10,
                    borderTopRightRadius = 10,
                    borderBottomLeftRadius = 10,
                    borderBottomRightRadius = 10,
                    paddingLeft = 8,
                    paddingRight = 4,
                    paddingTop = 2,
                    paddingBottom = 2,
                    marginRight = 4,
                    marginBottom = 4,
                    alignItems = Align.Center
                }
            };

            var label = new Label(tag) { style = { marginRight = 4 } };

            pill.RegisterCallback<MouseEnterEvent>(_ =>
            {
                pill.style.backgroundColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
            });
            pill.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                pill.style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
            });

            var removeBtn = new Button(() => onRemove?.Invoke()) {
                text = "×",
                style = {
                    width = 16,
                    height = 16,
                    fontSize = 10,
                    backgroundColor = Color.clear,
                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    paddingLeft = 0,
                    paddingRight = 0
                }
            };

            removeBtn.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            removeBtn.RegisterCallback<MouseEnterEvent>(_ =>
            {
                removeBtn.style.backgroundColor = new Color(0.8f, 0.3f, 0.3f);
                removeBtn.style.color = Color.white;
            });
            removeBtn.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                removeBtn.style.backgroundColor = Color.clear;
                removeBtn.style.color = new StyleColor(StyleKeyword.Null);
            });

            pill.Add(label);
            pill.Add(removeBtn);
            return pill;
        }

        private class DialogData {
            public TextField BoothUrlField;
            public TextField DescField;
            public TextField FileUrlField;
            public TextField NameField;
        }
    }
}