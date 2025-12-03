using System;
using System.Collections.Generic;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.AssetManager.Modules;
using _4OF.ee4v.AssetManager.Presenter;
using _4OF.ee4v.AssetManager.State;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Interfaces;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Components {
    public class AssetGridComponent : IAssetManagerComponent {
        private AssetView _assetView;
        private AssetManagerContext _context;
        private AssetGridPresenter _presenter;
        private Action<VisualElement> _sortMenuHandler;

        public string Name => "Asset Grid";
        public string Description => "Displays assets in a grid view with toolbar.";
        public AssetManagerComponentLocation Location => AssetManagerComponentLocation.MainView;
        public int Priority => 0;

        public void Initialize(AssetManagerContext context) {
            _context = context;
            _assetView = new AssetView();

            _presenter = new AssetGridPresenter(
                context.Repository,
                context.AssetService,
                context.FolderService,
                context.RequestRefresh
            );

            _assetView.SetController(context.ViewController);

            // 変更点: context.ShowDialog (Func) を Action にラップして渡す
            _assetView.Initialize(
                context.TextureService,
                context.Repository,
                context.AssetService,
                context.FolderService,
                content => context.ShowDialog(content)
            );

            _assetView.OnSelectionChange += OnSelectionChanged;
            _assetView.OnItemsDroppedToFolder += OnItemsDropped;

            _sortMenuHandler = element => ShowSortMenu(element);
            _assetView.OnSortMenuRequested += _sortMenuHandler;

            context.ViewController.ModeChanged += OnModeChanged;
            context.ViewController.BoothItemFoldersChanged += _assetView.ShowBoothItemFolders;
            context.ViewController.OnHistoryChanged += OnHistoryChanged;
        }

        public VisualElement CreateElement() {
            return _assetView;
        }

        public void Dispose() {
            if (_assetView != null) {
                _assetView.OnSelectionChange -= OnSelectionChanged;
                _assetView.OnItemsDroppedToFolder -= OnItemsDropped;
                _assetView.OnSortMenuRequested -= _sortMenuHandler;
            }

            if (_context?.ViewController != null) {
                _context.ViewController.ModeChanged -= OnModeChanged;
                _context.ViewController.BoothItemFoldersChanged -= _assetView.ShowBoothItemFolders;
                _context.ViewController.OnHistoryChanged -= OnHistoryChanged;
            }
        }

        private void OnSelectionChanged(List<object> selectedItems) {
            // Context経由でSelectionModelなどを更新する処理があればここに記述
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
            _assetView.style.display = mode == NavigationMode.TagList ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void OnHistoryChanged() {
            _assetView.ClearSelection();
        }

        private void ShowSortMenu(VisualElement element) {
            var menu = new GenericDropdownMenu();
            menu.AddItem(I18N.Get("UI.AssetManager.Sort.NameAsc"), false,
                () => _assetView.ApplySortType(AssetSortType.NameAsc));
            menu.AddItem(I18N.Get("UI.AssetManager.Sort.NameDesc"), false,
                () => _assetView.ApplySortType(AssetSortType.NameDesc));
            menu.AddSeparator("");
            menu.AddItem(I18N.Get("UI.AssetManager.Sort.DateAddedNewest"), false,
                () => _assetView.ApplySortType(AssetSortType.DateAddedNewest));
            menu.AddItem(I18N.Get("UI.AssetManager.Sort.DateAddedOldest"), false,
                () => _assetView.ApplySortType(AssetSortType.DateAddedOldest));
            menu.AddSeparator("");
            menu.AddItem(I18N.Get("UI.AssetManager.Sort.LastEditNewest"), false,
                () => _assetView.ApplySortType(AssetSortType.DateNewest));
            menu.AddItem(I18N.Get("UI.AssetManager.Sort.LastEditOldest"), false,
                () => _assetView.ApplySortType(AssetSortType.DateOldest));
            menu.AddSeparator("");
            menu.AddItem(I18N.Get("UI.AssetManager.Sort.SizeSmallest"), false,
                () => _assetView.ApplySortType(AssetSortType.SizeSmallest));
            menu.AddItem(I18N.Get("UI.AssetManager.Sort.SizeLargest"), false,
                () => _assetView.ApplySortType(AssetSortType.SizeLargest));
            menu.AddSeparator("");
            menu.AddItem(I18N.Get("UI.AssetManager.Sort.FileTypeAsc"), false,
                () => _assetView.ApplySortType(AssetSortType.ExtAsc));
            menu.AddItem(I18N.Get("UI.AssetManager.Sort.FileTypeDesc"), false,
                () => _assetView.ApplySortType(AssetSortType.ExtDesc));

            var targetElement = element ?? _assetView;
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
                    backgroundColor = new StyleColor(ColorPreset.WarningButton)
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