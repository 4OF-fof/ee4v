using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class TagListView : VisualElement {
        private readonly ListView _listView;
        private IAssetRepository _repository;
        private List<string> _tags = new();

        public TagListView() {
            style.flexGrow = 1;
            
            _listView = new ListView {
                style = { flexGrow = 1 },
                makeItem = () => {
                    var btn = new Button {
                        style = {
                            height = 24,
                            unityTextAlign = TextAnchor.MiddleLeft,
                            marginLeft = 0, marginRight = 0,
                            borderLeftWidth = 0, borderRightWidth = 0, borderTopWidth = 0, borderBottomWidth = 0,
                            backgroundColor = Color.clear
                        }
                    };
                    return btn;
                },
                bindItem = (e, i) => {
                    if (i < 0 || i >= _tags.Count) return;
                    var btn = (Button)e;
                    var tag = _tags[i];
                    btn.text = tag;
                    btn.SetEnabled(false);
                },
                fixedItemHeight = 24,
                selectionType = SelectionType.Single
            };
            
            _listView.makeItem = () => new Label {
                style = {
                    height = 24,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    paddingLeft = 4,
                    borderBottomWidth = 1,
                    borderBottomColor = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 0.2f))
                }
            };
            _listView.bindItem = (e, i) => {
                var label = (Label)e;
                label.text = _tags[i];
            };

            _listView.selectionChanged += OnSelectionChanged;
            Add(_listView);
        }

        public void Initialize(IAssetRepository repository) {
            _repository = repository;
            Refresh();
        }

        public event Action<string> OnTagSelected;

        public void Refresh() {
            if (_repository == null) return;

            _tags = _repository.GetAllAssets()
                .SelectMany(a => a.Tags)
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            _listView.itemsSource = _tags;
            _listView.Rebuild();
        }

        private void OnSelectionChanged(IEnumerable<object> selection) {
            var selected = selection.FirstOrDefault() as string;
            if (string.IsNullOrEmpty(selected)) return;
            OnTagSelected?.Invoke(selected);
            _listView.ClearSelection();
        }
    }
}