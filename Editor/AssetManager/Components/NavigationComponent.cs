using System;
using System.Collections.Generic;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.AssetManager.Presenter;
using _4OF.ee4v.AssetManager.State;
using _4OF.ee4v.AssetManager.Views;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.Utility;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Components {
    public class NavigationComponent : IAssetManagerComponent {
        private AssetManagerContext _context;
        private Action<Ulid, string, VisualElement, Vector2> _contextMenuHandler;
        private Navigation _navigationView;
        private AssetNavigationPresenter _presenter;

        public AssetManagerComponentLocation Location => AssetManagerComponentLocation.Navigation;
        public int Priority => 0;

        public void Initialize(AssetManagerContext context) {
            _context = context;
            _navigationView = new Navigation {
                style = {
                    flexGrow = 1
                }
            };

            _navigationView.SetRepository(context.Repository);
            _navigationView.SetShowDialogCallback(context.ShowDialog);

            _presenter = new AssetNavigationPresenter(
                context.Repository,
                context.AssetService,
                context.FolderService,
                context.ViewController,
                context.ShowToast,
                context.RequestRefresh,
                folders => _navigationView.SetFolders(folders),
                context.RequestTagListRefresh
            );

            _navigationView.NavigationChanged += _presenter.OnNavigationChanged;
            _navigationView.FolderSelected += _presenter.OnFolderSelected;
            _navigationView.TagListClicked += _presenter.OnTagListClicked;
            _navigationView.OnFolderRenamed += _presenter.OnFolderRenamed;
            _navigationView.OnFolderMoved += _presenter.OnFolderMoved;
            _navigationView.OnFolderCreated += _presenter.OnFolderCreated;
            _navigationView.OnFolderReordered += _presenter.OnFolderReordered;
            _navigationView.OnAssetCreated += _presenter.OnAssetCreated;

            _contextMenuHandler = (id, folderName, target, pos) =>
            {
                var menu = new GenericDropdownMenu();
                menu.AddItem(I18N.Get("UI.AssetManager.ContextMenu.Rename"), false,
                    () => _navigationView.ShowRenameFolderDialog(id, folderName));
                menu.AddItem(I18N.Get("UI.AssetManager.ContextMenu.Delete"), false,
                    () => _presenter.OnFolderDeleted(id));

                const float menuHeight = 10f + 2 * 19f;
                if (target.panel != null) {
                    var rootHeight = target.panel.visualTree.layout.height;
                    if (pos.y + menuHeight > rootHeight) pos.y -= menuHeight;
                }

                menu.DropDown(new Rect(pos.x, pos.y, 0, 0), target);
            };
            _navigationView.OnFolderContextMenuRequested += _contextMenuHandler;

            context.ViewController.FoldersChanged += OnFoldersChanged;
            context.ViewController.OnHistoryChanged += OnHistoryChanged;

            _presenter.OnNavigationChanged(
                NavigationMode.BoothItems,
                I18N.Get("UI.AssetManager.Navigation.BoothItemsContext"),
                a => !a.IsDeleted
            );
        }

        public VisualElement CreateElement() {
            return _navigationView;
        }

        public void Dispose() {
            if (_navigationView != null) {
                _navigationView.NavigationChanged -= _presenter.OnNavigationChanged;
                _navigationView.FolderSelected -= _presenter.OnFolderSelected;
                _navigationView.TagListClicked -= _presenter.OnTagListClicked;
                _navigationView.OnFolderRenamed -= _presenter.OnFolderRenamed;
                _navigationView.OnFolderMoved -= _presenter.OnFolderMoved;
                _navigationView.OnFolderCreated -= _presenter.OnFolderCreated;
                _navigationView.OnFolderReordered -= _presenter.OnFolderReordered;
                _navigationView.OnAssetCreated -= _presenter.OnAssetCreated;
                _navigationView.OnFolderContextMenuRequested -= _contextMenuHandler;
            }

            if (_context?.ViewController == null) return;
            _context.ViewController.FoldersChanged -= OnFoldersChanged;
            _context.ViewController.OnHistoryChanged -= OnHistoryChanged;
        }

        private void OnFoldersChanged(List<BaseFolder> folders) {
            _navigationView.SetFolders(folders);
        }

        private void OnHistoryChanged() {
            _navigationView.SelectState(_context.ViewController.CurrentMode, _context.ViewController.SelectedFolderId);
        }
    }
}