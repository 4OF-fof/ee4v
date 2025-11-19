using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class TagListView : VisualElement {
        private readonly ScrollView _container;
        private IAssetRepository _repository;

        public TagListView() {
            style.flexGrow = 1;
            _container = new ScrollView();
            Add(_container);
        }

        public void Initialize(IAssetRepository repository) {
            _repository = repository;
            Refresh();
        }

        public event Action<string> OnTagSelected;

        public void Refresh() {
            _container.Clear();
            if (_repository == null) return;

            // 全アセットからタグを収集して重複排除
            var tags = _repository.GetAllAssets()
                .SelectMany(a => a.Tags)
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            foreach (var tag in tags) {
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