using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Ee4v.AssetManager.Infrastructure.Unity
{
    internal static class AssetProtectionEditorBridge
    {
        internal static AssetProtectionService Current
        {
            get;
            private set;
        }

        internal static string BlockedMessage
        {
            get;
            private set;
        } = string.Empty;

        internal static void Configure(
            AssetProtectionService protection,
            string blockedMessage)
        {
            Current = protection;
            BlockedMessage = blockedMessage ?? string.Empty;
        }
    }

    internal sealed class AssetProtectionModificationProcessor :
        AssetModificationProcessor
    {
        private static bool IsOpenForEdit(
            string assetOrMetaFilePath,
            out string message)
        {
            var protection =
                AssetProtectionEditorBridge.Current;
            var editable = protection == null ||
                           !protection.IsPathProtected(
                               assetOrMetaFilePath);
            message = editable
                ? string.Empty
                : AssetProtectionEditorBridge
                    .BlockedMessage;
            return editable;
        }

        private static bool IsOpenForEdit(
            string[] assetOrMetaFilePaths,
            List<string> outNotEditablePaths,
            StatusQueryOptions statusQueryOptions)
        {
            var protection =
                AssetProtectionEditorBridge.Current;
            if (protection == null)
            {
                return true;
            }

            for (var i = 0;
                 i < assetOrMetaFilePaths.Length;
                 i++)
            {
                var path = assetOrMetaFilePaths[i];
                if (protection.IsPathProtected(path))
                {
                    outNotEditablePaths.Add(path);
                }
            }

            return outNotEditablePaths.Count == 0;
        }

        private static string[] OnWillSaveAssets(
            string[] paths)
        {
            var protection =
                AssetProtectionEditorBridge.Current;
            return protection == null
                ? paths
                : paths.Where(path =>
                        !protection.IsPathProtected(path))
                    .ToArray();
        }

        private static AssetDeleteResult OnWillDeleteAsset(
            string assetPath,
            RemoveAssetOptions options)
        {
            var protection =
                AssetProtectionEditorBridge.Current;
            return protection != null &&
                   protection
                       .WouldMutateProtectedAsset(
                           assetPath)
                ? AssetDeleteResult.FailedDelete
                : AssetDeleteResult
                    .DidNotDelete;
        }

        private static AssetMoveResult OnWillMoveAsset(
            string sourcePath,
            string destinationPath)
        {
            var protection =
                AssetProtectionEditorBridge.Current;
            return protection != null &&
                   (protection
                        .WouldMutateProtectedAsset(
                            sourcePath) ||
                    protection
                        .IsPathProtected(
                            destinationPath))
                ? AssetMoveResult.FailedMove
                : AssetMoveResult.DidNotMove;
        }
    }
}
