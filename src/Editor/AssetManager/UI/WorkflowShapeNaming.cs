using System.Collections.Generic;
using Ee4v.AvatarEditing;
using Ee4v.FaceExpression;
using UnityEngine;

namespace Ee4v.AssetManager.UI
{
    internal sealed class WorkflowShapeNaming : IAvatarShapeNaming
    {
        private readonly FaceExpressionShapeNamingSnapshot _snapshot;

        internal WorkflowShapeNaming(GameObject avatar, IEnumerable<Mesh> meshes)
        {
            _snapshot = FaceExpressionShapeNamingSnapshot.Create(avatar, meshes);
        }

        public bool IsHeader(string shapeName) => _snapshot.IsHeader(shapeName);

        public bool TryGetMapping(string assetGuid, long meshLocalId, string shapeName, out AvatarShapeMapping mapping)
        {
            var found = _snapshot.TryGetMapping(assetGuid, meshLocalId, shapeName, out var source);
            mapping = new AvatarShapeMapping(source.AppearancePart, source.AppearanceGroup, source.Role);
            return found;
        }
    }
}
