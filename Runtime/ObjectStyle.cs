using UnityEditor;
using UnityEngine;
using VRC.SDKBase;

namespace _4OF.ee4v.Runtime {
    [DisallowMultipleComponent]
    public class ObjectStyleComponent : MonoBehaviour, IEditorOnly {
        [Tooltip("Color")] public Color color = Color.clear;

        [Tooltip("Icon")] public Texture icon;
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(ObjectStyleComponent))]
    public class ObjectStyleComponentEditor : Editor {
        private void OnEnable() {
            serializedObject.FindProperty("m_Script");
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();
            var prop = serializedObject.GetIterator();
            var enterChildren = true;
            while (prop.NextVisible(enterChildren)) {
                enterChildren = false;
                if (prop.name == "m_Script")
                    continue;
                EditorGUILayout.PropertyField(prop, true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}