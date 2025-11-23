using System;
using System.Collections.Generic;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.Utility;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class Navigation : VisualElement {
        private readonly VisualElement _folderContainer;
        private readonly List<Label> _navLabels = new();

        private Ulid _currentSelectedFolderId = Ulid.Empty;
        private VisualElement _selectedFolderItem;
        private Label _selectedLabel;

        public Navigation() {
            style.flexDirection = FlexDirection.Column;
            style.paddingLeft = 6;
            style.paddingRight = 6;
            style.paddingTop = 6;

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

            var foldersLabel = new Label("Folders") {
                style = {
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 4,
                    paddingBottom = 4,
                    marginBottom = 2,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            foldersLabel.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                SetSelected(foldersLabel);
                NavigationChanged?.Invoke(NavigationMode.Folders, "Folders", a => !a.IsDeleted);
                evt.StopPropagation();
            });
            Add(foldersLabel);

            _folderContainer = new VisualElement();
            var scrollView = new ScrollView(ScrollViewMode.Vertical) {
                style = {
                    flexGrow = 1
                }
            };
            scrollView.Add(_folderContainer);
            Add(scrollView);
        }

        public event Action<NavigationMode, string, Func<AssetMetadata, bool>> NavigationChanged;
        public event Action<Ulid> FolderSelected;
        public event Action BoothItemClicked;
        public event Action TagListClicked;

        public void SetFolders(List<BaseFolder> folders) {
            folders ??= new List<BaseFolder>();
            _folderContainer.Clear();

            _selectedFolderItem = null;

            foreach (var folder in folders) {
                var itemContainer = new VisualElement {
                    userData = folder.ID,
                    style = {
                        flexDirection = FlexDirection.Row,
                        marginBottom = 1
                    }
                };

                var label = new Label(folder.Name) {
                    style = {
                        paddingLeft = 16,
                        paddingRight = 8,
                        paddingTop = 3,
                        paddingBottom = 3,
                        flexGrow = 1
                    }
                };

                itemContainer.Add(label);

                if (folder.ID == _currentSelectedFolderId) {
                    ApplySelectedStyle(itemContainer);
                    _selectedFolderItem = itemContainer;
                }

                itemContainer.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    OnFolderViewSelected((Ulid)itemContainer.userData, itemContainer);
                    evt.StopPropagation();
                });

                _folderContainer.Add(itemContainer);
            }
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
    }
}