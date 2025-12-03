using System;
using System.Collections.Generic;
using System.Linq;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.AssetManager.Services;
using _4OF.ee4v.Core.UI;
using _4OF.ee4v.Core.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Views.Components.AssetGrid {
    public class AssetCard : VisualElement {
        private readonly VisualElement _innerContainer;
        private readonly Label _nameLabel;
        private readonly VisualElement _thumbnail;
        private Texture2D _currentTexture;
        private bool _isSelected;

        public AssetCard() {
            style.paddingLeft = 5;
            style.paddingRight = 5;
            style.paddingTop = 5;
            style.paddingBottom = 5;

            _innerContainer = new VisualElement {
                style = {
                    borderTopLeftRadius = 5,
                    borderTopRightRadius = 5,
                    borderBottomLeftRadius = 5,
                    borderBottomRightRadius = 5,
                    overflow = Overflow.Hidden,
                    flexGrow = 1,
                    flexDirection = FlexDirection.Column,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = new StyleColor(Color.clear),
                    borderBottomColor = new StyleColor(Color.clear),
                    borderLeftColor = new StyleColor(Color.clear),
                    borderRightColor = new StyleColor(Color.clear)
                }
            };
            Add(_innerContainer);

            _thumbnail = new VisualElement {
                style = {
                    flexGrow = 1,
                    width = Length.Percent(100),
                    backgroundColor = new StyleColor(ColorPreset.InActiveItem),
                    backgroundSize = new BackgroundSize(BackgroundSizeType.Contain),
                    backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat),
                    backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center),
                    backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center)
                }
            };
            _innerContainer.Add(_thumbnail);

            _nameLabel = new Label {
                style = {
                    whiteSpace = WhiteSpace.Normal,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    paddingTop = 2,
                    paddingBottom = 2,
                    paddingLeft = 2,
                    paddingRight = 2,
                    height = 38,
                    flexShrink = 0,
                    fontSize = 11,
                    color = new StyleColor(ColorPreset.TextColor)
                }
            };
            _innerContainer.Add(_nameLabel);
        }

        public void SetData(string itemName) {
            _nameLabel.text = itemName;
            tooltip = itemName;
        }

        public void SetThumbnail(Texture2D texture, bool isFolder = false, bool isEmpty = false) {
            if (texture != null && _currentTexture == texture) return;
            _currentTexture = texture;

            if (texture == null) {
                var fallback = TextureService.GetDefaultFallback(isFolder, isEmpty);

                if (fallback != null) {
                    _thumbnail.style.backgroundImage = new StyleBackground(fallback);
                    _thumbnail.style.backgroundColor = new StyleColor(Color.clear);
                }
                else {
                    _thumbnail.style.backgroundImage = null;
                    _thumbnail.style.backgroundColor = new StyleColor(ColorPreset.InActiveItem);
                }

                return;
            }

            _thumbnail.style.backgroundImage = new StyleBackground(texture);
            _thumbnail.style.backgroundColor = new StyleColor(Color.clear);
        }

        public void SetSelected(bool selected) {
            _isSelected = selected;
            if (selected) {
                _innerContainer.style.backgroundColor = ColorPreset.ItemSelectedBackGround;
                var borderColor = ColorPreset.ItemSelectedBorder;
                _innerContainer.style.borderTopColor = borderColor;
                _innerContainer.style.borderBottomColor = borderColor;
                _innerContainer.style.borderLeftColor = borderColor;
                _innerContainer.style.borderRightColor = borderColor;
            }
            else {
                _innerContainer.style.backgroundColor = Color.clear;
                _innerContainer.style.borderTopColor = Color.clear;
                _innerContainer.style.borderBottomColor = Color.clear;
                _innerContainer.style.borderLeftColor = Color.clear;
                _innerContainer.style.borderRightColor = Color.clear;
            }
        }

        public void EnableDropZone() {
            RegisterCallback<DragEnterEvent>(OnDragEnter, TrickleDown.TrickleDown);
            RegisterCallback<DragLeaveEvent>(OnDragLeave, TrickleDown.TrickleDown);
            RegisterCallback<DragUpdatedEvent>(OnDragUpdated, TrickleDown.TrickleDown);
            RegisterCallback<DragPerformEvent>(OnDragPerform, TrickleDown.TrickleDown);
        }

        public void DisableDropZone() {
            UnregisterCallback<DragEnterEvent>(OnDragEnter, TrickleDown.TrickleDown);
            UnregisterCallback<DragLeaveEvent>(OnDragLeave, TrickleDown.TrickleDown);
            UnregisterCallback<DragUpdatedEvent>(OnDragUpdated, TrickleDown.TrickleDown);
            UnregisterCallback<DragPerformEvent>(OnDragPerform, TrickleDown.TrickleDown);
        }

        private void OnDragEnter(DragEnterEvent evt) {
            if (userData is not BaseFolder) return;
            if (!CanAcceptDrop()) return;

            if (userData is BoothItemFolder && HasFoldersToDrop()) {
                evt.StopImmediatePropagation();
                return;
            }

            _innerContainer.style.backgroundColor = ColorPreset.DropFolderArea;
            evt.StopImmediatePropagation();
        }

        private void OnDragLeave(DragLeaveEvent evt) {
            _innerContainer.style.backgroundColor = _isSelected ? ColorPreset.ItemSelectedBackGround : Color.clear;
            evt.StopImmediatePropagation();
        }

        private void OnDragUpdated(DragUpdatedEvent evt) {
            if (userData is not BaseFolder) return;
            if (!CanAcceptDrop()) return;

            if (userData is BoothItemFolder && HasFoldersToDrop())
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
            else
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;

            evt.StopImmediatePropagation();
        }

        private void OnDragPerform(DragPerformEvent evt) {
            if (userData is not BaseFolder folder) return;
            if (!CanAcceptDrop()) return;

            if (userData is BoothItemFolder && HasFoldersToDrop()) {
                evt.StopImmediatePropagation();
                return;
            }

            DragAndDrop.AcceptDrag();
            _innerContainer.style.backgroundColor = _isSelected ? ColorPreset.ItemSelectedBackGround : Color.clear;

            var assetIds = DragAndDrop.GetGenericData("AssetManagerAssets") as string[];
            var folderIds = DragAndDrop.GetGenericData("AssetManagerFolders") as string[];

            var assetUlidList = assetIds?.Select(Ulid.Parse).ToList() ?? new List<Ulid>();
            var folderUlidList = folderIds?.Select(Ulid.Parse).ToList() ?? new List<Ulid>();

            OnDropped?.Invoke(folder.ID, assetUlidList, folderUlidList);

            evt.StopImmediatePropagation();
        }

        private static bool CanAcceptDrop() {
            var hasAssets = DragAndDrop.GetGenericData("AssetManagerAssets") != null;
            var hasFolders = DragAndDrop.GetGenericData("AssetManagerFolders") != null;
            return hasAssets || hasFolders;
        }

        private static bool HasFoldersToDrop() {
            return DragAndDrop.GetGenericData("AssetManagerFolders") != null;
        }

        public event Action<Ulid, List<Ulid>, List<Ulid>> OnDropped;
    }
}