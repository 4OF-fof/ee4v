using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _4OF.ee4v.Core.Wraps {
    internal class MaterialEditorWrap : WrapBase {
        private static readonly Func<object, object> GetCustomShaderGUI = 
            GetField<object>(typeof(MaterialEditor), "m_CustomShaderGUI").g;

        public static void DrawMaterialInspector(MaterialEditor editor, Material material) {
            var customShaderGUI = GetCustomShaderGUI(editor);
            var props = MaterialEditor.GetMaterialProperties(new Object[] { material });

            if (customShaderGUI == null) return;
            var onGUI = customShaderGUI.GetType().GetMethod(
                "OnGUI", 
                BindingFlags.Public | BindingFlags.Instance,
                null, 
                new[] { typeof(MaterialEditor), typeof(MaterialProperty[]) }, 
                null
            );

            if (onGUI == null) return;
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