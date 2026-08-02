using System.Collections.Generic;
using Ee4v.AvatarModify.Domain;

namespace Ee4v.AvatarModify.Application
{
    public sealed class AvatarVariantCreation
    {
        public string ItemId { get; set; }
        public IReadOnlyList<PrefabCandidate> Candidates { get; set; }
        public string SelectedPrefabGuid { get; set; }
    }

    public sealed class VariantAssetRequest
    {
        public string VariantName { get; set; }
        public string SourcePrefabGuid { get; set; }
        public string DestinationRoot { get; set; }
    }

    public sealed class VariantAssetResult
    {
        public bool Succeeded { get; set; }
        public string VariantPrefabGuid { get; set; }
        public string VariantPath { get; set; }
        public string Error { get; set; }
    }

    public sealed class CreateAvatarVariantRequest
    {
        public string ItemId { get; set; }
        public string SourcePrefabGuid { get; set; }
        public string VariantName { get; set; }
        public string DestinationRoot { get; set; }
    }
}
