using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed class ObjectThumbnail : PreviewContainer
    {
        private const int MaximumRefreshAttempts = 50;
        private const long RefreshIntervalMilliseconds = 100;

        private readonly Image _image;
        private IVisualElementScheduledItem _refreshItem;
        private UnityEngine.Object _target;
        private int _refreshAttempts;

        public ObjectThumbnail(UnityEngine.Object target)
        {
            AddToClassList(
                "ee4v-ui-object-thumbnail");
            pickingMode = PickingMode.Ignore;

            _image = new Image
            {
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            _image.AddToClassList(
                "ee4v-ui-object-thumbnail__image");
            Placeholder.Add(new Icon(
                FluentUiIcons.CreateState(
                    "cube.png",
                    UiSizeTokens.Size31,
                    tintColor: UiColorTokens.TextMuted)));
            Content.Add(_image);

            RegisterCallback<AttachToPanelEvent>(_ => Refresh());
            RegisterCallback<DetachFromPanelEvent>(_ => StopRefreshing());
            SetTarget(target);
        }

        public void SetTarget(UnityEngine.Object target)
        {
            if (_target == target && target != null)
            {
                return;
            }

            _target = target;
            _refreshAttempts = 0;
            StopRefreshing();
            style.display = target != null
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            Refresh();
        }

        private void Refresh()
        {
            if (_target == null)
            {
                SetTexture(null);
                return;
            }

            var preview = AssetPreview.GetAssetPreview(_target);
            if (preview != null)
            {
                SetTexture(preview);
                StopRefreshing();
                return;
            }

            SetTexture(AssetPreview.GetMiniThumbnail(_target));
            if (panel != null && _refreshItem == null)
            {
                _refreshItem = schedule.Execute(PollPreview)
                    .Every(RefreshIntervalMilliseconds);
            }
        }

        private void PollPreview()
        {
            _refreshAttempts++;
            var preview = _target != null
                ? AssetPreview.GetAssetPreview(_target)
                : null;
            if (preview != null)
            {
                SetTexture(preview);
                StopRefreshing();
                return;
            }

            if (_refreshAttempts >= MaximumRefreshAttempts)
            {
                StopRefreshing();
            }
        }

        private void SetTexture(Texture texture)
        {
            _image.image = texture;
            SetHasContent(texture != null);
        }

        private void StopRefreshing()
        {
            _refreshItem?.Pause();
            _refreshItem = null;
        }
    }
}
