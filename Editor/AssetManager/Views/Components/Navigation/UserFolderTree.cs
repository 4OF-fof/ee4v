using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.AssetManager.Services;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Views.Components.Navigation {
    public class UserFolderTree : VisualElement {
        private readonly NavigationDragManipulator _dragManipulator;
        private readonly HashSet<Ulid> _expandedFolders = new();

        private readonly Dictionary<Ulid, VisualElement> _folderItemMap = new();
        private readonly Dictionary<Ulid, VisualElement> _folderRowMap = new();
        private readonly Label _headerLabel;
        private readonly VisualElement _treeContainer;

        private VisualElement _selectedFolderRow;

        public UserFolderTree() {
            style.flexGrow = 1;

            var header = new VisualElement {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 }
            };

            _headerLabel = new Label(I18N.Get("UI.AssetManager.Navigation.Folders")) {
                style = {
                    paddingLeft = 8, paddingRight = 8, paddingTop = 4, paddingBottom = 4,
                    unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1
                }
            };
            _headerLabel.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                ClearSelection();
                SetHeaderSelected(true);
                OnNavigationRequested?.Invoke(NavigationMode.Folders,
                    I18N.Get("UI.AssetManager.Navigation.FoldersContext"), a => !a.IsDeleted);
                evt.StopPropagation();
            });
            header.Add(_headerLabel);

            var plusBtn = new Label("+") {
                style = {
                    paddingLeft = 6, paddingRight = 6, paddingTop = 2, paddingBottom = 2, marginRight = 4,
                    unityFontStyleAndWeight = FontStyle.Bold, fontSize = 16, color = ColorPreset.InActiveItem
                }
            };
            plusBtn.RegisterCallback<PointerEnterEvent>(_ =>
            {
                plusBtn.style.color = ColorPreset.AccentBlue;
                plusBtn.style.backgroundColor = ColorPreset.AccentBlue20Style;
            });
            plusBtn.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                plusBtn.style.color = ColorPreset.InActiveItem;
                plusBtn.style.backgroundColor = new StyleColor(StyleKeyword.Null);
            });
            plusBtn.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                OnCreateFolderRequested?.Invoke();
                evt.StopPropagation();
            });
            header.Add(plusBtn);
            Add(header);

            var scrollView = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            _treeContainer = new VisualElement();
            scrollView.Add(_treeContainer);
            Add(scrollView);

            _dragManipulator = new NavigationDragManipulator(_folderRowMap);
            _dragManipulator.OnFolderMoved += (s, t) => OnFolderMoved?.Invoke(s, t);
            _dragManipulator.OnFolderReordered += (p, s, i) => OnFolderReordered?.Invoke(p, s, i);
            this.AddManipulator(_dragManipulator);
        }

        public event Action<NavigationMode, string, Func<AssetMetadata, bool>> OnNavigationRequested;
        public event Action OnCreateFolderRequested;
        public event Action<Ulid> OnFolderSelected;
        public event Action<Ulid, string, VisualElement, Vector2> OnContextMenuRequested;

        public event Action<Ulid, Ulid> OnFolderMoved;
        public event Action<Ulid, Ulid, int> OnFolderReordered;

        public void SetFolders(List<BaseFolder> folders) {
            _treeContainer.Clear();
            _folderItemMap.Clear();
            _folderRowMap.Clear();
            _selectedFolderRow = null;

            if (folders == null) return;
            foreach (var folder in folders) CreateFolderTreeItem(folder, _treeContainer, 0);
        }

        public void SelectFolder(Ulid folderId) {
            ClearSelection();
            if (folderId == Ulid.Empty) return;

            if (!_folderRowMap.TryGetValue(folderId, out var row)) return;
            ApplySelectedStyle(row);
            _selectedFolderRow = row;
        }

        public void ClearSelection() {
            SetHeaderSelected(false);
            if (_selectedFolderRow == null) return;
            RemoveSelectedStyle(_selectedFolderRow);
            _selectedFolderRow = null;
        }

        private void SetHeaderSelected(bool selected) {
            if (selected) {
                _headerLabel.AddToClassList("selected");
                _headerLabel.style.backgroundColor = ColorPreset.AccentBlue40Style;
                _headerLabel.style.color = ColorPreset.AccentBlue;
            }
            else {
                _headerLabel.RemoveFromClassList("selected");
                _headerLabel.style.backgroundColor = new StyleColor(StyleKeyword.Null);
                _headerLabel.style.color = new StyleColor(StyleKeyword.Null);
            }
        }

        private void CreateFolderTreeItem(BaseFolder folder, VisualElement parentContainer, int depth) {
            var treeItemContainer = new VisualElement {
                userData = folder.ID,
                style = { flexDirection = FlexDirection.Column, marginBottom = 1 }
            };

            var itemRow = new VisualElement {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, overflow = Overflow.Hidden }
            };

            var hasChildren = folder is Folder f && f.Children.Count > 0;
            var isExpanded = _expandedFolders.Contains(folder.ID);

            var expandToggle = new Label(hasChildren ? isExpanded ? "▼" : "▶" : " ") {
                style = {
                    paddingLeft = depth * 12, paddingRight = 0, width = 16 + depth * 12,
                    unityTextAlign = TextAnchor.MiddleLeft, fontSize = 10, flexShrink = 0
                }
            };
            if (hasChildren)
                expandToggle.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    ToggleExpand(folder.ID);
                    evt.StopPropagation();
                });
            itemRow.Add(expandToggle);

            var label = new Label(folder.Name) {
                tooltip = folder.Name,
                style = {
                    paddingLeft = 0, paddingRight = 8, paddingTop = 3, paddingBottom = 3,
                    flexGrow = 1, flexShrink = 1, overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis, whiteSpace = WhiteSpace.NoWrap
                }
            };
            itemRow.Add(label);
            treeItemContainer.Add(itemRow);

            itemRow.RegisterCallback<PointerDownEvent>(evt =>
            {
                switch (evt.button) {
                    case 0:
                        SelectFolder(folder.ID);
                        OnFolderSelected?.Invoke(folder.ID);
                        _dragManipulator.StartDrag(folder.ID, itemRow);
                        evt.StopPropagation();
                        break;
                    case 1:
                        var worldPos = itemRow.LocalToWorld(evt.localPosition);
                        OnContextMenuRequested?.Invoke(folder.ID, folder.Name, itemRow, worldPos);
                        evt.StopPropagation();
                        break;
                }
            });

            itemRow.RegisterCallback<DragEnterEvent>(_ =>
            {
                if (CanAcceptDrop()) itemRow.style.backgroundColor = ColorPreset.AccentBlue40Style;
            });
            itemRow.RegisterCallback<DragLeaveEvent>(_ =>
            {
                if (CanAcceptDrop() && _selectedFolderRow != itemRow)
                    itemRow.style.backgroundColor = new StyleColor(StyleKeyword.Null);
            });
            itemRow.RegisterCallback<DragUpdatedEvent>(_ =>
            {
                DragAndDrop.visualMode =
                    CanAcceptDrop() ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Rejected;
            });
            itemRow.RegisterCallback<DragPerformEvent>(evt =>
            {
                if (!CanAcceptDrop()) return;
                DragAndDrop.AcceptDrag();

                if (_selectedFolderRow == itemRow) ApplySelectedStyle(itemRow);
                else itemRow.style.backgroundColor = new StyleColor(StyleKeyword.Null);

                evt.StopPropagation();
            });

            _dragManipulator.RegisterFolderItem(itemRow, treeItemContainer, parentContainer,
                GetParentFolderId, GetChildIndex);

            var childrenContainer = new VisualElement {
                style = { display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None }
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

            if (_folderItemMap.TryGetValue(folderId, out var container)) {
                var toggle = container.Q<VisualElement>().Q<Label>();
                var children = container.ElementAt(1);

                var expanded = _expandedFolders.Contains(folderId);
                if (toggle != null) toggle.text = expanded ? "▼" : "▶";
                if (children != null) children.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private Ulid GetParentFolderId(Ulid folderId) {
            if (!_folderItemMap.TryGetValue(folderId, out var item)) return Ulid.Empty;
            var itemParent = item.parent;
            while (itemParent != null) {
                if (itemParent.userData is Ulid id) return id;
                itemParent = itemParent.parent;
            }

            return Ulid.Empty;
        }

        private int GetChildIndex(VisualElement container, Ulid folderId) {
            if (!_folderItemMap.TryGetValue(folderId, out var item)) return -1;
            return container.IndexOf(item);
        }

        private static bool CanAcceptDrop() {
            return DragAndDrop.GetGenericData("AssetManagerAssets") != null ||
                DragAndDrop.GetGenericData("AssetManagerFolders") != null;
        }

        private static void ApplySelectedStyle(VisualElement item) {
            item.AddToClassList("selected");
            item.style.backgroundColor = ColorPreset.AccentBlue40Style;
            foreach (var label in item.Children().OfType<Label>()) label.style.color = ColorPreset.AccentBlue;
        }

        private static void RemoveSelectedStyle(VisualElement item) {
            item.RemoveFromClassList("selected");
            item.style.backgroundColor = new StyleColor(StyleKeyword.Null);
            foreach (var label in item.Children().OfType<Label>())
                label.style.color = new StyleColor(StyleKeyword.Null);
        }
    }
}