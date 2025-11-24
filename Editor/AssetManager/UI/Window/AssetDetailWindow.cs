using System;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.UI.Window;
using _4OF.ee4v.Core.Utility;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.UI.Window {
    public class AssetDetailWindow : BaseWindow {
        private AssetMetadata _asset;
        private ScrollView _scrollView;
        private VisualElement _contentContainer;

        public static void Open(Vector2 screenPosition, AssetMetadata asset) {
            var window = OpenSetup<AssetDetailWindow>(screenPosition, asset.ID);
            window._asset = asset;
            window.IsLocked = true;
            window.position = new Rect(window.position.x, window.position.y, 400, 600);
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

            AddSection("Basic Information");
            AddField("Name", _asset.Name ?? "N/A");
            AddField("ID", _asset.ID.ToString());
            AddField("Extension", _asset.Ext ?? "N/A");

            if (!string.IsNullOrWhiteSpace(_asset.Description)) {
                AddSpacer();
                AddSection("Description");
                AddTextField(_asset.Description);
            }

            AddSpacer();
            AddSection("File Information");
            AddField("Size", FormatFileSize(_asset.Size));
            AddField("Modified", FormatDate(_asset.ModificationTime));

            if (_asset.Tags is { Count: > 0 }) {
                AddSpacer();
                AddSection("Tags");
                AddTagsField(_asset.Tags);
            }

            if (_asset.BoothData == null || string.IsNullOrWhiteSpace(_asset.BoothData.ItemId)) return;
            AddSpacer();
            AddSection("Booth Information");
            AddField("Shop Domain", _asset.BoothData.ShopDomain);
            AddField("Item ID", _asset.BoothData.ItemId);
            if (!string.IsNullOrWhiteSpace(_asset.BoothData.ItemUrl)) {
                AddField("Item URL", _asset.BoothData.ItemUrl);
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

        private void AddField(string label, string value) {
            var container = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    marginBottom = 4
                }
            };

            var labelElement = new Label(label + ":") {
                style = {
                    width = 100,
                    color = new Color(0.7f, 0.7f, 0.7f, 1f),
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

            container.Add(labelElement);
            container.Add(valueElement);
            _contentContainer.Add(container);
        }

        private void AddTextField(string text) {
            var textField = new TextField {
                value = text,
                multiline = true,
                isReadOnly = true,
                style = {
                    flexGrow = 1,
                    minHeight = 60,
                    whiteSpace = WhiteSpace.Normal,
                    color = ColorPreset.TextColor
                }
            };
            _contentContainer.Add(textField);
        }

        private void AddTagsField(System.Collections.Generic.IReadOnlyList<string> tags) {
            var tagsContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    marginTop = 4
                }
            };

            foreach (var tag in tags) {
                var tagLabel = new Label(tag) {
                    style = {
                        backgroundColor = new Color(0.3f, 0.5f, 0.7f, 0.8f),
                        color = Color.white,
                        paddingTop = 4,
                        paddingBottom = 4,
                        paddingLeft = 8,
                        paddingRight = 8,
                        marginRight = 4,
                        marginBottom = 4,
                        borderTopLeftRadius = 3,
                        borderTopRightRadius = 3,
                        borderBottomLeftRadius = 3,
                        borderBottomRightRadius = 3
                    }
                };
                tagsContainer.Add(tagLabel);
            }

            _contentContainer.Add(tagsContainer);
        }

        private void AddSpacer() {
            _contentContainer.Add(Spacer(16));
        }

        private static string FormatFileSize(long bytes) {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F2} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F2} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
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
