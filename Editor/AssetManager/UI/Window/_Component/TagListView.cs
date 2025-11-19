using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class TagListView : VisualElement {
        private readonly ScrollView _container;

        public TagListView() {
            style.flexGrow = 1;
            _container = new ScrollView();
            Add(_container);

            Refresh();
        }

        public event Action<string> OnTagSelected;

        public void Refresh() {
            _container.Clear();
            var tags = AssetLibrary.Instance?.GetAllTags() ?? new List<string>();

            foreach (var tag in tags.OrderBy(t => t)) {
                var button = new Button(() => OnTagSelected?.Invoke(tag)) {
                    text = tag,
                    style = {
                        height = 24,
                        unityTextAlign = TextAnchor.MiddleLeft
                    }
                };
                _container.Add(button);
            }
        }
    }
}