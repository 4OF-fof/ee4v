using System;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace _4OF.ee4v.AssetManager.Views.Components.AssetInfo {
    public class Actions : VisualElement {
        private readonly Button _downloadButton;
        private string _downloadUrl;

        public Actions() {
            _downloadButton = new Button(() =>
            {
                if (!string.IsNullOrEmpty(_downloadUrl)) OnDownloadRequested?.Invoke(_downloadUrl);
            }) {
                text = I18N.Get("UI.AssetManager.AssetInfo.DownloadFromBooth"),
                style = {
                    height = 30, marginTop = 10, marginBottom = 10,
                    backgroundColor = ColorPreset.PrimaryButtonStyle, color = ColorPreset.TextColor,
                    unityFontStyleAndWeight = FontStyle.Bold, alignSelf = Align.Center,
                    width = Length.Percent(90),
                    display = DisplayStyle.None
                }
            };

            _downloadButton.RegisterCallback<MouseEnterEvent>(_ =>
            {
                _downloadButton.style.backgroundColor = ColorPreset.PrimaryButtonHoverStyle;
            });
            _downloadButton.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                _downloadButton.style.backgroundColor = ColorPreset.PrimaryButtonStyle;
            });

            Add(_downloadButton);
        }

        public event Action<string> OnDownloadRequested;

        public void SetDownloadUrl(string url, bool hasPhysicalFile) {
            _downloadUrl = url;
            var show = !string.IsNullOrEmpty(url) && !hasPhysicalFile;
            _downloadButton.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}