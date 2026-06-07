using System;
using UnityEditor;

namespace Ee4v.AssetManager.Api
{
    internal static class AssetManagerDebugSyncMenu
    {
        [MenuItem("ee4v/Asset Manager/Debug/Sync Datasources", false, 999)]
        private static void SyncDatasources()
        {
            var blm = RunSyncWithoutDialog(() => AssetManagerApi.SyncBlm(new BlmSyncRequest()));
            var eagle = RunSyncWithoutDialog(() => AssetManagerApi.SyncEagle(new EagleSyncRequest()));
            EditorUtility.DisplayDialog(
                "AssetManager Debug Sync",
                string.Format(
                    "Datasource sync completed.\n\nBLM\nState: {0}\nCreated: {1}\nUpdated: {2}\nUnchanged: {3}\nError: {4}\n\nEagle\nState: {5}\nCreated: {6}\nUpdated: {7}\nUnchanged: {8}\nError: {9}",
                    blm.State,
                    blm.CreatedCount,
                    blm.UpdatedCount,
                    blm.UnchangedCount,
                    blm.ErrorCount,
                    eagle.State,
                    eagle.CreatedCount,
                    eagle.UpdatedCount,
                    eagle.UnchangedCount,
                    eagle.ErrorCount),
                "OK");
        }

        [MenuItem("ee4v/Asset Manager/Debug/Sync BLM", false, 1000)]
        private static void SyncBlm()
        {
            RunSync("BLM", () => AssetManagerApi.SyncBlm(new BlmSyncRequest()));
        }

        [MenuItem("ee4v/Asset Manager/Debug/Sync Eagle", false, 1001)]
        private static void SyncEagle()
        {
            RunSync("Eagle", () => AssetManagerApi.SyncEagle(new EagleSyncRequest()));
        }

        private static void RunSync(string label, Func<AssetSyncResult> sync)
        {
            try
            {
                var result = RunSyncWithoutDialog(sync);
                EditorUtility.DisplayDialog(
                    "AssetManager Debug Sync",
                    string.Format(
                        "{0} sync completed.\nState: {1}\nCreated: {2}\nUpdated: {3}\nUnchanged: {4}\nError: {5}",
                        label,
                        result.State,
                        result.CreatedCount,
                        result.UpdatedCount,
                        result.UnchangedCount,
                        result.ErrorCount),
                    "OK");
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog(
                    "AssetManager Debug Sync",
                    string.Format("{0} sync failed.\n{1}", label, exception.Message),
                    "OK");
            }
        }

        private static AssetSyncResult RunSyncWithoutDialog(Func<AssetSyncResult> sync)
        {
            try
            {
                return sync != null ? sync() : new AssetSyncResult(0, 0, 0, 1);
            }
            catch
            {
                return new AssetSyncResult(0, 0, 0, 1);
            }
        }
    }
}
