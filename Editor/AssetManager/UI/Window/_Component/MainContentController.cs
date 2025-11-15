using System;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Service;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class MainContentController {
        private readonly MainContent _view;
        private Func<AssetMetadata, bool> _filter;

        public MainContentController(MainContent view) {
            _view = view;
        }

        public void Refresh() {
            var assets = AssetLibrary.Instance?.Assets ?? Enumerable.Empty<AssetMetadata>();
            var filtered = _filter == null ? assets : assets.Where(_filter);
            _view.RefreshContents(filtered);
        }

        public void RefreshLibrary() {
            AssetLibraryService.RefreshAssetLibrary();
            Refresh();
        }

        public void SetFilter(Func<AssetMetadata, bool> predicate) {
            _filter = predicate;
            Refresh();
        }

        public void SetTextFilter(string query) {
            if (string.IsNullOrEmpty(query)) {
                ClearFilter();
                return;
            }
            var q = query.Trim();
            SetFilter(a => (a?.Name ?? string.Empty).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public void ClearFilter() {
            _filter = null;
            Refresh();
        }
    }
}
