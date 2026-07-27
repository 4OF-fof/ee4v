using UnityEditor;
using UnityEngine;

namespace Ee4v.HiddenObjects
{
    internal sealed class UnityHiddenObjectIconProvider
    {
        public Texture Load(int instanceId)
        {
            var target = EditorUtility.InstanceIDToObject(instanceId);
            return target != null
                ? AssetPreview.GetMiniThumbnail(target)
                : null;
        }
    }
}
