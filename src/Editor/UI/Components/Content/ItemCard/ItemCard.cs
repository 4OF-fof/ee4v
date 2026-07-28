using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class ItemCardState
    {
        public ItemCardState(string itemName, byte[] thumbnailData = null)
            : this(string.Empty, itemName, new ItemImageState(thumbnailData))
        {
        }

        public ItemCardState(string itemName, ItemImageState imageState)
            : this(string.Empty, itemName, imageState)
        {
        }

        public ItemCardState(string itemId, string itemName, ItemImageState imageState)
            : this(itemId, itemName, imageState, null)
        {
        }

        public ItemCardState(string itemId, string itemName, ItemImageState imageState, IconState iconState)
            : this(itemId, itemName, imageState, iconState, string.Empty)
        {
        }

        public ItemCardState(
            string itemId,
            string itemName,
            ItemImageState imageState,
            IconState iconState,
            string parentItemId,
            IReadOnlyList<ItemCardState> stackStates = null,
            IconState nameIconState = null)
        {
            ItemId = itemId ?? string.Empty;
            ItemName = itemName ?? string.Empty;
            ImageState = imageState ?? new ItemImageState();
            IconState = iconState;
            ParentItemId = parentItemId ?? string.Empty;
            StackStates = stackStates ??
                Array.Empty<ItemCardState>();
            NameIconState = nameIconState;
        }

        public string ItemId { get; }

        public string ItemName { get; }

        public ItemImageState ImageState { get; }

        public IconState IconState { get; }

        public string ParentItemId { get; }

        public IReadOnlyList<ItemCardState> StackStates { get; }

        public IconState NameIconState { get; }
    }

    internal class ItemCard : VisualElement
    {
        internal const float PreferredMinimumWidth = 48f;
        private const float AbsoluteMinimumWidth = 1f;
        private const string RootClassName = "ee4v-ui-item-card";
        private const float DefaultWidth = 132f;
        private const float DefaultIconSize = 44f;
        private const float NameIconSize = 14f;
        private readonly VisualElement _imageFrame;
        private readonly ItemImage _thumbnail;
        private readonly ImageStack _imageStack;
        private readonly Icon _icon;
        private readonly VisualElement _nameRow;
        private readonly Icon _nameIcon;
        private readonly UiTextElement _nameLabel;
        private float _width = DefaultWidth;
        private bool _showNameIcon;

        public ItemCard(ItemCardState state = null)
        {
            AddToClassList(RootClassName);

            _imageFrame = new VisualElement();
            _imageFrame.AddToClassList("ee4v-ui-item-card__image-frame");

            _thumbnail = new ItemImage();
            _imageStack = new ImageStack();
            _imageStack.AddToClassList(
                "ee4v-ui-item-card__image-stack");
            _icon = new Icon();
            _icon.AddToClassList("ee4v-ui-item-card__icon");

            _nameRow = new VisualElement();
            _nameRow.AddToClassList(
                "ee4v-ui-item-card__name-row");
            _nameIcon = new Icon();
            _nameIcon.AddToClassList(
                "ee4v-ui-item-card__name-icon");
            _nameLabel = UiTextFactory.Create(string.Empty, UiClassNames.ItemCardName);
            _nameLabel.SetWhiteSpace(WhiteSpace.NoWrap);

            _imageFrame.Add(_thumbnail);
            _imageFrame.Add(_imageStack);
            _imageFrame.Add(_icon);
            _nameRow.Add(_nameIcon);
            _nameRow.Add(_nameLabel);
            Add(_imageFrame);
            Add(_nameRow);

            SetWidth(DefaultWidth);
            SetState(state ?? new ItemCardState(string.Empty));
        }

        public void SetState(ItemCardState state)
        {
            var nextState = state ?? new ItemCardState(string.Empty);
            _thumbnail.SetState(nextState.ImageState);
            var showStack =
                nextState.StackStates.Count > 0;
            var hasImage =
                nextState.ImageState.CacheKey.Length > 0;
            _thumbnail.style.display =
                showStack ||
                (nextState.IconState != null &&
                 !hasImage)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            _imageStack.style.display =
                showStack
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            if (showStack)
            {
                _imageStack.SetItemStates(
                    nextState.StackStates);
                ApplyImageStackSize();
            }
            else
            {
                _imageStack.ClearImages();
            }

            var showIcon =
                !showStack &&
                nextState.IconState != null;
            _icon.style.display = showIcon ? DisplayStyle.Flex : DisplayStyle.None;
            if (showIcon)
            {
                _icon.SetState(nextState.IconState);
                ApplyIconSize();
            }

            _showNameIcon =
                nextState.NameIconState != null;
            _nameIcon.style.display =
                _showNameIcon
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            if (_showNameIcon)
            {
                _nameIcon.SetState(
                    nextState.NameIconState);
                _nameIcon.SetSize(NameIconSize);
            }
            ApplyNameLayout();

            _nameLabel.SetText(nextState.ItemName);
        }

        public void SetWidth(float width)
        {
            var safeWidth = Mathf.Max(AbsoluteMinimumWidth, width);
            _width = safeWidth;
            style.width = safeWidth;
            style.minWidth = safeWidth;
            style.maxWidth = safeWidth;

            _imageFrame.style.width = safeWidth;
            _imageFrame.style.height = safeWidth;
            _imageFrame.style.minWidth = safeWidth;
            _imageFrame.style.minHeight = safeWidth;
            _imageFrame.style.maxWidth = safeWidth;
            _imageFrame.style.maxHeight = safeWidth;

            _thumbnail.SetSize(safeWidth);
            ApplyImageStackSize();
            ApplyIconSize();

            _nameRow.style.width = safeWidth;
            _nameRow.style.minWidth = safeWidth;
            _nameRow.style.maxWidth = safeWidth;
            _nameLabel.style.width = StyleKeyword.Auto;
            _nameLabel.style.minWidth = 0f;
            _nameLabel.style.flexGrow = 0f;
            _nameLabel.style.flexShrink = 1f;
            ApplyNameLayout();
        }

        private void ApplyIconSize()
        {
            var iconSize = Mathf.Min(DefaultIconSize, _width);
            _icon.SetSize(iconSize);
            _icon.style.left = (_width - iconSize) * 0.5f;
            _icon.style.top = (_width - iconSize) * 0.5f;
        }

        private void ApplyImageStackSize()
        {
            var stackSize = Mathf.Max(
                PreferredMinimumWidth,
                _width * 0.84f);
            _imageStack.SetSize(stackSize);
            _imageStack.style.left =
                (_width - stackSize) * 0.5f;
            _imageStack.style.top =
                (_width - stackSize) * 0.5f;
        }

        private void ApplyNameLayout()
        {
            var reservedIconWidth = _showNameIcon
                ? NameIconSize + UiSpacingTokens.Xs
                : 0f;
            _nameLabel.style.maxWidth =
                Mathf.Max(0f, _width - reservedIconWidth);
        }

    }
}
