namespace Ee4v.AssetManager.Contracts
{
    public enum AssetDatasourceKind { Eagle, BoothLibraryManager, Ee4v }

    public sealed class AssetDatasourceRequest
    {
        public AssetDatasourceKind Kind { get; set; }
        public string LibraryPath { get; set; }
        public string DatabasePath { get; set; }
        public string TargetRoot { get; set; } = "VRCAsset";
    }

    public interface IAssetDatasourceManager
    {
        AssetSyncResult SyncDatasource(AssetDatasourceRequest request);
    }
}
