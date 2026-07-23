namespace Ee4v.AssetManager.Contracts
{
    public sealed class EagleSyncRequest
    {
        public EagleSyncRequest(string libraryPath = null, string targetRoot = null)
        {
            LibraryPath = libraryPath;
            TargetRoot = targetRoot;
        }

        public string LibraryPath { get; private set; }

        public string TargetRoot { get; private set; }
    }
}
