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

        private readonly Label _folderHeader;
        private readonly Label _folderNameLabel;
        private readonly VisualElement _folderRow;
        private readonly VisualElement _infoContainer;
        private readonly Label _infoHeader;
        private readonly VisualElement _multiSelectionContainer;
        private readonly Label _multiSelectionLabel;
        private readonly TextField _nameField;

        private readonly VisualElement _singleSelectionContainer;
        private readonly VisualElement _tagsContainer;
        private readonly VisualElement _thumbnailContainer;

        private Ulid _currentAssetFolderId;
        private AssetInfoPresenter _presenter;

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
                    width = 150,
                    height = 150,
                    marginBottom = 10,
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
            _nameField.RegisterCallback<FocusOutEvent>(_ => { OnNameChanged?.Invoke(_nameField.value); });
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
                OnDescriptionChanged?.Invoke(_descriptionField.value);
            });

            descriptionScrollView.Add(_descriptionField);
            _singleSelectionContainer.Add(descriptionScrollView);

            _folderHeader = new Label("Folder")
                { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 12, marginBottom = 4 } };
            _singleSelectionContainer.Add(_folderHeader);

            _folderRow = new VisualElement {
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
            _folderRow.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                if (_currentAssetFolderId != Ulid.Empty) OnFolderClicked?.Invoke(_currentAssetFolderId);
                evt.StopPropagation();
            });

            _folderRow.RegisterCallback<MouseEnterEvent>(_ =>
            {
                _folderRow.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.2f));
                if (_folderNameLabel != null)
                    _folderNameLabel.style.color = new StyleColor(ColorPreset.ItemSelectedBorder);
            });
            _folderRow.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                _folderRow.style.backgroundColor = new StyleColor(new Color(0, 0, 0, 0.1f));
                if (_folderNameLabel != null) _folderNameLabel.style.color = new StyleColor(StyleKeyword.Null);
            });

            var folderIcon = new Image {
                image = EditorGUIUtility.IconContent("Folder Icon").image,
                style = { width = 16, height = 16, marginRight = 4, flexShrink = 0 }
            };

            _folderNameLabel = new Label("-") {
                style = {
                    flexGrow = 1,
                    flexShrink = 1,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis,
                    whiteSpace = WhiteSpace.NoWrap
                }
            };

            _folderRow.Add(folderIcon);
            _folderRow.Add(_folderNameLabel);
            _singleSelectionContainer.Add(_folderRow);

            var tagLabel = new Label("Tags")
                { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 12, marginBottom = 4 } };
            _singleSelectionContainer.Add(tagLabel);

            _tagsContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    marginBottom = 10
                }
            };
            _singleSelectionContainer.Add(_tagsContainer);

            var addTagButton = new Button(OpenTagSelector) {
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
            _singleSelectionContainer.Add(addTagButton);

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
        public event Action<string> OnTagClicked;
        public event Action<Ulid> OnFolderClicked;

        public void Initialize(IAssetRepository repository, TextureService textureService,
            FolderService folderService) {
            _presenter = new AssetInfoPresenter(repository, textureService, folderService);
            _presenter.AssetDataUpdated += OnAssetDataUpdated;
            _presenter.FolderDataUpdated += OnFolderDataUpdated;
            _presenter.LibraryDataUpdated += OnLibraryDataUpdated;
            _presenter.MultiSelectionUpdated += OnMultiSelectionUpdated;

            RegisterCallback<DetachFromPanelEvent>(_ => { _presenter?.Dispose(); });

            UpdateSelection(null);
        }

        private void OpenTagSelector() {
            OnTagAdded?.Invoke("");
        }

        public void UpdateSelection(IReadOnlyList<object> selectedItems) {
            _presenter?.UpdateSelection(selectedItems);
        }

        private void OnAssetDataUpdated(AssetDisplayData data) {
            ShowSingleSelection();
            _nameField.SetValueWithoutNotify(data.Name);
            _descriptionField.SetValueWithoutNotify(data.Description);

            _presenter.LoadThumbnail(data.Id, false,
                tex =>
                {
                    _thumbnailContainer.style.backgroundImage = tex != null
                        ? new StyleBackground(tex)
                        : new StyleBackground(TextureService.GetDefaultFallback(false));
                });

            RefreshTags(data.Tags);

            _currentAssetFolderId = data.FolderId;
            if (data.FolderId != Ulid.Empty) {
                _folderHeader.style.display = DisplayStyle.Flex;
                _folderRow.style.display = DisplayStyle.Flex;
                _folderNameLabel.text = data.FolderName;
                _folderNameLabel.tooltip = data.FolderName;
            }
            else {
                _folderHeader.style.display = DisplayStyle.None;
                _folderRow.style.display = DisplayStyle.None;
            }

            _infoContainer.Clear();
            AddInfoRow("Size", FormatSize(data.Size));
            AddInfoRow("Type", data.Extension);
            AddInfoRow("Modified", data.ModificationTime.ToString("yyyy/MM/dd HH:mm"));
        }

        private void OnFolderDataUpdated(FolderDisplayData data) {
            ShowSingleSelection();
            _nameField.SetValueWithoutNotify(data.Name);
            _descriptionField.SetValueWithoutNotify(data.Description);

            if (data.ParentFolderId != Ulid.Empty) {
                _folderHeader.style.display = DisplayStyle.Flex;
                _folderRow.style.display = DisplayStyle.Flex;
                _folderNameLabel.text = data.ParentFolderName;
            }
            else {
                _folderHeader.style.display = DisplayStyle.None;
                _folderRow.style.display = DisplayStyle.None;
            }

            RefreshTags(data.Tags);

            _presenter.LoadThumbnail(data.Id, true,
                tex =>
                {
                    _thumbnailContainer.style.backgroundImage = tex != null
                        ? new StyleBackground(tex)
                        : new StyleBackground(TextureService.GetDefaultFallback(true,
                            data.SubFolderCount == 0 && data.AssetCount == 0));
                });

            _infoContainer.Clear();
            if (data.IsFolder) AddInfoRow("Sub Folders", data.SubFolderCount.ToString());
            AddInfoRow("Assets", data.AssetCount.ToString());
            AddInfoRow("Modified", data.ModificationTime.ToString("yyyy/MM/dd HH:mm"));

            if (!data.IsBoothItemFolder) return;
            if (!string.IsNullOrEmpty(data.ShopName)) {
                AddInfoRowWithLink("Shop", data.ShopName, data.ShopUrl);
            }
            if (!string.IsNullOrEmpty(data.ItemId)) {
                AddInfoRowWithLink("Item", data.ItemId, data.ItemUrl);
            }
        }

        private void OnLibraryDataUpdated(LibraryDisplayData data) {
            _singleSelectionContainer.style.display = DisplayStyle.None;
            _multiSelectionContainer.style.display = DisplayStyle.Flex;
            _infoHeader.style.display = DisplayStyle.None;
            _multiSelectionLabel.text = "Library Overview";

            _infoContainer.Clear();
            AddInfoRow("Total Assets", data.TotalAssets.ToString());
            AddInfoRow("Total Size", FormatSize(data.TotalSize));
            AddInfoRow("Total Tags", data.TotalTags.ToString());
        }

        private void OnMultiSelectionUpdated(int count) {
            ShowMultiSelectionInfo(count);
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

                pill.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    OnTagClicked?.Invoke(tag);
                    evt.StopPropagation();
                });

                var label = new Label(tag) { style = { marginRight = 4 } };

                pill.RegisterCallback<MouseEnterEvent>(_ =>
                {
                    pill.style.backgroundColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
                });
                pill.RegisterCallback<MouseLeaveEvent>(_ =>
                {
                    pill.style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
                });

                var removeBtn = new Button(() => { OnTagRemoved?.Invoke(tag); }) {
                    text = "×",
                    style = {
                        width = 16, height = 16,
                        fontSize = 10,
                        backgroundColor = Color.clear,
                        borderTopWidth = 0, borderBottomWidth = 0, borderLeftWidth = 0, borderRightWidth = 0,
                        paddingLeft = 0, paddingRight = 0
                    }
                };

                removeBtn.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                removeBtn.RegisterCallback<MouseEnterEvent>(_ =>
                {
                    removeBtn.style.backgroundColor = ColorPreset.TabCloseButtonHover;
                    removeBtn.style.color = Color.white;
                });
                removeBtn.RegisterCallback<MouseLeaveEvent>(_ =>
                {
                    removeBtn.style.backgroundColor = Color.clear;
                    removeBtn.style.color = new StyleColor(StyleKeyword.Null);
                });

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
        
        private void AddInfoRowWithLink(string label, string displayText, string url) {
            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.SpaceBetween,
                    marginBottom = 2
                }
            };
            row.Add(new Label(label) { style = { color = Color.gray, width = 80 } });
            
            var linkLabel = new Label(displayText) {
                style = {
                    flexGrow = 1,
                    unityTextAlign = TextAnchor.MiddleRight,
                    color = new StyleColor(new Color(0.4f, 0.6f, 1.0f))
                }
            };
            
            if (!string.IsNullOrEmpty(url)) {
                linkLabel.RegisterCallback<PointerDownEvent>(evt => {
                    if (evt.button != 0) return;
                    Application.OpenURL(url);
                    evt.StopPropagation();
                });
                
                linkLabel.RegisterCallback<MouseEnterEvent>(_ => {
                    linkLabel.style.color = new StyleColor(new Color(0.6f, 0.8f, 1.0f));
                });
                linkLabel.RegisterCallback<MouseLeaveEvent>(_ => {
                    linkLabel.style.color = new StyleColor(new Color(0.4f, 0.6f, 1.0f));
                });
            }
            
            row.Add(linkLabel);
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