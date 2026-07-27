namespace Ee4v.AssetManager.Contracts
{
    public sealed class AssetSyncResult
    {
        public AssetSyncResult(int createdCount, int updatedCount, int unchangedCount, int errorCount)
            : this(createdCount, updatedCount, unchangedCount, errorCount, ResolveState(createdCount, updatedCount, unchangedCount, errorCount))
        {
        }

        public AssetSyncResult(int createdCount, int updatedCount, int unchangedCount, int errorCount, AssetSyncState state)
        {
            CreatedCount = createdCount;
            UpdatedCount = updatedCount;
            UnchangedCount = unchangedCount;
            ErrorCount = errorCount;
            State = state;
        }

        public int CreatedCount { get; private set; }

        public int UpdatedCount { get; private set; }

        public int UnchangedCount { get; private set; }

        public int ErrorCount { get; private set; }

        public AssetSyncState State { get; private set; }

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

    public enum AssetSyncStatus
    {
        Created,
        Updated,
        Unchanged,
        Error
    }
}
