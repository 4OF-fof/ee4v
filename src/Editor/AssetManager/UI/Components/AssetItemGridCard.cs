using System;
using Ee4v.Core.Images;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetItemGridCard : VisualElement, IDisposable
    {
        private readonly PreviewContainer _imageFrame;
        private readonly CachedImage _image;
        private readonly VisualElement _placeholder;
        private readonly UiTextElement _name;
        private string _itemId = string.Empty;

        public AssetItemGridCard(CachedImageCache imageCache)
        {
            AddToClassList("ee4v-asset-grid-card");
            focusable = true;

            _imageFrame = new PreviewContainer();
            _imageFrame.AddToClassList("ee4v-asset-grid-card__image-frame");
            _image = new CachedImage(imageCache)
            {
                scaleMode = ScaleMode.ScaleAndCrop,
                pickingMode = PickingMode.Ignore
            };
            _image.AddToClassList("ee4v-asset-grid-card__image");
            _placeholder = new VisualElement();
            _placeholder.AddToClassList(
                "ee4v-asset-grid-card__placeholder");
            _placeholder.pickingMode = PickingMode.Ignore;
            _imageFrame.Placeholder.Add(_placeholder);
            _imageFrame.Content.Add(_image);

            _name = UiTextFactory.Create(
                string.Empty,
                "ee4v-asset-grid-card__name");
            _name.SetWhiteSpace(WhiteSpace.NoWrap);
            _name.SetTextAlign(TextAnchor.MiddleCenter);

            Add(_imageFrame);
            Add(_name);

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<ContextClickEvent>(OnContextClick);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        public event Action<string, bool, bool> Clicked;
        public event Action<string> DoubleClicked;
        public event Action<string, VisualElement> ContextClicked;

        public void SetState(AssetItemGridEntry state, bool selected)
        {
            _itemId = state.Id;
            _name.SetText(state.Name);
            EnableInClassList(
                "ee4v-asset-grid-card--selected",
                selected);
            _image.SetSource(state.Id);
            UpdateImageVisibility();
        }

        public void SetWidth(float width)
        {
            style.width = width;
            style.minWidth = width;
            style.maxWidth = width;
            _imageFrame.style.width = width;
            _imageFrame.style.height = width;
            _imageFrame.style.minWidth = width;
            _imageFrame.style.minHeight = width;
            _imageFrame.style.maxWidth = width;
            _imageFrame.style.maxHeight = width;
        }

        public void SetPlaceholderIcon(IconState state)
        {
            _placeholder.Clear();
            _placeholder.EnableInClassList(
                "ee4v-asset-grid-card__placeholder--icon",
                state != null);
            if (state != null)
            {
                _placeholder.Add(new Icon(state));
            }
        }

        public void Dispose()
        {
            _image.ClearSource();
            UpdateImageVisibility();
        }

        private void UpdateImageVisibility()
        {
            _imageFrame.SetHasContent(_image.DisplayedTexture != null);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != (int)MouseButton.LeftMouse)
            {
                return;
            }

            Focus();
            Clicked?.Invoke(
                _itemId,
                evt.ctrlKey || evt.commandKey,
                evt.shiftKey);
            if (evt.clickCount == 2)
            {
                DoubleClicked?.Invoke(_itemId);
            }
            evt.StopPropagation();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Return &&
                evt.keyCode != KeyCode.Space)
            {
                return;
            }

            Clicked?.Invoke(
                _itemId,
                evt.ctrlKey || evt.commandKey,
                evt.shiftKey);
            evt.StopPropagation();
        }

        private void OnContextClick(ContextClickEvent evt)
        {
            Focus();
            ContextClicked?.Invoke(_itemId, this);
            evt.StopPropagation();
        }
    }
}
