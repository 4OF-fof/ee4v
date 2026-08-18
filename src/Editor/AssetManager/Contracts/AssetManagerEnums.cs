namespace Ee4v.AssetManager.Contracts
{
    public enum AssetSourceType
    {
        Eagle,
        Ee4v
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

    public enum AssetSyncState
    {
        Success,
        Failed,
        Partial
    }
}
