using System;
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
                ResolveInitialDirectory(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.UserProfile)),
                string.Empty);
            if (string.IsNullOrWhiteSpace(path) ||
                !Path.IsPathRooted(path))
            {
                return null;
            }

            return ReadFile(path);
        }

        internal static string ResolveInitialDirectory(
            string userProfile)
        {
            if (string.IsNullOrWhiteSpace(userProfile))
            {
                return string.Empty;
            }

            var downloads = Path.Combine(
                userProfile,
                "Downloads");
            if (Directory.Exists(downloads))
            {
                return downloads;
            }

            return Directory.Exists(userProfile)
                ? userProfile
                : string.Empty;
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
