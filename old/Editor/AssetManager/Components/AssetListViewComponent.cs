using System;
using System.Collections.Generic;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.AssetManager.Presenter;
using _4OF.ee4v.AssetManager.Services;
using _4OF.ee4v.AssetManager.Views;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Components {
    public class AssetListViewComponent : IAssetManagerComponent {
        private AssetListView _assetListView;
        private AssetManagerContext _context;
        private AssetGridPresenter _presenter;
        private Action<VisualElement> _sortMenuHandler;

        public AssetManagerComponentLocation Location => AssetManagerComponentLocation.MainView;
        public int Priority => 0;

        public void Initialize(AssetManagerContext context) {
            _context = context;
            _assetListView = new AssetListView();

            _presenter = new AssetGridPresenter(
                context.Repository,
                context.AssetService,
                context.FolderService,
                context.RequestRefresh
            );

            _assetListView.SetController(context.ViewController);

            _assetListView.Initialize(
                context.TextureService,
                context.Repository,
                context.AssetService,
                context.FolderService,
                content => context.ShowDialog(content)
            );

            _assetListView.OnItemsDroppedToFolder += OnItemsDropped;

            _sortMenuHandler = ShowSortMenu;
            _assetListView.OnSortMenuRequested += _sortMenuHandler;

            context.ViewController.ModeChanged += OnModeChanged;
            context.ViewController.BoothItemFoldersChanged += _assetListView.ShowBoothItemFolders;
            context.ViewController.OnHistoryChanged += OnHistoryChanged;
        }

        public VisualElement CreateElement() {
            return _assetListView;
        }

        public void Dispose() {
            if (_assetListView != null) {
                _assetListView.OnItemsDroppedToFolder -= OnItemsDropped;
                _assetListView.OnSortMenuRequested -= _sortMenuHandler;
            }

            if (_context?.ViewController == null) return;
            _context.ViewController.ModeChanged -= OnModeChanged;
            if (_assetListView != null)
                _context.ViewController.BoothItemFoldersChanged -= _assetListView.ShowBoothItemFolders;
            _context.ViewController.OnHistoryChanged -= OnHistoryChanged;
        }

        private void OnItemsDropped(List<Ulid> assetIds, List<Ulid> folderIds, Ulid targetFolderId) {
            if (assetIds.Count > 0) {
                var assetsFromBoothItemFolder = _presenter.FindAssetsFromBoothItemFolder(assetIds);
                if (assetsFromBoothItemFolder.Count > 0) {
                    ShowBoothItemFolderWarningDialog(assetIds, targetFolderId, assetsFromBoothItemFolder);
                    return;
                }
            }

            _presenter.PerformItemsDroppedToFolder(assetIds, folderIds, targetFolderId);
        }

        private void OnModeChanged(NavigationMode mode) {
            _assetListView.style.display = mode == NavigationMode.TagList ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void OnHistoryChanged() {
            _assetListView.ClearSelection();
            _assetListView.ResetSearch();
        }

        private void ShowSortMenu(VisualElement element) {
            var menu = new GenericDropdownMenu();
            menu.AddItem(I18N.Get("UI.AssetManager.Sort.NameAsc"), false,
                () => _assetListView.ApplySortType(AssetSortType.NameAsc));
            menu.AddItem(I18N.Get("UI.AssetManager.Sort.NameDesc"), false,
                () => _assetListView.ApplySortType(AssetSortType.NameDesc));
            menu.AddSeparator("");
            menu.AddItem(I18N.Get("UI.AssetManager.Sort.DateAddedNewest"), false,
                () => _assetListView.ApplySortType(AssetSortType.DateAddedNewest));
            menu.AddItem(I18N.Get("UI.AssetManager.Sort.DateAddedOldest"), false,
                () => _assetListView.ApplySortType(AssetSortType.DateAddedOldest));
            menu.AddSeparator("");
            menu.AddItem(I18N.Get("UI.AssetManager.Sort.LastEditNewest"), false,
                () => _assetListView.ApplySortType(AssetSortType.DateNewest));
            menu.AddItem(I18N.Get("UI.AssetManager.Sort.LastEditOldest"), false,
                () => _assetListView.ApplySortType(AssetSortType.DateOldest));
            menu.AddSeparator("");
            menu.AddItem(I18N.Get("UI.AssetManager.Sort.SizeSmallest"), false,
                () => _assetListView.ApplySortType(AssetSortType.SizeSmallest));
            menu.AddItem(I18N.Get("UI.AssetManager.Sort.SizeLargest"), false,
                () => _assetListView.ApplySortType(AssetSortType.SizeLargest));
            menu.AddSeparator("");
            menu.AddItem(I18N.Get("UI.AssetManager.Sort.FileTypeAsc"), false,
                () => _assetListView.ApplySortType(AssetSortType.ExtAsc));
            menu.AddItem(I18N.Get("UI.AssetManager.Sort.FileTypeDesc"), false,
                () => _assetListView.ApplySortType(AssetSortType.ExtDesc));

            var targetElement = element ?? _assetListView;
            menu.DropDown(targetElement.worldBound, targetElement);
        }

        private void ShowBoothItemFolderWarningDialog(List<Ulid> assetIds, Ulid targetFolderId,
            List<AssetMetadata> assetsFromBoothItemFolder) {
            var content = new VisualElement();

            var titleLabel = new Label(I18N.Get("UI.AssetManager.Dialog.BoothItemWarning.Title")) {
                style = {
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 10
                }
            };
            content.Add(titleLabel);

            var messageText = assetsFromBoothItemFolder.Count == 1
                ? I18N.Get("UI.AssetManager.Dialog.BoothItemWarning.Single", assetsFromBoothItemFolder[0].Name)
                : I18N.Get("UI.AssetManager.Dialog.BoothItemWarning.Multi", assetsFromBoothItemFolder.Count);

            var message = new Label(messageText) {
                style = {
                    marginBottom = 15,
                    whiteSpace = WhiteSpace.Normal
                }
            };
            content.Add(message);

            var buttonRow = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexEnd
                }
            };

            var cancelBtn = new Button {
                text = I18N.Get("UI.AssetManager.Dialog.Button.Cancel"),
                style = { marginRight = 5 }
            };
            buttonRow.Add(cancelBtn);

            var continueBtn = new Button {
                text = I18N.Get("UI.AssetManager.Dialog.Button.Continue"),
                style = {
                    backgroundColor = new StyleColor(ColorPreset.Error)
                }
            };
            buttonRow.Add(continueBtn);

            content.Add(buttonRow);

            var dialogContainer = _context.ShowDialog(content);

            cancelBtn.clicked += () => dialogContainer?.RemoveFromHierarchy();
            continueBtn.clicked += () =>
            {
                foreach (var assetId in assetIds) _context.AssetService.SetFolder(assetId, targetFolderId);
                _context.RequestRefresh(true);
                dialogContainer?.RemoveFromHierarchy();
            };
        }
    }
}