namespace Ee4v.AssetManager.Contracts
{
    public interface IAssetManagerAssetDerivationService
    {
        bool IsManaged(string assetGuid);
        bool IsProtected(string assetGuid);
        bool CanCreateMaterialVariant(string assetGuid);
        bool CanCreatePrefabVariant(string assetGuid);
        bool CreateEditableCopy(
            string assetGuid,
            string destinationAssetPath);
        bool CreateMaterialVariant(
            string assetGuid,
            string destinationAssetPath);
        bool CreatePrefabVariant(
            string assetGuid,
            string destinationAssetPath);
    }
}
