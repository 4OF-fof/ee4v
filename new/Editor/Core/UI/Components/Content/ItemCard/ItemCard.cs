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

        public ItemCardState(string itemId, string itemName, ItemImageState imageState, IconState iconState, string parentItemId)
        {
            ItemId = itemId ?? string.Empty;
            ItemName = itemName ?? string.Empty;
            ImageState = imageState ?? new ItemImageState();
            IconState = iconState;
            ParentItemId = parentItemId ?? string.Empty;
        }

        public string ItemId { get; }

        public string ItemName { get; }

        public ItemImageState ImageState { get; }

        public IconState IconState { get; }

        public string ParentItemId { get; }
    }

    internal class ItemCard : VisualElement
    {
        private const string RootClassName = "ee4v-ui-item-card";
        private const float DefaultWidth = 132f;
        private const float DefaultIconSize = 44f;
        private readonly VisualElement _imageFrame;
        private readonly ItemImage _thumbnail;
        private readonly Icon _icon;
        private readonly UiTextElement _nameLabel;

        public ItemCard(ItemCardState state = null)
        {
            AddToClassList(RootClassName);

            _imageFrame = new VisualElement();
            _imageFrame.AddToClassList("ee4v-ui-item-card__image-frame");

            _thumbnail = new ItemImage();
            _icon = new Icon();
            _icon.AddToClassList("ee4v-ui-item-card__icon");

            _nameLabel = UiTextFactory.Create(string.Empty, UiClassNames.ItemCardName);
            _nameLabel.SetWhiteSpace(WhiteSpace.NoWrap);

            _imageFrame.Add(_thumbnail);
            _imageFrame.Add(_icon);
            Add(_imageFrame);
            Add(_nameLabel);

            SetWidth(DefaultWidth);
            SetState(state ?? new ItemCardState(string.Empty));
        }

        public void SetState(ItemCardState state)
        {
            var nextState = state ?? new ItemCardState(string.Empty);
            _thumbnail.SetState(nextState.ImageState);
            var showIcon = nextState.IconState != null;
            _icon.style.display = showIcon ? DisplayStyle.Flex : DisplayStyle.None;
            if (showIcon)
            {
                _icon.SetState(nextState.IconState);
            }

            _nameLabel.SetText(nextState.ItemName);
        }

        public void SetWidth(float width)
        {
            var safeWidth = Mathf.Max(48f, width);
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
            _icon.style.left = (safeWidth - DefaultIconSize) * 0.5f;
            _icon.style.top = (safeWidth - DefaultIconSize) * 0.5f;

            _nameLabel.style.width = safeWidth;
            _nameLabel.style.minWidth = safeWidth;
            _nameLabel.style.maxWidth = safeWidth;
        }
    }
}
