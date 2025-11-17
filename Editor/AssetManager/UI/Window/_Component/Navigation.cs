using System;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.Utility;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class Navigation : VisualElement {
        private readonly Button _allButton;
        private readonly Button _trashButton;
        private readonly Button _uncategorizedButton;

        public Navigation() {
            style.flexDirection = FlexDirection.Column;
            style.paddingLeft = 6;
            style.paddingRight = 6;
            style.paddingTop = 6;

            _allButton = new Button(() =>
            {
                SetFilter(a => !a.IsDeleted);
                SetSelected(_allButton);
            }) { text = "All items" };
            Add(_allButton);

            _uncategorizedButton = new Button(() =>
            {
                SetFilter(a => !a.IsDeleted && a.Folder == Ulid.Empty && (a.Tags == null || a.Tags.Count == 0));
                SetSelected(_uncategorizedButton);
            }) { text = "Uncategorized" };
            Add(_uncategorizedButton);

            _trashButton = new Button(() =>
            {
                SetFilter(a => a.IsDeleted);
                SetSelected(_trashButton);
            }) { text = "Trash" };
            Add(_trashButton);
        }

        public event Action<Func<AssetMetadata, bool>> FilterChanged;

        private void SetFilter(Func<AssetMetadata, bool> predicate) {
            FilterChanged?.Invoke(predicate);
        }

        private void SetSelected(Button selected) {
            _allButton.RemoveFromClassList("selected");
            _uncategorizedButton.RemoveFromClassList("selected");
            _trashButton.RemoveFromClassList("selected");
            selected.AddToClassList("selected");
        }

        public void SelectAll() {
            SetFilter(a => !a.IsDeleted);
            SetSelected(_allButton);
        }
    }
}