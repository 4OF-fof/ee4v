using System;
using System.Collections.Generic;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.UI
{
    internal interface IAssetManagerProjectCacheSource
    {
        event Action<AssetManagerChange> Changed;

        IReadOnlyList<AssetImportedAssetAssociation>
            GetImportedAssetAssociations();

        IReadOnlyDictionary<string, AssetThumbnail>
            GetThumbnails(IReadOnlyList<string> itemIds);
    }

    internal sealed class AssetManagerProjectCacheSource :
        IAssetManagerProjectCacheSource
    {
        private readonly IAssetManager _assetManager;

        internal AssetManagerProjectCacheSource(
            IAssetManager assetManager)
        {
            _assetManager = assetManager ??
                throw new ArgumentNullException(
                    nameof(assetManager));
        }

        public event Action<AssetManagerChange> Changed
        {
            add { _assetManager.Changed += value; }
            remove { _assetManager.Changed -= value; }
        }

        public IReadOnlyList<AssetImportedAssetAssociation>
            GetImportedAssetAssociations() =>
            _assetManager.GetImportedAssetAssociations();

        public IReadOnlyDictionary<string, AssetThumbnail>
            GetThumbnails(IReadOnlyList<string> itemIds) =>
            _assetManager.GetThumbnails(itemIds);
    }
}
