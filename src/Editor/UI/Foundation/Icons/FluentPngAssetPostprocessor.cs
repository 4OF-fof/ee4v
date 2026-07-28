using System;
using UnityEditor;
using UnityEngine;

namespace Ee4v.UI
{
    internal sealed class FluentPngAssetPostprocessor
        : AssetPostprocessor
    {
        private const string PngDirectoryMarker =
            "/Editor/ThirdParty/" +
            "FluentUiSystemIcons/Png512/";

        private void OnPreprocessTexture()
        {
            if (!IsFluentPngAssetPath(assetPath))
            {
                return;
            }

            var textureImporter =
                assetImporter as TextureImporter;
            if (textureImporter == null)
            {
                return;
            }

            textureImporter.textureType =
                TextureImporterType.Default;
            textureImporter.textureShape =
                TextureImporterShape.Texture2D;
            textureImporter.sRGBTexture = true;
            textureImporter.alphaIsTransparency = true;
            textureImporter.mipmapEnabled = false;
            textureImporter.isReadable = false;
            textureImporter.npotScale =
                TextureImporterNPOTScale.None;
            textureImporter.wrapMode =
                TextureWrapMode.Clamp;
            textureImporter.filterMode =
                FilterMode.Bilinear;
            textureImporter.anisoLevel = 0;
            textureImporter.textureCompression =
                TextureImporterCompression.Uncompressed;
            textureImporter.maxTextureSize = 512;
        }

        public override uint GetVersion()
        {
            return 1;
        }

        private static bool IsFluentPngAssetPath(
            string path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   path.EndsWith(
                       ".png",
                       StringComparison.OrdinalIgnoreCase) &&
                   path.IndexOf(
                       PngDirectoryMarker,
                       StringComparison.Ordinal) >= 0;
        }
    }
}
