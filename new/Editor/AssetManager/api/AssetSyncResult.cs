namespace Ee4v.AssetManager.Api
{
    public sealed class AssetSyncResult
    {
        public AssetSyncResult(int createdCount, int updatedCount, int unchangedCount, int errorCount)
        {
            CreatedCount = createdCount;
            UpdatedCount = updatedCount;
            UnchangedCount = unchangedCount;
            ErrorCount = errorCount;
        }

        public int CreatedCount { get; private set; }

        public int UpdatedCount { get; private set; }

        public int UnchangedCount { get; private set; }

        public int ErrorCount { get; private set; }
    }

    public enum AssetSyncStatus
    {
        Created,
        Updated,
        Unchanged,
        Error
    }
}
