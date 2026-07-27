using System;
using Ee4v.AssetManager.Infrastructure.Files;
using Ee4v.Core.Internal.EditorAPI;

namespace Ee4v.AssetManager.Infrastructure.Unity
{
    internal sealed class UnityAssetFileImportEnvironment : IAssetFileImportEnvironment
    {
        public string AssetsDirectory => AssetImport.AssetsDirectory;

        public void ImportPackage(string packagePath, bool interactive, Action onFinished)
        {
            AssetImport.ImportPackage(packagePath, interactive, onFinished);
        }

        public void Refresh()
        {
            AssetImport.Refresh();
        }
    }
}
