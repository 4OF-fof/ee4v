using System;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class Navigation : VisualElement {
        public event Action<NavigationMode> ModeChanged;
        private readonly Button _allButton;
        private readonly Button _uncategorizedButton;
        private readonly Button _trashButton;

        public Navigation() {
            style.flexDirection = FlexDirection.Column;
            style.paddingLeft = 6;
            style.paddingRight = 6;
            style.paddingTop = 6;

            _allButton = new Button(() => { SetMode(NavigationMode.All); }) { text = "All items" };
            Add(_allButton);

            _uncategorizedButton = new Button(() => { SetMode(NavigationMode.Uncategorized); }) { text = "Uncategorized" };
            Add(_uncategorizedButton);

            _trashButton = new Button(() => { SetMode(NavigationMode.Trash); }) { text = "Trash" };
            Add(_trashButton);
        }

        private void SetMode(NavigationMode mode) {
            ModeChanged?.Invoke(mode);
            UpdateSelectionVisual(mode);
        }

        private void UpdateSelectionVisual(NavigationMode mode) {
            _allButton.RemoveFromClassList("selected");
            _uncategorizedButton.RemoveFromClassList("selected");
            _trashButton.RemoveFromClassList("selected");
            switch (mode) {
                case NavigationMode.All:
                    _allButton.AddToClassList("selected");
                    break;
                case NavigationMode.Uncategorized:
                    _uncategorizedButton.AddToClassList("selected");
                    break;
                case NavigationMode.Trash:
                    _trashButton.AddToClassList("selected");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        public void SelectAll() {
            SetMode(NavigationMode.All);
        }
    }
}