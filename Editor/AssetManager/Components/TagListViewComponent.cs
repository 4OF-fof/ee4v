using _4OF.ee4v.AssetManager.Presenter;
using _4OF.ee4v.AssetManager.State;
using _4OF.ee4v.AssetManager.Views;
using _4OF.ee4v.Core.Interfaces;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Components {
    public class TagListViewComponent : IAssetManagerComponent {
        private AssetManagerContext _context;
        private AssetNavigationPresenter _presenter; // TagList操作はNavigationPresenterが担当している
        private TagListView _tagListView;

        public AssetManagerComponentLocation Location => AssetManagerComponentLocation.MainView;
        public int Priority => 10;

        public void Initialize(AssetManagerContext context) {
            _context = context;
            _tagListView = new TagListView {
                style = { display = DisplayStyle.None }
            };

            _tagListView.Initialize(context.Repository);
            _tagListView.SetController(context.ViewController);
            _tagListView.SetShowDialogCallback(context.ShowDialog);

            // PresenterはNavigationComponentと共有せず新規作成（Tag操作用）
            _presenter = new AssetNavigationPresenter(
                context.Repository,
                context.AssetService,
                context.FolderService,
                context.ViewController,
                context.ShowToast,
                context.RequestRefresh,
                _ => { }, // フォルダ設定はこのコンポーネントでは行わない
                () => _tagListView.Refresh()
            );

            _tagListView.OnTagSelected += OnTagSelected;
            _tagListView.OnTagRenamed += _presenter.OnTagRenamed;
            _tagListView.OnTagDeleted += _presenter.OnTagDeleted;

            context.ViewController.ModeChanged += OnModeChanged;
            context.RequestTagListRefresh += OnRefreshRequested;
        }

        public VisualElement CreateElement() {
            return _tagListView;
        }

        public void Dispose() {
            if (_tagListView != null) {
                _tagListView.OnTagSelected -= OnTagSelected;
                _tagListView.OnTagRenamed -= _presenter.OnTagRenamed;
                _tagListView.OnTagDeleted -= _presenter.OnTagDeleted;
            }

            if (_context?.ViewController != null) _context.ViewController.ModeChanged -= OnModeChanged;

            if (_context != null) _context.RequestTagListRefresh -= OnRefreshRequested;
        }

        private void OnTagSelected(string tag) {
            // タグ選択時はNavigationPresenter経由でモード切り替え
            _presenter.OnTagSelected(tag);
        }

        private void OnModeChanged(NavigationMode mode) {
            _tagListView.style.display = mode == NavigationMode.TagList ? DisplayStyle.Flex : DisplayStyle.None;
            if (mode == NavigationMode.TagList) _tagListView.Refresh();
        }

        private void OnRefreshRequested() {
            _tagListView.Refresh();
        }
    }
}