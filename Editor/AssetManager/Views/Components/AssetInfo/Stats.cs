using System.Collections.Generic;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Views.Components.AssetInfo {
    public class Stats : VisualElement {
        private readonly VisualElement _container;
        private readonly Dictionary<string, LinkInfoRow> _linkRows = new();
        private readonly Dictionary<string, InfoRow> _rows = new();

        public Stats() {
            Add(new Label(I18N.Get("UI.AssetManager.AssetInfo.Information"))
                { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 12, marginBottom = 4 } });

            _container = new VisualElement { style = { paddingLeft = 4 } };
            Add(_container);

            CreateRow("Size", I18N.Get("UI.AssetManager.AssetInfo.Size"));
            CreateRow("Type", I18N.Get("UI.AssetManager.AssetInfo.Type"));
            CreateRow("SubFolders", I18N.Get("UI.AssetManager.AssetInfo.SubFolders"));
            CreateRow("Assets", I18N.Get("UI.AssetManager.AssetInfo.Assets"));
            CreateRow("TotalAssets", I18N.Get("UI.AssetManager.AssetInfo.TotalAssets"));
            CreateRow("TotalSize", I18N.Get("UI.AssetManager.AssetInfo.TotalSize"));
            CreateRow("TotalTags", I18N.Get("UI.AssetManager.AssetInfo.TotalTags"));
            CreateRow("Modified", I18N.Get("UI.AssetManager.AssetInfo.Modified"));

            CreateLinkRow("Shop", I18N.Get("UI.AssetManager.AssetInfo.Shop"));
            CreateLinkRow("Item", I18N.Get("UI.AssetManager.AssetInfo.Item"));
        }

        public new void Clear() {
            foreach (var r in _rows.Values) r.Hide();
            foreach (var r in _linkRows.Values) r.Hide();
        }

        public void SetRow(string key, string value) {
            if (_rows.TryGetValue(key, out var row)) row.Show(value);
        }

        public void SetLinkRow(string key, string text, string url) {
            if (_linkRows.TryGetValue(key, out var row)) row.Show(text, url);
        }

        private void CreateRow(string key, string label) {
            _rows[key] = new InfoRow(_container, label);
        }

        private void CreateLinkRow(string key, string label) {
            _linkRows[key] = new LinkInfoRow(_container, label);
        }

        private class InfoRow {
            private readonly VisualElement _el;
            private readonly Label _val;

            public InfoRow(VisualElement parent, string label) {
                _el = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, marginBottom = 2,
                        display = DisplayStyle.None
                    }
                };
                _el.Add(new Label(label) { style = { color = ColorPreset.InActiveItem, width = 80 } });
                _val = new Label { style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleRight } };
                _el.Add(_val);
                parent.Add(_el);
            }

            public void Show(string v) {
                _val.text = v;
                _el.style.display = DisplayStyle.Flex;
            }

            public void Hide() {
                _el.style.display = DisplayStyle.None;
            }
        }

        private class LinkInfoRow {
            private readonly VisualElement _el;
            private readonly Label _val;
            private string _url;

            public LinkInfoRow(VisualElement parent, string label) {
                _el = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, marginBottom = 2,
                        display = DisplayStyle.None
                    }
                };
                _el.Add(new Label(label) { style = { color = ColorPreset.InActiveItem, width = 80 } });
                _val = new Label {
                    style = {
                        flexGrow = 1, unityTextAlign = TextAnchor.MiddleRight, color = ColorPreset.AccentBlueStyle,
                        overflow = Overflow.Hidden,
                        textOverflow = TextOverflow.Ellipsis
                    }
                };
                _val.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button == 0 && !string.IsNullOrEmpty(_url)) Application.OpenURL(_url);
                });
                _el.Add(_val);
                parent.Add(_el);
            }

            public void Show(string t, string u) {
                _val.text = t;
                _val.tooltip = t;
                _url = u;
                _el.style.display = DisplayStyle.Flex;
            }

            public void Hide() {
                _el.style.display = DisplayStyle.None;
            }
        }
    }
}