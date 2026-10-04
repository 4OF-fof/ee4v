using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ee4v.AvatarEditing
{
    /// <summary>Optional naming provider supplied by the feature that owns BlendShape presets.</summary>
    public static class AvatarShapeNaming
    {
        private static Func<GameObject, IEnumerable<Mesh>, IAvatarShapeNaming> _factory;
        private static readonly IAvatarShapeNaming Unclassified = new UnclassifiedNaming();
        public static event Action Changed;
        public static void Register(Func<GameObject, IEnumerable<Mesh>, IAvatarShapeNaming> factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            NotifyChanged();
        }
        public static void NotifyChanged() => Changed?.Invoke();
        public static IAvatarShapeNaming Create(GameObject avatar, IEnumerable<Mesh> meshes) =>
            _factory?.Invoke(avatar, meshes) ?? Unclassified;
        private sealed class UnclassifiedNaming : IAvatarShapeNaming
        {
            public bool IsHeader(string name) => false;
            public bool TryGetMapping(string guid, long id, string name, out AvatarShapeMapping mapping)
            { mapping = default; return false; }
        }
    }
}
