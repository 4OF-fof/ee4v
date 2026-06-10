using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class ImageStack : VisualElement
    {
        private const string RootClassName = "ee4v-ui-image-stack";
        private const string ImageClassName = "ee4v-ui-image-stack__image";
        private const int MaxImageCount = 3;
        private const int BaseSlot = 1;
        private static readonly int[] SlotOrder = { 0, BaseSlot, 2 };
        private static readonly float[] SlotRotations = { -4.5f, 0.8f, 4.2f };
        private readonly ItemImage[] _images;
        private float _size;
        private int _visibleCount;

        public ImageStack()
        {
            AddToClassList(RootClassName);
            _images = new ItemImage[MaxImageCount];
            for (var i = 0; i < _images.Length; i++)
            {
                var image = new ItemImage();
                image.AddToClassList(ImageClassName);
                image.style.display = DisplayStyle.None;
                _images[i] = image;
                Add(image);
            }
        }

        public void SetStates(IReadOnlyList<ItemImageState> states)
        {
            Clear();
            if (states == null || states.Count == 0)
            {
                return;
            }

            var count = UnityEngine.Mathf.Min(MaxImageCount, states.Count);
            _visibleCount = count;
            for (var i = 0; i < count; i++)
            {
                var slot = SlotOrder[i];
                var image = _images[slot];
                image.style.display = DisplayStyle.Flex;
                image.SetState(states[i]);
            }

            UpdateImageLayout();
        }

        public void Clear()
        {
            for (var i = 0; i < _images.Length; i++)
            {
                _images[i].style.display = DisplayStyle.None;
                _images[i].SetState(new ItemImageState());
            }

            _visibleCount = 0;
        }

        public void SetSize(float size)
        {
            _size = UnityEngine.Mathf.Max(48f, size);
            style.width = _size;
            style.height = _size;
            style.minWidth = _size;
            style.minHeight = _size;
            style.maxWidth = _size;
            style.maxHeight = _size;
            UpdateImageLayout();
        }

        private void UpdateImageLayout()
        {
            if (_size <= 0f)
            {
                return;
            }

            var offset = UnityEngine.Mathf.Clamp(_size * 0.065f, 6f, 18f);
            var imageSize = UnityEngine.Mathf.Max(48f, _size - (offset * 4f));
            var centerLeft = (_size - imageSize) * 0.5f;
            var centerTop = (_size - imageSize) * 0.5f;

            if (_visibleCount == 1)
            {
                ApplySlotLayout(0, _size, 0f, 0f, 0f);
                ApplySlotLayout(BaseSlot, _size, 0f, 0f, 0f);
                ApplySlotLayout(2, _size, 0f, 0f, 0f);
                return;
            }

            ApplySlotLayout(0, imageSize, centerLeft - (offset * 0.85f), centerTop - (offset * 0.55f));
            ApplySlotLayout(BaseSlot, imageSize, centerLeft, centerTop);
            ApplySlotLayout(2, imageSize, centerLeft + (offset * 0.85f), centerTop + (offset * 0.85f));
        }

        private void ApplySlotLayout(int slot, float size, float left, float top)
        {
            ApplySlotLayout(slot, size, left, top, SlotRotations[slot]);
        }

        private void ApplySlotLayout(int slot, float size, float left, float top, float rotation)
        {
            var image = _images[slot];
            image.SetSize(size);
            image.style.left = left;
            image.style.top = top;
            image.style.rotate = new Rotate(new Angle(rotation, AngleUnit.Degree));
        }
    }
}
