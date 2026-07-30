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
        Search,
        Image,
        Music,
        Code,
        Cube,
        Database,
        Heart,
        Library,
        Collections,
        Group,
        Grid,
        List,
        Table,
        Camera,
        Video,
        Document,
        Archive,
        Cloud,
        Color,
        Lightbulb,
        Wrench,
        Settings,
        Pin,
        Home,
        Apps,
        Key
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
        InvalidCollectionHierarchy,
        InvalidSmartCollectionCondition,
        DatabaseError,
        DatasourceError,
        DatabaseSchemaIncompatible
    }

    public enum AssetSyncState
    {
        Success,
        Failed,
        Partial
    }
}
