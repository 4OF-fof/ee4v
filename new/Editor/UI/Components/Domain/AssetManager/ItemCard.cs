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
        private readonly Image _thumbnailImage;
        private readonly VisualElement _thumbnailPlaceholder;
        private readonly UiTextElement _nameLabel;

        public ItemCard(ItemCardState state = null)
        {
            AddToClassList(RootClassName);

            var thumbnailFrame = new VisualElement();
            thumbnailFrame.AddToClassList(ThumbnailFrameClassName);

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

            thumbnailFrame.Add(_thumbnailPlaceholder);
            thumbnailFrame.Add(_thumbnailImage);

            _nameLabel = UiTextFactory.Create(string.Empty, UiClassNames.ItemCardName);
            _nameLabel.SetWhiteSpace(WhiteSpace.Normal);

            Add(thumbnailFrame);
            Add(_nameLabel);

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
    }
}
