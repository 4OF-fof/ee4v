using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class MainViewHost : IDisposable
    {
        private readonly MainViewController _controller;
        private readonly CollectionNavigationController
            _collectionController;
        private readonly bool _ownsController;
        private bool _disposed;

        public MainViewHost(MainViewController controller = null)
        {
            _controller = controller ?? new MainViewController();
            _ownsController = controller == null;
            _collectionController =
                new CollectionNavigationController();

            MainView = new MainView(_controller);
            Toolbar = new MainToolbar(
                MainView.GridSize,
                MainView.HistoryOverlayMaximumItems,
                MainView.History.State);
            Toolbar.SetMinimumGridSize(MainView.MinimumGridSize);
            Toolbar.SetGridSizeVisible(
                !string.Equals(
                    _controller.SelectedNavigationItemId,
                    "tags",
                    StringComparison.Ordinal));
            NavigationPanel = new NavigationPanel(
                _controller.NavigationItems,
                _controller.SelectedNavigationItemId);
            NavigationPanel.SetCollections(
                _collectionController.Collections,
                _controller.SelectedNavigationItemId);

            Toolbar.GridSizeChanged += MainView.SetGridSize;
            Toolbar.SearchTextChanged += MainView.SetSearchText;
            Toolbar.BackClicked += MainView.GoBack;
            Toolbar.ForwardClicked += MainView.GoForward;
            Toolbar.BackHistoryClicked += MainView.GoBack;
            Toolbar.ForwardHistoryClicked += MainView.GoForward;
            Toolbar.BreadcrumbClicked += MainView.GoToBreadcrumb;
            NavigationPanel.SelectionChanged += _controller.SetSelectedNavigationItem;
            NavigationPanel.CreateCollectionRequested +=
                OnCreateCollectionRequested;
            NavigationPanel.CreateSmartCollectionRequested +=
                OnCreateSmartCollectionRequested;
            NavigationPanel.MoveCollectionsRequested +=
                _collectionController.MoveCollections;
            NavigationPanel.ItemsDroppedOnCollection +=
                _collectionController.AddItemsToCollection;
            NavigationPanel.RenameCollectionRequested +=
                OnRenameCollectionRequested;
            NavigationPanel.EditSmartCollectionRequested +=
                OnEditSmartCollectionRequested;
            NavigationPanel.DeleteCollectionsRequested +=
                OnDeleteCollectionsRequested;
            NavigationPanel.ManualSyncRequested +=
                OnManualSyncRequested;

            MainView.GridSizeChanged += Toolbar.SetGridSizeValue;
            MainView.GridSizeMinimumChanged += Toolbar.SetMinimumGridSize;
            MainView.HistoryOverlayMaximumItemsChanged += Toolbar.SetHistoryOverlayMaximumItems;
            MainView.History.Changed += Toolbar.SetHistoryState;
            _controller.NavigationChanged += OnNavigationChanged;
            _collectionController.CollectionsChanged +=
                OnCollectionsChanged;
            _collectionController.CollectionOpenRequested +=
                OnCollectionOpenRequested;
            _collectionController.ErrorChanged +=
                NavigationPanel.SetCollectionError;
            _collectionController.Activate();
        }

        public MainView MainView { get; }

        public MainToolbar Toolbar { get; }

        public NavigationPanel NavigationPanel { get; }

        public void SetSelectedNavigationItem(string itemId)
        {
            _controller.SetSelectedNavigationItem(itemId);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Toolbar.GridSizeChanged -= MainView.SetGridSize;
            Toolbar.SearchTextChanged -= MainView.SetSearchText;
            Toolbar.BackClicked -= MainView.GoBack;
            Toolbar.ForwardClicked -= MainView.GoForward;
            Toolbar.BackHistoryClicked -= MainView.GoBack;
            Toolbar.ForwardHistoryClicked -= MainView.GoForward;
            Toolbar.BreadcrumbClicked -= MainView.GoToBreadcrumb;
            NavigationPanel.SelectionChanged -= _controller.SetSelectedNavigationItem;
            NavigationPanel.CreateCollectionRequested -=
                OnCreateCollectionRequested;
            NavigationPanel.CreateSmartCollectionRequested -=
                OnCreateSmartCollectionRequested;
            NavigationPanel.MoveCollectionsRequested -=
                _collectionController.MoveCollections;
            NavigationPanel.ItemsDroppedOnCollection -=
                _collectionController.AddItemsToCollection;
            NavigationPanel.RenameCollectionRequested -=
                OnRenameCollectionRequested;
            NavigationPanel.EditSmartCollectionRequested -=
                OnEditSmartCollectionRequested;
            NavigationPanel.DeleteCollectionsRequested -=
                OnDeleteCollectionsRequested;
            NavigationPanel.ManualSyncRequested -=
                OnManualSyncRequested;

            MainView.GridSizeChanged -= Toolbar.SetGridSizeValue;
            MainView.GridSizeMinimumChanged -= Toolbar.SetMinimumGridSize;
            MainView.HistoryOverlayMaximumItemsChanged -= Toolbar.SetHistoryOverlayMaximumItems;
            MainView.History.Changed -= Toolbar.SetHistoryState;
            _controller.NavigationChanged -= OnNavigationChanged;
            _collectionController.CollectionsChanged -=
                OnCollectionsChanged;
            _collectionController.CollectionOpenRequested -=
                OnCollectionOpenRequested;
            _collectionController.ErrorChanged -=
                NavigationPanel.SetCollectionError;
            _collectionController.Dispose();

            if (_ownsController)
            {
                _controller.Dispose();
            }
        }

        private void OnNavigationChanged(string itemId)
        {
            NavigationPanel.SetSelectedItem(itemId);
            Toolbar.SetGridSizeVisible(
                !string.Equals(itemId, "tags", StringComparison.Ordinal));
        }

        private void OnCollectionsChanged(
            IReadOnlyList<AssetCollection> collections)
        {
            _controller.SetCollections(collections);
            NavigationPanel.SetCollections(
                collections,
                _controller.SelectedNavigationItemId);
        }

        private void OnManualSyncRequested()
        {
            AssetManagerUiDependencies.RequestManualSync(
                _collectionController.Reload);
        }

        private void OnCollectionOpenRequested(
            AssetCollection collection)
        {
            if (collection == null)
            {
                return;
            }

            _controller.SetSelectedNavigationItem(
                AssetManagerCollectionViewId.Encode(collection.Id));
        }

        private void OnCreateCollectionRequested(
            VisualElement anchor)
        {
            CollectionCreationWindow.Show(
                anchor,
                false,
                _collectionController.CreateCollection,
                _collectionController.CreateSmartCollection);
        }

        private void OnCreateSmartCollectionRequested(
            VisualElement anchor)
        {
            CollectionCreationWindow.Show(
                anchor,
                true,
                _collectionController.CreateCollection,
                _collectionController.CreateSmartCollection);
        }

        private void OnRenameCollectionRequested(
            AssetCollection collection,
            VisualElement anchor,
            Vector2 panelPosition)
        {
            CollectionCreationWindow.ShowRename(
                anchor,
                panelPosition,
                collection,
                _collectionController.UpdateCollection);
        }

        private void OnEditSmartCollectionRequested(
            AssetCollection collection,
            VisualElement anchor,
            Vector2 panelPosition)
        {
            CollectionCreationWindow.ShowSmartConditions(
                anchor,
                panelPosition,
                collection,
                _collectionController.UpdateSmartCollection);
        }

        private void OnDeleteCollectionsRequested(
            IReadOnlyList<AssetCollection> collections)
        {
            if (CollectionDeletionConfirmation.Confirm(
                    collections))
            {
                _collectionController.DeleteCollections(
                    collections
                        .Where(collection => collection != null)
                        .Select(collection => collection.Id)
                        .ToArray());
            }
        }
    }
}
