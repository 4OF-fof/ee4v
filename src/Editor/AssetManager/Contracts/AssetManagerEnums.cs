namespace Ee4v.AssetManager.Contracts
{
    public enum AssetSourceType
    {
        Eagle,
        Ee4v
    }

    public enum AssetFileAnalysisKind
    {
        Zip,
        UnityPackage
    }

    public enum AssetFileContentEntryKind
    {
        File,
        Directory
    }

    public enum AssetCollectionIcon
    {
        File = 0,
        Folder = 1,
        Star = 2,
        Tag = 3,
        Library = 4,
        Image = 5,
        Cube = 6,
        Archive = 7,
        Pin = 8
    }

    public enum AssetFilterNodeType
    {
        And,
        Or,
        Not,
        Condition
    }

    public enum AssetFilterConditionType
    {
        NameContains,
        DescriptionContains,
        HasTag,
        HasFileExtension
    }

    public enum AssetManagerErrorCode
    {
        NotFound,
        Duplicate,
        InvalidRequest,
        DatabaseError,
        DatasourceError
    }

    public enum AssetImportState
    {
        Success,
        Failed,
        Canceled
    }

    public enum AssetSyncState
    {
        Success,
        Failed,
        Partial
    }
}
