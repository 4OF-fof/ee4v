using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.Service;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.UI.Window;
using _4OF.ee4v.Core.Utility;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window {
    public class AssetDetailWindow : BaseWindow {
        private AssetMetadata _asset;
        private VisualElement _contentContainer;
        private ScrollView _scrollView;
        private VisualElement _thumbnailBox;

        public static void Open(Vector2 screenPosition, AssetMetadata asset) {
            var window = OpenSetup<AssetDetailWindow>(screenPosition, asset.ID);
            window._asset = asset;
            window.IsLocked = true;
            window.position = new Rect(window.position.x, window.position.y, 520, 700);
            window.ShowPopup();
            window.Refresh();
        }

        protected override bool CanReuseFor(object reuseKey) {
            if (reuseKey is Ulid id && _asset != null)
                return _asset.ID == id;
            return false;
        }

        protected override VisualElement HeaderContent() {
            var root = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    height = 24,
                    flexGrow = 1
                }
            };

            var titleLabel = new Label("Asset Details") {
                style = {
                    flexGrow = 1,
                    marginLeft = 8,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = ColorPreset.TextColor
                }
            };

            root.Add(titleLabel);
            return root;
        }

        protected override VisualElement Content() {
            var root = base.Content();

            _scrollView = new ScrollView {
                style = {
                    flexGrow = 1
                }
            };

            _contentContainer = new VisualElement {
                style = {
                    paddingTop = 8,
                    paddingBottom = 8,
                    paddingLeft = 8,
                    paddingRight = 8
                }
            };

            _scrollView.Add(_contentContainer);
            root.Add(_scrollView);

            return root;
        }

        private void Refresh() {
            if (_asset == null || _contentContainer == null) return;

            _contentContainer.Clear();

            var topRow = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 8
                }
            };

            _thumbnailBox = new VisualElement {
                style = {
                    width = 208,
                    height = 208,
                    marginRight = 12,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    overflow = Overflow.Hidden
                }
            };
            topRow.Add(_thumbnailBox);

            var infoPanel = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Column,
                    flexGrow = 1
                }
            };

            var nameLabel = new Label(_asset.Name ?? "Untitled") {
                style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 16, marginBottom = 10,color = ColorPreset.TextColor }
            };
            infoPanel.Add(nameLabel);
            try {
                var repo = AssetManagerContainer.Repository;
                var folder = repo?.GetLibraryMetadata()?.GetFolder(_asset.Folder);
                var folderName = folder?.Name;

                if (!string.IsNullOrWhiteSpace(folderName))
                    AddInfoRowTo(infoPanel, "Folder", folderName);
            }
            catch {
                // ignore
            }

            if (_asset.Tags is { Count: > 0 }) {
                var tagsElement = BuildTagsElement(_asset.Tags);
                AddInfoRowTo(infoPanel, "Tags", tagsElement);
            }

            AddInfoRowTo(infoPanel, "Extension", _asset.Ext ?? "N/A");

            if (_asset.BoothData != null && !string.IsNullOrWhiteSpace(_asset.BoothData.ShopUrl)) {
                var linkRow = new VisualElement {
                    style = { flexDirection = FlexDirection.Row, marginBottom = 2, alignItems = Align.Center }
                };
                linkRow.Add(new Label("Shop URL:") { style = { width = 80, color = Color.gray } });
                var linkLabel = new Label(_asset.BoothData.ShopUrl) {
                    style = {
                        flexGrow = 1, color = new StyleColor(new Color(0.4f, 0.6f, 1.0f)),
                        unityTextAlign = TextAnchor.MiddleLeft, whiteSpace = WhiteSpace.NoWrap
                    }
                };
                linkLabel.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button == 0) Application.OpenURL(_asset.BoothData.ShopUrl);
                });
                linkRow.Add(linkLabel);
                infoPanel.Add(linkRow);
            }

            if (_asset.BoothData != null && !string.IsNullOrWhiteSpace(_asset.BoothData.ItemUrl)) {
                var linkRow = new VisualElement {
                    style = { flexDirection = FlexDirection.Row, marginBottom = 2, alignItems = Align.Center }
                };
                linkRow.Add(new Label("Item URL:") { style = { width = 80, color = Color.gray } });
                var linkLabel = new Label(_asset.BoothData.ItemUrl) {
                    style = {
                        flexGrow = 1, color = new StyleColor(new Color(0.4f, 0.6f, 1.0f)),
                        unityTextAlign = TextAnchor.MiddleLeft, whiteSpace = WhiteSpace.NoWrap
                    }
                };
                linkLabel.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button == 0) Application.OpenURL(_asset.BoothData.ItemUrl);
                });
                linkRow.Add(linkLabel);
                infoPanel.Add(linkRow);
            }

            AddInfoRowTo(infoPanel, "Size", FormatFileSize(_asset.Size));
            AddInfoRowTo(infoPanel, "Modified", FormatDate(_asset.ModificationTime));

            if (_asset.UnityData?.DependenceItemList is { Count: > 0 }) {
                var depsElem = BuildDependenciesElement(_asset.UnityData.DependenceItemList);
                AddInfoRowTo(infoPanel, "Dependencies", depsElem);
            }

            topRow.Add(infoPanel);
            _contentContainer.Add(topRow);

            _ = LoadThumbnailAsync(_asset.ID);

            AddSpacer();
            AddSection("Description");
            var descText = string.IsNullOrWhiteSpace(_asset.Description)
                ? "(No description provided)"
                : _asset.Description;
            var descLabel = new Label(descText) {
                style = {
                    marginTop = 2, whiteSpace = WhiteSpace.Normal,
                    color = string.IsNullOrWhiteSpace(_asset.Description)
                        ? new Color(0.7f, 0.7f, 0.7f, 1f)
                        : ColorPreset.TextColor,
                    unityFontStyleAndWeight = string.IsNullOrWhiteSpace(_asset.Description)
                        ? FontStyle.Italic
                        : FontStyle.Normal
                }
            };
            _contentContainer.Add(descLabel);
        }

        private static void AddInfoRowTo(VisualElement container, string label, string value) {
            var c = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    marginBottom = 4,
                    alignItems = Align.Center
                }
            };

            var labelElement = new Label(label + ":") {
                style = {
                    width = 80,
                    color = Color.gray,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };

            var valueElement = new Label(value) {
                style = {
                    flexGrow = 1,
                    color = ColorPreset.TextColor,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    whiteSpace = WhiteSpace.Normal
                }
            };

            c.Add(labelElement);
            c.Add(valueElement);
            container.Add(c);
        }

        private static void AddInfoRowTo(VisualElement container, string label, VisualElement valueElement) {
            var c = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    marginBottom = 4,
                    alignItems = Align.Center
                }
            };

            var labelElement = new Label(label + ":") {
                style = {
                    width = 80,
                    color = Color.gray,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };

            valueElement.style.flexGrow = 1;

            c.Add(labelElement);
            c.Add(valueElement);
            container.Add(c);
        }

        private async Task LoadThumbnailAsync(Ulid id) {
            try {
                var tex = AssetManagerContainer.TextureService?.GetAssetThumbnailAsync(id);
                if (tex == null) {
                    var fallback = TextureService.GetDefaultFallback(false);
                    _thumbnailBox.style.backgroundImage = new StyleBackground(fallback);
                    var ph = _thumbnailBox?.Q<Label>("__thumb_placeholder");
                    if (ph != null) ph.style.display = DisplayStyle.None;
                    return;
                }

                var texture = await tex;
                if (_thumbnailBox != null) {
                    _thumbnailBox.style.backgroundImage = texture != null
                        ? new StyleBackground(texture)
                        : new StyleBackground(TextureService.GetDefaultFallback(false));
                    var ph = _thumbnailBox?.Q<Label>("__thumb_placeholder");
                    if (ph != null) ph.style.display = DisplayStyle.None;
                }
            }
            catch {
                // ignore
            }
        }

        private void AddSection(string sectionTitle) {
            var label = new Label(sectionTitle) {
                style = {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 14,
                    marginTop = 4,
                    marginBottom = 8,
                    color = ColorPreset.TextColor
                }
            };
            _contentContainer.Add(label);
        }


        private static VisualElement BuildTagsElement(IReadOnlyList<string> tags) {
            var scroll = new ScrollView(ScrollViewMode.Horizontal) {
                style = { flexGrow = 1, height = 28, marginTop = 2 },
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Hidden
            };

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            if (tags == null || tags.Count == 0)
                row.Add(new Label("(no tags)") { style = { color = new Color(0.7f, 0.7f, 0.7f, 1f) } });

            foreach (var tag in tags ?? Array.Empty<string>()) {
                var tagLabel = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Row,
                        backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f)),
                        borderTopLeftRadius = 10,
                        borderTopRightRadius = 10,
                        borderBottomLeftRadius = 10,
                        borderBottomRightRadius = 10,
                        paddingLeft = 8, paddingRight = 4, paddingTop = 2, paddingBottom = 2,
                        marginRight = 4,
                        alignItems = Align.Center
                    }
                };

                var tagText = new Label(tag) { style = { marginRight = 4, color = Color.white } };
                tagLabel.Add(tagText);
                row.Add(tagLabel);
            }

            scroll.Add(row);
            return scroll;
        }

        private VisualElement BuildDependenciesElement(IReadOnlyList<Ulid> deps) {
            var depsContainer = new VisualElement { style = { flexDirection = FlexDirection.Column, marginTop = 4 } };

            if (deps == null || deps.Count == 0) {
                depsContainer.Add(new Label("(no dependencies)")
                    { style = { color = new Color(0.7f, 0.7f, 0.7f, 1f), marginTop = 2 } });
                return depsContainer;
            }

            foreach (var dep in deps) {
                var repo = AssetManagerContainer.Repository;
                var linked = repo?.GetAsset(dep);
                var text = linked != null ? linked.Name : "(missing asset)";

                var label = new Label(text) {
                    style = { color = ColorPreset.TextColor, unityTextAlign = TextAnchor.MiddleLeft, marginBottom = 4 }
                };

                if (linked != null) {
                    label.RegisterCallback<PointerDownEvent>(evt =>
                    {
                        if (evt.button != 0) return;
                        try {
                            Open(new Vector2(position.x + position.width + 20, position.y + 20), linked);
                        }
                        catch {
                            // ignored
                        }

                        evt.StopPropagation();
                    });

                    label.RegisterCallback<MouseEnterEvent>(_ =>
                    {
                        label.style.color = new StyleColor(new Color(0.6f, 0.8f, 1.0f));
                    });
                    label.RegisterCallback<MouseLeaveEvent>(_ =>
                    {
                        label.style.color = new StyleColor(ColorPreset.TextColor);
                    });
                }

                depsContainer.Add(label);
            }

            return depsContainer;
        }

        private void AddSpacer() {
            _contentContainer.Add(Spacer(16));
        }

        private static string FormatFileSize(long bytes) {
            return bytes switch {
                < 1024               => $"{bytes} B",
                < 1024 * 1024        => $"{bytes / 1024.0:F2} KB",
                < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F2} MB",
                _                    => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
            };
        }

        private static string FormatDate(long timestamp) {
            if (timestamp == 0) return "N/A";
            try {
                var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime;
                return date.ToString("yyyy/MM/dd HH:mm:ss");
            }
            catch {
                return "Invalid Date";
            }
        }
    }
}