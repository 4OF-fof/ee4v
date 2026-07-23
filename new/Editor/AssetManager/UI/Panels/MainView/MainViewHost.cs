using System;

namespace Ee4v.AssetManager
{
    internal sealed class MainViewHost : IDisposable
    {
        private readonly MainViewController _controller;
        private readonly bool _ownsController;
        private bool _disposed;

        public MainViewHost(MainViewController controller = null)
        {
            _controller = controller ?? new MainViewController();
            _ownsController = controller == null;

            MainView = new MainView(_controller);
            Toolbar = new MainToolbar(
                MainView.GridSize,
                MainView.HistoryOverlayMaximumItems,
                MainView.History.State);
            NavigationPanel = new NavigationPanel(
                _controller.NavigationItems,
                _controller.SelectedNavigationItemId);

            Toolbar.GridSizeChanged += MainView.SetGridSize;
            Toolbar.SearchTextChanged += MainView.SetSearchText;
            Toolbar.BackClicked += MainView.GoBack;
            Toolbar.ForwardClicked += MainView.GoForward;
            Toolbar.BackHistoryClicked += MainView.GoBack;
            Toolbar.ForwardHistoryClicked += MainView.GoForward;
            Toolbar.BreadcrumbClicked += MainView.GoToBreadcrumb;
            NavigationPanel.SelectionChanged += _controller.SetSelectedNavigationItem;

            MainView.GridSizeChanged += Toolbar.SetGridSizeValue;
            MainView.HistoryOverlayMaximumItemsChanged += Toolbar.SetHistoryOverlayMaximumItems;
            MainView.History.Changed += Toolbar.SetHistoryState;
            _controller.NavigationChanged += NavigationPanel.SetSelectedItem;
        }

        public MainView MainView { get; }

        public MainToolbar Toolbar { get; }

        public NavigationPanel NavigationPanel { get; }

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

            MainView.GridSizeChanged -= Toolbar.SetGridSizeValue;
            MainView.HistoryOverlayMaximumItemsChanged -= Toolbar.SetHistoryOverlayMaximumItems;
            MainView.History.Changed -= Toolbar.SetHistoryState;
            _controller.NavigationChanged -= NavigationPanel.SetSelectedItem;

            if (_ownsController)
            {
                _controller.Dispose();
            }
        }
    }
}
