using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class DerivedAssetPrefabSelector : NavigationItem
    {
        private readonly IReadOnlyList<GameObject> _candidates;
        private readonly HashSet<string> _candidatePaths;
        private GameObject _value;

        internal DerivedAssetPrefabSelector(
            IReadOnlyList<GameObject> candidates)
            : base(onClick: null)
        {
            _candidates = candidates ?? Array.Empty<GameObject>();
            _candidatePaths = new HashSet<string>(
                _candidates
                    .Where(candidate => candidate != null)
                    .Select(AssetDatabase.GetAssetPath),
                StringComparer.OrdinalIgnoreCase);
            AddToClassList(
                "ee4v-asset-manager__prefab-selector");
            AddToClassList(
                "ee4v-asset-manager__prefab-selector-trigger");
            clicked += OpenPopup;
            Trailing.Add(new Icon(
                AssetManagerControls.LoadFluentIconState(
                    "chevron_down.png",
                    UiSizeTokens.Size12)));

            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);
            SetEnabled(_candidates.Count > 0);
            Refresh();
        }

        internal event Action<GameObject> ValueChanged;

        internal event Action SelectionRejected;

        internal GameObject Value => _value;

        internal void SetValueWithoutNotify(GameObject prefab)
        {
            if (!Accepts(prefab))
            {
                return;
            }

            _value = prefab;
            Refresh();
        }

        private void OpenPopup()
        {
            DerivedAssetPrefabPickerWindow.Show(
                this,
                _candidates,
                _value,
                SetValue);
        }

        private void SetValue(GameObject prefab)
        {
            if (!Accepts(prefab))
            {
                SelectionRejected?.Invoke();
                return;
            }

            if (_value == prefab)
            {
                return;
            }

            _value = prefab;
            Refresh();
            ValueChanged?.Invoke(_value);
        }

        private void Refresh()
        {
            var path = AssetDatabase.GetAssetPath(_value);
            SetState(new NavigationItemState(
                _value != null
                    ? _value.name
                    : I18N.Get("detail.derivedAssetPrefabSelect"),
                path));
            tooltip = path;
        }

        private bool Accepts(GameObject prefab)
        {
            return prefab != null &&
                   _candidatePaths.Contains(
                       AssetDatabase.GetAssetPath(prefab));
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            DragAndDrop.visualMode = Accepts(GetDraggedPrefab())
                ? DragAndDropVisualMode.Link
                : DragAndDropVisualMode.Rejected;
            evt.StopPropagation();
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            var prefab = GetDraggedPrefab();
            if (Accepts(prefab))
            {
                DragAndDrop.AcceptDrag();
                SetValue(prefab);
            }
            else
            {
                SelectionRejected?.Invoke();
            }

            evt.StopPropagation();
        }

        private static GameObject GetDraggedPrefab()
        {
            return DragAndDrop.objectReferences
                .OfType<GameObject>()
                .FirstOrDefault();
        }
    }

    internal sealed class DerivedAssetPrefabPreview : PreviewContainer
    {
        private const int MaximumRefreshAttempts = 50;
        private const long RefreshIntervalMilliseconds = 100;

        private readonly Image _image;
        private IVisualElementScheduledItem _refreshItem;
        private GameObject _prefab;
        private int _refreshAttempts;

        internal DerivedAssetPrefabPreview(GameObject prefab)
        {
            AddToClassList(
                "ee4v-asset-manager__prefab-preview");
            AddToClassList(
                "ee4v-asset-manager__prefab-preview-container");
            pickingMode = PickingMode.Ignore;

            _image = new Image
            {
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            _image.AddToClassList(
                "ee4v-asset-manager__prefab-preview-image");
            Placeholder.Add(new Icon(
                AssetManagerControls.LoadFluentIconState(
                    "cube.png",
                    UiSizeTokens.Size31,
                    tintColor: UiColorTokens.TextMuted)));
            Content.Add(_image);

            RegisterCallback<AttachToPanelEvent>(_ => Refresh());
            RegisterCallback<DetachFromPanelEvent>(_ => StopRefreshing());
            SetPrefab(prefab);
        }

        internal void SetPrefab(GameObject prefab)
        {
            if (_prefab == prefab && prefab != null)
            {
                return;
            }

            _prefab = prefab;
            _refreshAttempts = 0;
            StopRefreshing();
            style.display = prefab != null
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            Refresh();
        }

        private void Refresh()
        {
            if (_prefab == null)
            {
                SetTexture(null);
                return;
            }

            var preview = AssetPreview.GetAssetPreview(_prefab);
            if (preview != null)
            {
                SetTexture(preview);
                StopRefreshing();
                return;
            }

            SetTexture(AssetPreview.GetMiniThumbnail(_prefab));
            if (panel != null && _refreshItem == null)
            {
                _refreshItem = schedule.Execute(PollPreview)
                    .Every(RefreshIntervalMilliseconds);
            }
        }

        private void PollPreview()
        {
            _refreshAttempts++;
            var preview = _prefab != null
                ? AssetPreview.GetAssetPreview(_prefab)
                : null;
            if (preview != null)
            {
                SetTexture(preview);
                StopRefreshing();
                return;
            }

            if (_refreshAttempts >= MaximumRefreshAttempts)
            {
                StopRefreshing();
            }
        }

        private void SetTexture(Texture texture)
        {
            _image.image = texture;
            SetHasContent(texture != null);
        }

        private void StopRefreshing()
        {
            _refreshItem?.Pause();
            _refreshItem = null;
        }
    }

    internal sealed class DerivedAssetPrefabScenePreview
        : VisualElement, IDisposable
    {
        private sealed class MaterialPreviewTarget
        {
            internal Renderer Renderer { get; set; }
            internal Mesh Mesh { get; set; }
            internal int SubMeshIndex { get; set; }
            internal Material Material { get; set; }
        }

        private sealed class BlendShapePreviewTarget
        {
            internal BlendShapePreviewTarget(
                SkinnedMeshRenderer renderer,
                int shapeIndex)
            {
                Renderer = renderer;
                ShapeIndex = shapeIndex;
            }

            internal SkinnedMeshRenderer Renderer { get; }
            internal int ShapeIndex { get; }
        }

        private static readonly int PreviewControlHash =
            nameof(DerivedAssetPrefabScenePreview).GetHashCode();
        private const float PreviewFitPadding = 1.05f;
        private const float MinimumPreviewSize = 320f;
        private const float MaximumPreviewSize = 560f;

        private readonly ScenePreviewViewport _viewport;
        private readonly PreviewOrbitController _orbit;
        private readonly Dictionary<Transform, Vector3>
            _pendingTransformScales =
                new Dictionary<Transform, Vector3>();
        private readonly Dictionary<BlendShapePreviewTarget, float>
            _pendingBlendShapeWeights =
                new Dictionary<BlendShapePreviewTarget, float>();
        private readonly List<MaterialPreviewTarget> _materialTargets =
            new List<MaterialPreviewTarget>();
        private readonly Dictionary<string, Transform> _transformTargets =
            new Dictionary<string, Transform>(StringComparer.Ordinal);
        private readonly Dictionary<string,
            Dictionary<string, BlendShapePreviewTarget>>
            _blendShapeTargets =
                new Dictionary<string,
                    Dictionary<string, BlendShapePreviewTarget>>(
                        StringComparer.Ordinal);
        private readonly List<Renderer> _filteredRenderers =
            new List<Renderer>();
        private readonly Dictionary<SkinnedMeshRenderer, Mesh>
            _bakedMeshes =
                new Dictionary<SkinnedMeshRenderer, Mesh>();
        private readonly List<Renderer> _temporarilyHiddenRenderers =
            new List<Renderer>();
        private PreviewRenderUtility _utility;
        private GameObject _prefab;
        private GameObject _instance;
        private Texture _previewTexture;
        private Vector2 _previewTextureSize;
        private Renderer[] _renderers = Array.Empty<Renderer>();
        private SkinnedMeshRenderer[] _skinnedRenderers =
            Array.Empty<SkinnedMeshRenderer>();
        private readonly HashSet<Material> _hiddenMaterials =
            new HashSet<Material>();
        private Bounds _bounds;
        private bool _pendingBoundsRefresh;
        private bool _bakedMeshesDirty;
        private bool _forceSkinningRecalculation;
        private bool _previewDirty = true;
        private bool _flexibleLayout;

        internal DerivedAssetPrefabScenePreview()
        {
            AddToClassList(
                "ee4v-asset-manager__prefab-scene-preview");

            _viewport = new ScenePreviewViewport(
                DrawPreview,
                ResetView,
                I18N.Get("detail.derivedAssetPreviewBackground"),
                I18N.Get("detail.derivedAssetPreviewReset"));
            _viewport.AddToClassList(
                "ee4v-asset-manager__prefab-scene-preview-viewport");
            _orbit = new PreviewOrbitController(
                PreviewControlHash,
                RequestPreviewRepaint);
            Add(_viewport);
            SetPreviewAvailable(false);

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                RebuildPreview();
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                CleanupPreview();
            });
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        internal void SetPrefab(GameObject prefab)
        {
            if (_prefab == prefab && _instance != null)
            {
                return;
            }

            _prefab = prefab;
            RebuildPreview();
        }

        internal void RefreshPreview()
        {
            RequestPreviewRepaint();
        }

        internal void ReloadPrefab()
        {
            RebuildPreview();
        }

        internal void SetTransformScales(
            IReadOnlyDictionary<string, Vector3> scales,
            bool recalculateBounds = true)
        {
            if (_instance == null || scales == null)
            {
                return;
            }

            foreach (var pair in scales)
            {
                if (!_transformTargets.TryGetValue(
                        pair.Key ?? string.Empty,
                        out var target) ||
                    target == null)
                {
                    continue;
                }

                _pendingTransformScales[target] = pair.Value;
            }

            _pendingBoundsRefresh |= recalculateBounds;
            RequestPreviewRepaint();
        }

        internal void SetBlendShapeWeight(
            string rendererPath,
            string shapeName,
            float weight,
            bool recalculateBounds = true)
        {
            if (_instance == null || string.IsNullOrEmpty(shapeName))
            {
                return;
            }

            if (!_blendShapeTargets.TryGetValue(
                    rendererPath ?? string.Empty,
                    out var shapes) ||
                !shapes.TryGetValue(shapeName, out var target) ||
                target.Renderer == null)
            {
                return;
            }

            _pendingBlendShapeWeights[target] = weight;
            _pendingBoundsRefresh |= recalculateBounds;
            RequestPreviewRepaint();
        }

        internal void FlushUpdates(bool recalculateBounds)
        {
            _pendingBoundsRefresh |= recalculateBounds;
            ApplyPendingUpdates();
            RequestPreviewRepaint();
        }

        internal void SetHiddenMaterials(
            IEnumerable<Material> materials)
        {
            var next = materials == null
                ? new HashSet<Material>()
                : new HashSet<Material>(
                    materials.Where(material => material != null));
            if (_hiddenMaterials.SetEquals(next))
            {
                return;
            }

            _hiddenMaterials.Clear();
            _hiddenMaterials.UnionWith(next);
            RebuildMaterialTargets();
            RequestPreviewRepaint();
        }

        internal void SetFlexibleLayout(bool flexible)
        {
            _flexibleLayout = flexible;
        }

        public void Dispose()
        {
            CleanupPreview();
            _viewport.Dispose();
        }

        private void RebuildPreview()
        {
            CleanupPreview();
            if (_prefab == null || panel == null)
            {
                return;
            }

            try
            {
                _instance = UnityEngine.Object.Instantiate(_prefab);
                _instance.name =
                    _prefab.name + " (Derived Asset Preview)";
                SetHideFlags(_instance.transform);
                _renderers = _instance
                    .GetComponentsInChildren<Renderer>(true);
                _skinnedRenderers = _renderers
                    .OfType<SkinnedMeshRenderer>()
                    .ToArray();
                _forceSkinningRecalculation = true;
                SetSkinningRecalculation(true);
                RebuildPreviewTargets();

                _utility = new PreviewRenderUtility();
                _utility.cameraFieldOfView = 30f;
                _utility.camera.clearFlags = CameraClearFlags.Color;
                _utility.camera.backgroundColor = Color.clear;
                _utility.lights[0].intensity = 1.1f;
                _utility.lights[0].transform.rotation =
                    Quaternion.Euler(35f, 35f, 0f);
                _utility.lights[1].intensity = 0.7f;
                _utility.AddSingleGO(_instance);
                _bounds = CalculateBounds(_instance);
                RebuildMaterialTargets();
                SetPreviewAvailable(true);
                ResetView();
            }
            catch (Exception exception)
            {
                CleanupPreview();
                Debug.LogException(exception);
            }
        }

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
                return;
            }
            ConfigureCamera();
            var forceSkinning = _forceSkinningRecalculation;
            _utility.BeginPreview(rect, GUIStyle.none);
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
                if (forceSkinning)
                {
                    SetSkinningRecalculation(false);
                    _forceSkinningRecalculation = false;
                }
            }
            _previewTexture = _utility.EndPreview();
            _previewTextureSize = previewSize;
            _previewDirty = false;
            GUI.DrawTexture(
                rect,
                _previewTexture,
                ScaleMode.StretchToFill,
                true);
        }

        internal void ResetView()
        {
            if (_instance == null)
            {
                return;
            }

            var previewRect = _viewport.PreviewRect;
            var aspect = previewRect.height > 1f
                ? previewRect.width / previewRect.height
                : 1f;
            var halfViewSize = Mathf.Max(
                _bounds.extents.y,
                _bounds.extents.x / Mathf.Max(0.01f, aspect));
            var distance = _bounds.extents.z +
                Mathf.Max(0.05f, halfViewSize) /
                Mathf.Tan(_utility.cameraFieldOfView * 0.5f *
                          Mathf.Deg2Rad) * PreviewFitPadding;
            _orbit.Reset(_bounds.center, distance);
            RequestPreviewRepaint();
        }

        private void ConfigureCamera()
        {
            _orbit.ConfigureCamera(
                _utility.camera,
                _instance.transform.rotation);
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
                ResetView();
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
                    _instance.transform.position,
                    _renderers);
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

        private static void SetHideFlags(Transform transform)
        {
            transform.gameObject.hideFlags = HideFlags.HideAndDontSave;
            for (var index = 0; index < transform.childCount; index++)
            {
                SetHideFlags(transform.GetChild(index));
            }
        }

        private void SetPreviewAvailable(bool available)
        {
            _viewport.SetPreviewAvailable(available);
        }

        private void CleanupPreview()
        {
            DestroyBakedMeshes();
            _pendingTransformScales.Clear();
            _pendingBlendShapeWeights.Clear();
            _pendingBoundsRefresh = false;
            _forceSkinningRecalculation = false;
            _previewTexture = null;
            _previewTextureSize = Vector2.zero;
            _previewDirty = true;
            _materialTargets.Clear();
            _transformTargets.Clear();
            _blendShapeTargets.Clear();
            _filteredRenderers.Clear();
            _temporarilyHiddenRenderers.Clear();
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

    internal sealed class DerivedAssetPrefabPickerWindow
        : CustomPopupWindow
    {
        private static readonly Vector2 PopupSize =
            new Vector2(520f, 420f);

        private IReadOnlyList<GameObject> _candidates;
        private GameObject _selected;
        private Action<GameObject> _select;
        private SearchField _search;
        private VisualElement _options;
        private IReadOnlyList<GameObject> _filtered =
            Array.Empty<GameObject>();

        internal static void Show(
            VisualElement anchor,
            IReadOnlyList<GameObject> candidates,
            GameObject selected,
            Action<GameObject> select)
        {
            if (anchor == null ||
                candidates == null ||
                candidates.Count == 0 ||
                select == null)
            {
                return;
            }

            foreach (var existing in Resources
                         .FindObjectsOfTypeAll<
                             DerivedAssetPrefabPickerWindow>())
            {
                existing.Close();
            }

            var window = CreateInstance<
                DerivedAssetPrefabPickerWindow>();
            window._candidates = candidates;
            window._selected = selected;
            window._select = select;
            window.ShowAsPopup(anchor, PopupSize);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            AssetManagerWindowSession.PrepareRoot(root);
            root.AddToClassList("ee4v-asset-manager");
            ConfigureCloseAndSubmitKeys(root, SelectFirst);

            var popup = new CustomPopup(
                I18N.Get("detail.derivedAssetPrefabPickerTitle"),
                closeTooltip: I18N.Get(
                    "detail.derivedAssetPrefabPickerClose"));

            var body = new VisualElement();
            body.AddToClassList(
                "ee4v-asset-manager__prefab-picker-body");
            _search = new SearchField(new SearchFieldState(
                placeholder: I18N.Get(
                    "detail.derivedAssetPrefabSearch"),
                searchIconState:
                    AssetManagerControls.LoadFluentIconState(
                        "search.png",
                        UiSizeTokens.Size14),
                clearIconState:
                    AssetManagerControls.LoadFluentIconState(
                        "dismiss.png",
                        UiSizeTokens.Size10)));
            _search.AddToClassList(
                "ee4v-asset-manager__prefab-picker-search");
            _search.ValueChanged += _ => RebuildOptions();
            body.Add(_search);

            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden
            };
            scroll.AddToClassList(
                "ee4v-asset-manager__prefab-picker-content");
            _options = scroll.contentContainer;
            _options.AddToClassList(
                "ee4v-asset-manager__prefab-picker-options");
            body.Add(scroll);
            popup.Content.Add(body);
            SetPopup(popup);
            RebuildOptions();

            _search.schedule.Execute(() =>
                _search.Q<TextField>()?.Focus());
        }

        private void RebuildOptions()
        {
            if (_options == null)
            {
                return;
            }

            _options.Clear();
            var query = (_search?.Value ?? string.Empty).Trim();
            _filtered = (_candidates ?? Array.Empty<GameObject>())
                .Where(prefab => Matches(prefab, query))
                .ToArray();
            foreach (var prefab in _filtered)
            {
                var selectedPrefab = prefab;
                var path = AssetDatabase.GetAssetPath(prefab);
                var option = new NavigationItem(
                    new NavigationItemState(
                        prefab.name,
                        path,
                        selected: prefab == _selected),
                    () => Select(selectedPrefab));
                option.tooltip = path;
                option.AddToClassList(
                    "ee4v-asset-manager__prefab-picker-option");
                option.Leading.Add(
                    new DerivedAssetPrefabPreview(prefab));
                _options.Add(option);
            }

            if (_filtered.Count == 0)
            {
                var empty = new EmptyState(new EmptyStateState(
                    string.Empty,
                    I18N.Get("detail.derivedAssetPrefabNoMatch")));
                empty.AddToClassList(
                    "ee4v-asset-manager__prefab-picker-empty");
                _options.Add(empty);
            }
        }

        private static bool Matches(GameObject prefab, string query)
        {
            if (prefab == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(query))
            {
                return true;
            }

            return prefab.name.IndexOf(
                       query,
                       StringComparison.OrdinalIgnoreCase) >= 0 ||
                   AssetDatabase.GetAssetPath(prefab).IndexOf(
                       query,
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void Select(GameObject prefab)
        {
            if (prefab == null)
            {
                return;
            }

            _select?.Invoke(prefab);
            Close();
        }

        private void SelectFirst()
        {
            if (_filtered.Count > 0)
            {
                Select(_filtered[0]);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _select = null;
            _candidates = null;
            _selected = null;
            _filtered = Array.Empty<GameObject>();
        }
    }
}
