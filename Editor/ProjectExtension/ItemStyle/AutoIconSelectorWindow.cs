using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.API;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.UI.Window;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.ProjectExtension.ItemStyle {
    public class AutoIconSelectorWindow : BaseWindow {
        private readonly List<(string ulid, Image img)> _loadingImages = new();
        private Dictionary<string, string> _candidates;
        private Action<string> _onSelected;

        public static void Open(string folderGuid, Vector2 screenPosition, Action<string> onSelected) {
            var candidates = AssetManagerAPI.GetAssetsAssociatedWithGuid(folderGuid);

            if (candidates is { Count: 1 }) {
                onSelected?.Invoke(candidates.Keys.First());
                return;
            }

            var window = OpenSetup<AutoIconSelectorWindow>(screenPosition);
            window._onSelected = onSelected;
            window._candidates = candidates;

            window.position = new Rect(screenPosition.x, screenPosition.y, 350, 400);

            window.ShowPopup();
            window.UpdateContent();
        }

        private void UpdateContent() {
            rootVisualElement.Clear();
            CreateGUI();
        }

        protected override VisualElement HeaderContent() {
            var root = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                    height = 24,
                    flexGrow = 1
                }
            };

            var label = new Label(I18N.Get("UI.ProjectExtension.SelectAssetIcon")) {
                style = {
                    fontSize = 14,
                    color = ColorPreset.TextColor
                }
            };
            root.Add(label);
            return root;
        }

        protected override VisualElement Content() {
            _loadingImages.Clear();
            var root = base.Content();

            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;

            if (_candidates == null || _candidates.Count == 0) {
                var noAssetLabel = new Label(I18N.Get("UI.ProjectExtension.NoAssociatedAssetsFound")) {
                    style = {
                        marginTop = 20,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        color = ColorPreset.NonFavorite
                    }
                };
                root.Add(noAssetLabel);
                return root;
            }

            var scroll = new ScrollView();
            var gridContainer = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    justifyContent = Justify.FlexStart
                }
            };
            scroll.Add(gridContainer);
            root.Add(scroll);

            foreach (var (ulid, text) in _candidates) {
                var card = new VisualElement {
                    style = {
                        width = 100,
                        height = 130,
                        marginBottom = 4,
                        marginRight = 4,
                        backgroundColor = Color.clear,
                        borderTopLeftRadius = 4, borderTopRightRadius = 4,
                        borderBottomLeftRadius = 4, borderBottomRightRadius = 4,
                        borderLeftWidth = 1, borderRightWidth = 1, borderTopWidth = 1, borderBottomWidth = 1,
                        borderLeftColor = Color.clear,
                        borderRightColor = Color.clear,
                        borderTopColor = Color.clear,
                        borderBottomColor = Color.clear,
                        alignItems = Align.Center,
                        paddingTop = 4, paddingBottom = 4, paddingLeft = 4, paddingRight = 4
                    }
                };

                var thumb = AssetManagerAPI.GetAssetThumbnail(ulid);
                var img = new Image {
                    scaleMode = ScaleMode.ScaleToFit,
                    style = {
                        width = 90,
                        height = 90,
                        backgroundColor = ColorPreset.WindowHeader,
                        marginBottom = 4,
                        borderTopLeftRadius = 4, borderTopRightRadius = 4,
                        borderBottomLeftRadius = 4, borderBottomRightRadius = 4
                    }
                };

                if (thumb != null)
                    img.image = thumb;
                else
                    _loadingImages.Add((ulid, img));

                card.Add(img);

                var nameLabel = new Label(text) {
                    style = {
                        fontSize = 10,
                        whiteSpace = WhiteSpace.NoWrap,
                        overflow = Overflow.Hidden,
                        textOverflow = TextOverflow.Ellipsis,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        width = 92,
                        color = ColorPreset.TextColor
                    }
                };
                card.Add(nameLabel);

                card.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    _onSelected?.Invoke(ulid);
                    Close();
                });

                card.RegisterCallback<MouseEnterEvent>(_ =>
                {
                    card.style.backgroundColor = ColorPreset.MouseOverBackground;
                });
                card.RegisterCallback<MouseLeaveEvent>(_ => { card.style.backgroundColor = Color.clear; });

                gridContainer.Add(card);
            }

            if (_loadingImages.Count > 0) root.schedule.Execute(UpdateLoadingImages).Every(100);

            return root;
        }

        private void UpdateLoadingImages() {
            if (_loadingImages.Count == 0) return;

            for (var i = _loadingImages.Count - 1; i >= 0; i--) {
                var (ulid, img) = _loadingImages[i];
                var tex = AssetManagerAPI.GetAssetThumbnail(ulid);
                if (tex == null) continue;
                img.image = tex;
                _loadingImages.RemoveAt(i);
            }
        }
    }
}