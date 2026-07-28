namespace Ee4v.AssetManager.Contracts
{
    public enum AssetSourceType
    {
        Blm,
        Eagle,
        Ee4v
    }

    public enum AssetFileLifecycle
    {
        Active,
        Archived
    }

    public enum AssetDependencyEndpointType
    {
        File,
        VersionGroup,
        VariantGroup
    }

    public enum SmartCollectionMatchMode
    {
        All,
        Any
    }

    public enum AssetCollectionIcon
    {
        Folder,
        Star,
        Package,
        Tag,
        Search
    }

    public enum SmartCollectionConditionField
    {
        Name,
        Description,
        Tag,
        FileName,
        Extension
    }

    public enum SmartCollectionConditionOperator
    {
        Contains,
        Equals,
        In,
        Exists
    }

    public enum AssetManagerErrorCode
    {
        Unknown,
        NotFound,
        Duplicate,
        InvalidRequest,
        CollectionCycle,
        InvalidSmartCollectionCondition,
        DatabaseError,
        DatasourceError
    }

    public enum AssetSyncState
    {
        Success,
        Failed,
        Partial
    }
}
