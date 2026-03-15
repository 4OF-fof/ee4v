using System;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Views.Components.AssetInfo {
    public class Identity : VisualElement {
        private readonly TextField _descriptionField;
        private readonly Label _folderHeader;
        private readonly Label _folderNameLabel;
        private readonly VisualElement _folderRow;
        private readonly TextField _nameField;
        private Ulid _currentFolderId;

        public Identity() {
            Add(new Label(I18N.Get("UI.AssetManager.AssetInfo.Name"))
                { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 12, marginBottom = 4 } });

            _nameField = new TextField
                { isDelayed = true, style = { marginBottom = 4, unityFontStyleAndWeight = FontStyle.Bold } };
            _nameField.RegisterCallback<ChangeEvent<string>>(evt => OnNameChanged?.Invoke(evt.newValue));
            Add(_nameField);

            Add(new Label(I18N.Get("UI.AssetManager.AssetInfo.Description"))
                { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 12, marginBottom = 4 } });

            var descScroll = new ScrollView(ScrollViewMode.Vertical) { style = { maxHeight = 200, marginBottom = 4 } };
            _descriptionField = new TextField
                { multiline = true, isDelayed = true, style = { minHeight = 40, whiteSpace = WhiteSpace.Normal } };
            _descriptionField.RegisterCallback<ChangeEvent<string>>(evt =>
                OnDescriptionChanged?.Invoke(evt.newValue));
            descScroll.Add(_descriptionField);
            Add(descScroll);

            _folderHeader = new Label(I18N.Get("UI.AssetManager.AssetInfo.Folder"))
                { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 12, marginBottom = 4 } };
            Add(_folderHeader);

            _folderRow = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 10,
                    backgroundColor = ColorPreset.GroupBackGround,
                    paddingLeft = 4, paddingTop = 4, paddingBottom = 4,
                    borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4
                }
            };
            _folderRow.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0 || _currentFolderId == Ulid.Empty) return;
                OnFolderClicked?.Invoke(_currentFolderId);
                evt.StopPropagation();
            });
            _folderRow.RegisterCallback<MouseEnterEvent>(_ =>
                _folderRow.style.backgroundColor = ColorPreset.GroupBackGroundHover);
            _folderRow.RegisterCallback<MouseLeaveEvent>(_ =>
                _folderRow.style.backgroundColor = ColorPreset.GroupBackGround);

            _folderRow.Add(new Image {
                image = EditorGUIUtility.IconContent("Folder Icon").image,
                style = { width = 16, height = 16, marginRight = 4, flexShrink = 0 }
            });

            _folderNameLabel = new Label("-") {
                style = {
                    flexGrow = 1, flexShrink = 1, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis,
                    whiteSpace = WhiteSpace.NoWrap
                }
            };
            _folderRow.Add(_folderNameLabel);
            Add(_folderRow);
        }

        public event Action<string> OnNameChanged;
        public event Action<string> OnDescriptionChanged;
        public event Action<Ulid> OnFolderClicked;

        public void SetData(string assetName, string desc, Ulid folderId, string folderName) {
            _nameField.SetValueWithoutNotify(assetName);
            _descriptionField.SetValueWithoutNotify(desc);
            _currentFolderId = folderId;

            if (folderId != Ulid.Empty) {
                _folderHeader.style.display = DisplayStyle.Flex;
                _folderRow.style.display = DisplayStyle.Flex;
                _folderNameLabel.text = folderName;
                _folderNameLabel.tooltip = folderName;
            }
            else {
                _folderHeader.style.display = DisplayStyle.None;
                _folderRow.style.display = DisplayStyle.None;
            }
        }
    }
}