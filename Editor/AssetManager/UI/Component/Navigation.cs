using System;
using System.Collections.Generic;
using _4OF.ee4v.AssetManager.Booth.Dialog;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.AssetManager.UI.Dialog;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Component {
    public sealed class Navigation : VisualElement {
        private readonly CreateAssetDialog _createAssetDialog;
        private readonly CreateFolderDialog _createFolderDialog;
        private readonly NavigationDragManipulator _dragManipulator;
        private readonly HashSet<Ulid> _expandedFolders = new();
        private readonly VisualElement _folderContainer;
        private readonly Dictionary<Ulid, VisualElement> _folderItemMap = new();
        private readonly Dictionary<Ulid, VisualElement> _folderRowMap = new();
        private readonly Label _foldersLabel;
        private readonly List<Label> _navLabels = new();
        private readonly RenameFolderDialog _renameFolderDialog;

        private Ulid _currentSelectedFolderId = Ulid.Empty;

        private VisualElement _selectedFolderItem;
        private Label _selectedLabel;
        private Func<VisualElement, VisualElement> _showDialogCallback;

        public Navigation() {
            _createAssetDialog = new CreateAssetDialog();
            _createAssetDialog.OnAssetCreated += (assetName, desc, fileOrUrl, tags, shop, item) =>
                OnAssetCreated?.Invoke(assetName, desc, fileOrUrl, tags, shop, item);
            _createAssetDialog.OnImportFromBoothRequested += () =>
            {
                if (_showDialogCallback == null) return;
                var waitContent = WaitBoothSyncDialog.CreateContent(_showDialogCallback);
                _showDialogCallback.Invoke(waitContent);
            };

            _createFolderDialog = new CreateFolderDialog();
            _createFolderDialog.OnFolderCreated += folderName => OnFolderCreated?.Invoke(folderName);

            _renameFolderDialog = new RenameFolderDialog();
            _renameFolderDialog.OnFolderRenamed += (id, folderName) => OnFolderRenamed?.Invoke(id, folderName);

            _dragManipulator = new NavigationDragManipulator(_folderRowMap);
            _dragManipulator.OnFolderMoved += (sourceId, targetId) => OnFolderMoved?.Invoke(sourceId, targetId);
            _dragManipulator.OnFolderReordered += (parentId, sourceId, index) =>
                OnFolderReordered?.Invoke(parentId, sourceId, index);
            this.AddManipulator(_dragManipulator);

            style.flexDirection = FlexDirection.Column;
            style.paddingLeft = 6;
            style.paddingRight = 6;
            style.paddingTop = 6;

            CreateNavLabel(I18N.Get("UI.AssetManager.Navigation.AllItems"),
                () => FireNav(NavigationMode.AllItems, I18N.Get("UI.AssetManager.Navigation.AllItemsContext"),
                    a => !a.IsDeleted));
            CreateNavLabel(I18N.Get("UI.AssetManager.Navigation.BoothItems"), () =>
            {
                FireNav(NavigationMode.BoothItems, I18N.Get("UI.AssetManager.Navigation.BoothItemsContext"),
                    a => !a.IsDeleted);
                BoothItemClicked?.Invoke();
            });
            CreateNavLabel(I18N.Get("UI.AssetManager.Navigation.Backups"),
                () => FireNav(NavigationMode.Backups, I18N.Get("UI.AssetManager.Navigation.BackupsContext"),
                    a => !a.IsDeleted));

            CreateNavLabel(I18N.Get("UI.AssetManager.Navigation.Uncategorized"), () =>
                FireNav(NavigationMode.Uncategorized, I18N.Get("UI.AssetManager.Navigation.UncategorizedContext"),
                    a => !a.IsDeleted && a.Folder == Ulid.Empty && (a.Tags == null || a.Tags.Count == 0)));
            CreateNavLabel(I18N.Get("UI.AssetManager.Navigation.TagList"), () => { TagListClicked?.Invoke(); });
            CreateNavLabel(I18N.Get("UI.AssetManager.Navigation.Trash"),
                () => FireNav(NavigationMode.Trash, I18N.Get("UI.AssetManager.Navigation.TrashContext"),
                    a => a.IsDeleted));

            Add(new VisualElement { style = { height = 10 } });

            var foldersHeader = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 2
                }
            };

            _foldersLabel = new Label(I18N.Get("UI.AssetManager.Navigation.Folders")) {
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
                NavigationChanged?.Invoke(NavigationMode.Folders, I18N.Get("UI.AssetManager.Navigation.FoldersContext"),
                    a => !a.IsDeleted);
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
                    color = ColorPreset.InActiveItem
                }
            };

            plusButton.RegisterCallback<PointerEnterEvent>(_ =>
            {
                plusButton.style.color = ColorPreset.AccentBlue;
                plusButton.style.backgroundColor = ColorPreset.AccentBlue20Style;
            });

            plusButton.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                plusButton.style.color = ColorPreset.InActiveItem;
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

            var createAssetButton = new Button {
                text = I18N.Get("UI.AssetManager.Navigation.NewAsset"),
                style = {
                    marginTop = 15,
                    marginBottom = 20,
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
                    backgroundColor = ColorPreset.AccentBlue40Style,
                    color = ColorPreset.TextColor,
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
                createAssetButton.style.backgroundColor = ColorPreset.AccentBlueStyle;
            });

            createAssetButton.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                createAssetButton.style.backgroundColor = ColorPreset.AccentBlue40Style;
            });

            createAssetButton.clicked += ShowCreateAssetDialog;

            Add(createAssetButton);
        }

        public void SetShowDialogCallback(Func<VisualElement, VisualElement> callback) {
            _showDialogCallback = callback;
        }

        public void SetRepository(IAssetRepository repository) {
            _createAssetDialog.SetRepository(repository);
        }

        public event Action<NavigationMode, string, Func<AssetMetadata, bool>> NavigationChanged;
        public event Action<Ulid> FolderSelected;
        public event Action BoothItemClicked;
        public event Action TagListClicked;
        public event Action<Ulid, string> OnFolderRenamed;
        public event Action<Ulid> OnFolderDeleted;
        public event Action<Ulid, Ulid> OnFolderMoved;
        public event Action<Ulid, string, VisualElement> OnFolderContextMenuRequested;
        public event Action<string> OnFolderCreated;
        public event Action<Ulid, string[], string[]> OnDropRequested;
        public event Action<Ulid, Ulid, int> OnFolderReordered;

        public event Action<string, string, string, List<string>, string, string> OnAssetCreated;

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
                case NavigationMode.Backups:
                    if (_navLabels.Count > 2) SetSelected(_navLabels[2]);
                    break;
                case NavigationMode.Uncategorized:
                    if (_navLabels.Count > 3) SetSelected(_navLabels[3]);
                    break;
                case NavigationMode.TagList:
                    if (_navLabels.Count > 4) SetSelected(_navLabels[4]);
                    break;
                case NavigationMode.Trash:
                    if (_navLabels.Count > 5) SetSelected(_navLabels[5]);
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
                        OnFolderViewSelected((Ulid)treeItemContainer.userData, itemRow);
                        _dragManipulator.StartDrag(folder.ID, evt.position, itemRow);
                        evt.StopPropagation();
                        break;
                    case 1:
                        OnFolderContextMenuRequested?.Invoke(folder.ID, folder.Name, itemRow);
                        evt.StopPropagation();
                        break;
                }
            });

            _dragManipulator.RegisterFolderItem(
                itemRow,
                treeItemContainer,
                parentContainer,
                GetParentFolderId,
                GetChildIndex,
                ApplySelectedStyle
            );

            itemRow.RegisterCallback<DragEnterEvent>(_ =>
            {
                if (DragAndDrop.GetGenericData("AssetManagerAssets") == null &&
                    DragAndDrop.GetGenericData("AssetManagerFolders") == null) return;
                itemRow.style.backgroundColor = ColorPreset.AccentBlue40Style;
            });

            itemRow.RegisterCallback<DragLeaveEvent>(_ =>
            {
                if (DragAndDrop.GetGenericData("AssetManagerAssets") == null &&
                    DragAndDrop.GetGenericData("AssetManagerFolders") == null) return;
                if (_currentSelectedFolderId == (Ulid)treeItemContainer.userData)
                    ApplySelectedStyle(itemRow);
                else
                    itemRow.style.backgroundColor = new StyleColor(StyleKeyword.Null);
            });

            itemRow.RegisterCallback<DragUpdatedEvent>(_ =>
            {
                if (DragAndDrop.GetGenericData("AssetManagerAssets") == null &&
                    DragAndDrop.GetGenericData("AssetManagerFolders") == null) {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                    return;
                }

                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
            });

            itemRow.RegisterCallback<DragPerformEvent>(evt =>
            {
                var assetIdsData = DragAndDrop.GetGenericData("AssetManagerAssets") as string[];
                var folderIdsData = DragAndDrop.GetGenericData("AssetManagerFolders") as string[];
                if (assetIdsData == null && folderIdsData == null) return;

                var targetFolderId = (Ulid)treeItemContainer.userData;

                DragAndDrop.AcceptDrag();

                if (_currentSelectedFolderId == targetFolderId)
                    ApplySelectedStyle(itemRow);
                else
                    itemRow.style.backgroundColor = new StyleColor(StyleKeyword.Null);

                OnDropRequested?.Invoke(targetFolderId, assetIdsData, folderIdsData);

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

        public void SelectBoothItems() {
            if (_navLabels.Count <= 1) return;
            var label = _navLabels[1];
            SetSelected(label);
            (label.userData as Action)?.Invoke();
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
            item.style.backgroundColor = ColorPreset.AccentBlue40Style;
            foreach (var child in item.Children())
                if (child is Label childLabel)
                    childLabel.style.color = ColorPreset.AccentBlue;
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
            _selectedLabel.style.backgroundColor = ColorPreset.AccentBlue40Style;
            _selectedLabel.style.color = ColorPreset.AccentBlue;
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

        private Ulid GetParentFolderId(Ulid folderId) {
            if (!_folderItemMap.TryGetValue(folderId, out var treeItem)) return Ulid.Empty;

            var parentElement = treeItem.parent;
            while (parentElement != null) {
                if (parentElement.userData is Ulid parentId) return parentId;
                parentElement = parentElement.parent;
            }

            return Ulid.Empty;
        }

        private int GetChildIndex(VisualElement parentContainer, Ulid folderId) {
            if (!_folderItemMap.TryGetValue(folderId, out var treeItem)) return -1;

            for (var i = 0; i < parentContainer.childCount; i++)
                if (parentContainer[i] == treeItem)
                    return i;
            return -1;
        }


        private void ShowCreateAssetDialog() {
            if (_showDialogCallback == null) return;
            var content = _createAssetDialog.CreateContent();
            _showDialogCallback.Invoke(content);
        }

        private void ShowCreateFolderDialog() {
            if (_showDialogCallback == null) return;
            var content = _createFolderDialog.CreateContent();
            _showDialogCallback.Invoke(content);
        }

        public void ShowRenameFolderDialog(Ulid folderId, string oldName) {
            if (_showDialogCallback == null) return;
            var content = _renameFolderDialog.CreateContent(folderId, oldName);
            _showDialogCallback.Invoke(content);
        }

        private void OnOnFolderDeleted(Ulid obj) {
            OnFolderDeleted?.Invoke(obj);
        }
    }
}