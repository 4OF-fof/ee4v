using System.Collections.Generic;
using UnityEngine.UIElements;
using _4OF.ee4v.AssetManager.Data;

namespace _4OF.ee4v.AssetManager.UI.Window._Component {
    public class MainContent : VisualElement {
        private readonly VisualElement _grid;

        public MainContent() {
            style.flexGrow = 1;

            var scrollView = new ScrollView {
                style = {
                    flexGrow = 1
                }
            };

            _grid = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    alignItems = Align.FlexStart,
                    paddingLeft = 6,
                    paddingTop = 6
                }
            };

            scrollView.Add(_grid);
            Add(scrollView);
        }

        public void RefreshContents(IEnumerable<AssetMetadata> assets) {
            _grid.Clear();
            foreach (var asset in assets) {
                var card = new AssetCard(asset);
                _grid.Add(card);
            }
        }
    }
}
