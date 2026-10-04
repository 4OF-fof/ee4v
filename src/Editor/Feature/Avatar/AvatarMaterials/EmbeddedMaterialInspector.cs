using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AvatarMaterials
{
    internal sealed class EmbeddedMaterialInspector
        : VisualElement, IDisposable
    {
        private const float HorizontalPadding = 12f;

        private readonly Material _material;
        private readonly Action _onChanged;
        private readonly IMGUIContainer _container;
        private MaterialEditor _editor;

        internal bool IsAvailable => _editor != null;

        internal EmbeddedMaterialInspector(
            Material material,
            Action onChanged)
        {
            _material = material;
            _onChanged = onChanged;
            AddToClassList(
                "ee4v-modification-workflow__material-editor");

            _editor = Editor.CreateEditor(material) as MaterialEditor;
            if (_editor == null)
            {
                return;
            }

            _container = new IMGUIContainer(DrawInspector);
            _container.AddToClassList(
                "ee4v-modification-workflow__material-editor-imgui");
            _container.RegisterCallback<GeometryChangedEvent>(
                OnGeometryChanged);
            Add(_container);
        }

        public void Dispose()
        {
            if (_container != null)
            {
                _container.UnregisterCallback<GeometryChangedEvent>(
                    OnGeometryChanged);
            }

            if (_editor == null)
            {
                return;
            }

            UnityEngine.Object.DestroyImmediate(_editor);
            _editor = null;
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if (Mathf.Abs(
                    evt.newRect.width - evt.oldRect.width) < 0.5f)
            {
                return;
            }

            _container?.MarkDirtyRepaint();
        }

        private void DrawInspector()
        {
            if (_editor == null || _material == null)
            {
                return;
            }

            var containerWidth = Mathf.Floor(
                _container.contentRect.width);
            if (containerWidth < 2f)
            {
                return;
            }

            var contentWidth = Mathf.Max(
                1f,
                containerWidth - HorizontalPadding * 2f);
            var previousWideMode = EditorGUIUtility.wideMode;
            var previousLabelWidth = EditorGUIUtility.labelWidth;
            var previousHierarchyMode =
                EditorGUIUtility.hierarchyMode;
            var previousIndentLevel = EditorGUI.indentLevel;
            var materialChanged = false;
            EditorGUI.BeginChangeCheck();
            try
            {
                EditorGUIUtility.wideMode = contentWidth > 330f;
                EditorGUIUtility.labelWidth = Mathf.Clamp(
                    contentWidth * 0.45f,
                    120f,
                    220f);
                EditorGUIUtility.hierarchyMode = true;
                EditorGUI.indentLevel = 0;

                using (new GUILayout.HorizontalScope(
                           GUILayout.Width(containerWidth)))
                {
                    GUILayout.Space(HorizontalPadding);
                    using (new GUILayout.VerticalScope(
                               GUILayout.Width(contentWidth)))
                    {
                        DrawMaterialInspector(_editor, _material);
                    }
                    GUILayout.Space(HorizontalPadding);
                }
            }
            finally
            {
                materialChanged = EditorGUI.EndChangeCheck();
                EditorGUIUtility.wideMode = previousWideMode;
                EditorGUIUtility.labelWidth = previousLabelWidth;
                EditorGUIUtility.hierarchyMode =
                    previousHierarchyMode;
                EditorGUI.indentLevel = previousIndentLevel;
            }

            if (!materialChanged)
            {
                return;
            }

            EditorUtility.SetDirty(_material);
            _onChanged?.Invoke();
        }

        private static void DrawMaterialInspector(
            MaterialEditor materialEditor,
            Material material)
        {
            var shaderGui = materialEditor.customShaderGUI;
            if (shaderGui == null)
            {
                materialEditor.PropertiesGUI();
                return;
            }

            var properties = MaterialEditor.GetMaterialProperties(
                new UnityEngine.Object[] { material });
            shaderGui.OnGUI(materialEditor, properties);
        }
    }
}
