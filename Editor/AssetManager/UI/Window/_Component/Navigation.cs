using System;
using System.Collections.Generic;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.Utility;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class Navigation : VisualElement {
        private readonly List<Button> _buttons = new();
        private readonly FolderView _folderView;
        private Button _selectedButton;

        public Navigation() {
            style.flexDirection = FlexDirection.Column;
            style.paddingLeft = 6;
            style.paddingRight = 6;
            style.paddingTop = 6;

            CreateNavButton("All items", () => FireNav(NavigationMode.AllItems, "All Items", a => !a.IsDeleted));
            CreateNavButton("Booth Items", () => {
                FireNav(NavigationMode.BoothItems, "Booth Items", a => !a.IsDeleted);
                BoothItemClicked?.Invoke();
            });
            CreateNavButton("Tag List", () => {
                TagListClicked?.Invoke();
                _folderView?.ClearSelection();
            });
            CreateNavButton("Uncategorized", () => FireNav(NavigationMode.Uncategorized, "Uncategorized", 
                a => !a.IsDeleted && a.Folder == Ulid.Empty && (a.Tags == null || a.Tags.Count == 0)));
            CreateNavButton("Trash", () => FireNav(NavigationMode.Trash, "Trash", a => a.IsDeleted));

            Add(new VisualElement { style = { height = 10 } });

            var foldersLabel = new Label("Folders") {
                style = {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 4,
                    paddingTop = 2,
                    paddingBottom = 2
                }
            };
            foldersLabel.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                SetSelected(null);
                _folderView?.ClearSelection();
                NavigationChanged?.Invoke(NavigationMode.Folders, "Folders", a => !a.IsDeleted);
                evt.StopPropagation();
            });
            Add(foldersLabel);

            _folderView = new FolderView();
            _folderView.OnFolderSelected += OnFolderViewSelected;
            Add(_folderView);
        }

        public event Action<NavigationMode, string, Func<AssetMetadata, bool>> NavigationChanged;
        public event Action<Ulid> FolderSelected;
        public event Action BoothItemClicked;
        public event Action TagListClicked;

        public void Initialize(IAssetRepository repository) { }

        public void SetFolders(List<BaseFolder> folders) {
            _folderView.SetFolders(folders);
        }

        public void SelectAll() {
            if (_buttons.Count > 0) _buttons[0].SendEvent(new ClickEvent());
        }

        private void CreateNavButton(string text, Action onClick) {
            var btn = new Button { text = text };
            btn.clicked += () => {
                SetSelected(btn);
                onClick?.Invoke();
            };
            _buttons.Add(btn);
            Add(btn);
        }

        private void FireNav(NavigationMode mode, string naviName, Func<AssetMetadata, bool> filter) {
            _folderView.ClearSelection();
            FolderSelected?.Invoke(Ulid.Empty);
            NavigationChanged?.Invoke(mode, naviName, filter);
        }

        private void SetSelected(Button btn) {
            _selectedButton?.RemoveFromClassList("selected");
            _selectedButton = btn;
            _selectedButton?.AddToClassList("selected");
        }

        private void OnFolderViewSelected(Ulid folderId) {
            SetSelected(null);
            FolderSelected?.Invoke(folderId);
        }
    }
}