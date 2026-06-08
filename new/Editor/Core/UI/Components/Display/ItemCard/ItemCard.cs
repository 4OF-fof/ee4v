using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class ItemCardState
    {
        public ItemCardState(string itemName, byte[] thumbnailData = null)
            : this(itemName, new ItemImageState(thumbnailData))
        {
        }

        public ItemCardState(string itemName, ItemImageState imageState)
        {
            ItemName = itemName ?? string.Empty;
            ImageState = imageState ?? new ItemImageState();
        }

        public string ItemName { get; }

        public ItemImageState ImageState { get; }
    }

    internal class ItemCard : VisualElement
    {
        private const string RootClassName = "ee4v-ui-item-card";
        private const float DefaultWidth = 132f;
        private readonly ItemImage _thumbnail;
        private readonly UiTextElement _nameLabel;

        public ItemCard(ItemCardState state = null)
        {
            AddToClassList(RootClassName);

            _thumbnail = new ItemImage();

            _nameLabel = UiTextFactory.Create(string.Empty, UiClassNames.ItemCardName);
            _nameLabel.SetWhiteSpace(WhiteSpace.NoWrap);

            Add(_thumbnail);
            Add(_nameLabel);

            SetWidth(DefaultWidth);
            SetState(state ?? new ItemCardState(string.Empty));
        }

        public void SetState(ItemCardState state)
        {
            var nextState = state ?? new ItemCardState(string.Empty);
            _thumbnail.SetState(nextState.ImageState);
            _nameLabel.SetText(nextState.ItemName);
        }

        public void SetWidth(float width)
        {
            var safeWidth = Mathf.Max(48f, width);
            style.width = safeWidth;
            style.minWidth = safeWidth;
            style.maxWidth = safeWidth;

            _thumbnail.SetSize(safeWidth);

            _nameLabel.style.width = safeWidth;
            _nameLabel.style.minWidth = safeWidth;
            _nameLabel.style.maxWidth = safeWidth;
        }
    }
}
