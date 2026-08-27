using System;
using System.Collections.Generic;
using Ee4v.Core.Images;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetThumbnailStack : VisualElement, IDisposable
    {
        private const int MaximumThumbnailCount = 3;
        private const float MinimumSize = 48f;
        private const float MaximumSize = 288f;
        private const float MultiImageInsetMultiplier = 2f;
        private static readonly float[] SlotLeftOffsetMultipliers =
            { -0.85f, 0f, 0.85f };
        private static readonly float[] SlotTopOffsetMultipliers =
            { -0.55f, 0f, 0.85f };
        private static readonly float[] SlotRotations =
            { -4.5f, 0.8f, 4.2f };
        private readonly List<ThumbnailSlot> _slots =
            new List<ThumbnailSlot>();

        public AssetThumbnailStack(
            CachedImageCache imageCache,
            IReadOnlyList<string> itemIds)
        {
            if (imageCache == null)
            {
                throw new ArgumentNullException(nameof(imageCache));
            }

            AddToClassList("ee4v-asset-manager__thumbnail-stack");
            var safeIds = itemIds ?? Array.Empty<string>();
            var firstIndex = Math.Max(
                0,
                safeIds.Count - MaximumThumbnailCount);
            for (var index = firstIndex; index < safeIds.Count; index++)
            {
                var slot = new ThumbnailSlot(
                    imageCache,
                    safeIds[index]);
                _slots.Add(slot);
                Add(slot);
            }

            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        public void Refresh(string itemId)
        {
            for (var index = 0; index < _slots.Count; index++)
            {
                if (string.Equals(
                        _slots[index].ItemId,
                        itemId,
                        StringComparison.Ordinal))
                {
                    _slots[index].Refresh();
                }
            }
        }

        public void Dispose()
        {
            UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            for (var index = 0; index < _slots.Count; index++)
            {
                _slots[index].Dispose();
            }
            _slots.Clear();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            var size = Mathf.Clamp(
                evt.newRect.width,
                MinimumSize,
                MaximumSize);
            if (float.IsNaN(size) || size <= 0f)
            {
                return;
            }

            style.height = size;
            style.minHeight = size;
            style.maxHeight = size;
            if (_slots.Count == 1)
            {
                ApplySlotLayout(0, size, 0f, 0f, 0f);
                return;
            }

            var offset = Mathf.Clamp(size * 0.065f, 6f, 18f);
            var imageSize = Mathf.Max(
                MinimumSize,
                size - (offset * MultiImageInsetMultiplier));
            var centerOffset = (size - imageSize) * 0.5f;
            for (var index = 0; index < _slots.Count; index++)
            {
                ApplySlotLayout(
                    index,
                    imageSize,
                    centerOffset +
                    (offset * SlotLeftOffsetMultipliers[index]),
                    centerOffset +
                    (offset * SlotTopOffsetMultipliers[index]),
                    SlotRotations[index]);
            }
        }

        private void ApplySlotLayout(
            int index,
            float size,
            float left,
            float top,
            float rotation)
        {
            var slot = _slots[index];
            slot.style.width = size;
            slot.style.height = size;
            slot.style.left = left;
            slot.style.top = top;
            slot.style.rotate = new Rotate(new Angle(
                rotation,
                AngleUnit.Degree));
        }

        private sealed class ThumbnailSlot : PreviewContainer, IDisposable
        {
            private readonly CachedImage _image;

            public ThumbnailSlot(
                CachedImageCache imageCache,
                string itemId)
            {
                ItemId = itemId ?? string.Empty;
                AddToClassList(
                    "ee4v-asset-manager__thumbnail-stack-image");
                var placeholder = new VisualElement();
                placeholder.AddToClassList(
                    "ee4v-asset-manager__thumbnail-placeholder");
                _image = new CachedImage(imageCache)
                {
                    scaleMode = ScaleMode.ScaleAndCrop
                };
                _image.AddToClassList(
                    "ee4v-asset-manager__thumbnail-image");
                Placeholder.Add(placeholder);
                Content.Add(_image);
                Refresh();
            }

            public string ItemId { get; }

            public void Refresh()
            {
                _image.SetSource(ItemId);
                SetHasContent(
                    _image.DisplayedTexture != null);
            }

            public void Dispose()
            {
                _image.Dispose();
            }
        }
    }
}
