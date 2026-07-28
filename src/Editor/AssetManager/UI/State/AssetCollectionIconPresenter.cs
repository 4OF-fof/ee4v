using Ee4v.AssetManager.Contracts;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.UI
{
    internal static class AssetCollectionIconPresenter
    {
        private static readonly UiFluentIcon[] PresetIcons =
        {
            UiFluentIcon.Folder,
            UiFluentIcon.Star,
            UiFluentIcon.Box,
            UiFluentIcon.Tag,
            UiFluentIcon.Search,
            UiFluentIcon.Image,
            UiFluentIcon.MusicNote2,
            UiFluentIcon.DocumentCode,
            UiFluentIcon.Cube,
            UiFluentIcon.Database,
            UiFluentIcon.Heart,
            UiFluentIcon.Library,
            UiFluentIcon.Collections,
            UiFluentIcon.Group,
            UiFluentIcon.Grid,
            UiFluentIcon.List,
            UiFluentIcon.Table,
            UiFluentIcon.Camera,
            UiFluentIcon.Video,
            UiFluentIcon.Document,
            UiFluentIcon.Archive,
            UiFluentIcon.Cloud,
            UiFluentIcon.Color,
            UiFluentIcon.Lightbulb,
            UiFluentIcon.Wrench,
            UiFluentIcon.Settings,
            UiFluentIcon.Pin,
            UiFluentIcon.Home,
            UiFluentIcon.Apps,
            UiFluentIcon.Key
        };

        public static IconState CreateState(
            AssetCollection collection,
            float size = UiSizeTokens.Size12)
        {
            if (collection == null ||
                !collection.IsSmartCollection)
            {
                return IconState.FromFluentIcon(
                    UiFluentIcon.Folder,
                    size);
            }

            var texture = collection != null
                ? LoadAssetIcon(collection.IconAssetGuid)
                : null;
            if (texture != null)
            {
                return IconState.FromTexture(texture, size);
            }

            return IconState.FromFluentIcon(
                Resolve(
                    collection != null
                        ? collection.Icon
                        : AssetCollectionIcon.Folder),
                size);
        }

        public static UiFluentIcon Resolve(AssetCollectionIcon icon)
        {
            var index = (int)icon;
            return index >= 0 &&
                   index < PresetIcons.Length
                ? PresetIcons[index]
                : UiFluentIcon.Folder;
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
