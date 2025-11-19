using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.Utility;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class FolderView : VisualElement {
        private readonly ListView _listView;
        private List<BaseFolder> _folders = new();

        public FolderView() {
            _listView = new ListView {
                makeItem = () => new Label(),
                bindItem = (element, i) =>
                {
                    var label = (Label)element;
                    label.text = _folders[i].Name;
                },
                selectionType = SelectionType.Single,
                fixedItemHeight = 20
            };

            _listView.selectionChanged += OnSelectionChanged;
            Add(_listView);
        }

        public event Action<Ulid> OnFolderSelected;

        public void SetFolders(List<BaseFolder> folders) {
            _folders = folders ?? new List<BaseFolder>();
            _listView.itemsSource = _folders;
            _listView.Rebuild();
        }

        public void ClearSelection() {
            _listView.ClearSelection();
        }

        private void OnSelectionChanged(IEnumerable<object> selectedItems) {
            var selectedFolder = selectedItems.FirstOrDefault() as BaseFolder;
            OnFolderSelected?.Invoke(selectedFolder?.ID ?? Ulid.Empty);
        }
    }
}