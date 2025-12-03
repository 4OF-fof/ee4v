using System;
using System.Collections.Generic;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.AssetManager.State;
using _4OF.ee4v.AssetManager.Views.Components.Navigation;
using _4OF.ee4v.AssetManager.Views.Dialog;
using _4OF.ee4v.Core.Utility;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Views {
    public sealed class Navigation : VisualElement {
        private readonly CreateAssetDialog _createAssetDialog;
        private readonly CreateFolderDialog _createFolderDialog;
        private readonly UserFolderTree _folderTree;
        private readonly NavigationFooter _footer;
        private readonly RenameFolderDialog _renameFolderDialog;
        private readonly SystemFolderList _systemList;

        private Func<VisualElement, VisualElement> _showDialogCallback;

        public Navigation() {
            style.flexDirection = FlexDirection.Column;
            style.paddingLeft = 6;
            style.paddingRight = 6;
            style.paddingTop = 6;

            _systemList = new SystemFolderList();
            _systemList.OnNavigationRequested += (mode, ctx, filter) =>
            {
                _folderTree?.ClearSelection();
                FireNav(mode, ctx, filter);
            };
            _systemList.OnTagListRequested += () => TagListClicked?.Invoke();
            Add(_systemList);

            Add(new VisualElement { style = { height = 10 } });

            _folderTree = new UserFolderTree();
            _folderTree.OnNavigationRequested += (mode, ctx, filter) =>
            {
                _systemList.ClearSelection();
                NavigationChanged?.Invoke(mode, ctx, filter);
                FolderSelected?.Invoke(Ulid.Empty);
            };
            _folderTree.OnCreateFolderRequested += ShowCreateFolderDialog;
            _folderTree.OnFolderSelected += id =>
            {
                _systemList.ClearSelection();
                FolderSelected?.Invoke(id);
            };
            _folderTree.OnContextMenuRequested += (id, folderName, target, pos) =>
                OnFolderContextMenuRequested?.Invoke(id, folderName, target, pos);
            _folderTree.OnFolderMoved += (s, t) => OnFolderMoved?.Invoke(s, t);
            _folderTree.OnFolderReordered += (p, s, i) => OnFolderReordered?.Invoke(p, s, i);
            Add(_folderTree);

            _footer = new NavigationFooter();
            _footer.OnCreateAssetRequested += ShowCreateAssetDialog;
            Add(_footer);

            _createAssetDialog = new CreateAssetDialog();
            _createAssetDialog.OnAssetCreated += (n, d, f, t, s, i) => OnAssetCreated?.Invoke(n, d, f, t, s, i);
            _createAssetDialog.OnImportFromBoothRequested += () =>
            {
                if (_showDialogCallback == null) return;
                _showDialogCallback.Invoke(WaitBoothSyncDialog.CreateContent(_showDialogCallback));
            };

            _createFolderDialog = new CreateFolderDialog();
            _createFolderDialog.OnFolderCreated += n => OnFolderCreated?.Invoke(n);

            _renameFolderDialog = new RenameFolderDialog();
            _renameFolderDialog.OnFolderRenamed += (id, n) => OnFolderRenamed?.Invoke(id, n);
        }

        public event Action<NavigationMode, string, Func<AssetMetadata, bool>> NavigationChanged;
        public event Action<Ulid> FolderSelected;
        public event Action TagListClicked;
        public event Action<Ulid, string> OnFolderRenamed;
        public event Action<Ulid, Ulid> OnFolderMoved;
        public event Action<Ulid, string, VisualElement, Vector2> OnFolderContextMenuRequested;
        public event Action<string> OnFolderCreated;
        public event Action<Ulid, Ulid, int> OnFolderReordered;
        public event Action<string, string, string, List<string>, string, string> OnAssetCreated;

        public void SetRepository(IAssetRepository repository) {
            _createAssetDialog.SetRepository(repository);
        }

        public void SetShowDialogCallback(Func<VisualElement, VisualElement> callback) {
            _showDialogCallback = callback;
        }

        public void SetFolders(List<BaseFolder> folders) {
            _folderTree.SetFolders(folders);
        }

        public void SelectState(NavigationMode mode, Ulid folderId) {
            _systemList.ClearSelection();
            _folderTree.ClearSelection();

            switch (mode) {
                case NavigationMode.Folders:
                    _folderTree.SelectFolder(folderId);
                    break;
                case NavigationMode.AllItems:
                    _systemList.SelectByIndex(0);
                    break;
                case NavigationMode.BoothItems:
                    _systemList.SelectByIndex(1);
                    break;
                case NavigationMode.Backups:
                    _systemList.SelectByIndex(2);
                    break;
                case NavigationMode.Uncategorized:
                    _systemList.SelectByIndex(3);
                    break;
                case NavigationMode.TagList:
                    _systemList.SelectByIndex(4);
                    break;
                case NavigationMode.Trash:
                    _systemList.SelectByIndex(5);
                    break;
            }
        }

        public void ShowRenameFolderDialog(Ulid folderId, string oldName) {
            _showDialogCallback?.Invoke(_renameFolderDialog.CreateContent(folderId, oldName));
        }

        private void ShowCreateAssetDialog() {
            _showDialogCallback?.Invoke(_createAssetDialog.CreateContent());
        }

        private void ShowCreateFolderDialog() {
            _showDialogCallback?.Invoke(_createFolderDialog.CreateContent());
        }

        private void FireNav(NavigationMode mode, string naviName, Func<AssetMetadata, bool> filter) {
            FolderSelected?.Invoke(Ulid.Empty);
            NavigationChanged?.Invoke(mode, naviName, filter);
        }
    }
}