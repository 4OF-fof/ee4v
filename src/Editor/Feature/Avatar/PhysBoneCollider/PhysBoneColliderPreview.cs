using System;
using System.Collections.Generic;
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
        private readonly List<PreviewCapsule> _capsules = new List<PreviewCapsule>();
        private PreviewRenderUtility _utility;
        private GameObject _clone;
        private Material _normalMaterial;
        private Material _selectedMaterial;
        private Vector3 _target;
        private float _distance = 1f;
        private float _yaw;
        private float _pitch;
        private int _dragButton = -1;

        internal PhysBoneColliderPreview(Action repaint)
        {
            _repaint = repaint;
        }

        internal void SetAvatar(
            GameObject avatar,
            IReadOnlyList<PhysBoneColliderDraft> drafts,
            int selectedIndex)
        {
            Cleanup();
            if (avatar == null)
            {
                return;
            }

            _utility = new PreviewRenderUtility();
            _utility.cameraFieldOfView = 30f;
            _utility.lights[0].intensity = 1.1f;
            _utility.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            _utility.lights[1].intensity = 0.7f;
            _clone = UnityEngine.Object.Instantiate(avatar);
            _clone.name = avatar.name + " (PhysBone Collider Preview)";
            SetHideFlags(_clone.transform);
            CreatePreviewAssets();

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

            _utility.AddSingleGO(_clone);
            Update(drafts, selectedIndex);
            ResetView();
        }

        internal void Update(
            IReadOnlyList<PhysBoneColliderDraft> drafts,
            int selectedIndex)
        {
            foreach (var capsule in _capsules)
            {
                if (capsule.Index < 0 || capsule.Index >= drafts.Count)
                {
                    continue;
                }

                var draft = drafts[capsule.Index];
                capsule.Renderer.enabled = draft.Enabled;
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

            _repaint?.Invoke();
        }

        internal void ResetView()
        {
            if (_clone == null)
            {
                return;
            }

            var bounds = CalculateBounds();
            _target = bounds.center;
            var radius = Mathf.Max(0.05f, bounds.extents.magnitude);
            _distance = radius / Mathf.Tan(15f * Mathf.Deg2Rad) * 1.1f;
            _yaw = 0f;
            _pitch = 0f;
            _repaint?.Invoke();
        }

        internal void Draw(Rect rect)
        {
            if (_utility == null || _clone == null || rect.width < 2f || rect.height < 2f)
            {
                EditorGUI.DrawRect(rect, new Color(0.08f, 0.09f, 0.1f, 1f));
                return;
            }

            HandleInput(rect);
            ConfigureCamera();
            _utility.BeginPreview(rect, GUIStyle.none);
            _utility.camera.Render();
            var texture = _utility.EndPreview();
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
        }

        public void Dispose()
        {
            Cleanup();
        }

        private void HandleInput(Rect rect)
        {
            var current = Event.current;
            if (current == null)
            {
                return;
            }

            var controlId = GUIUtility.GetControlID(ControlHash, FocusType.Passive, rect);
            if (current.type == EventType.MouseDown &&
                rect.Contains(current.mousePosition) &&
                (current.button == 1 || current.button == 2))
            {
                GUIUtility.hotControl = controlId;
                _dragButton = current.button;
                current.Use();
                return;
            }

            if (current.type == EventType.MouseDrag && GUIUtility.hotControl == controlId)
            {
                if (_dragButton == 1)
                {
                    _yaw += current.delta.x * 0.5f;
                    _pitch = Mathf.Clamp(_pitch - current.delta.y * 0.5f, -80f, 80f);
                }
                else if (_dragButton == 2)
                {
                    var unitsPerPixel = 2f * _distance *
                        Mathf.Tan(_utility.cameraFieldOfView * 0.5f * Mathf.Deg2Rad) /
                        Mathf.Max(1f, rect.height);
                    _target +=
                        -_utility.camera.transform.right * current.delta.x * unitsPerPixel +
                        _utility.camera.transform.up * current.delta.y * unitsPerPixel;
                }

                current.Use();
                _repaint?.Invoke();
                return;
            }

            if (current.type == EventType.MouseUp &&
                GUIUtility.hotControl == controlId &&
                current.button == _dragButton)
            {
                GUIUtility.hotControl = 0;
                _dragButton = -1;
                current.Use();
                return;
            }

            if (current.type == EventType.ScrollWheel && rect.Contains(current.mousePosition))
            {
                _distance = Mathf.Clamp(
                    _distance * (1f + current.delta.y * 0.05f),
                    0.03f,
                    100f);
                current.Use();
                _repaint?.Invoke();
            }
        }

        private void ConfigureCamera()
        {
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var direction = _clone.transform.rotation * rotation * Vector3.forward;
            var up = _clone.transform.rotation * rotation * Vector3.up;
            _utility.camera.transform.position = _target + direction * _distance;
            _utility.camera.transform.rotation = Quaternion.LookRotation(-direction, up);
            _utility.camera.nearClipPlane = Mathf.Max(0.001f, _distance * 0.01f);
            _utility.camera.farClipPlane = Mathf.Max(100f, _distance * 20f);
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
            _normalMaterial = null;
            _selectedMaterial = null;
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
        }
    }
}
