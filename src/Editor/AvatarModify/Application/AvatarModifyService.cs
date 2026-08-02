using System;
using Ee4v.AssetManager.Contracts;
using Ee4v.AvatarModify.Domain;

namespace Ee4v.AvatarModify.Application
{
    public sealed class AvatarModifyService
    {
        private readonly IAssetManager _assetManager;
        private readonly IAvatarAssetGateway _assets;
        private readonly IAvatarVariantGateway _variants;

        public AvatarModifyService(
            IAssetManager assetManager,
            IAvatarAssetGateway assets,
            IAvatarVariantGateway variants)
        {
            _assetManager = assetManager ??
                throw new ArgumentNullException(nameof(assetManager));
            _assets = assets ??
                throw new ArgumentNullException(nameof(assets));
            _variants = variants ??
                throw new ArgumentNullException(nameof(variants));
        }

        public bool IsImportedItem(string itemId)
        {
            return !string.IsNullOrWhiteSpace(itemId) &&
                   (_assetManager.GetItemImportedAssetGuids(itemId)?.Count ?? 0) > 0;
        }

        public AvatarVariantCreation GetCreation(string itemId)
        {
            if (!IsImportedItem(itemId))
            {
                return new AvatarVariantCreation
                {
                    ItemId = itemId ?? string.Empty,
                    Candidates = Array.Empty<PrefabCandidate>()
                };
            }

            var guids = _assetManager.GetItemImportedAssetGuids(itemId) ??
                        Array.Empty<string>();
            var candidates = AvatarSelectionPolicy.OrderCandidates(
                _assets.FindPrefabs(guids));
            return new AvatarVariantCreation
            {
                ItemId = itemId,
                Candidates = candidates,
                SelectedPrefabGuid =
                    AvatarSelectionPolicy.SelectAutomatically(candidates)
            };
        }

        public VariantAssetResult CreateVariant(
            CreateAvatarVariantRequest request)
        {
            if (request == null ||
                !IsImportedItem(request.ItemId) ||
                string.IsNullOrWhiteSpace(request.SourcePrefabGuid) ||
                string.IsNullOrWhiteSpace(request.VariantName))
            {
                return new VariantAssetResult
                {
                    Error = "Imported item, source prefab, and variant name are required."
                };
            }

            return _variants.Create(new VariantAssetRequest
            {
                VariantName = request.VariantName,
                SourcePrefabGuid = request.SourcePrefabGuid,
                DestinationRoot = request.DestinationRoot
            });
        }
    }
}
