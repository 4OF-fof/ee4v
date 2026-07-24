using UnityEditor;
using UnityEngine;

namespace Ee4v.HiddenObjects
{
    internal sealed class UnityHiddenObjectNavigator
        : IHiddenObjectNavigator
    {
        public void Focus(int instanceId)
        {
            var target = EditorUtility.InstanceIDToObject(instanceId);
            if (target == null)
            {
                return;
            }

            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }
    }
}
