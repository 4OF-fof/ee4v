using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.UI
{
    [InitializeOnLoad]
    internal static class AssetManagerProjectThumbnailBootstrap
    {
        private static AssetManagerProjectThumbnailImporter _importer;

        static AssetManagerProjectThumbnailBootstrap()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            AssetManagerWindowSession.ManagerInvalidated +=
                RefreshImporter;
            AssetManagerSettings.ProjectThumbnailImportChanged +=
                RefreshImporter;
            RefreshImporter();
        }

        private static void RefreshImporter()
        {
            _importer?.Dispose();
            _importer = null;
            if (!AssetManagerSettings
                    .ApplyProjectThumbnailOnImportEnabled)
            {
                return;
            }

            _importer = new AssetManagerProjectThumbnailImporter(
                AssetManagerWindowSession.GetManager());
        }
    }
}
