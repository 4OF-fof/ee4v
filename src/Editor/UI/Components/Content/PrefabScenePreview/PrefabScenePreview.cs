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
            nameof(PrefabScenePreview).GetHashCode();
        private const float PreviewFitPadding = 1.05f;
        private const float AppearanceFullBodyDistanceScale = 0.52f;
        private const float AppearanceFullBodyVerticalOffsetScale = 0.025f;
        private const float HeadFaceViewScale = 1.5f;
        private const float CameraTransitionDuration = 0.45f;
        private const float MinimumPreviewSize = 320f;
        private const float MaximumPreviewSize = 560f;
        private const string ShapeChangerTypeName =
            "nadena.dev.modular_avatar.core.ModularAvatarShapeChanger";

        private readonly ScenePreviewViewport _viewport;
        private readonly PreviewOrbitController _orbit;
        private readonly VisualElement _viewToggle;
        private readonly UiButton _primaryViewButton;
        private readonly UiButton _secondaryViewButton;
        private readonly VisualElement _selectionOverlay;
        private readonly UiTextElement _selectionLabel;
        private readonly UiButton _selectionHighlightButton;
        private Material _outlineMaterial;
        private Material _invisiblePreviewMaterial;
        private readonly List<Material> _pickMaterialsById =
            new List<Material>();
        private Texture2D _pickReadback;
        private RenderTexture _outlineTexture;
        private Vector2 _outlineTextureSize;
        private bool _outlineDirty = true;
        private bool _selectionHighlightVisible = true;
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
        private readonly List<Renderer> _temporarilyScopedRenderers =
            new List<Renderer>();
        private readonly HashSet<int> _prefabSiblingIndices =
            new HashSet<int>();
        private readonly HashSet<int> _hiddenPrefabSiblingIndices =
            new HashSet<int>();
        private readonly HashSet<string> _hiddenPartKeys =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<Renderer> _hiddenPartRenderers =
            new HashSet<Renderer>();
        private readonly HashSet<Transform> _temporarilyEnabledEditorOnlyParts =
            new HashSet<Transform>();
        private bool _basePrefabHidden;
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
        private bool _cameraAnimationSubscribed;
        private bool _flexibleLayout;
        private bool _fitWholeAvatar = true;
        private BodyPartCategory? _focusedBodyPart;
        private bool _shoulderLeftSide = true;
        private bool _handLeftSide = true;
        private bool _wholeBackView;
        private bool _headBackView;
        private bool _waistBackView;
        private int? _scopeSiblingIndex;
        private Vector2? _pendingPickPosition;
        private Renderer _pickedRenderer;
        private Material _pickedMaterial;
        private string _requestedPartKey;
        private Material _requestedMaterial;
        private readonly HashSet<Renderer> _outlinedRenderers =
            new HashSet<Renderer>();
        private Material _outlinedMaterial;

        public event Action<string, Material> PreviewObjectClicked;
        public event Action PreviewSelectionCleared;

        public PrefabScenePreview()
        {
            AddToClassList(
                "ee4v-ui-prefab-scene-preview");

            _viewport = new ScenePreviewViewport(
                DrawPreview,
                ResetView,
                UiLocalization.Get("ui.prefabPreview.background"),
                UiLocalization.Get("ui.prefabPreview.reset"));
            _viewport.AddToClassList(
                "ee4v-ui-prefab-scene-preview-viewport");
            _orbit = new PreviewOrbitController(
                PreviewControlHash,
                RequestPreviewRepaint);
            _viewToggle = new VisualElement();
            _viewToggle.AddToClassList(
                "ee4v-ui-prefab-scene-preview__side-toggle");
            _primaryViewButton = new UiButton(
                UiLocalization.Get("ui.prefabPreview.left"),
                () => SelectPreviewView(true),
                variant: UiButtonVariant.Ghost);
            _secondaryViewButton = new UiButton(
                UiLocalization.Get("ui.prefabPreview.right"),
                () => SelectPreviewView(false),
                variant: UiButtonVariant.Ghost);
            _primaryViewButton.AddToClassList(
                "ee4v-ui-prefab-scene-preview__side-button");
            _secondaryViewButton.AddToClassList(
                "ee4v-ui-prefab-scene-preview__side-button");
            _viewToggle.Add(_primaryViewButton);
            _viewToggle.Add(_secondaryViewButton);
            _viewport.FeatureOverlay.Add(_viewToggle);
            _selectionOverlay = new VisualElement
            {
                pickingMode = PickingMode.Position
            };
            _selectionOverlay.AddToClassList(
                "ee4v-ui-prefab-scene-preview__selection");
            _selectionOverlay.style.display = DisplayStyle.None;
            _selectionLabel = UiTextFactory.Create(
                string.Empty,
                "ee4v-ui-prefab-scene-preview__selection-label");
            _selectionLabel.pickingMode = PickingMode.Ignore;
            _selectionOverlay.Add(_selectionLabel);
            _selectionHighlightButton = new UiButton(
                string.Empty,
                ToggleSelectionHighlight,
                variant: UiButtonVariant.Ghost);
            _selectionHighlightButton.AddToClassList(
                "ee4v-ui-prefab-scene-preview__selection-toggle");
            _selectionOverlay.Add(_selectionHighlightButton);
            _viewport.FeatureOverlay.Add(_selectionOverlay);
            RefreshSelectionHighlightButton();
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

        public void SetPrefab(GameObject prefab)
        {
            if (_prefab == prefab && _instance != null)
            {
                return;
            }

            _prefab = prefab;
            RebuildPreview();
        }

        public void SetListSelection(string partKey, Material material)
        {
            if (_requestedPartKey == partKey &&
                _requestedMaterial == material)
            {
                return;
            }
            _requestedPartKey = partKey;
            _requestedMaterial = material;
            ApplyRequestedSelection();
        }

        private void ApplyRequestedSelection()
        {
            ClearPickedSelection();
            if (_instance == null)
            {
                return;
            }

            if (_requestedMaterial != null)
            {
                ForEachVisibleMaterialSlot((renderer, material, slot) =>
                {
                    if (material == _requestedMaterial)
                    {
                        _outlinedRenderers.Add(renderer);
                        if (_pickedRenderer == null)
                        {
                            _pickedRenderer = renderer;
                        }
                    }
                });
                if (_outlinedRenderers.Count > 0)
                {
                    _pickedMaterial = _requestedMaterial;
                    _outlinedMaterial = _requestedMaterial;
                    _selectionLabel.SetText(_requestedMaterial.name);
                }
            }
            else if (_requestedPartKey != null)
            {
                var target = _instance.transform;
                foreach (var segment in _requestedPartKey.Split('/'))
                {
                    if (segment.Length == 0)
                    {
                        continue;
                    }
                    if (!int.TryParse(segment, out var index) ||
                        index < 0 || index >= target.childCount)
                    {
                        target = null;
                        break;
                    }
                    target = target.GetChild(index);
                }
                if (target != null)
                {
                    var descendants = new HashSet<Renderer>(
                        target.GetComponentsInChildren<Renderer>(true));
                    ForEachVisibleMaterialSlot((renderer, material, slot) =>
                    {
                        if (!descendants.Contains(renderer))
                        {
                            return;
                        }
                        _outlinedRenderers.Add(renderer);
                        if (_pickedRenderer == null)
                        {
                            _pickedRenderer = renderer;
                            _pickedMaterial = material;
                        }
                    });
                    if (_outlinedRenderers.Count > 0)
                    {
                        _selectionLabel.SetText(target.name);
                    }
                }
            }
            _selectionOverlay.style.display =
                _outlinedRenderers.Count > 0
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            _outlineDirty = true;
            _viewport.RequestRepaint();
        }

        private void ToggleSelectionHighlight()
        {
            _selectionHighlightVisible = !_selectionHighlightVisible;
            _outlineDirty = true;
            RefreshSelectionHighlightButton();
            _viewport.RequestRepaint();
        }

        private void RefreshSelectionHighlightButton()
        {
            var tooltip = UiLocalization.Get(_selectionHighlightVisible
                ? "ui.prefabPreview.hideHighlight"
                : "ui.prefabPreview.showHighlight");
            _selectionHighlightButton.tooltip = tooltip;
            _selectionHighlightButton.SetIcon(FluentUiIcons.CreateState(
                _selectionHighlightVisible ? "eye.png" : "eye_off.png",
                UiSizeTokens.Size18,
                tooltip));
            _selectionOverlay.EnableInClassList(
                "ee4v-ui-prefab-scene-preview__selection--highlight-hidden",
                !_selectionHighlightVisible);
        }

        public void SetScope(
            int? siblingIndex,
            IEnumerable<int> prefabSiblingIndices)
        {
            _scopeSiblingIndex = siblingIndex;
            _prefabSiblingIndices.Clear();
            if (prefabSiblingIndices != null)
            {
                _prefabSiblingIndices.UnionWith(prefabSiblingIndices);
            }
            if (_instance != null)
            {
                RebuildPreview();
            }
        }

        public void SetHiddenPrefabs(
            bool baseHidden,
            IEnumerable<int> hiddenSiblingIndices)
        {
            var next = hiddenSiblingIndices == null
                ? new HashSet<int>()
                : new HashSet<int>(hiddenSiblingIndices);
            if (_basePrefabHidden == baseHidden &&
                _hiddenPrefabSiblingIndices.SetEquals(next))
            {
                return;
            }
            _basePrefabHidden = baseHidden;
            _hiddenPrefabSiblingIndices.Clear();
            _hiddenPrefabSiblingIndices.UnionWith(next);
            RefreshSelectionAfterVisibilityChange();
            RequestPreviewRepaint();
        }

        public static string GetPartKey(
            int prefabSiblingIndex,
            IReadOnlyList<int> siblingPath)
        {
            var indices = prefabSiblingIndex >= 0
                ? new[] { prefabSiblingIndex }.Concat(siblingPath)
                : siblingPath;
            return string.Join("/", indices);
        }

        public void SetHiddenParts(IEnumerable<string> hiddenPartKeys)
        {
            var next = hiddenPartKeys == null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(hiddenPartKeys, StringComparer.Ordinal);
            if (_hiddenPartKeys.SetEquals(next))
            {
                return;
            }
            _hiddenPartKeys.Clear();
            _hiddenPartKeys.UnionWith(next);
            RebuildHiddenPartRenderers();
            RefreshSelectionAfterVisibilityChange();
            RefreshBounds();
            RequestPreviewRepaint();
        }

        private void RebuildHiddenPartRenderers()
        {
            _hiddenPartRenderers.Clear();
            if (_instance == null)
            {
                return;
            }
            foreach (var target in _instance
                         .GetComponentsInChildren<Transform>(true))
            {
                if (!string.Equals(target.tag, "EditorOnly",
                        StringComparison.Ordinal) ||
                    _temporarilyEnabledEditorOnlyParts.Contains(target) ||
                    (_scopeSiblingIndex.HasValue &&
                     _scopeSiblingIndex.Value >= 0 &&
                     target.parent == _instance.transform &&
                     target.GetSiblingIndex() == _scopeSiblingIndex.Value))
                {
                    continue;
                }
                _hiddenPartRenderers.UnionWith(
                    target.GetComponentsInChildren<Renderer>(true));
            }
            foreach (var key in _hiddenPartKeys)
            {
                var target = _instance.transform;
                foreach (var segment in key.Split('/'))
                {
                    if (!int.TryParse(segment, out var index) ||
                        index < 0 || index >= target.childCount)
                    {
                        target = null;
                        break;
                    }
                    target = target.GetChild(index);
                }
                if (target != null)
                {
                    _hiddenPartRenderers.UnionWith(
                        target.GetComponentsInChildren<Renderer>(true));
                }
            }
        }

        public void RefreshPreview()
        {
            RequestPreviewRepaint();
        }

        public void ReloadPrefab()
        {
            RebuildPreview();
        }

        public void ReloadPrefabPreservingView(GameObject prefab)
        {
            _orbit.UpdateTransition(EditorApplication.timeSinceStartup);
            _prefab = prefab;
            RebuildPreview(true);
        }

        public void UpdatePrefabReference(GameObject prefab)
        {
            _prefab = prefab;
        }

        public void SetPartVisibility(
            GameObject prefab,
            int prefabSiblingIndex,
            IReadOnlyList<int> siblingPath,
            string objectName,
            bool activeSelf,
            bool included)
        {
            _prefab = prefab;
            if (_instance == null || siblingPath == null)
            {
                ReloadPrefabPreservingView(prefab);
                return;
            }

            var current = _instance.transform;
            if (prefabSiblingIndex >= 0)
            {
                if (prefabSiblingIndex >= current.childCount)
                {
                    ReloadPrefabPreservingView(prefab);
                    return;
                }
                current = current.GetChild(prefabSiblingIndex);
            }
            else if (prefabSiblingIndex != -1)
            {
                ReloadPrefabPreservingView(prefab);
                return;
            }
            foreach (var index in siblingPath)
            {
                if (index < 0 || index >= current.childCount)
                {
                    ReloadPrefabPreservingView(prefab);
                    return;
                }
                current = current.GetChild(index);
            }
            if (!string.Equals(current.name, objectName,
                    StringComparison.Ordinal))
            {
                ReloadPrefabPreservingView(prefab);
                return;
            }

            current.gameObject.SetActive(activeSelf);
            var hiddenRenderersChanged = included &&
                string.Equals(current.tag, "EditorOnly",
                    StringComparison.Ordinal)
                    ? _temporarilyEnabledEditorOnlyParts.Add(current)
                    : _temporarilyEnabledEditorOnlyParts.Remove(current);
            if (hiddenRenderersChanged)
            {
                RebuildHiddenPartRenderers();
            }
            if (activeSelf)
            {
                _forceSkinningRecalculation = true;
                SetSkinningRecalculation(true);
            }
            _bakedMeshesDirty = true;
            _pendingBoundsRefresh = true;
            RequestPreviewRepaint();
        }

        public void FocusBodyPart(BodyPartCategory? part)
        {
            _focusedBodyPart = part;
            RefreshViewToggle();
            FrameCurrentSelection(true);
        }

        private bool IsLeftSide(BodyPartCategory part)
        {
            switch (part)
            {
                case BodyPartCategory.Shoulders:
                case BodyPartCategory.Arms:
                    return _shoulderLeftSide;
                case BodyPartCategory.Hands:
                    return _handLeftSide;
                case BodyPartCategory.Feet:
                    return true;
                default:
                    return false;
            }
        }

        private bool IsBackView(BodyPartCategory? part)
        {
            if (!part.HasValue)
            {
                return _wholeBackView;
            }

            switch (part.Value)
            {
                case BodyPartCategory.Head:
                    return _headBackView;
                case BodyPartCategory.Waist:
                    return _waistBackView;
                default:
                    return false;
            }
        }

        private void SelectPreviewView(bool primary)
        {
            if (!_focusedBodyPart.HasValue)
            {
                if (!_flexibleLayout)
                {
                    return;
                }

                _wholeBackView = !primary;
            }
            else
            {
                switch (_focusedBodyPart.Value)
                {
                    case BodyPartCategory.Shoulders:
                    case BodyPartCategory.Arms:
                        _shoulderLeftSide = primary;
                        break;
                    case BodyPartCategory.Hands:
                        _handLeftSide = primary;
                        break;
                    case BodyPartCategory.Head:
                        _headBackView = !primary;
                        break;
                    case BodyPartCategory.Waist:
                        _waistBackView = !primary;
                        break;
                    default:
                        return;
                }
            }

            RefreshViewToggle();
            FrameCurrentSelection(true);
        }

        private void RefreshViewToggle()
        {
            var sideSelection = _focusedBodyPart ==
                    BodyPartCategory.Shoulders ||
                _focusedBodyPart == BodyPartCategory.Arms ||
                _focusedBodyPart == BodyPartCategory.Hands;
            var backSelection = _flexibleLayout &&
                (!_focusedBodyPart.HasValue ||
                 _focusedBodyPart == BodyPartCategory.Head ||
                 _focusedBodyPart == BodyPartCategory.Waist);
            var visible = _instance != null &&
                (sideSelection || backSelection);
            _viewToggle.style.display = visible
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            if (!visible)
            {
                return;
            }

            _primaryViewButton.SetLabel(UiLocalization.Get(sideSelection
                ? "ui.prefabPreview.left"
                : "ui.prefabPreview.front"));
            _secondaryViewButton.SetLabel(UiLocalization.Get(sideSelection
                ? "ui.prefabPreview.right"
                : "ui.prefabPreview.back"));
            var primaryActive = sideSelection
                ? IsLeftSide(_focusedBodyPart.Value)
                : !IsBackView(_focusedBodyPart);
            _primaryViewButton.EnableInClassList(
                "ee4v-ui-prefab-scene-preview__side-button--active",
                primaryActive);
            _secondaryViewButton.EnableInClassList(
                "ee4v-ui-prefab-scene-preview__side-button--active",
                !primaryActive);
        }

        public void SetTransformScales(
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

        public void SetBlendShapeWeight(
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

        public void FlushUpdates(bool recalculateBounds)
        {
            _pendingBoundsRefresh |= recalculateBounds;
            ApplyPendingUpdates();
            RequestPreviewRepaint();
        }

        public void SetHiddenMaterials(
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
            RefreshSelectionAfterVisibilityChange();
            RebuildMaterialTargets();
            RequestPreviewRepaint();
        }

        public void SetFlexibleLayout(bool flexible)
        {
            _flexibleLayout = flexible;
            RefreshViewToggle();
        }

        public void SetFullBodyFraming(bool fitWholeAvatar)
        {
            _fitWholeAvatar = fitWholeAvatar;
            FrameCurrentSelection();
        }

        public void Dispose()
        {
            CleanupPreview();
            _viewport.Dispose();
        }

        private void RebuildPreview(bool preserveView = false)
        {
            CleanupPreview();
            if (_prefab == null || panel == null)
            {
                return;
            }

            try
            {
                _utility = new PreviewRenderUtility();
                if (EditorApplication.isPlaying)
                {
                    var staging = new GameObject("Derived Asset Preview Staging");
                    staging.SetActive(false);
                    _utility.AddSingleGO(staging);
                    _instance = UnityEngine.Object.Instantiate(_prefab, staging.transform, false);
                    foreach (var behaviour in _instance.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (behaviour != null) { UnityEngine.Object.DestroyImmediate(behaviour); }
                    }
                    foreach (var animator in _instance.GetComponentsInChildren<Animator>(true))
                    {
                        animator.enabled = false;
                    }
                    _instance.transform.SetParent(null, false);
                    UnityEngine.Object.DestroyImmediate(staging);
                }
                else
                {
                    _instance = UnityEngine.Object.Instantiate(_prefab);
                }
                _instance.name =
                    _prefab.name + " (Derived Asset Preview)";
                _instance.SetActive(true);
                foreach (var index in _prefabSiblingIndices)
                {
                    if (index >= 0 && index < _instance.transform.childCount)
                    {
                        _instance.transform.GetChild(index).gameObject
                            .SetActive(true);
                    }
                }
                EditorSceneApi.HidePreviewHierarchy(_instance.transform);
                ApplyInitialShapeChanges();
                _renderers = _instance
                    .GetComponentsInChildren<Renderer>(true);
                RebuildHiddenPartRenderers();
                _skinnedRenderers = _renderers
                    .OfType<SkinnedMeshRenderer>()
                    .ToArray();
                _forceSkinningRecalculation = true;
                SetSkinningRecalculation(true);
                RebuildPreviewTargets();

                _utility.cameraFieldOfView = 30f;
                _utility.camera.clearFlags = CameraClearFlags.Color;
                _utility.camera.backgroundColor = Color.clear;
                _utility.lights[0].intensity = 1.1f;
                _utility.lights[0].transform.rotation =
                    Quaternion.Euler(35f, 35f, 0f);
                _utility.lights[1].intensity = 0.7f;
                _utility.AddSingleGO(_instance);
                RefreshBounds();
                RebuildMaterialTargets();
                ApplyRequestedSelection();
                SetPreviewAvailable(true);
                if (!preserveView)
                {
                    FrameCurrentSelection();
                }
                else
                {
                    RequestPreviewRepaint();
                }
            }
            catch (Exception exception)
            {
                CleanupPreview();
                Debug.LogException(exception);
            }
        }

    }
}
