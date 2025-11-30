using System;
using System.Threading;
using _4OF.ee4v.AssetManager.Core;
using _4OF.ee4v.Core.i18n;
using _4OF.ee4v.Core.Utility;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.UI.Component {
    public class AssetThumbnailLoader {
        private readonly TextureService _textureService;

        public AssetThumbnailLoader(TextureService textureService) {
            _textureService = textureService;
        }

        public async void LoadThumbnailAsync(AssetCard card, Ulid id, bool isFolder, CancellationToken token) {
            if (_textureService == null) return;

            try {
                Texture2D tex;
                if (isFolder)
                    tex = await _textureService.GetFolderThumbnailAsync(id);
                else
                    tex = await _textureService.GetAssetThumbnailAsync(id);

                if (token.IsCancellationRequested) return;

                switch (card.userData) {
                    case AssetMetadata meta when meta.ID != id:
                    case BaseFolder folder when folder.ID != id:
                        return;
                }

                card.SetThumbnail(tex, isFolder);
            }
            catch (OperationCanceledException) {
                // Ignore
            }
            catch (Exception e) {
                Debug.LogWarning(I18N.Get("Debug.AssetManager.ThumbnailLoadFailedFmt", e.Message));
            }
        }
    }
}