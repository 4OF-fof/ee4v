using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _4OF.ee4v.Core.Wraps {
    internal class MaterialEditorWrap : WrapBase {
        private static readonly (Func<object, object> g, Action<object, object> s) FiMCustomShaderGUI =
            GetField(typeof(MaterialEditor), "m_CustomShaderGUI");

        public static void DrawMaterialInspector(MaterialEditor editor, Material material) {
            var customShaderGUI = FiMCustomShaderGUI.g(editor);
            var props = MaterialEditor.GetMaterialProperties(new Object[] { material });

            if (customShaderGUI != null) {
                var onGUI = customShaderGUI.GetType().GetMethod("OnGUI", BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(MaterialEditor), typeof(MaterialProperty[]) }, null);
                if (onGUI != null)
                    using (new GUILayout.HorizontalScope()) {
                        GUILayout.Space(8);
                        using (new GUILayout.VerticalScope()) {
                            onGUI.Invoke(customShaderGUI, new object[] { editor, props });
                        }

                        GUILayout.Space(4);
                    }
            }
        }
    }
}