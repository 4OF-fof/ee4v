using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FolderContentOverlay
{
    internal sealed class FolderAssetIconResolver
    {
        private static readonly Texture DefaultAssetIcon =
            EditorGUIUtility.IconContent("DefaultAsset Icon").image;

        public Texture Resolve(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            var assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            var stableTypeIcon = ResolveStableTypeIcon(
                assetPath,
                assetType);
            if (stableTypeIcon != null)
            {
                return stableTypeIcon;
            }

            var cachedIcon = AssetDatabase.GetCachedIcon(assetPath);
            if (cachedIcon != null && !IsGenericFileIcon(cachedIcon))
            {
                return cachedIcon;
            }

            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            var thumbnail = asset == null
                ? null
                : AssetPreview.GetMiniThumbnail(asset);
            if (thumbnail != null && !IsGenericFileIcon(thumbnail))
            {
                return thumbnail;
            }

            if (cachedIcon != null)
            {
                return cachedIcon;
            }

            return assetType == null
                ? null
                : AssetPreview.GetMiniTypeThumbnail(assetType);
        }

        private static Texture ResolveStableTypeIcon(
            string assetPath,
            Type assetType)
        {
            if (assetType == null)
            {
                return null;
            }

            if (typeof(Texture).IsAssignableFrom(assetType))
            {
                return GetBuiltinIcon("Texture Icon", assetType);
            }

            if (typeof(Material).IsAssignableFrom(assetType))
            {
                return GetBuiltinIcon("Material Icon", assetType);
            }

            if (typeof(Mesh).IsAssignableFrom(assetType))
            {
                return GetBuiltinIcon("Mesh Icon", assetType);
            }

            if (typeof(GameObject).IsAssignableFrom(assetType))
            {
                var importer = AssetImporter.GetAtPath(assetPath);
                if (importer is ModelImporter)
                {
                    return GetBuiltinIcon(
                        "ModelImporter Icon",
                        assetType);
                }

                if (string.Equals(
                        Path.GetExtension(assetPath),
                        ".prefab",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return GetBuiltinIcon("Prefab Icon", assetType);
                }
            }

            return null;
        }

        private static Texture GetBuiltinIcon(
            string iconName,
            Type fallbackType)
        {
            return EditorGUIUtility.IconContent(iconName).image ??
                AssetPreview.GetMiniTypeThumbnail(fallbackType);
        }

        private static bool IsGenericFileIcon(Texture icon)
        {
            if (icon == null)
            {
                return false;
            }

            if (DefaultAssetIcon != null && icon == DefaultAssetIcon)
            {
                return true;
            }

            return !string.IsNullOrEmpty(icon.name) &&
                icon.name.IndexOf(
                    "DefaultAsset",
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
