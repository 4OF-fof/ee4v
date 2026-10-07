using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nadena.dev.ndmf.preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Ee4v.Core.Preview
{
    public sealed partial class AvatarPreviewRenderer : IDisposable
    {
        public static bool IsPreviewScene(Scene scene) =>
            EditorSceneManager.IsPreviewScene(scene) || NDMFPreviewSceneManager.IsPreviewScene(scene);

        private readonly PreviewRenderUtility _utility;
        private readonly PreviewFilter _filter;
        private readonly Dictionary<Renderer, Renderer> _frameProxies = new Dictionary<Renderer, Renderer>();
        private readonly Dictionary<Renderer, Renderer> _resolved = new Dictionary<Renderer, Renderer>();
        private readonly Dictionary<Renderer, bool> _renderingStates = new Dictionary<Renderer, bool>();
        private readonly Dictionary<Renderer, Material[]> _materialStates = new Dictionary<Renderer, Material[]>();
        private readonly Dictionary<Transform, Vector3> _scales = new Dictionary<Transform, Vector3>();
        private readonly Dictionary<Transform, Quaternion> _rotations = new Dictionary<Transform, Quaternion>();
        private readonly Dictionary<Transform, Quaternion> _temporaryRotations = new Dictionary<Transform, Quaternion>();
        private readonly Dictionary<Transform, Vector3> _temporaryPositions = new Dictionary<Transform, Vector3>();
        private readonly Dictionary<GameObject, bool> _activeStates = new Dictionary<GameObject, bool>();
        private readonly Dictionary<Transform, Vector3> _temporaryScales = new Dictionary<Transform, Vector3>();
        private readonly Dictionary<GameObject, bool> _temporaryActiveStates = new Dictionary<GameObject, bool>();
        private readonly Dictionary<(SkinnedMeshRenderer, int), float> _temporaryShapes = new Dictionary<(SkinnedMeshRenderer, int), float>();
        private readonly Dictionary<SkinnedMeshRenderer, Dictionary<string, float>> _shapes =
            new Dictionary<SkinnedMeshRenderer, Dictionary<string, float>>();
        private PreviewSession _parentSession;
        private PreviewSession _session;
        private Renderer[] _sceneRenderers = Array.Empty<Renderer>();
        private Renderer[] _sourceRenderers = Array.Empty<Renderer>();
        private bool _hierarchyDirty = true;
        private bool _rendering;
        private bool _disposed;
        private readonly bool _ownsRoot;
        private Material _invisibleMaterial;
        private Func<Renderer, Material, int, bool> _includeSlot;

        public AvatarPreviewRenderer(GameObject source, bool isolatedSnapshot = false)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _utility = new PreviewRenderUtility();
            _filter = new PreviewFilter(this);
            Root = source;
            _ownsRoot = EditorUtility.IsPersistent(source) || isolatedSnapshot;
            if (_ownsRoot)
            {
                var staging = new GameObject("ee4v Asset Preview") { hideFlags = HideFlags.HideAndDontSave };
                staging.SetActive(false);
                _utility.AddSingleGO(staging);
                Root = Object.Instantiate(source, staging.transform, true);
                RefreshPoseBindings();
                foreach (var behaviour in Root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour != null) Object.DestroyImmediate(behaviour);
                foreach (var animator in Root.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                foreach (var renderer in Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    renderer.forceMatrixRecalculationPerRender = true;
                    renderer.updateWhenOffscreen = true;
                }
                Root.transform.SetParent(null, false);
                _utility.AddSingleGO(Root);
                Root.hideFlags = HideFlags.HideAndDontSave;
                Object.DestroyImmediate(staging);
                Root.SetActive(true);
            }
            RefreshHierarchy();
            Camera.transform.SetParent(null, false);
            Camera.clearFlags = CameraClearFlags.Color;
            Camera.backgroundColor = Color.clear;
            Camera.overrideSceneCullingMask = ulong.MaxValue;
            FieldOfView = 30f;
            _utility.lights[0].intensity = 1.1f;
            _utility.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            _utility.lights[1].intensity = 0.7f;
            Camera.onPreCull += OnPreCull;
            Camera.onPostRender += OnPostRender;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EnsureSession();
        }

        public GameObject Root { get; }
        public Camera Camera => _utility.camera;
        public float FieldOfView
        {
            get => _utility.cameraFieldOfView;
            set { _utility.cameraFieldOfView = value; Camera.fieldOfView = value; }
        }
        public Func<Renderer, bool> IsVisible { get; set; }
        public Func<Material, bool> IsMaterialVisible { get; set; }
        public bool UsesNdmf => !_ownsRoot && _session != null;

        public void RefreshHierarchy()
        {
            _sourceRenderers = Root == null ? Array.Empty<Renderer>() : Root.GetComponentsInChildren<Renderer>(true);
            if (!_ownsRoot && Root != null) RefreshPoseBindings();
            _hierarchyDirty = true;
        }

        public Renderer ResolveRenderer(Renderer source)
        {
            return source != null && _resolved.TryGetValue(source, out var proxy) && proxy != null ? proxy : source;
        }

        public Bounds GetBounds(Renderer source)
        {
            var renderer = ResolveRenderer(source);
            if (renderer == null) return new Bounds();
            var bounds = renderer.bounds;
            if (bounds.size.sqrMagnitude > 0.000001f) return bounds;
            Mesh mesh = null;
            if (renderer is SkinnedMeshRenderer skinned)
            {
                mesh = skinned.sharedMesh;
            }
            else if (renderer is MeshRenderer && renderer.TryGetComponent<MeshFilter>(out var filter))
            {
                mesh = filter.sharedMesh;
            }
            if (mesh == null) return bounds;
            var matrix = renderer.localToWorldMatrix;
            var local = mesh.bounds;
            var x = matrix.MultiplyVector(new Vector3(local.extents.x, 0, 0));
            var y = matrix.MultiplyVector(new Vector3(0, local.extents.y, 0));
            var z = matrix.MultiplyVector(new Vector3(0, 0, local.extents.z));
            var extents = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            return new Bounds(matrix.MultiplyPoint3x4(local.center), extents * 2);
        }

        public void SetTransformScale(Transform source, Vector3 value)
        {
            if (source == null) return;
            if (source.localScale == value) _scales.Remove(source);
            else _scales[source] = value;
        }

        public void SetBlendShapeWeight(SkinnedMeshRenderer source, string name, float value)
        {
            if (source == null) return;
            if (!_shapes.TryGetValue(source, out var shapes))
                _shapes[source] = shapes = new Dictionary<string, float>(StringComparer.Ordinal);
            shapes[name] = value;
        }

        public void SetPartActive(GameObject source, bool active)
        {
            if (source == null) return;
            if (source.activeSelf == active) _activeStates.Remove(source);
            else _activeStates[source] = active;
        }

        public bool IsRendererVisible(Renderer source)
        {
            var renderer = ResolveRenderer(source);
            return renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy &&
                (IsVisible == null || IsVisible(source));
        }

        public void ClearOverrides()
        {
            _scales.Clear();
            _rotations.Clear();
            _shapes.Clear();
            _activeStates.Clear();
        }

        public Texture Render(Rect rect, Func<Renderer, Material, int, bool> includeSlot = null)
        {
            if (_disposed || Root == null) return null;
            EnsureSession();
            _includeSlot = includeSlot;
            _utility.BeginPreview(rect, GUIStyle.none);
            var completed = false;
            try
            {
                CaptureRenderingStates();
                _frameProxies.Clear();
                _rendering = true;
                Camera.onPreCull -= OnPreCull;
                Camera.onPreCull += OnPreCull;
                Camera.onPostRender -= OnPostRender;
                Camera.onPostRender += OnPostRender;
                ApplySnapshotOverrides();
                Camera.Render();
                var texture = _utility.EndPreview();
                completed = true;
                return texture;
            }
            finally
            {
                _rendering = false;
                RestoreRenderingStates();
                _includeSlot = null;
                if (!completed) _utility.EndPreview();
            }
        }

        private void EnsureSession()
        {
            var parent = !EditorApplication.isPlayingOrWillChangePlaymode && !_ownsRoot
                ? PreviewSession.Current : null;
            var needsSession = !EditorApplication.isPlayingOrWillChangePlaymode;
            if (ReferenceEquals(parent, _parentSession) && (_session != null) == needsSession) return;
            _session?.Dispose();
            _session = null;
            _resolved.Clear();
            _parentSession = parent;
            if (!needsSession) return;
            _session = parent != null ? parent.Fork("ee4v Avatar Preview") : new PreviewSession();
            if (!_ownsRoot)
            {
                _session.HiddenRenderers = context => context.GetComponentsByType<Renderer>()
                    .Where(renderer => renderer != null && !renderer.transform.IsChildOf(Root.transform)).ToImmutableHashSet();
                _session.AddMutator(new SequencePoint { DebugString = "ee4v preview overrides" }, _filter);
            }
            _session.OverrideCamera(Camera);
        }

        private void OnHierarchyChanged() => _hierarchyDirty = true;

        private void CaptureRenderingStates()
        {
            if (_hierarchyDirty)
            {
                _sceneRenderers = Resources.FindObjectsOfTypeAll<Renderer>()
                    .Where(renderer => renderer != null && renderer.gameObject.scene.IsValid()).ToArray();
                _hierarchyDirty = false;
            }
            _renderingStates.Clear();
            foreach (var renderer in _sceneRenderers)
                if (renderer != null) _renderingStates[renderer] = renderer.forceRenderingOff;
        }

        private void ApplySnapshotOverrides()
        {
            if (_ownsRoot)
            {
                var poseMatrices = new Dictionary<Transform, Matrix4x4>();
                var attachments = _rotations.Count > 0 || _scales.Count > 0
                    ? _poseBindings.Keys.Where(source => source != null)
                        .ToDictionary(source => source, source => GetPoseLocalMatrix(source, poseMatrices))
                    : new Dictionary<Transform, Matrix4x4>();
                foreach (var pair in _activeStates)
                {
                    if (pair.Key == null) continue;
                    _temporaryActiveStates[pair.Key] = pair.Key.activeSelf;
                    pair.Key.SetActive(pair.Value);
                }
                foreach (var pair in _scales)
                {
                    if (pair.Key == null) continue;
                    _temporaryScales[pair.Key] = pair.Key.localScale;
                    pair.Key.localScale = pair.Value;
                }
                foreach (var pair in _rotations)
                {
                    if (pair.Key == null) continue;
                    _temporaryRotations[pair.Key] = pair.Key.localRotation;
                    pair.Key.localRotation = pair.Value;
                }
                foreach (var pair in attachments)
                {
                    var source = pair.Key;
                    _temporaryPositions[source] = source.localPosition;
                    if (!_temporaryRotations.ContainsKey(source)) _temporaryRotations[source] = source.localRotation;
                    if (!_temporaryScales.ContainsKey(source)) _temporaryScales[source] = source.localScale;
                    source.localPosition = pair.Value.GetColumn(3);
                    source.localRotation = pair.Value.rotation;
                    source.localScale = pair.Value.lossyScale;
                }
                foreach (var pair in _shapes)
                {
                    if (pair.Key == null || pair.Key.sharedMesh == null) continue;
                    foreach (var shape in pair.Value)
                    {
                        var index = pair.Key.sharedMesh.GetBlendShapeIndex(shape.Key);
                        if (index >= 0)
                        {
                            _temporaryShapes[(pair.Key, index)] = pair.Key.GetBlendShapeWeight(index);
                            pair.Key.SetBlendShapeWeight(index, shape.Value);
                        }
                    }
                }
            }
        }

        private void OnPreCull(Camera camera)
        {
            if (!_rendering || camera != Camera) return;
            _resolved.Clear();
            var visible = new HashSet<Renderer>();
            foreach (var original in _sourceRenderers)
            {
                if (original == null) continue;
                var renderer = _frameProxies.TryGetValue(original, out var proxy) && proxy != null &&
                    !proxy.forceRenderingOff ? proxy : original;
                _resolved[original] = renderer;
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    (IsVisible != null && !IsVisible(original))) continue;
                visible.Add(renderer);
                FilterMaterialSlots(original, renderer);
            }
            foreach (var renderer in _sceneRenderers)
            {
                if (renderer != null) renderer.forceRenderingOff = !visible.Contains(renderer);
            }
            foreach (var renderer in visible)
            {
                if (!_renderingStates.ContainsKey(renderer)) _renderingStates[renderer] = true;
                renderer.forceRenderingOff = false;
            }
        }

        private void FilterMaterialSlots(Renderer original, Renderer renderer)
        {
            var materials = renderer.sharedMaterials;
            var sourceMaterials = original.sharedMaterials;
            Material[] changed = null;
            for (var slot = 0; slot < materials.Length; slot++)
            {
                var source = slot < sourceMaterials.Length ? sourceMaterials[slot] : materials[slot];
                if ((IsMaterialVisible == null || IsMaterialVisible(source)) &&
                    (_includeSlot == null || _includeSlot(original, source, slot))) continue;
                if (changed == null) changed = (Material[])materials.Clone();
                changed[slot] = GetInvisibleMaterial();
            }
            if (changed == null) return;
            _materialStates[renderer] = materials;
            renderer.sharedMaterials = changed;
        }

        private Material GetInvisibleMaterial()
        {
            if (_invisibleMaterial != null) return _invisibleMaterial;
            _invisibleMaterial = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
            _invisibleMaterial.SetColor("_Color", Color.clear);
            _invisibleMaterial.SetInt("_SrcBlend", 0);
            _invisibleMaterial.SetInt("_DstBlend", 1);
            _invisibleMaterial.SetInt("_ZWrite", 0);
            _invisibleMaterial.SetInt("_ZTest", 8);
            _invisibleMaterial.SetInt("_ColorMask", 0);
            return _invisibleMaterial;
        }

        private void OnPostRender(Camera camera)
        {
            if (_rendering && camera == Camera) RestoreRenderingStates();
        }

        private void RestoreRenderingStates()
        {
            foreach (var pair in _temporaryActiveStates) if (pair.Key != null) pair.Key.SetActive(pair.Value);
            _temporaryActiveStates.Clear();
            foreach (var pair in _temporaryScales) if (pair.Key != null) pair.Key.localScale = pair.Value;
            _temporaryScales.Clear();
            foreach (var pair in _temporaryRotations) if (pair.Key != null) pair.Key.localRotation = pair.Value;
            _temporaryRotations.Clear();
            foreach (var pair in _temporaryPositions) if (pair.Key != null) pair.Key.localPosition = pair.Value;
            _temporaryPositions.Clear();
            foreach (var pair in _temporaryShapes) if (pair.Key.Item1 != null) pair.Key.Item1.SetBlendShapeWeight(pair.Key.Item2, pair.Value);
            _temporaryShapes.Clear();
            foreach (var pair in _materialStates)
                if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
            _materialStates.Clear();
            foreach (var pair in _renderingStates)
                if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
            _renderingStates.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Camera.onPreCull -= OnPreCull;
            Camera.onPostRender -= OnPostRender;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            RestoreRenderingStates();
            _session?.Dispose();
            PreviewSession.ClearCameraOverride(Camera);
            if (_invisibleMaterial != null) Object.DestroyImmediate(_invisibleMaterial);
            if (_ownsRoot && Root != null) Object.DestroyImmediate(Root);
            _utility.Cleanup();
        }

    }
}
