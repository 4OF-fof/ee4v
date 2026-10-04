using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
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
                    _utility.Camera,
                    _utility.FieldOfView);
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
            _previewTexture = _utility.Render(rect);
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
                _utility.Camera,
                _instance.transform.rotation);
        }

        private bool IsInScope(Renderer renderer)
        {
            return IsInScope(renderer, _scopeSiblingIndex);
        }

        private bool IsInScope(Renderer renderer, int? scopeSiblingIndex)
        {
            if (renderer == null || _instance == null)
            {
                return true;
            }
            if (scopeSiblingIndex >= 0)
            {
                var index = scopeSiblingIndex.Value;
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
            if (scopeSiblingIndex == -1)
            {
                return !isChildPrefab;
            }
            return isChildPrefab
                ? !_hiddenPrefabSiblingIndices.Contains(
                    current.GetSiblingIndex())
                : !_basePrefabHidden;
        }

        private void ApplyPendingUpdates()
        {
            foreach (var pair in _pendingTransformScales)
            {
                if (pair.Key != null)
                {
                    _utility.SetTransformScale(pair.Key, pair.Value);
                }
            }
            _pendingTransformScales.Clear();

            foreach (var pair in _pendingBlendShapeWeights)
            {
                if (pair.Key.Renderer != null)
                {
                    var mesh = pair.Key.Renderer.sharedMesh;
                    if (mesh != null && pair.Key.ShapeIndex < mesh.blendShapeCount)
                        _utility.SetBlendShapeWeight(pair.Key.Renderer,
                            mesh.GetBlendShapeName(pair.Key.ShapeIndex), pair.Value);
                }
            }
            _pendingBlendShapeWeights.Clear();

            if (_pendingBoundsRefresh)
            {
                _pendingBoundsRefresh = false;
                RefreshBounds();
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

        private Bounds CalculateBounds(
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
                    bounds = _utility.GetBounds(renderer);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(_utility.GetBounds(renderer));
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
                        !_hiddenPartRenderers.Contains(renderer))
                        .Select(renderer => _utility.ResolveRenderer(renderer)).ToArray());
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
            _pendingTransformScales.Clear();
            _pendingBlendShapeWeights.Clear();
            _pendingBoundsRefresh = false;
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
            _transformTargets.Clear();
            _blendShapeTargets.Clear();
            _hiddenPartRenderers.Clear();
            _temporarilyEnabledEditorOnlyParts.Clear();
            _renderers = Array.Empty<Renderer>();
            if (_utility != null)
            {
                _utility.Dispose();
                _utility = null;
            }

            if (_instance != null)
            {
                _instance = null;
            }

            _orbit.CancelInteraction();
            SetPreviewAvailable(false);
            _viewport.RequestRepaint();
        }
    }
}
