using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class Navigation : VisualElement {
        private readonly HashSet<Ulid> _expandedFolders = new();
        private readonly VisualElement _folderContainer;
        private readonly Dictionary<Ulid, VisualElement> _folderItemMap = new();
        private readonly Dictionary<Ulid, VisualElement> _folderRowMap = new();
        private readonly Label _foldersLabel;
        private readonly List<Label> _navLabels = new();
        private readonly List<string> _tempTags = new();

        private Ulid _currentSelectedFolderId = Ulid.Empty;
        private Ulid _draggingFolderId = Ulid.Empty;
        private VisualElement _dragIndicator;
        private Vector2 _dragStartPosition;

        private IAssetRepository _repository;
        private VisualElement _selectedFolderItem;
        private Label _selectedLabel;
        private Func<VisualElement, VisualElement> _showDialogCallback;

        public Navigation() {
            style.flexDirection = FlexDirection.Column;
            style.paddingLeft = 6;
            style.paddingRight = 6;
            style.paddingTop = 6;

            // Create Asset button at the top
            var createAssetButton = new Button {
                text = "+ New Asset",
                style = {
                    marginBottom = 10,
                    marginLeft = 4,
                    marginRight = 4,
                    paddingLeft = 12,
                    paddingRight = 12,
                    paddingTop = 8,
                    paddingBottom = 8,
                    borderTopLeftRadius = 16,
                    borderTopRightRadius = 16,
                    borderBottomLeftRadius = 16,
                    borderBottomRightRadius = 16,
                    backgroundColor = new Color(0.3f, 0.5f, 0.8f, 0.8f),
                    color = Color.white,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 12,
                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    borderLeftWidth = 0,
                    borderRightWidth = 0
                }
            };

            createAssetButton.RegisterCallback<PointerEnterEvent>(_ =>
            {
                createAssetButton.style.backgroundColor = new Color(0.4f, 0.6f, 0.9f, 1.0f);
            });

            createAssetButton.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                createAssetButton.style.backgroundColor = new Color(0.3f, 0.5f, 0.8f, 0.8f);
            });

            createAssetButton.clicked += ShowCreateAssetDialog;

            Add(createAssetButton);

            RegisterCallback<PointerUpEvent>(_ =>
            {
                if (_draggingFolderId == Ulid.Empty) return;
                if (_folderRowMap.TryGetValue(_draggingFolderId, out var row))
                    row.style.opacity = 1.0f;
                else
                    Debug.LogWarning($"itemRow not found for: {_draggingFolderId}");
                _draggingFolderId = Ulid.Empty;
            });

            RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (_draggingFolderId == Ulid.Empty) return;
                if (_folderRowMap.TryGetValue(_draggingFolderId, out var row)) row.style.opacity = 1.0f;
                _draggingFolderId = Ulid.Empty;
            });

            CreateNavLabel("All items", () => FireNav(NavigationMode.AllItems, "All Items", a => !a.IsDeleted));
            CreateNavLabel("Booth Items", () =>
            {
                FireNav(NavigationMode.BoothItems, "Booth Items", a => !a.IsDeleted);
                BoothItemClicked?.Invoke();
            });
            CreateNavLabel("Uncategorized", () => FireNav(NavigationMode.Uncategorized, "Uncategorized",
                a => !a.IsDeleted && a.Folder == Ulid.Empty && (a.Tags == null || a.Tags.Count == 0)));
            CreateNavLabel("Tag List", () => { TagListClicked?.Invoke(); });
            CreateNavLabel("Trash", () => FireNav(NavigationMode.Trash, "Trash", a => a.IsDeleted));

            Add(new VisualElement { style = { height = 10 } });

            var foldersHeader = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 2
                }
            };

            _foldersLabel = new Label("Folders") {
                style = {
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 4,
                    paddingBottom = 4,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    flexGrow = 1
                }
            };
            _foldersLabel.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                SetSelected(_foldersLabel);
                NavigationChanged?.Invoke(NavigationMode.Folders, "Folders", a => !a.IsDeleted);
                evt.StopPropagation();
            });

            var plusButton = new Label("+") {
                style = {
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 2,
                    paddingBottom = 2,
                    marginRight = 4,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 16,
                    color = new Color(0.7f, 0.7f, 0.7f)
                }
            };

            plusButton.RegisterCallback<PointerEnterEvent>(_ =>
            {
                plusButton.style.color = new Color(0.4f, 0.7f, 1.0f);
                plusButton.style.backgroundColor = new Color(0.3f, 0.5f, 0.8f, 0.2f);
            });

            plusButton.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                plusButton.style.color = new Color(0.7f, 0.7f, 0.7f);
                plusButton.style.backgroundColor = new StyleColor(StyleKeyword.Null);
            });

            plusButton.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                ShowCreateFolderDialog();
                evt.StopPropagation();
            });

            foldersHeader.Add(_foldersLabel);
            foldersHeader.Add(plusButton);
            Add(foldersHeader);

            _folderContainer = new VisualElement();
            var scrollView = new ScrollView(ScrollViewMode.Vertical) {
                style = {
                    flexGrow = 1
                }
            };
            scrollView.Add(_folderContainer);
            Add(scrollView);
        }

        public void SetShowDialogCallback(Func<VisualElement, VisualElement> callback) {
            _showDialogCallback = callback;
        }

        public void SetRepository(IAssetRepository repository) {
            _repository = repository;
        }

        public event Action<NavigationMode, string, Func<AssetMetadata, bool>> NavigationChanged;
        public event Action<Ulid> FolderSelected;
        public event Action BoothItemClicked;
        public event Action TagListClicked;
        public event Action<Ulid, string> OnFolderRenamed;
        public event Action<Ulid> OnFolderDeleted;
        public event Action<Ulid, Ulid> OnFolderMoved;
        public event Action<string> OnFolderCreated;
        public event Action<List<Ulid>, Ulid> OnAssetsDroppedToFolder;
        public event Action<Ulid, Ulid, int> OnFolderReordered;

        public event Action<string, string, string, List<string>, string, string>
            OnAssetCreated; // name, description, fileOrUrl, tags, boothUrl, shopDomain, itemId

        public void SelectState(NavigationMode mode, Ulid folderId) {
            SetSelectedFolderItem(null);
            SetSelected(null);

            _currentSelectedFolderId = Ulid.Empty;

            switch (mode) {
                case NavigationMode.Folders:
                    if (folderId == Ulid.Empty) {
                        SetSelected(_foldersLabel);
                        _currentSelectedFolderId = Ulid.Empty;
                        return;
                    }

                    if (_folderRowMap.TryGetValue(folderId, out var row)) {
                        _currentSelectedFolderId = folderId;
                        ApplySelectedStyle(row);
                        _selectedFolderItem = row;
                    }

                    break;
                case NavigationMode.AllItems:
                    if (_navLabels.Count > 0) SetSelected(_navLabels[0]);
                    break;
                case NavigationMode.BoothItems:
                    if (_navLabels.Count > 1) SetSelected(_navLabels[1]);
                    break;
                case NavigationMode.Uncategorized:
                    if (_navLabels.Count > 2) SetSelected(_navLabels[2]);
                    break;
                case NavigationMode.TagList:
                    if (_navLabels.Count > 3) SetSelected(_navLabels[3]);
                    break;
                case NavigationMode.Trash:
                    if (_navLabels.Count > 4) SetSelected(_navLabels[4]);
                    break;
                case NavigationMode.Tag:
                    break;
            }
        }

        public void SetFolders(List<BaseFolder> folders) {
            folders ??= new List<BaseFolder>();
            _folderContainer.Clear();
            _folderItemMap.Clear();
            _folderRowMap.Clear();

            _selectedFolderItem = null;

            foreach (var folder in folders) CreateFolderTreeItem(folder, _folderContainer, 0);
        }

        private void CreateFolderTreeItem(BaseFolder folder, VisualElement parentContainer, int depth) {
            var treeItemContainer = new VisualElement {
                userData = folder.ID,
                style = {
                    flexDirection = FlexDirection.Column,
                    marginBottom = 1
                }
            };

            var itemRow = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };

            var hasChildren = folder is Folder f && f.Children.Count > 0;
            var isExpanded = _expandedFolders.Contains(folder.ID);

            var expandToggle = new Label(hasChildren ? isExpanded ? "▼" : "▶" : " ") {
                style = {
                    paddingLeft = depth * 12,
                    paddingRight = 0,
                    width = 16 + depth * 12,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    fontSize = 10
                }
            };

            if (hasChildren)
                expandToggle.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    ToggleExpand(folder.ID);
                    evt.StopPropagation();
                });

            var label = new Label(folder.Name) {
                style = {
                    paddingLeft = 0,
                    paddingRight = 8,
                    paddingTop = 3,
                    paddingBottom = 3,
                    flexGrow = 1
                }
            };

            itemRow.Add(expandToggle);
            itemRow.Add(label);
            treeItemContainer.Add(itemRow);

            if (folder.ID == _currentSelectedFolderId) {
                ApplySelectedStyle(itemRow);
                _selectedFolderItem = itemRow;
            }

            itemRow.RegisterCallback<PointerDownEvent>(evt =>
            {
                switch (evt.button) {
                    case 0:
                        _dragStartPosition = evt.position;
                        OnFolderViewSelected((Ulid)treeItemContainer.userData, itemRow);
                        evt.StopPropagation();
                        break;
                    case 1:
                        ShowFolderContextMenu(itemRow, folder.ID, folder.Name);
                        evt.StopPropagation();
                        break;
                }
            });

            itemRow.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (evt.pressedButtons != 1) return;

                if (_draggingFolderId == Ulid.Empty) {
                    var distance = Vector2.Distance(_dragStartPosition, evt.position);
                    if (distance < 4f) return;

                    _draggingFolderId = (Ulid)treeItemContainer.userData;
                    itemRow.style.opacity = 0.5f;
                }
                else if (_draggingFolderId != (Ulid)treeItemContainer.userData) {
                    UpdateDropVisualFeedback(itemRow, evt.position);
                }
            });

            itemRow.RegisterCallback<PointerEnterEvent>(evt =>
            {
                if (_draggingFolderId == Ulid.Empty || _draggingFolderId == (Ulid)treeItemContainer.userData) return;
                UpdateDropVisualFeedback(itemRow, evt.position);
            });

            itemRow.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (_draggingFolderId == Ulid.Empty || _draggingFolderId == (Ulid)treeItemContainer.userData) return;
                ClearDropVisualFeedback(itemRow);
                if (_currentSelectedFolderId == (Ulid)treeItemContainer.userData)
                    ApplySelectedStyle(itemRow);
                else
                    itemRow.style.backgroundColor = new StyleColor(StyleKeyword.Null);
            });

            itemRow.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (_draggingFolderId == Ulid.Empty || _draggingFolderId == (Ulid)treeItemContainer.userData) return;
                var targetFolderId = (Ulid)treeItemContainer.userData;
                var sourceFolderId = _draggingFolderId;

                ClearDropVisualFeedback(itemRow);

                var localPos = itemRow.WorldToLocal(evt.position);
                var height = itemRow.resolvedStyle.height;
                var normalizedY = localPos.y / height;

                if (normalizedY < 0.25f) {
                    var targetParentId = GetParentFolderId(targetFolderId);
                    var targetIndex = GetChildIndex(parentContainer, targetFolderId);
                    if (targetIndex >= 0)
                        OnFolderReordered?.Invoke(targetParentId, sourceFolderId, targetIndex);
                }
                else if (normalizedY > 0.75f) {
                    var targetParentId = GetParentFolderId(targetFolderId);
                    var targetIndex = GetChildIndex(parentContainer, targetFolderId);
                    if (targetIndex >= 0)
                        OnFolderReordered?.Invoke(targetParentId, sourceFolderId, targetIndex + 1);
                }
                else {
                    OnFolderMoved?.Invoke(sourceFolderId, targetFolderId);
                }

                if (_currentSelectedFolderId == targetFolderId)
                    ApplySelectedStyle(itemRow);
                else
                    itemRow.style.backgroundColor = new StyleColor(StyleKeyword.Null);
            });

            itemRow.RegisterCallback<DragEnterEvent>(_ =>
            {
                if (DragAndDrop.GetGenericData("AssetManagerAssets") == null) return;
                itemRow.style.backgroundColor = new Color(0.4f, 0.7f, 1.0f, 0.4f);
            });

            itemRow.RegisterCallback<DragLeaveEvent>(_ =>
            {
                if (DragAndDrop.GetGenericData("AssetManagerAssets") == null) return;
                if (_currentSelectedFolderId == (Ulid)treeItemContainer.userData)
                    ApplySelectedStyle(itemRow);
                else
                    itemRow.style.backgroundColor = new StyleColor(StyleKeyword.Null);
            });

            itemRow.RegisterCallback<DragUpdatedEvent>(_ =>
            {
                if (DragAndDrop.GetGenericData("AssetManagerAssets") == null) {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                    return;
                }

                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
            });

            itemRow.RegisterCallback<DragPerformEvent>(evt =>
            {
                var assetIdsData = DragAndDrop.GetGenericData("AssetManagerAssets");
                if (assetIdsData == null) return;

                var targetFolderId = (Ulid)treeItemContainer.userData;
                var assetIds = ((string[])assetIdsData).Select(Ulid.Parse).ToList();

                DragAndDrop.AcceptDrag();

                if (_currentSelectedFolderId == targetFolderId)
                    ApplySelectedStyle(itemRow);
                else
                    itemRow.style.backgroundColor = new StyleColor(StyleKeyword.Null);

                OnAssetsDroppedToFolder?.Invoke(assetIds, targetFolderId);
                evt.StopPropagation();
            });

            var childrenContainer = new VisualElement {
                style = {
                    display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None
                }
            };
            treeItemContainer.Add(childrenContainer);

            if (folder is Folder folderWithChildren)
                foreach (var child in folderWithChildren.Children)
                    CreateFolderTreeItem(child, childrenContainer, depth + 1);

            parentContainer.Add(treeItemContainer);
            _folderItemMap[folder.ID] = treeItemContainer;
            _folderRowMap[folder.ID] = itemRow;
        }

        private void ToggleExpand(Ulid folderId) {
            if (!_expandedFolders.Add(folderId)) _expandedFolders.Remove(folderId);

            if (!_folderItemMap.TryGetValue(folderId, out var treeItem)) return;

            var itemRow = treeItem.Q<VisualElement>();
            var expandToggle = itemRow?.Q<Label>();
            var childrenContainer = treeItem.ElementAt(1);

            var isExpanded = _expandedFolders.Contains(folderId);
            if (expandToggle != null) expandToggle.text = isExpanded ? "▼" : "▶";
            if (childrenContainer != null)
                childrenContainer.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SelectAll() {
            if (_navLabels.Count <= 0) return;
            var firstLabel = _navLabels[0];
            SetSelected(firstLabel);
            (firstLabel.userData as Action)?.Invoke();
        }

        private void CreateNavLabel(string text, Action onClick) {
            var label = new Label(text) {
                userData = onClick,
                style = {
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 4,
                    paddingBottom = 4,
                    marginBottom = 2,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            label.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                SetSelected(label);
                onClick?.Invoke();
                evt.StopPropagation();
            });
            _navLabels.Add(label);
            Add(label);
        }

        private void FireNav(NavigationMode mode, string naviName, Func<AssetMetadata, bool> filter) {
            _currentSelectedFolderId = Ulid.Empty;

            FolderSelected?.Invoke(Ulid.Empty);
            NavigationChanged?.Invoke(mode, naviName, filter);
        }


        private static void ApplySelectedStyle(VisualElement item) {
            item.AddToClassList("selected");
            item.style.backgroundColor = new Color(0.3f, 0.5f, 0.8f, 0.3f);
            foreach (var child in item.Children())
                if (child is Label childLabel)
                    childLabel.style.color = new Color(0.4f, 0.7f, 1.0f);
        }

        private void RemoveSelectedStyle(VisualElement item) {
            item.RemoveFromClassList("selected");
            item.style.backgroundColor = new StyleColor(StyleKeyword.Null);
            foreach (var child in item.Children())
                if (child is Label childLabel)
                    childLabel.style.color = new StyleColor(StyleKeyword.Null);
        }


        private void SetSelected(Label label) {
            if (_selectedFolderItem != null) {
                RemoveSelectedStyle(_selectedFolderItem);
                _selectedFolderItem = null;
                _currentSelectedFolderId = Ulid.Empty;
            }

            if (_selectedLabel != null) {
                _selectedLabel.RemoveFromClassList("selected");
                _selectedLabel.style.backgroundColor = new StyleColor(StyleKeyword.Null);
                _selectedLabel.style.color = new StyleColor(StyleKeyword.Null);
            }

            _selectedLabel = label;
            if (_selectedLabel == null) return;

            _selectedLabel.AddToClassList("selected");
            _selectedLabel.style.backgroundColor = new Color(0.3f, 0.5f, 0.8f, 0.3f);
            _selectedLabel.style.color = new Color(0.4f, 0.7f, 1.0f);
        }

        private void SetSelectedFolderItem(VisualElement folderItem) {
            if (_selectedLabel != null) {
                _selectedLabel.RemoveFromClassList("selected");
                _selectedLabel.style.backgroundColor = new StyleColor(StyleKeyword.Null);
                _selectedLabel.style.color = new StyleColor(StyleKeyword.Null);
                _selectedLabel = null;
            }

            if (_selectedFolderItem != null) RemoveSelectedStyle(_selectedFolderItem);

            _selectedFolderItem = folderItem;
            if (_selectedFolderItem == null) return;

            ApplySelectedStyle(_selectedFolderItem);
        }

        private void OnFolderViewSelected(Ulid folderId, VisualElement folderItem) {
            _currentSelectedFolderId = folderId;

            SetSelectedFolderItem(folderItem);
            FolderSelected?.Invoke(folderId);
        }

        private void ShowFolderContextMenu(VisualElement target, Ulid folderId, string folderName) {
            var menu = new GenericDropdownMenu();
            menu.AddItem("Rename", false, () => ShowRenameFolderDialog(folderId, folderName));
            menu.AddItem("Delete", false, () => DeleteFolder(folderId));

            var rect = target.worldBound;
            var menuRect = new Rect(rect.x, rect.yMax, Mathf.Max(rect.width, 100), 0);
            menu.DropDown(menuRect, target);
        }

        private void ShowRenameFolderDialog(Ulid folderId, string oldName) {
            if (_showDialogCallback == null) return;

            var content = new VisualElement();

            var title = new Label("Rename Folder") {
                style = {
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 10
                }
            };
            content.Add(title);

            var label = new Label("New folder name:") {
                style = { marginBottom = 5 }
            };
            content.Add(label);

            var textField = new TextField { value = oldName, style = { marginBottom = 10 } };
            content.Add(textField);

            var buttonRow = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexEnd
                }
            };

            var cancelBtn = new Button {
                text = "Cancel",
                style = { marginRight = 5 }
            };
            buttonRow.Add(cancelBtn);

            var okBtn = new Button {
                text = "OK"
            };
            buttonRow.Add(okBtn);

            content.Add(buttonRow);

            var dialogContainer = _showDialogCallback.Invoke(content);

            cancelBtn.clicked += () => dialogContainer?.RemoveFromHierarchy();
            okBtn.clicked += () =>
            {
                var newName = textField.value;
                // Allow invoking rename even for empty/whitespace so the service layer can validate and UI can display error
                if (newName != oldName || string.IsNullOrWhiteSpace(newName))
                    OnFolderRenamed?.Invoke(folderId, newName);

                dialogContainer?.RemoveFromHierarchy();
            };

            content.schedule.Execute(() =>
            {
                textField.Focus();
                textField.SelectAll();
            });
        }

        private void DeleteFolder(Ulid folderId) {
            OnFolderDeleted?.Invoke(folderId);
        }

        private void ShowCreateAssetDialog() {
            if (_showDialogCallback == null) return;

            _tempTags.Clear();
            var content = new VisualElement();

            var title = new Label("Create New Asset") {
                style = {
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 10
                }
            };
            content.Add(title);

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

            // Booth Metadata Section
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

            // Tags Section
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

            void RefreshTagsUI() {
                tagsContainer.Clear();
                foreach (var tag in _tempTags) {
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

                    var tagToRemove = tag;
                    var removeBtn = new Button(() =>
                    {
                        _tempTags.Remove(tagToRemove);
                        RefreshTagsUI();
                    }) {
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
                    tagsContainer.Add(pill);
                }
            }

            var addTagButton = new Button(() =>
            {
                if (_repository == null) return;
                var screenPosition = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                TagSelectorWindow.Show(screenPosition, _repository, tag =>
                {
                    if (!string.IsNullOrEmpty(tag) && !_tempTags.Contains(tag)) {
                        _tempTags.Add(tag);
                        RefreshTagsUI();
                    }
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

            var dialogContainer = _showDialogCallback.Invoke(content);

            cancelBtn.clicked += () => dialogContainer?.RemoveFromHierarchy();
            createBtn.clicked += () =>
            {
                var assetName = nameField.value;
                var description = descField.value;
                var fileOrUrl = fileUrlField.value;
                var boothUrl = boothUrlField.value.Trim();

                // Parse Booth URL
                var shopDomain = "";
                var itemId = "";
                if (!string.IsNullOrWhiteSpace(boothUrl)) {
                    // Remove protocol if present
                    var url = boothUrl.Replace("https://", "").Replace("http://", "");

                    // Expected format: [shopname].booth.pm/items/[itemid]
                    var match = Regex.Match(url, @"^([^.]+)\.booth\.pm/items/(\d+)");
                    if (match.Success) {
                        shopDomain = match.Groups[1].Value;
                        itemId = match.Groups[2].Value;
                    }
                }

                OnAssetCreated?.Invoke(assetName, description, fileOrUrl, new List<string>(_tempTags), shopDomain,
                    itemId);

                dialogContainer?.RemoveFromHierarchy();
            };

            content.schedule.Execute(() => { nameField.Focus(); });
        }

        private void ShowCreateFolderDialog() {
            if (_showDialogCallback == null) return;

            var content = new VisualElement();

            var title = new Label("Create New Folder") {
                style = {
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 10
                }
            };
            content.Add(title);

            var label = new Label("Folder name:") {
                style = { marginBottom = 5 }
            };
            content.Add(label);

            var textField = new TextField { value = "", style = { marginBottom = 10 } };
            content.Add(textField);

            var buttonRow = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexEnd
                }
            };

            var cancelBtn = new Button {
                text = "Cancel",
                style = { marginRight = 5 }
            };
            buttonRow.Add(cancelBtn);

            var okBtn = new Button {
                text = "Create"
            };
            buttonRow.Add(okBtn);

            content.Add(buttonRow);

            var dialogContainer = _showDialogCallback.Invoke(content);

            cancelBtn.clicked += () => dialogContainer?.RemoveFromHierarchy();
            okBtn.clicked += () =>
            {
                var folderName = textField.value;
                // Always invoke creation so service layer can validate and UI can provide feedback
                OnFolderCreated?.Invoke(folderName);

                dialogContainer?.RemoveFromHierarchy();
            };

            content.schedule.Execute(() => { textField.Focus(); });
        }

        private Ulid GetParentFolderId(Ulid childFolderId) {
            if (!_folderItemMap.TryGetValue(childFolderId, out var childItem)) return Ulid.Empty;
            var parentElement = childItem.parent;
            while (parentElement != null) {
                if (parentElement.userData is Ulid parentId && parentId != childFolderId)
                    return parentId;
                parentElement = parentElement.parent;
                if (parentElement == _folderContainer || parentElement == this) return Ulid.Empty;
            }

            return Ulid.Empty;
        }

        private static int GetChildIndex(VisualElement container, Ulid folderId) {
            for (var i = 0; i < container.childCount; i++) {
                var child = container[i];
                if (child.userData is Ulid id && id == folderId)
                    return i;
            }

            return -1;
        }

        private static void UpdateDropVisualFeedback(VisualElement itemRow, Vector2 worldPosition) {
            ClearDropVisualFeedback(itemRow);

            var localPos = itemRow.WorldToLocal(worldPosition);
            var height = itemRow.resolvedStyle.height;
            var normalizedY = localPos.y / height;

            if (normalizedY < 0.25f) {
                itemRow.style.borderTopWidth = 2;
                itemRow.style.borderTopColor = new Color(0.4f, 0.7f, 1.0f);
            }
            else if (normalizedY > 0.75f) {
                itemRow.style.borderBottomWidth = 2;
                itemRow.style.borderBottomColor = new Color(0.4f, 0.7f, 1.0f);
            }
            else {
                itemRow.style.backgroundColor = new Color(0.4f, 0.6f, 0.9f, 0.4f);
            }
        }

        private static void ClearDropVisualFeedback(VisualElement itemRow) {
            itemRow.style.borderTopWidth = 0;
            itemRow.style.borderBottomWidth = 0;
            itemRow.style.backgroundColor = new StyleColor(StyleKeyword.Null);
        }
    }
}