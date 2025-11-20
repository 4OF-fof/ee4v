using System;
using System.Collections.Generic;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.Utility;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class Navigation : VisualElement {
        private readonly Button _allButton;
        private readonly Button _boothItemButton;
        private readonly FolderView _folderView;
        private readonly Button _tagListButton;
        private readonly Button _trashButton;
        private readonly Button _uncategorizedButton;

        private IAssetRepository _repository;

        public Navigation() {
            style.flexDirection = FlexDirection.Column;
            style.paddingLeft = 6;
            style.paddingRight = 6;
            style.paddingTop = 6;

            _allButton = new Button(() =>
            {
                SetSelected(_allButton);
                OnNavigationChanged("All Items", a => !a.IsDeleted);
                OnFolderSelected(Ulid.Empty);
                _folderView?.ClearSelection();
            }) { text = "All items" };
            Add(_allButton);

            _boothItemButton = new Button(() =>
            {
                SetSelected(_boothItemButton);
                OnNavigationChanged("Booth Items", a => !a.IsDeleted, true);
                OnBoothItemClicked();
                _folderView?.ClearSelection();
            }) { text = "Booth Items" };
            Add(_boothItemButton);

            _tagListButton = new Button(() =>
            {
                SetSelected(_tagListButton);
                OnTagListClicked();
                _folderView?.ClearSelection();
            }) { text = "Tag List" };
            Add(_tagListButton);

            _uncategorizedButton = new Button(() =>
            {
                SetSelected(_uncategorizedButton);
                OnNavigationChanged("Uncategorized",
                    a => !a.IsDeleted && a.Folder == Ulid.Empty && (a.Tags == null || a.Tags.Count == 0));
                OnFolderSelected(Ulid.Empty);
                _folderView?.ClearSelection();
            }) { text = "Uncategorized" };
            Add(_uncategorizedButton);

            _trashButton = new Button(() =>
            {
                SetSelected(_trashButton);
                OnNavigationChanged("Trash", a => a.IsDeleted);
                OnFolderSelected(Ulid.Empty);
                _folderView?.ClearSelection();
            }) { text = "Trash" };
            Add(_trashButton);

            var spacer = new VisualElement { style = { height = 10 } };
            Add(spacer);

            Add(new Label("Folders") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4 } });

            _folderView = new FolderView();
            _folderView.OnFolderSelected += OnFolderViewSelected;
            Add(_folderView);
        }

        public void Initialize(IAssetRepository repository) {
            _repository = repository;
        }

        public event Action<string, Func<AssetMetadata, bool>, bool> NavigationChanged;

        public event Action<Func<AssetMetadata, bool>> FilterChanged;
        public event Action<Ulid> FolderSelected;
        public event Action BoothItemClicked;
        public event Action TagListClicked;

        public void SetFolders(List<BaseFolder> folders) {
            _folderView.SetFolders(folders);
        }

        private void OnNavigationChanged(string rootName, Func<AssetMetadata, bool> filter, bool isBoothMode = false) {
            NavigationChanged?.Invoke(rootName, filter, isBoothMode);
            FilterChanged?.Invoke(filter);
        }

        private void SetSelected(Button selected) {
            _allButton?.RemoveFromClassList("selected");
            _boothItemButton?.RemoveFromClassList("selected");
            _tagListButton?.RemoveFromClassList("selected");
            _uncategorizedButton?.RemoveFromClassList("selected");
            _trashButton?.RemoveFromClassList("selected");
            selected?.AddToClassList("selected");
        }

        public void SelectAll() {
            if (_allButton == null) return;
            SetSelected(_allButton);
            OnNavigationChanged("All Items", a => !a.IsDeleted);
            _folderView?.ClearSelection();
        }

        private void OnFolderViewSelected(Ulid folderId) {
            SetSelected(null);
            OnNavigationChanged("Folders", a => !a.IsDeleted);
            OnFolderSelected(folderId);
        }

        private void OnFolderSelected(Ulid folderId) {
            FolderSelected?.Invoke(folderId);
        }

        private void OnBoothItemClicked() {
            BoothItemClicked?.Invoke();
        }

        private void OnTagListClicked() {
            TagListClicked?.Invoke();
        }
    }
}