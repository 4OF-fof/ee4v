namespace Ee4v.AssetManager.Api
{
    public sealed class BlmSyncRequest
    {
        public BlmSyncRequest(string databasePath = null, string itemDirectoryPath = null)
        {
            DatabasePath = databasePath;
            ItemDirectoryPath = itemDirectoryPath;
        }

        public string DatabasePath { get; private set; }

        public string ItemDirectoryPath { get; private set; }
    }
}
