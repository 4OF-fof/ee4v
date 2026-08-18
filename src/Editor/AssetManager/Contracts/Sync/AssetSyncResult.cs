using System;
using System.Collections.Generic;

namespace Ee4v.AssetManager.Contracts
{
    public sealed class AssetSyncResult
    {
        public AssetSyncResult(
            IReadOnlyList<string> createdItemIds,
            IReadOnlyList<string> updatedItemIds,
            IReadOnlyList<string> deletedItemIds,
            IReadOnlyList<string> createdFileIds,
            IReadOnlyList<string> updatedFileIds,
            IReadOnlyList<string> deletedFileIds,
            IReadOnlyList<string> affectedItemIds,
            IReadOnlyList<string> affectedFileIds,
            int unchangedCount,
            IReadOnlyList<string> errorMessages)
        {
            CreatedItemIds = createdItemIds ?? Array.Empty<string>();
            UpdatedItemIds = updatedItemIds ?? Array.Empty<string>();
            DeletedItemIds = deletedItemIds ?? Array.Empty<string>();
            CreatedFileIds = createdFileIds ?? Array.Empty<string>();
            UpdatedFileIds = updatedFileIds ?? Array.Empty<string>();
            DeletedFileIds = deletedFileIds ?? Array.Empty<string>();
            AffectedItemIds = affectedItemIds ?? Array.Empty<string>();
            AffectedFileIds = affectedFileIds ?? Array.Empty<string>();
            ErrorMessages = errorMessages ?? Array.Empty<string>();
            CreatedCount = CreatedItemIds.Count + CreatedFileIds.Count;
            UpdatedCount = UpdatedItemIds.Count + UpdatedFileIds.Count;
            DeletedCount = DeletedItemIds.Count + DeletedFileIds.Count;
            UnchangedCount = unchangedCount;
            ErrorCount = ErrorMessages.Count;
            State = ResolveState(
                CreatedCount + DeletedCount,
                UpdatedCount,
                UnchangedCount,
                ErrorCount);
        }

        public int CreatedCount { get; private set; }

        public int UpdatedCount { get; private set; }

        public int DeletedCount { get; private set; }

        public int UnchangedCount { get; private set; }

        public int ErrorCount { get; private set; }

        public AssetSyncState State { get; private set; }

        public IReadOnlyList<string> CreatedItemIds { get; private set; }
        public IReadOnlyList<string> UpdatedItemIds { get; private set; }
        public IReadOnlyList<string> DeletedItemIds { get; private set; }
        public IReadOnlyList<string> CreatedFileIds { get; private set; }
        public IReadOnlyList<string> UpdatedFileIds { get; private set; }
        public IReadOnlyList<string> DeletedFileIds { get; private set; }
        public IReadOnlyList<string> AffectedItemIds { get; private set; }
        public IReadOnlyList<string> AffectedFileIds { get; private set; }
        public IReadOnlyList<string> ErrorMessages { get; private set; }

        private static AssetSyncState ResolveState(int createdCount, int updatedCount, int unchangedCount, int errorCount)
        {
            if (errorCount <= 0)
            {
                return AssetSyncState.Success;
            }

            return createdCount > 0 || updatedCount > 0 || unchangedCount > 0
                ? AssetSyncState.Partial
                : AssetSyncState.Failed;
        }
    }
}
