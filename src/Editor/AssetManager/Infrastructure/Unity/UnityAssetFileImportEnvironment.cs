using System;
using Ee4v.AssetManager.Infrastructure.Files;
using Ee4v.Core.Internal.EditorAPI;
using System.IO;
using UnityEditor;

namespace Ee4v.AssetManager.Infrastructure.Unity
{
    internal sealed class UnityAssetFileImportEnvironment : IAssetFileImportEnvironment
    {
        public string AssetsDirectory => AssetImport.AssetsDirectory;

        public void ImportPackage(
            string packagePath,
            bool interactive,
            Action<bool> onFinished)
        {
            AssetImport.ImportPackage(packagePath, interactive, onFinished);
        }

        public void Refresh()
        {
            AssetImport.Refresh();
        }

        public string GetAssetGuid(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                return string.Empty;
            }

            var assetsDirectory = Path.GetFullPath(
                AssetsDirectory)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            var path = Path.GetFullPath(absolutePath);
            var prefix = assetsDirectory +
                         Path.DirectorySeparatorChar;
            if (!path.StartsWith(
                    prefix,
                    System.StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(
                    path,
                    assetsDirectory,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var relativePath = path.Length == assetsDirectory.Length
                ? string.Empty
                : path.Substring(prefix.Length)
                    .Replace('\\', '/');
            var assetPath = string.IsNullOrEmpty(relativePath)
                ? "Assets"
                : "Assets/" + relativePath;
            return AssetDatabase.AssetPathToGUID(assetPath);
        }

        public bool AssetGuidExists(string assetGuid)
        {
            return !string.IsNullOrWhiteSpace(assetGuid) &&
                   !string.IsNullOrWhiteSpace(
                       AssetDatabase.GUIDToAssetPath(
                           assetGuid));
        }
    }
}
