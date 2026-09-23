using System;
using System.Collections.Generic;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ee4v.PhysBoneCollider
{
    internal sealed class PhysBoneColliderPreview : IDisposable
    {
        private static readonly int ControlHash =
            nameof(PhysBoneColliderPreview).GetHashCode();

        private readonly Action _repaint;
        private readonly PreviewOrbitController _orbit;
        private readonly List<PreviewCapsule> _capsules = new List<PreviewCapsule>();
        private IReadOnlyList<PhysBoneTarget> _physBones =
            Array.Empty<PhysBoneTarget>();
        private PreviewRenderUtility _utility;
        private GameObject _clone;
        private Material _normalMaterial;
        private Material _selectedMaterial;
        private Material _physBoneMaterial;
        private Material _selectedPhysBoneMaterial;
        private Mesh _physBoneMesh;
        private Mesh _selectedPhysBoneMesh;
        private MeshRenderer _physBoneRenderer;
        private MeshRenderer _selectedPhysBoneRenderer;
        private bool _showColliders = true;
        private bool _showPhysBones = true;
        internal PhysBoneColliderPreview(Action repaint)
        {
            _repaint = repaint;
            _orbit = new PreviewOrbitController(ControlHash, repaint);
        }

        internal void SetAvatar(
            GameObject avatar,
            IReadOnlyList<PhysBoneColliderDraft> drafts,
            IReadOnlyList<PhysBoneTarget> physBones,
            int selectedIndex,
            string selectedPhysBonePath)
        {
            Cleanup();
            if (avatar == null)
            {
                return;
            }

            _utility = new PreviewRenderUtility();
            _utility.cameraFieldOfView = 30f;
            _utility.camera.clearFlags = CameraClearFlags.Color;
            _utility.camera.backgroundColor = Color.clear;
            _utility.lights[0].intensity = 1.1f;
            _utility.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            _utility.lights[1].intensity = 0.7f;
            _clone = UnityEngine.Object.Instantiate(avatar);
            _clone.name = avatar.name + " (PhysBone Collider Preview)";
            SetHideFlags(_clone.transform);
            CreatePreviewAssets();
            _physBones = physBones ?? Array.Empty<PhysBoneTarget>();

            for (var index = 0; index < drafts.Count; index++)
            {
                var draft = drafts[index];
                var bone = _clone.transform.Find(draft.Path);
                if (bone == null)
                {
                    continue;
                }

                var proxy = new GameObject("Collider Preview");
                proxy.hideFlags = HideFlags.HideAndDontSave;
                proxy.transform.SetParent(bone, false);
                var filter = proxy.AddComponent<MeshFilter>();
                var mesh = new Mesh
                {
                    name = "PhysBone Collider Preview Capsule",
                    hideFlags = HideFlags.HideAndDontSave
                };
                filter.sharedMesh = mesh;
                var renderer = proxy.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _normalMaterial;
                _capsules.Add(new PreviewCapsule(
                    index,
                    bone,
                    proxy.transform,
                    renderer,
                    mesh));
            }

            CreatePhysBonePreview();

            _utility.AddSingleGO(_clone);
            Update(drafts, selectedIndex, selectedPhysBonePath);
            ResetView();
        }

        internal void Update(
            IReadOnlyList<PhysBoneColliderDraft> drafts,
            int selectedIndex,
            string selectedPhysBonePath)
        {
            if (_clone == null)
            {
                return;
            }

            foreach (var capsule in _capsules)
            {
                if (capsule.Index < 0 || capsule.Index >= drafts.Count)
                {
                    continue;
                }

                var draft = drafts[capsule.Index];
                capsule.SourceEnabled = draft.Enabled;
                capsule.Renderer.enabled = _showColliders && draft.Enabled;
                capsule.Renderer.sharedMaterial = capsule.Index == selectedIndex
                    ? _selectedMaterial
                    : _normalMaterial;
                capsule.Transform.localPosition = draft.Position;
                capsule.Transform.localRotation = draft.Rotation;
                var scale = capsule.Bone.lossyScale;
                capsule.Transform.localScale = new Vector3(
                    1f / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                    1f / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                    1f / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
                UpdateWireCapsuleMesh(
                    capsule.Mesh,
                    Mathf.Max(0.001f, draft.Radius),
                    Mathf.Max(draft.Radius * 2f, draft.Height),
                    capsule.Index == selectedIndex);
            }

            UpdatePhysBonePreview(
                drafts,
                selectedIndex,
                selectedPhysBonePath);
            _repaint?.Invoke();
        }

        internal void SetVisibility(bool showColliders, bool showPhysBones)
        {
            _showColliders = showColliders;
            _showPhysBones = showPhysBones;
            foreach (var capsule in _capsules)
            {
                capsule.Renderer.enabled = showColliders && capsule.SourceEnabled;
            }

            if (_physBoneRenderer != null)
            {
                _physBoneRenderer.enabled = showPhysBones;
            }

            if (_selectedPhysBoneRenderer != null)
            {
                _selectedPhysBoneRenderer.enabled = showPhysBones;
            }

            _repaint?.Invoke();
        }

        internal void ResetView()
        {
            if (_clone == null)
            {
                return;
            }

            var bounds = CalculateBounds();
            var radius = Mathf.Max(0.05f, bounds.extents.magnitude);
            _orbit.Reset(
                bounds.center,
                radius / Mathf.Tan(15f * Mathf.Deg2Rad) * 1.1f);
        }

        internal void Draw(Rect rect)
        {
            if (_utility == null || _clone == null || rect.width < 2f || rect.height < 2f)
            {
                return;
            }

            _orbit.HandleInput(
                rect,
                _utility.camera,
                _utility.cameraFieldOfView);
            if (Event.current == null ||
                Event.current.type != EventType.Repaint)
            {
                return;
            }

            ConfigureCamera();
            _utility.BeginPreview(rect, GUIStyle.none);
            _utility.camera.Render();
            var texture = _utility.EndPreview();
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
        }

        public void Dispose()
        {
            Cleanup();
        }

        private void ConfigureCamera()
        {
            _orbit.ConfigureCamera(
                _utility.camera,
                _clone.transform.rotation);
        }

        private Bounds CalculateBounds()
        {
            var hasBounds = false;
            var bounds = new Bounds(_clone.transform.position, Vector3.one * 0.2f);
            foreach (var renderer in _clone.GetComponentsInChildren<Renderer>(true))
            {
                if (_capsules.Exists(capsule => capsule.Renderer == renderer))
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

        private void CreatePreviewAssets()
        {
            _normalMaterial = CreateMaterial(new Color(0.1f, 0.85f, 1f, 0.82f));
            _selectedMaterial = CreateMaterial(new Color(1f, 0.72f, 0.1f, 1f));
            _physBoneMaterial = CreateMaterial(new Color(1f, 0.25f, 0.72f, 0.92f));
            _selectedPhysBoneMaterial = CreateMaterial(
                new Color(0.45f, 1f, 0.35f, 1f));
        }

        private void CreatePhysBonePreview()
        {
            CreatePhysBonePreviewRenderer(
                "PhysBone Preview",
                _physBoneMaterial,
                out _physBoneMesh,
                out _physBoneRenderer);
            CreatePhysBonePreviewRenderer(
                "Selected PhysBone Preview",
                _selectedPhysBoneMaterial,
                out _selectedPhysBoneMesh,
                out _selectedPhysBoneRenderer);
        }

        private void CreatePhysBonePreviewRenderer(
            string name,
            Material material,
            out Mesh mesh,
            out MeshRenderer meshRenderer)
        {
            var holder = new GameObject(name);
            holder.hideFlags = HideFlags.HideAndDontSave;
            holder.transform.SetParent(_clone.transform, false);
            var filter = holder.AddComponent<MeshFilter>();
            mesh = new Mesh
            {
                name = name + " Lines",
                hideFlags = HideFlags.HideAndDontSave
            };
            filter.sharedMesh = mesh;
            meshRenderer = holder.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.enabled = _showPhysBones;
        }

        private void UpdatePhysBonePreview(
            IReadOnlyList<PhysBoneColliderDraft> drafts,
            int selectedIndex,
            string selectedPhysBonePath)
        {
            if (_physBoneMesh == null || _selectedPhysBoneMesh == null)
            {
                return;
            }

            var paths = new HashSet<string>(StringComparer.Ordinal);
            var selectedPaths = new HashSet<string>(StringComparer.Ordinal);
            if (selectedIndex >= 0 && selectedIndex < drafts.Count)
            {
                var assignments = drafts[selectedIndex].AssignedPhysBonePaths;
                foreach (var target in _physBones)
                {
                    if (!assignments.Contains(target.Path))
                    {
                        continue;
                    }

                    if (string.Equals(
                            target.Path,
                            selectedPhysBonePath,
                            StringComparison.Ordinal))
                    {
                        selectedPaths.UnionWith(target.TransformPaths);
                    }
                    else
                    {
                        paths.UnionWith(target.TransformPaths);
                    }
                }
            }

            UpdatePhysBoneMesh(_physBoneMesh, paths);
            UpdatePhysBoneMesh(_selectedPhysBoneMesh, selectedPaths);
        }

        private void UpdatePhysBoneMesh(Mesh mesh, ISet<string> paths)
        {
            const float markerSize = 0.008f;
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            foreach (var path in paths)
            {
                var transform = string.IsNullOrEmpty(path)
                    ? _clone.transform
                    : _clone.transform.Find(path);
                if (transform == null)
                {
                    continue;
                }

                var position = _clone.transform.InverseTransformPoint(transform.position);
                AddCross(vertices, indices, position, markerSize);
                if (transform == _clone.transform)
                {
                    continue;
                }

                var parentPath = AnimationUtility.CalculateTransformPath(
                    transform.parent,
                    _clone.transform);
                if (paths.Contains(parentPath))
                {
                    AddLine(
                        vertices,
                        indices,
                        _clone.transform.InverseTransformPoint(transform.parent.position),
                        position);
                }
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
        }

        private static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Hidden/Internal-Colored") ??
                         Shader.Find("Unlit/Color") ??
                         Shader.Find("Standard");
            var material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = (int)RenderQueue.Overlay
            };
            material.SetColor("_Color", color);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_Cull", (int)CullMode.Off);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_ZTest", (int)CompareFunction.Always);
            return material;
        }

        private static void UpdateWireCapsuleMesh(
            Mesh mesh,
            float radius,
            float height,
            bool showCenterMarker)
        {
            const int segments = 32;
            var halfLine = Mathf.Max(0f, height * 0.5f - radius);
            var vertices = new List<Vector3>(segments * 6 + 8);
            var indices = new List<int>(segments * 6 + 8);

            AddRing(vertices, indices, segments, radius, halfLine);
            AddRing(vertices, indices, segments, radius, -halfLine);
            AddCap(vertices, indices, segments, radius, halfLine, true, true);
            AddCap(vertices, indices, segments, radius, halfLine, true, false);
            AddCap(vertices, indices, segments, radius, -halfLine, false, true);
            AddCap(vertices, indices, segments, radius, -halfLine, false, false);

            AddLine(vertices, indices,
                new Vector3(radius, -halfLine, 0f),
                new Vector3(radius, halfLine, 0f));
            AddLine(vertices, indices,
                new Vector3(-radius, -halfLine, 0f),
                new Vector3(-radius, halfLine, 0f));
            AddLine(vertices, indices,
                new Vector3(0f, -halfLine, radius),
                new Vector3(0f, halfLine, radius));
            AddLine(vertices, indices,
                new Vector3(0f, -halfLine, -radius),
                new Vector3(0f, halfLine, -radius));

            if (showCenterMarker)
            {
                var markerSize = Mathf.Max(radius * 1.35f, height * 0.04f);
                AddLine(vertices, indices,
                    Vector3.left * markerSize,
                    Vector3.right * markerSize);
                AddLine(vertices, indices,
                    Vector3.down * markerSize,
                    Vector3.up * markerSize);
                AddLine(vertices, indices,
                    Vector3.back * markerSize,
                    Vector3.forward * markerSize);
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
        }

        private static void AddRing(
            ICollection<Vector3> vertices,
            ICollection<int> indices,
            int segments,
            float radius,
            float y)
        {
            var offset = vertices.Count;
            for (var index = 0; index < segments; index++)
            {
                var angle = index * Mathf.PI * 2f / segments;
                vertices.Add(new Vector3(
                    Mathf.Cos(angle) * radius,
                    y,
                    Mathf.Sin(angle) * radius));
                indices.Add(offset + index);
                indices.Add(offset + (index + 1) % segments);
            }
        }

        private static void AddCap(
            ICollection<Vector3> vertices,
            ICollection<int> indices,
            int segments,
            float radius,
            float centerY,
            bool upper,
            bool xyPlane)
        {
            var offset = vertices.Count;
            var capSegments = segments / 2;
            for (var index = 0; index <= capSegments; index++)
            {
                var angle = index * Mathf.PI / capSegments;
                var horizontal = Mathf.Cos(angle) * radius;
                var vertical = Mathf.Sin(angle) * radius * (upper ? 1f : -1f);
                vertices.Add(xyPlane
                    ? new Vector3(horizontal, centerY + vertical, 0f)
                    : new Vector3(0f, centerY + vertical, horizontal));
                if (index > 0)
                {
                    indices.Add(offset + index - 1);
                    indices.Add(offset + index);
                }
            }
        }

        private static void AddLine(
            ICollection<Vector3> vertices,
            ICollection<int> indices,
            Vector3 from,
            Vector3 to)
        {
            var offset = vertices.Count;
            vertices.Add(from);
            vertices.Add(to);
            indices.Add(offset);
            indices.Add(offset + 1);
        }

        private static void AddCross(
            ICollection<Vector3> vertices,
            ICollection<int> indices,
            Vector3 center,
            float size)
        {
            AddLine(vertices, indices, center + Vector3.left * size, center + Vector3.right * size);
            AddLine(vertices, indices, center + Vector3.down * size, center + Vector3.up * size);
            AddLine(vertices, indices, center + Vector3.back * size, center + Vector3.forward * size);
        }

        private static void SetHideFlags(Transform transform)
        {
            transform.gameObject.hideFlags = HideFlags.HideAndDontSave;
            for (var index = 0; index < transform.childCount; index++)
            {
                SetHideFlags(transform.GetChild(index));
            }
        }

        private void Cleanup()
        {
            _orbit.CancelInteraction();
            foreach (var capsule in _capsules)
            {
                UnityEngine.Object.DestroyImmediate(capsule.Mesh);
            }

            _capsules.Clear();
            if (_utility != null)
            {
                _utility.Cleanup();
                _utility = null;
            }

            if (_clone != null)
            {
                UnityEngine.Object.DestroyImmediate(_clone);
                _clone = null;
            }

            UnityEngine.Object.DestroyImmediate(_normalMaterial);
            UnityEngine.Object.DestroyImmediate(_selectedMaterial);
            UnityEngine.Object.DestroyImmediate(_physBoneMaterial);
            UnityEngine.Object.DestroyImmediate(_selectedPhysBoneMaterial);
            UnityEngine.Object.DestroyImmediate(_physBoneMesh);
            UnityEngine.Object.DestroyImmediate(_selectedPhysBoneMesh);
            _normalMaterial = null;
            _selectedMaterial = null;
            _physBoneMaterial = null;
            _selectedPhysBoneMaterial = null;
            _physBoneMesh = null;
            _selectedPhysBoneMesh = null;
            _physBoneRenderer = null;
            _selectedPhysBoneRenderer = null;
            _physBones = Array.Empty<PhysBoneTarget>();
        }

        private sealed class PreviewCapsule
        {
            internal PreviewCapsule(
                int index,
                Transform bone,
                Transform transform,
                MeshRenderer renderer,
                Mesh mesh)
            {
                Index = index;
                Bone = bone;
                Transform = transform;
                Renderer = renderer;
                Mesh = mesh;
            }

            internal int Index { get; }

            internal Transform Bone { get; }

            internal Transform Transform { get; }

            internal MeshRenderer Renderer { get; }

            internal Mesh Mesh { get; }

            internal bool SourceEnabled { get; set; }
        }
    }
}
