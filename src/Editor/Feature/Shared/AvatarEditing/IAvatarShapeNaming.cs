using System;

namespace Ee4v.AvatarEditing
{
    public readonly struct AvatarShapeMapping
    {
        public AvatarShapeMapping(string appearancePart, string appearanceGroup, string role)
        {
            AppearancePart = appearancePart;
            AppearanceGroup = appearanceGroup;
            Role = role;
        }

        public string AppearancePart { get; }
        public string AppearanceGroup { get; }
        public string Role { get; }
    }

    public interface IAvatarShapeNaming
    {
        bool IsHeader(string shapeName);
        bool TryGetMapping(string assetGuid, long meshLocalId, string shapeName, out AvatarShapeMapping mapping);
    }
}
