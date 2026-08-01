using System.IO;
using Ee4v.AssetManager.Contracts;
using UnityEditor;

namespace Ee4v.AssetManager.Infrastructure.Unity
{
    internal sealed class UnityAssetManagerFilePicker :
        IAssetManagerFilePicker
    {
        public AssetManagerFileSelection SelectFile(string title)
        {
            var path = EditorUtility.OpenFilePanel(
                title ?? string.Empty,
                string.Empty,
                string.Empty);
            if (string.IsNullOrWhiteSpace(path) ||
                !Path.IsPathRooted(path))
            {
                return null;
            }

            return ReadFile(path);
        }

        public AssetManagerFileSelection ReadFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                !Path.IsPathRooted(path))
            {
                return null;
            }

            var file = new FileInfo(path);
            if (!file.Exists)
            {
                return null;
            }

            return new AssetManagerFileSelection(
                file.FullName,
                file.Name,
                file.Length);
        }
    }
}
