using System.Collections.Generic;
using Ee4v.AvatarModify.Domain;

namespace Ee4v.AvatarModify.Application
{
    public interface IAvatarAssetGateway
    {
        IReadOnlyList<PrefabCandidate> FindPrefabs(
            IReadOnlyList<string> assetGuids);
    }

    public interface IAvatarVariantGateway
    {
        VariantAssetResult Create(VariantAssetRequest request);
    }
}
