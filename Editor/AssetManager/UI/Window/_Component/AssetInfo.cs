using System;
using System.Collections.Generic;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Service;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class AssetInfo : VisualElement {
        private readonly TextField _descriptionField;
        private readonly Label _folderNameLabel;
        private readonly VisualElement _infoContainer;
        private readonly TextField _nameField;
        private readonly TextField _newTagField;
        private readonly VisualElement _tagsContainer;
        private readonly VisualElement _thumbnailContainer;

        private AssetMetadata _currentAsset;
        private BaseFolder _currentFolder;
        private IAssetRepository _repository;
        private TextureService _textureService;

        public AssetInfo() {
            style.flexDirection = FlexDirection.Column;
            style.paddingLeft = 10;
            style.paddingRight = 10;
            style.paddingTop = 10;
            style.paddingBottom = 10;
            style.backgroundColor = ColorPreset.DefaultBackground;

            _thumbnailContainer = new VisualElement {
                style = {
                    alignSelf = Align.Center,
                    width = 100,
                    height = 100,
                    marginBottom = 10,
                    backgroundColor = new StyleColor(new Color(0, 0, 0, 0.2f)),
                    borderTopLeftRadius = 4, borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4, borderBottomRightRadius = 4,
                    overflow = Overflow.Hidden
                }
            };
            Add(_thumbnailContainer);

            _nameField = CreateTextField(true);
            _nameField.style.marginBottom = 4;
            _nameField.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (_currentAsset != null && _nameField.value != _currentAsset.Name)
                    OnNameChanged?.Invoke(_nameField.value);
            });
            Add(_nameField);

            _descriptionField = CreateTextField(false);
            _descriptionField.multiline = true;
            _descriptionField.style.minHeight = 40;
            _descriptionField.style.marginBottom = 4;
            _descriptionField.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (_currentAsset != null && _descriptionField.value != _currentAsset.Description)
                    OnDescriptionChanged?.Invoke(_descriptionField.value);
            });
            Add(_descriptionField);

            var tagLabel = new Label("Tags")
                { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 12, marginBottom = 4 } };
            Add(tagLabel);

            _tagsContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    marginBottom = 4
                }
            };
            Add(_tagsContainer);

            var tagInputContainer = new VisualElement
                { style = { flexDirection = FlexDirection.Row, marginBottom = 10 } };
            _newTagField = new TextField { style = { flexGrow = 1, marginRight = 4 } };
            _newTagField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return) AddNewTag();
            });

            var addTagButton = new Button(AddNewTag) { text = "+", style = { width = 24 } };
            tagInputContainer.Add(_newTagField);
            tagInputContainer.Add(addTagButton);
            Add(tagInputContainer);

            var folderHeader = new Label("Folder")
                { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 12, marginBottom = 4 } };
            Add(folderHeader);

            var folderRow = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 10,
                    backgroundColor = new StyleColor(new Color(0, 0, 0, 0.1f)),
                    paddingLeft = 4, paddingTop = 4, paddingBottom = 4,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4
                }
            };
            var folderIcon = new Image {
                image = EditorGUIUtility.IconContent("Folder Icon").image,
                style = { width = 16, height = 16, marginRight = 4 }
            };
            _folderNameLabel = new Label("-") { style = { flexGrow = 1 } };

            folderRow.Add(folderIcon);
            folderRow.Add(_folderNameLabel);
            Add(folderRow);

            var infoHeader = new Label("Information")
                { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 12, marginBottom = 4 } };
            Add(infoHeader);

            _infoContainer = new VisualElement { style = { paddingLeft = 4 } };
            Add(_infoContainer);
        }

        public event Action<string> OnNameChanged;
        public event Action<string> OnDescriptionChanged;
        public event Action<string> OnTagAdded;
        public event Action<string> OnTagRemoved;

        public void Initialize(IAssetRepository repository, TextureService textureService) {
            _repository = repository;
            _textureService = textureService;
        }

        private static TextField CreateTextField(bool isBold) {
            var field = new TextField {
                isDelayed = true
            };
            if (isBold) field.style.unityFontStyleAndWeight = FontStyle.Bold;
            return field;
        }

        private void AddNewTag() {
            var tag = _newTagField.value?.Trim();
            if (string.IsNullOrEmpty(tag)) return;
            OnTagAdded?.Invoke(tag);
            _newTagField.value = "";
        }

        public void SetAsset(AssetMetadata asset) {
            _currentAsset = asset;
            _currentFolder = null;

            if (asset == null) {
                SetEnabled(false);
                ClearFields();
                return;
            }

            SetEnabled(true);

            _nameField.SetValueWithoutNotify(asset.Name);
            _descriptionField.SetValueWithoutNotify(asset.Description);

            LoadThumbnailAsync(asset.ID, false);
            RefreshTags(asset.Tags);

            var folderId = asset.Folder;
            var folder = _repository?.GetLibraryMetadata()?.GetFolder(folderId);
            _folderNameLabel.text = folder != null ? folder.Name : "Uncategorized";

            _infoContainer.Clear();
            AddInfoRow("Size", FormatSize(asset.Size));
            AddInfoRow("Type", asset.Ext.TrimStart('.').ToUpper());
            var date = DateTimeOffset.FromUnixTimeMilliseconds(asset.ModificationTime).ToLocalTime();
            AddInfoRow("Modified", date.ToString("yyyy/MM/dd HH:mm"));
        }

        public void SetFolder(BaseFolder folder) {
            _currentAsset = null;
            _currentFolder = folder;
            SetEnabled(true);

            ClearFields();
            if (folder == null) return;

            _nameField.value = folder.Name;
            _descriptionField.value = folder.Description;
            _folderNameLabel.text = "This is a Folder";

            LoadThumbnailAsync(folder.ID, true);
        }

        private void ClearFields() {
            _nameField.value = "";
            _descriptionField.value = "";
            _tagsContainer.Clear();
            _folderNameLabel.text = "-";
            _infoContainer.Clear();
            _thumbnailContainer.style.backgroundImage = null;
        }

        private async void LoadThumbnailAsync(Ulid id, bool isFolder) {
            _thumbnailContainer.style.backgroundImage = null;
            if (_textureService == null) return;

            try {
                Texture2D tex;
                if (isFolder) {
                    tex = await _textureService.GetFolderThumbnailAsync(id);
                    if (_currentFolder?.ID != id) return;
                }
                else {
                    tex = await _textureService.GetAssetThumbnailAsync(id);
                    if (_currentAsset?.ID != id) return;
                }

                if (tex != null)
                    _thumbnailContainer.style.backgroundImage = new StyleBackground(tex);
                else if (isFolder)
                    _thumbnailContainer.style.backgroundImage =
                        new StyleBackground(EditorGUIUtility.IconContent("Folder Icon").image as Texture2D);
            }
            catch {
                // ignore
            }
        }

        private void RefreshTags(IReadOnlyList<string> tags) {
            _tagsContainer.Clear();
            if (tags == null) return;

            foreach (var tag in tags) {
                var pill = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Row,
                        backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f)),
                        borderTopLeftRadius = 10,
                        borderTopRightRadius = 10,
                        borderBottomLeftRadius = 10,
                        borderBottomRightRadius = 10,
                        paddingLeft = 8, paddingRight = 4, paddingTop = 2, paddingBottom = 2,
                        marginRight = 4, marginBottom = 4,
                        alignItems = Align.Center
                    }
                };

                var label = new Label(tag) { style = { marginRight = 4 } };
                var removeBtn = new Button(() => OnTagRemoved?.Invoke(tag)) {
                    text = "×",
                    style = {
                        width = 16, height = 16,
                        fontSize = 10,
                        backgroundColor = Color.clear,
                        borderTopWidth = 0, borderBottomWidth = 0, borderLeftWidth = 0, borderRightWidth = 0,
                        paddingLeft = 0, paddingRight = 0
                    }
                };

                pill.Add(label);
                pill.Add(removeBtn);
                _tagsContainer.Add(pill);
            }
        }

        private void AddInfoRow(string label, string value) {
            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.SpaceBetween,
                    marginBottom = 2
                }
            };
            row.Add(new Label(label) { style = { color = Color.gray, width = 80 } });
            row.Add(new Label(value) { style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleRight } });
            _infoContainer.Add(row);
        }

        private static string FormatSize(long bytes) {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            var counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1) {
                number /= 1024;
                counter++;
            }

            return $"{number:n2} {suffixes[counter]}";
        }
    }
}