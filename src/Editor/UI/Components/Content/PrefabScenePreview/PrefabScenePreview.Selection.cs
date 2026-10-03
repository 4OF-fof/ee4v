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
        private void ProcessPendingPick(Rect rect)
        {
            if (!_pendingPickPosition.HasValue)
            {
                return;
            }
            var position = _pendingPickPosition.Value;
            _pendingPickPosition = null;
            if (TryPickPreviewObject(rect, position,
                    out var renderer, out var material))
            {
                var instance = _instance;
                EditorApplication.delayCall += () =>
                {
                    if (_instance == null || _instance != instance ||
                        renderer == null || material == null)
                    {
                        return;
                    }
                    ApplyPickedSelection(renderer, material);
                };
            }
            else
            {
                ClearSelectionFromPreview();
            }
        }

        private void ApplyPickedSelection(
            Renderer renderer,
            Material material)
        {
            _requestedPartKey = null;
            _requestedMaterial = null;
            _pickedRenderer = renderer;
            _pickedMaterial = material;
            _outlinedRenderers.Clear();
            _outlinedRenderers.Add(renderer);
            _outlinedMaterial = null;
            _outlineDirty = true;
            _selectionLabel.SetText(renderer.name + " / " + material.name);
            _selectionOverlay.style.display = DisplayStyle.Flex;
            RequestPreviewRepaint();
            var partKey = GetPreviewPartKey(renderer.transform);
            PreviewObjectClicked?.Invoke(partKey, material);
        }

        private bool TryPickPreviewObject(
            Rect rect,
            Vector2 position,
            out Renderer pickedRenderer,
            out Material pickedMaterial)
        {
            pickedRenderer = null;
            pickedMaterial = null;
            var width = Mathf.Max(2, Mathf.CeilToInt(rect.width));
            var height = Mathf.Max(2, Mathf.CeilToInt(rect.height));
            var pixelX = Mathf.Clamp(Mathf.FloorToInt(
                (position.x - rect.x) / rect.width * width),
                0, width - 1);
            var pixelY = Mathf.Clamp(Mathf.FloorToInt(
                (position.y - rect.y) / rect.height * height),
                0, height - 1);
            if (_pickReadback == null)
            {
                _pickReadback = new Texture2D(
                    1, 1, TextureFormat.RGBA32, false, true)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            var readback = _pickReadback;
            var comparison = CreateComparisonTexture(width, height);
            var baseline = _previewTexture as RenderTexture;
            var skipped = new List<MaterialPreviewTarget>();
            try
            {
                if (_hiddenMaterials.Count > 0 || baseline == null)
                {
                    RenderPreviewForComparison(comparison);
                    baseline = comparison;
                }
                var visiblePixel = ReadPreviewPixel(
                    baseline, pixelX, pixelY, readback);
                while (TryPickPreviewCandidate(rect, pixelX, pixelY,
                           skipped, readback, out var candidate))
                {
                    RenderPreviewForComparison(comparison,
                        candidate.Renderer, candidate.SubMeshIndex);
                    var withoutCandidate = ReadPreviewPixel(
                        comparison, pixelX, pixelY, readback);
                    if (PreviewPixelsDiffer(
                            visiblePixel, withoutCandidate))
                    {
                        pickedRenderer = candidate.Renderer;
                        pickedMaterial = candidate.Material;
                        return true;
                    }
                    skipped.Add(candidate);
                }
                return false;
            }
            finally
            {
                RenderTexture.ReleaseTemporary(comparison);
            }
        }

        private bool TryPickPreviewCandidate(
            Rect rect,
            int pixelX,
            int pixelY,
            List<MaterialPreviewTarget> skipped,
            Texture2D readback,
            out MaterialPreviewTarget candidate)
        {
            candidate = null;
            var shader = Shader.Find(
                "Hidden/ee4v/PreviewSelectionOutline");
            if (shader == null)
            {
                return false;
            }
            var pickPass = GetPickMaterial(shader, 1).FindPass("Pick");
            if (pickPass < 0)
            {
                return false;
            }

            var camera = _utility.Camera;
            var width = Mathf.Max(2, Mathf.CeilToInt(rect.width));
            var height = Mathf.Max(2, Mathf.CeilToInt(rect.height));
            var texture = RenderTexture.GetTemporary(
                width, height, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            var command = new CommandBuffer
            {
                name = "ee4v Preview Picking"
            };
            var targets = new List<MaterialPreviewTarget> { null };
            var previousTarget = RenderTexture.active;
            try
            {
                command.SetRenderTarget(texture);
                command.SetViewport(new Rect(0f, 0f, width, height));
                command.ClearRenderTarget(true, true, Color.black);
                command.SetViewProjectionMatrices(
                    camera.worldToCameraMatrix,
                    GL.GetGPUProjectionMatrix(camera.projectionMatrix, false));
                ForEachVisibleMaterialSlot((renderer, material, slot) =>
                {
                    if (skipped.Any(target =>
                            target.Renderer == renderer &&
                            target.SubMeshIndex == slot))
                    {
                        return;
                    }
                    if (targets.Count >= 0xFFFFFF)
                    {
                        return;
                    }
                    var id = targets.Count;
                    targets.Add(new MaterialPreviewTarget
                    {
                        Renderer = renderer,
                        Material = material,
                        SubMeshIndex = slot
                    });
                    command.DrawRenderer(
                        _utility.ResolveRenderer(renderer), GetPickMaterial(shader, id), slot,
                        pickPass);
                });
                if (targets.Count == 1)
                {
                    return false;
                }

                Graphics.ExecuteCommandBuffer(command);
                RenderTexture.active = texture;
                readback.ReadPixels(
                    new Rect(pixelX, pixelY, 1f, 1f), 0, 0);
                readback.Apply(false, false);
                var pixel = readback.GetPixel(0, 0);
                var selectedId =
                    Mathf.RoundToInt(pixel.r * 255f) |
                    Mathf.RoundToInt(pixel.g * 255f) << 8 |
                    Mathf.RoundToInt(pixel.b * 255f) << 16;
                if (selectedId <= 0 || selectedId >= targets.Count)
                {
                    return false;
                }
                candidate = targets[selectedId];
                return candidate.Renderer != null &&
                    candidate.Material != null;
            }
            finally
            {
                RenderTexture.active = previousTarget;
                command.Dispose();
                RenderTexture.ReleaseTemporary(texture);
            }
        }

        private Material GetPickMaterial(Shader shader, int id)
        {
            while (_pickMaterialsById.Count < id)
            {
                var nextId = _pickMaterialsById.Count + 1;
                var color = new Color(
                    (nextId & 255) / 255f,
                    ((nextId >> 8) & 255) / 255f,
                    ((nextId >> 16) & 255) / 255f,
                    1f);
                var material = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                material.SetVector("_PickColor",
                    new Vector4(color.r, color.g, color.b, 1f));
                _pickMaterialsById.Add(material);
            }
            return _pickMaterialsById[id - 1];
        }

        private static RenderTexture CreateComparisonTexture(
            int width,
            int height)
        {
            var texture = RenderTexture.GetTemporary(
                width, height, 24, RenderTextureFormat.ARGBHalf,
                RenderTextureReadWrite.Linear);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        private static Color ReadPreviewPixel(
            RenderTexture texture,
            int x,
            int y,
            Texture2D readback)
        {
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = texture;
                readback.ReadPixels(new Rect(x, y, 1f, 1f), 0, 0);
                readback.Apply(false, false);
                return readback.GetPixel(0, 0);
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private static bool PreviewPixelsDiffer(Color first, Color second)
        {
            const float threshold = 1f / 255f;
            return Mathf.Abs(first.r - second.r) > threshold ||
                Mathf.Abs(first.g - second.g) > threshold ||
                Mathf.Abs(first.b - second.b) > threshold ||
                Mathf.Abs(first.a - second.a) > threshold;
        }

        private void RenderPreviewForComparison(
            RenderTexture target,
            Renderer omittedRenderer = null,
            int omittedSlot = -1,
            IReadOnlyCollection<Renderer> omittedRenderers = null,
            Material omittedMaterial = null)
        {
            var previewTexture = _previewTexture as RenderTexture;
            RenderTexture saved = null;
            if (previewTexture != null)
            {
                saved = RenderTexture.GetTemporary(previewTexture.descriptor);
                Graphics.Blit(previewTexture, saved);
            }
            try
            {
                var texture = _utility.Render(new Rect(0f, 0f, target.width, target.height),
                    (renderer, material, slot) =>
                        !(renderer == omittedRenderer && (omittedSlot < 0 || slot == omittedSlot)) &&
                        (omittedRenderers == null || !omittedRenderers.Contains(renderer)) &&
                        (omittedMaterial == null || material != omittedMaterial));
                Graphics.Blit(texture, target);
            }
            finally
            {
                if (saved != null)
                {
                    Graphics.Blit(saved, previewTexture);
                    RenderTexture.ReleaseTemporary(saved);
                }
            }
        }

        private void DrawPickedOutline(Rect rect)
        {
            if (!_selectionHighlightVisible ||
                _outlinedRenderers.Count == 0 ||
                (_outlinedMaterial != null &&
                 _hiddenMaterials.Contains(_outlinedMaterial)) ||
                !_outlinedRenderers.Any(renderer =>
                    renderer != null && _utility.IsRendererVisible(renderer) &&
                    IsInScope(renderer) &&
                    !_hiddenPartRenderers.Contains(renderer)))
            {
                return;
            }

            if (_outlineDirty || _outlineTexture == null ||
                !Approximately(_outlineTextureSize, rect.size))
            {
                UpdatePickedOutline(rect);
            }
            if (_outlineTexture != null)
            {
                GUI.DrawTexture(
                    rect, _outlineTexture, ScaleMode.StretchToFill, true);
            }
        }

        private void UpdatePickedOutline(Rect rect)
        {
            var outlineShader = Shader.Find(
                "Hidden/ee4v/PreviewSelectionOutline");
            if (outlineShader == null)
            {
                return;
            }

            var width = Mathf.Max(2, Mathf.CeilToInt(rect.width));
            var height = Mathf.Max(2, Mathf.CeilToInt(rect.height));
            if (_outlineTexture == null ||
                _outlineTexture.width != width ||
                _outlineTexture.height != height)
            {
                if (_outlineTexture != null)
                {
                    _outlineTexture.Release();
                    UnityEngine.Object.DestroyImmediate(_outlineTexture);
                }
                _outlineTexture = new RenderTexture(
                    width, height, 0, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Linear)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear
                };
                _outlineTexture.Create();
            }
            if (_outlineMaterial == null)
            {
                _outlineMaterial = new Material(outlineShader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            var outlineColor = new Color(1f, 0.4f, 0f, 1f);
            _outlineMaterial.SetColor("_OutlineColor",
                QualitySettings.activeColorSpace == ColorSpace.Linear
                    ? outlineColor.gamma : outlineColor);

            var mask = RenderTexture.GetTemporary(
                width, height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            mask.wrapMode = TextureWrapMode.Clamp;
            mask.filterMode = FilterMode.Point;
            var withoutSelection = CreateComparisonTexture(width, height);
            RenderTexture baseline = null;
            var previousTarget = RenderTexture.active;
            try
            {
                Texture source = _previewTexture;
                if (_hiddenMaterials.Count > 0 || source == null)
                {
                    baseline = CreateComparisonTexture(width, height);
                    RenderPreviewForComparison(baseline);
                    source = baseline;
                }
                RenderPreviewForComparison(
                    withoutSelection,
                    omittedRenderers: _outlinedMaterial == null
                        ? _outlinedRenderers : null,
                    omittedMaterial: _outlinedMaterial);
                _outlineMaterial.SetTexture(
                    "_WithoutSelectionTex", withoutSelection);
                Graphics.Blit(source, mask, _outlineMaterial, 0);
                Graphics.Blit(mask, _outlineTexture,
                    _outlineMaterial, 1);
                _outlineTextureSize = rect.size;
                _outlineDirty = false;
            }
            finally
            {
                RenderTexture.active = previousTarget;
                _outlineMaterial.SetTexture(
                    "_WithoutSelectionTex", null);
                if (baseline != null)
                {
                    RenderTexture.ReleaseTemporary(baseline);
                }
                RenderTexture.ReleaseTemporary(withoutSelection);
                RenderTexture.ReleaseTemporary(mask);
            }
        }

        private void ForEachVisibleMaterialSlot(
            Action<Renderer, Material, int> visit)
        {
            foreach (var renderer in _renderers)
            {
                if (renderer == null || !_utility.IsRendererVisible(renderer) ||
                    !IsInScope(renderer) ||
                    _hiddenPartRenderers.Contains(renderer))
                {
                    continue;
                }
                Mesh mesh = null;
                var resolved = _utility.ResolveRenderer(renderer);
                if (!resolved.enabled) continue;
                if (resolved is SkinnedMeshRenderer skinned)
                {
                    mesh = skinned.sharedMesh;
                }
                else if (resolved is MeshRenderer meshRenderer)
                {
                    var filter = meshRenderer.GetComponent<MeshFilter>();
                    mesh = filter == null ? null : filter.sharedMesh;
                }
                if (mesh == null)
                {
                    continue;
                }
                var materials = renderer.sharedMaterials;
                var slotCount = Mathf.Min(mesh.subMeshCount,
                    materials.Length);
                for (var slot = 0; slot < slotCount; slot++)
                {
                    var material = materials[slot];
                    if (material != null &&
                        !_hiddenMaterials.Contains(material))
                    {
                        visit(renderer, material, slot);
                    }
                }
            }
        }

        private void ClearPickedSelection()
        {
            _pickedRenderer = null;
            _pickedMaterial = null;
            _outlinedRenderers.Clear();
            _outlinedMaterial = null;
            _outlineDirty = true;
            _selectionLabel.SetText(string.Empty);
            _selectionOverlay.style.display = DisplayStyle.None;
        }

        private void ClearSelectionFromPreview()
        {
            _requestedPartKey = null;
            _requestedMaterial = null;
            ClearPickedSelection();
            _viewport.RequestRepaint();
            EditorApplication.delayCall += () =>
                PreviewSelectionCleared?.Invoke();
        }

        private void RefreshSelectionAfterVisibilityChange()
        {
            if (_instance == null)
            {
                return;
            }
            if (_requestedPartKey != null || _requestedMaterial != null)
            {
                ApplyRequestedSelection();
                if (_outlinedRenderers.Count == 0)
                {
                    ClearSelectionFromPreview();
                }
                return;
            }
            if (_pickedRenderer != null &&
                (!_utility.IsRendererVisible(_pickedRenderer) ||
                 !IsInScope(_pickedRenderer) ||
                 _hiddenPartRenderers.Contains(_pickedRenderer) ||
                 _pickedMaterial != null &&
                 _hiddenMaterials.Contains(_pickedMaterial)))
            {
                ClearSelectionFromPreview();
            }
        }

        private string GetPreviewPartKey(Transform target)
        {
            var indices = new List<int>();
            var current = target;
            while (current != null && current != _instance.transform)
            {
                var parent = current.parent;
                if (parent == _instance.transform &&
                    _prefabSiblingIndices.Contains(current.GetSiblingIndex()))
                {
                    indices.Reverse();
                    return GetPartKey(current.GetSiblingIndex(), indices);
                }
                indices.Add(current.GetSiblingIndex());
                current = parent;
            }
            indices.Reverse();
            return GetPartKey(-1, indices);
        }
    }
}
