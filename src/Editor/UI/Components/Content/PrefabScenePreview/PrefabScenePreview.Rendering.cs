using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.EditorIntegration;
using Ee4v.Core.I18n;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    public sealed partial class PrefabScenePreview
    {
        private void DrawPreview(Rect rect)
        {
            var current = Event.current;
            if (current == null)
            {
                return;
            }

            if (rect.width < 2f || rect.height < 2f)
            {
                return;
            }

            if (_utility != null && _instance != null)
            {
                _orbit.HandleInput(
                    rect,
                    _utility.camera,
                    _utility.cameraFieldOfView);
            }
            if (_utility == null || _instance == null)
            {
                return;
            }

            ApplyPendingUpdates();
            if (PreviewObjectClicked != null &&
                current.type == EventType.MouseDown &&
                current.button == 0 && !current.alt &&
                rect.Contains(current.mousePosition))
            {
                _pendingPickPosition = current.mousePosition;
                current.Use();
                RequestPreviewRepaint();
                return;
            }
            if (current.type != EventType.Repaint)
            {
                return;
            }

            if (_orbit.UpdateTransition(EditorApplication.timeSinceStartup))
            {
                _previewDirty = true;
                if (!_orbit.IsTransitioning)
                {
                    StopCameraAnimation();
                }
            }
            var previewSize = rect.size;
            if (!_previewDirty &&
                _previewTexture != null &&
                Approximately(_previewTextureSize, previewSize))
            {
                GUI.DrawTexture(
                    rect,
                    _previewTexture,
                    ScaleMode.StretchToFill,
                    true);
                ProcessPendingPick(rect);
                DrawPickedOutline(rect);
                return;
            }
            ConfigureCamera();
            var forceSkinning = _forceSkinningRecalculation;
            _utility.BeginPreview(rect, GUIStyle.none);
            HideOutOfScopeRenderers();
            try
            {
                if (_hiddenMaterials.Count == 0)
                {
                    _utility.camera.Render();
                }
                else
                {
                    RenderFilteredMaterials();
                }
            }
            finally
            {
                RestoreOutOfScopeRenderers();
                if (forceSkinning)
                {
                    SetSkinningRecalculation(false);
                    _forceSkinningRecalculation = false;
                }
            }
            _previewTexture = _utility.EndPreview();
            _previewTextureSize = previewSize;
            _previewDirty = false;
            _outlineDirty = true;
            GUI.DrawTexture(
                rect,
                _previewTexture,
                ScaleMode.StretchToFill,
                true);
            ProcessPendingPick(rect);
            DrawPickedOutline(rect);
        }

        private void ConfigureCamera()
        {
            _orbit.ConfigureCamera(
                _utility.camera,
                _instance.transform.rotation);
        }

        private bool IsInScope(Renderer renderer)
        {
            if (renderer == null || _instance == null)
            {
                return true;
            }
            if (_scopeSiblingIndex >= 0)
            {
                var index = _scopeSiblingIndex.Value;
                return index < _instance.transform.childCount &&
                    renderer.transform.IsChildOf(
                        _instance.transform.GetChild(index));
            }

            var current = renderer.transform;
            while (current.parent != null &&
                   current.parent != _instance.transform)
            {
                current = current.parent;
            }
            var isChildPrefab = current.parent == _instance.transform &&
                                _prefabSiblingIndices.Contains(
                                    current.GetSiblingIndex());
            if (_scopeSiblingIndex == -1)
            {
                return !isChildPrefab;
            }
            return isChildPrefab
                ? !_hiddenPrefabSiblingIndices.Contains(
                    current.GetSiblingIndex())
                : !_basePrefabHidden;
        }

        private void HideOutOfScopeRenderers()
        {
            _temporarilyScopedRenderers.Clear();
            if (!_scopeSiblingIndex.HasValue &&
                !_basePrefabHidden &&
                _hiddenPrefabSiblingIndices.Count == 0 &&
                _hiddenPartRenderers.Count == 0)
            {
                return;
            }
            foreach (var renderer in _renderers)
            {
                if (renderer == null || !renderer.enabled ||
                    (IsInScope(renderer) &&
                     !_hiddenPartRenderers.Contains(renderer)))
                {
                    continue;
                }
                renderer.enabled = false;
                _temporarilyScopedRenderers.Add(renderer);
            }
        }

        private void RestoreOutOfScopeRenderers()
        {
            foreach (var renderer in _temporarilyScopedRenderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
            _temporarilyScopedRenderers.Clear();
        }

        private void RebuildMaterialTargets()
        {
            DestroyBakedMeshes();
            _materialTargets.Clear();
            _filteredRenderers.Clear();
            if (_hiddenMaterials.Count == 0 || _instance == null)
            {
                _bakedMeshesDirty = false;
                return;
            }

            foreach (var renderer in _renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                var materials = renderer.sharedMaterials;
                if (!materials.Any(material =>
                        material != null &&
                        _hiddenMaterials.Contains(material)))
                {
                    continue;
                }

                var mesh = GetPreviewMesh(renderer);
                if (mesh == null)
                {
                    continue;
                }

                _filteredRenderers.Add(renderer);
                var count = Mathf.Min(materials.Length, mesh.subMeshCount);
                for (var index = 0; index < count; index++)
                {
                    if (materials[index] == null ||
                        _hiddenMaterials.Contains(materials[index]))
                    {
                        continue;
                    }

                    _materialTargets.Add(new MaterialPreviewTarget
                    {
                        Renderer = renderer,
                        Mesh = mesh,
                        SubMeshIndex = index,
                        Material = materials[index]
                    });
                }
            }
            _bakedMeshesDirty = false;
        }

        private Mesh GetPreviewMesh(Renderer renderer)
        {
            if (renderer is MeshRenderer meshRenderer)
            {
                var filter = meshRenderer.GetComponent<MeshFilter>();
                return filter != null ? filter.sharedMesh : null;
            }

            if (!(renderer is SkinnedMeshRenderer skinned) ||
                skinned.sharedMesh == null)
            {
                return null;
            }

            var baked = new Mesh
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = skinned.sharedMesh.name + " (Material Preview)"
            };
            skinned.BakeMesh(baked);
            _bakedMeshes.Add(skinned, baked);
            return baked;
        }

        private void RenderFilteredMaterials()
        {
            if (_bakedMeshesDirty)
            {
                foreach (var pair in _bakedMeshes)
                {
                    if (pair.Key != null && pair.Value != null)
                    {
                        pair.Key.BakeMesh(pair.Value);
                    }
                }
                _bakedMeshesDirty = false;
            }

            foreach (var target in _materialTargets)
            {
                if (target.Renderer == null ||
                    target.Mesh == null ||
                    target.Material == null ||
                    !target.Renderer.enabled ||
                    !target.Renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                _utility.DrawMesh(
                    target.Mesh,
                    target.Renderer.localToWorldMatrix,
                    target.Material,
                    target.SubMeshIndex);
            }

            _temporarilyHiddenRenderers.Clear();
            foreach (var renderer in _filteredRenderers)
            {
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                renderer.enabled = false;
                _temporarilyHiddenRenderers.Add(renderer);
            }

            try
            {
                _utility.camera.Render();
            }
            finally
            {
                foreach (var renderer in _temporarilyHiddenRenderers)
                {
                    if (renderer != null)
                    {
                        renderer.enabled = true;
                    }
                }
                _temporarilyHiddenRenderers.Clear();
            }
        }

        private void DestroyBakedMeshes()
        {
            foreach (var mesh in _bakedMeshes.Values)
            {
                if (mesh != null)
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
            _bakedMeshes.Clear();
            _bakedMeshesDirty = false;
        }

        private void ApplyPendingUpdates()
        {
            var transformChanged = _pendingTransformScales.Count > 0;
            var blendShapeChanged = _pendingBlendShapeWeights.Count > 0;
            foreach (var pair in _pendingTransformScales)
            {
                if (pair.Key != null)
                {
                    pair.Key.localScale = pair.Value;
                }
            }
            _pendingTransformScales.Clear();

            foreach (var pair in _pendingBlendShapeWeights)
            {
                if (pair.Key.Renderer != null)
                {
                    pair.Key.Renderer.SetBlendShapeWeight(
                        pair.Key.ShapeIndex,
                        pair.Value);
                }
            }
            _pendingBlendShapeWeights.Clear();

            if (transformChanged || blendShapeChanged)
            {
                _forceSkinningRecalculation = true;
                SetSkinningRecalculation(true);
                _bakedMeshesDirty = true;
            }
            if (_pendingBoundsRefresh)
            {
                _pendingBoundsRefresh = false;
                RefreshBounds();
            }
        }

        private void SetSkinningRecalculation(bool enabled)
        {
            foreach (var renderer in _skinnedRenderers)
            {
                if (renderer != null)
                {
                    renderer.forceMatrixRecalculationPerRender = enabled;
                }
            }
        }

        private void ApplyInitialShapeChanges()
        {
            foreach (var changer in _instance
                         .GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (changer == null ||
                    changer.GetType().FullName != ShapeChangerTypeName ||
                    !changer.isActiveAndEnabled)
                {
                    continue;
                }

                var changerType = changer.GetType();
                var inverted = changerType.BaseType?
                    .GetProperty("Inverted")?.GetValue(changer);
                if (inverted is bool value && value)
                {
                    continue;
                }

                var shapes = changerType.GetProperty("Shapes")?
                    .GetValue(changer) as System.Collections.IEnumerable;
                if (shapes == null)
                {
                    continue;
                }

                foreach (var shape in shapes)
                {
                    if (shape == null)
                    {
                        continue;
                    }
                    var shapeType = shape.GetType();
                    var changeType = shapeType.GetField("ChangeType")?
                        .GetValue(shape);
                    if (!string.Equals(changeType?.ToString(), "Set",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var reference = shapeType.GetField("Object")?
                        .GetValue(shape);
                    var getTarget = reference?.GetType().GetMethod(
                        "Get", new[] { typeof(Component) });
                    var target = getTarget?.Invoke(
                        reference, new object[] { changer }) as GameObject;
                    var renderer = target != null
                        ? target.GetComponent<SkinnedMeshRenderer>()
                        : null;
                    var shapeName = shapeType.GetField("ShapeName")?
                        .GetValue(shape) as string;
                    if (renderer?.sharedMesh == null ||
                        string.IsNullOrEmpty(shapeName))
                    {
                        continue;
                    }
                    var index = renderer.sharedMesh
                        .GetBlendShapeIndex(shapeName);
                    if (index < 0)
                    {
                        continue;
                    }
                    var weight = shapeType.GetField("Value")?
                        .GetValue(shape);
                    if (weight is float blendShapeWeight)
                    {
                        renderer.SetBlendShapeWeight(index,
                            Mathf.Clamp(blendShapeWeight, 0f, 100f));
                    }
                }
            }
        }

        private void RequestPreviewRepaint()
        {
            _previewDirty = true;
            _viewport.RequestRepaint();
        }

        private static bool Approximately(Vector2 left, Vector2 right)
        {
            return Mathf.Abs(left.x - right.x) < 0.5f &&
                   Mathf.Abs(left.y - right.y) < 0.5f;
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if (_flexibleLayout)
            {
                if (Mathf.Abs(evt.newRect.width - evt.oldRect.width) >= 0.5f ||
                    Mathf.Abs(evt.newRect.height - evt.oldRect.height) >= 0.5f)
                {
                    FrameCurrentSelection(_orbit.IsTransitioning);
                }
                return;
            }

            var size = Mathf.Clamp(
                evt.newRect.width,
                MinimumPreviewSize,
                MaximumPreviewSize);
            if (Mathf.Abs(evt.newRect.height - size) < 0.5f)
            {
                return;
            }

            style.height = size;
            RequestPreviewRepaint();
        }

        private static Bounds CalculateBounds(GameObject instance)
        {
            return CalculateBounds(
                instance.transform.position,
                instance.GetComponentsInChildren<Renderer>());
        }

        private static Bounds CalculateBounds(
            Vector3 fallbackCenter,
            IReadOnlyList<Renderer> renderers)
        {
            var bounds = new Bounds(fallbackCenter, Vector3.one * 0.2f);
            var hasBounds = false;
            for (var index = 0; index < renderers.Count; index++)
            {
                var renderer = renderers[index];
                if (renderer == null ||
                    !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }

        private void RefreshBounds()
        {
            if (_instance != null)
            {
                _bounds = CalculateBounds(
                    _scopeSiblingIndex >= 0 &&
                    _scopeSiblingIndex.Value <
                    _instance.transform.childCount
                        ? _instance.transform.GetChild(
                            _scopeSiblingIndex.Value).position
                        : _instance.transform.position,
                    _renderers.Where(renderer =>
                        IsInScope(renderer) &&
                        !_hiddenPartRenderers.Contains(renderer)).ToArray());
            }
        }

        private void RebuildPreviewTargets()
        {
            _transformTargets.Clear();
            _blendShapeTargets.Clear();
            if (_instance == null)
            {
                return;
            }

            foreach (var transform in _instance
                         .GetComponentsInChildren<Transform>(true))
            {
                var path = AnimationUtility.CalculateTransformPath(
                    transform,
                    _instance.transform);
                _transformTargets[path] = transform;
            }

            foreach (var renderer in _renderers
                         .OfType<SkinnedMeshRenderer>())
            {
                if (renderer.sharedMesh == null)
                {
                    continue;
                }

                var path = AnimationUtility.CalculateTransformPath(
                    renderer.transform,
                    _instance.transform);
                var shapes = new Dictionary<string,
                    BlendShapePreviewTarget>(StringComparer.Ordinal);
                for (var index = 0;
                     index < renderer.sharedMesh.blendShapeCount;
                     index++)
                {
                    shapes[renderer.sharedMesh.GetBlendShapeName(index)] =
                        new BlendShapePreviewTarget(renderer, index);
                }
                _blendShapeTargets[path] = shapes;
            }
        }

        private void SetPreviewAvailable(bool available)
        {
            _viewport.SetPreviewAvailable(available);
            RefreshViewToggle();
        }

        private void CleanupPreview()
        {
            StopCameraAnimation();
            _orbit.CancelTransition();
            DestroyBakedMeshes();
            _pendingTransformScales.Clear();
            _pendingBlendShapeWeights.Clear();
            _pendingBoundsRefresh = false;
            _forceSkinningRecalculation = false;
            _previewTexture = null;
            _previewTextureSize = Vector2.zero;
            _previewDirty = true;
            _pendingPickPosition = null;
            if (_outlineTexture != null)
            {
                _outlineTexture.Release();
                UnityEngine.Object.DestroyImmediate(_outlineTexture);
                _outlineTexture = null;
            }
            if (_outlineMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(_outlineMaterial);
                _outlineMaterial = null;
            }
            if (_invisiblePreviewMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(_invisiblePreviewMaterial);
                _invisiblePreviewMaterial = null;
            }
            foreach (var material in _pickMaterialsById)
            {
                if (material != null)
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }
            _pickMaterialsById.Clear();
            if (_pickReadback != null)
            {
                UnityEngine.Object.DestroyImmediate(_pickReadback);
                _pickReadback = null;
            }
            _outlineTextureSize = Vector2.zero;
            _outlineDirty = true;
            ClearPickedSelection();
            _materialTargets.Clear();
            _transformTargets.Clear();
            _blendShapeTargets.Clear();
            _filteredRenderers.Clear();
            _temporarilyHiddenRenderers.Clear();
            _hiddenPartRenderers.Clear();
            _temporarilyEnabledEditorOnlyParts.Clear();
            _renderers = Array.Empty<Renderer>();
            _skinnedRenderers = Array.Empty<SkinnedMeshRenderer>();
            if (_utility != null)
            {
                _utility.Cleanup();
                _utility = null;
            }

            if (_instance != null)
            {
                UnityEngine.Object.DestroyImmediate(_instance);
                _instance = null;
            }

            _orbit.CancelInteraction();
            SetPreviewAvailable(false);
            _viewport.RequestRepaint();
        }
    }
}
