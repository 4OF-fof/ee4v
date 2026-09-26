namespace Ee4v.AssetManager.Contracts
{
    public sealed class AssetItemQuery
    {
        public AssetFilterNode Filter { get; set; }
        public bool IncludeArchived { get; set; }
        public int Offset { get; set; }
        public int Limit { get; set; }
    }

    public sealed class CreateAssetItemRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public sealed class UpdateAssetItemRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public sealed class RegisterFileRequest
    {
        public string LibraryPath { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
    }

    public sealed class CreateAssetCollectionRequest
    {
        public string Name { get; set; }
        public AssetCollectionIcon Icon { get; set; } = AssetCollectionIcon.Folder;
        public AssetFilterNode Root { get; set; }
    }

    public sealed class UpdateAssetCollectionRequest
    {
        public string Name { get; set; }
        public AssetCollectionIcon? Icon { get; set; }
        public AssetFilterNode Root { get; set; }
    }
}
