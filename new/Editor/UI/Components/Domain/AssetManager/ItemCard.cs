using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class ItemCardState
    {
        public ItemCardState(string itemName, Texture2D thumbnail = null)
        {
            ItemName = itemName ?? string.Empty;
            Thumbnail = thumbnail;
        }

        public string ItemName { get; }

        public Texture2D Thumbnail { get; }
    }

    internal sealed class ItemCard : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-item-card";
        private const string ThumbnailFrameClassName = "ee4v-asset-manager-item-card__thumbnail-frame";
        private const string ThumbnailClassName = "ee4v-asset-manager-item-card__thumbnail";
        private const string ThumbnailPlaceholderClassName = "ee4v-asset-manager-item-card__thumbnail-placeholder";
        private const float DefaultWidth = 132f;
        private readonly VisualElement _thumbnailFrame;
        private readonly Image _thumbnailImage;
        private readonly VisualElement _thumbnailPlaceholder;
        private readonly UiTextElement _nameLabel;

        public ItemCard(ItemCardState state = null)
        {
            AddToClassList(RootClassName);

            _thumbnailFrame = new VisualElement();
            _thumbnailFrame.AddToClassList(ThumbnailFrameClassName);

            _thumbnailImage = new Image
            {
                pickingMode = PickingMode.Ignore,
                scaleMode = ScaleMode.ScaleAndCrop
            };
            _thumbnailImage.AddToClassList(ThumbnailClassName);

            _thumbnailPlaceholder = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            _thumbnailPlaceholder.AddToClassList(ThumbnailPlaceholderClassName);

            _thumbnailFrame.Add(_thumbnailPlaceholder);
            _thumbnailFrame.Add(_thumbnailImage);

            _nameLabel = UiTextFactory.Create(string.Empty, UiClassNames.ItemCardName);
            _nameLabel.SetWhiteSpace(WhiteSpace.NoWrap);

            Add(_thumbnailFrame);
            Add(_nameLabel);

            SetWidth(DefaultWidth);
            SetState(state ?? new ItemCardState(string.Empty));
        }

        public void SetState(ItemCardState state)
        {
            var nextState = state ?? new ItemCardState(string.Empty);
            SetThumbnail(nextState.Thumbnail);
            SetItemName(nextState.ItemName);
        }

        public void SetItemName(string itemName)
        {
            _nameLabel.SetText(itemName);
        }

        public void SetThumbnail(Texture2D thumbnail)
        {
            _thumbnailImage.image = thumbnail;
            var hasThumbnail = thumbnail != null;
            _thumbnailImage.style.display = hasThumbnail ? DisplayStyle.Flex : DisplayStyle.None;
            _thumbnailPlaceholder.style.display = hasThumbnail ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void SetWidth(float width)
        {
            var safeWidth = Mathf.Max(48f, width);
            style.width = safeWidth;
            style.minWidth = safeWidth;
            style.maxWidth = safeWidth;

            _thumbnailFrame.style.width = safeWidth;
            _thumbnailFrame.style.height = safeWidth;
            _thumbnailFrame.style.minWidth = safeWidth;
            _thumbnailFrame.style.minHeight = safeWidth;
            _thumbnailFrame.style.maxWidth = safeWidth;
            _thumbnailFrame.style.maxHeight = safeWidth;

            _nameLabel.style.width = safeWidth;
            _nameLabel.style.minWidth = safeWidth;
            _nameLabel.style.maxWidth = safeWidth;
        }
    }
}
