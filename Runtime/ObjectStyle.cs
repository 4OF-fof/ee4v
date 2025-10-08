using UnityEditor;
using UnityEngine;
using VRC.SDKBase;

namespace _4OF.ee4v.Runtime {
    [DisallowMultipleComponent]
    public class ObjectStyleComponent : MonoBehaviour, IEditorOnly {
        [Tooltip("Color")] public Color color = Color.clear;

        [Tooltip("Icon")] public Texture icon;
    }
}