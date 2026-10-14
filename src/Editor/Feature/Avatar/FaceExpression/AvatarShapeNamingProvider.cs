using System.Collections.Generic;
using Ee4v.AvatarEditing;
using UnityEditor;
using UnityEngine;

namespace Ee4v.FaceExpression
{
    [InitializeOnLoad]
    internal sealed class AvatarShapeNamingProvider : IAvatarShapeNaming
    {
        private readonly FaceExpressionShapeNamingSnapshot _snapshot;
        static AvatarShapeNamingProvider()
        {
            AvatarShapeNaming.Register((avatar, meshes) => new AvatarShapeNamingProvider(avatar, meshes, true),
                (avatar, meshes) => new AvatarShapeNamingProvider(avatar, meshes, false));
            FaceExpressionShapeNamingSnapshot.PresetsChanged += AvatarShapeNaming.NotifyChanged;
            Ee4v.Core.Settings.CoreSettings.Current.Changed += (_, args) =>
            {
                if (FaceExpressionShapeNamingSnapshot.IsSeparatorSetting(args.Definition))
                    AvatarShapeNaming.NotifyChanged();
            };
        }
        private AvatarShapeNamingProvider(GameObject avatar, IEnumerable<Mesh> meshes, bool ensurePresets)
        { _snapshot = FaceExpressionShapeNamingSnapshot.Create(avatar, meshes, ensurePresets); }
        public bool IsHeader(string name) => _snapshot.IsHeader(name);
        public bool TryGetMapping(string guid, long id, string name, out AvatarShapeMapping mapping)
        {
            var found = _snapshot.TryGetMapping(guid, id, name, out var source);
            mapping = new AvatarShapeMapping(source.AppearancePart, source.AppearanceGroup, source.Role);
            return found;
        }
    }
}
