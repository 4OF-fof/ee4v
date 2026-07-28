using Ee4v.AssetManager.Contracts;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.UI
{
    internal static class AssetCollectionIconPresenter
    {
        public static IconState CreateState(
            AssetCollection collection,
            float size = UiSizeTokens.Size12)
        {
            if (collection == null ||
                !collection.IsSmartCollection)
            {
                return IconState.FromBuiltinIcon(
                    UiBuiltinIcon.Folder,
                    size);
            }

            var texture = collection != null
                ? LoadAssetIcon(collection.IconAssetGuid)
                : null;
            if (texture != null)
            {
                return IconState.FromTexture(texture, size);
            }

            return IconState.FromBuiltinIcon(
                Resolve(
                    collection != null
                        ? collection.Icon
                        : AssetCollectionIcon.Folder),
                size);
        }

        public static UiBuiltinIcon Resolve(AssetCollectionIcon icon)
        {
            if (icon == AssetCollectionIcon.Star)
            {
                return UiBuiltinIcon.Star;
            }

            if (icon == AssetCollectionIcon.Package)
            {
                return UiBuiltinIcon.ArchiveFile;
            }

            if (icon == AssetCollectionIcon.Tag)
            {
                return UiBuiltinIcon.Tag;
            }

            if (icon == AssetCollectionIcon.Search)
            {
                return UiBuiltinIcon.Search;
            }

            return UiBuiltinIcon.Folder;
        }

        private static Texture LoadAssetIcon(
            string assetGuid)
        {
            if (string.IsNullOrWhiteSpace(assetGuid))
            {
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(assetGuid);
            return string.IsNullOrWhiteSpace(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture>(path);
        }
    }
}
