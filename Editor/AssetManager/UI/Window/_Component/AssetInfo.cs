using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly Label _infoHeader;
        private readonly VisualElement _multiSelectionContainer;
        private readonly Label _multiSelectionLabel;
        private readonly TextField _nameField;
        private readonly TextField _newTagField;

        private readonly VisualElement _singleSelectionContainer;
        private readonly VisualElement _tagsContainer;
        private readonly VisualElement _thumbnailContainer;

        private AssetMetadata _currentAsset;
        private BaseFolder _currentFolder;
        private IAssetRepository _repository;
        private TextureService _textureService;

        public AssetInfo() {
            style.flexDirection = FlexDirection.Column;
            style.backgroundColor = ColorPreset.DefaultBackground;

            var scrollView = new ScrollView {
                style = { flexGrow = 1 }
            };
            scrollView.contentContainer.style.paddingLeft = 10;
            scrollView.contentContainer.style.paddingRight = 10;
            scrollView.contentContainer.style.paddingTop = 10;
            scrollView.contentContainer.style.paddingBottom = 10;
            Add(scrollView);

            _singleSelectionContainer = new VisualElement();
            scrollView.Add(_singleSelectionContainer);

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
            _singleSelectionContainer.Add(_thumbnailContainer);

            var nameLabel = new Label("Name")
                { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 12, marginBottom = 4 } };
            _singleSelectionContainer.Add(nameLabel);

            _nameField = CreateTextField(true);
            _nameField.style.marginBottom = 4;
            _nameField.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (_currentAsset != null && _nameField.value != _currentAsset.Name)
                    OnNameChanged?.Invoke(_nameField.value);
            });
            _singleSelectionContainer.Add(_nameField);

            var descriptionLabel = new Label("Description")
                { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 12, marginBottom = 4 } };
            _singleSelectionContainer.Add(descriptionLabel);

            var descriptionScrollView = new ScrollView {
                style = {
                    flexWrap = Wrap.Wrap,
                    maxHeight = 200,
                    marginBottom = 4
                }
            };

            _descriptionField = CreateTextField(false);
            _descriptionField.multiline = true;
            _descriptionField.style.minHeight = 40;

            _descriptionField.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (_currentAsset != null && _descriptionField.value != _currentAsset.Description)
                    OnDescriptionChanged?.Invoke(_descriptionField.value);
            });

            descriptionScrollView.Add(_descriptionField);
            _singleSelectionContainer.Add(descriptionScrollView);

            var tagLabel = new Label("Tags")
                { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 12, marginBottom = 4 } };
            _singleSelectionContainer.Add(tagLabel);

            _tagsContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    marginBottom = 4
                }
            };
            _singleSelectionContainer.Add(_tagsContainer);

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
            _singleSelectionContainer.Add(tagInputContainer);

            var folderHeader = new Label("Folder")
                { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 12, marginBottom = 4 } };
            _singleSelectionContainer.Add(folderHeader);

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
            _singleSelectionContainer.Add(folderRow);
            _multiSelectionContainer = new VisualElement {
                style = { display = DisplayStyle.None, alignItems = Align.Center, marginTop = 20, marginBottom = 40 }
            };
            _multiSelectionLabel = new Label {
                style = {
                    fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold, whiteSpace = WhiteSpace.Normal,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            _multiSelectionContainer.Add(_multiSelectionLabel);
            scrollView.Add(_multiSelectionContainer);

            _infoHeader = new Label("Information")
                { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 12, marginBottom = 4 } };
            scrollView.Add(_infoHeader);

            _infoContainer = new VisualElement { style = { paddingLeft = 4 } };
            scrollView.Add(_infoContainer);
        }

        public event Action<string> OnNameChanged;
        public event Action<string> OnDescriptionChanged;
        public event Action<string> OnTagAdded;
        public event Action<string> OnTagRemoved;

        public void Initialize(IAssetRepository repository, TextureService textureService) {
            _repository = repository;
            _textureService = textureService;
            UpdateSelection(null);
        }

        public void UpdateSelection(IReadOnlyList<object> selectedItems) {
            if (selectedItems == null || selectedItems.Count == 0) {
                ShowLibraryInfo();
            }
            else if (selectedItems.Count == 1) {
                ShowSingleSelection();
                var item = selectedItems[0];
                switch (item) {
                    case AssetMetadata asset:
                        SetAsset(asset);
                        break;
                    case BaseFolder folder:
                        SetFolder(folder);
                        break;
                }
            }
            else {
                ShowMultiSelectionInfo(selectedItems.Count);
            }
        }

        private void ShowLibraryInfo() {
            _singleSelectionContainer.style.display = DisplayStyle.None;
            _multiSelectionContainer.style.display = DisplayStyle.Flex;
            _infoHeader.style.display = DisplayStyle.None;
            _multiSelectionLabel.text = "Library Overview";

            _infoContainer.Clear();
            if (_repository == null) return;

            var allAssets = _repository.GetAllAssets().ToList();
            var totalSize = allAssets.Sum(a => a.Size);

            AddInfoRow("Total Assets", allAssets.Count.ToString());
            AddInfoRow("Total Size", FormatSize(totalSize));
            AddInfoRow("Total Tags", _repository.GetAllTags().Count.ToString());
        }

        private void ShowMultiSelectionInfo(int count) {
            _singleSelectionContainer.style.display = DisplayStyle.None;
            _multiSelectionContainer.style.display = DisplayStyle.Flex;
            _infoHeader.style.display = DisplayStyle.None;
            _multiSelectionLabel.text = $"{count} items selected";
            _infoContainer.Clear();
        }

        private void ShowSingleSelection() {
            _singleSelectionContainer.style.display = DisplayStyle.Flex;
            _multiSelectionContainer.style.display = DisplayStyle.None;
            _infoHeader.style.display = DisplayStyle.Flex;
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
                ShowLibraryInfo();
                return;
            }

            _singleSelectionContainer.SetEnabled(true);
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

        private void SetFolder(BaseFolder folder) {
            _currentAsset = null;
            _currentFolder = folder;

            if (folder == null) {
                ShowLibraryInfo();
                return;
            }

            _singleSelectionContainer.SetEnabled(true);

            ClearFields();
            _nameField.value = folder.Name;
            _descriptionField.value = folder.Description;
            _folderNameLabel.text = "This is a Folder";

            LoadThumbnailAsync(folder.ID, true);
            _infoContainer.Clear();

            var date = DateTimeOffset.FromUnixTimeMilliseconds(folder.ModificationTime).ToLocalTime();
            AddInfoRow("Modified", date.ToString("yyyy/MM/dd HH:mm"));
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