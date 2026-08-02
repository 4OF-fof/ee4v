using Ee4v.AssetManager.Composition;
using Ee4v.AvatarModify.Composition;
using Ee4v.SaveAndBackup.Composition;
using UnityEditor;

namespace Ee4v.Composition
{
    [InitializeOnLoad]
    internal static class PackageBootstrap
    {
        static PackageBootstrap()
        {
            AssetManagerBootstrap.GetAvatarModifyDependencies(
                out var assetManager,
                out var derivationService,
                out var contextActions);
            AvatarModifyBootstrap.Initialize(
                assetManager,
                derivationService,
                contextActions);
            SaveAndBackupBootstrap.Initialize();
        }
    }
}
