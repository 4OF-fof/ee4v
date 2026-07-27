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

    public enum SmartCollectionConditionField
    {
        Name,
        Description,
        Tag,
        SourceType,
        FileName,
        Extension,
        Lifecycle
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
