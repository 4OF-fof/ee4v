using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.EditorIntegration;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal static class AssetManagerPrefabUtility
    {
        internal static IReadOnlyCollection<string> SplitName(string name)
        {
            var words = new List<string>();
            var start = -1;
            for (var index = 0; index <= name.Length; index++)
            {
                var end = index == name.Length ||
                    !char.IsLetterOrDigit(name[index]);
                var camelBreak = !end && start >= 0 &&
                    char.IsUpper(name[index]) &&
                    char.IsLower(name[index - 1]);
                if ((end || camelBreak) && start >= 0)
                {
                    words.Add(name.Substring(start, index - start)
                        .ToLowerInvariant());
                    start = -1;
                }
                if (!end && start < 0)
                {
                    start = index;
                }
            }
            return words;
        }

        internal static bool IsInScope(
            Transform target,
            Transform root,
            int? selectedSiblingIndex,
            IReadOnlyCollection<int> prefabSiblingIndices)
        {
            if (root == null || target == null ||
                (target != root && !target.IsChildOf(root)))
            {
                return false;
            }
            if (!selectedSiblingIndex.HasValue)
            {
                return true;
            }
            if (selectedSiblingIndex.Value >= 0)
            {
                var index = selectedSiblingIndex.Value;
                if (index >= root.childCount)
                {
                    return false;
                }
                var selected = root.GetChild(index);
                return target == selected || target.IsChildOf(selected);
            }

            var current = target;
            while (current.parent != null && current.parent != root)
            {
                current = current.parent;
            }
            return current.parent != root ||
                   !prefabSiblingIndices.Contains(current.GetSiblingIndex());
        }
    }

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

        internal event Action<string, Material> PreviewObjectClicked;
        internal event Action PreviewSelectionCleared;

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
            _viewToggle = new VisualElement();
            _viewToggle.AddToClassList(
                "ee4v-asset-manager__preview-side-toggle");
            _primaryViewButton = new UiButton(
                I18N.Get("workflow.preview.sideLeft"),
                () => SelectPreviewView(true),
                variant: UiButtonVariant.Ghost);
            _secondaryViewButton = new UiButton(
                I18N.Get("workflow.preview.sideRight"),
                () => SelectPreviewView(false),
                variant: UiButtonVariant.Ghost);
            _primaryViewButton.AddToClassList(
                "ee4v-asset-manager__preview-side-button");
            _secondaryViewButton.AddToClassList(
                "ee4v-asset-manager__preview-side-button");
            _viewToggle.Add(_primaryViewButton);
            _viewToggle.Add(_secondaryViewButton);
            _viewport.FeatureOverlay.Add(_viewToggle);
            _selectionOverlay = new VisualElement
            {
                pickingMode = PickingMode.Position
            };
            _selectionOverlay.AddToClassList(
                "ee4v-asset-manager__preview-selection");
            _selectionOverlay.style.display = DisplayStyle.None;
            _selectionLabel = UiTextFactory.Create(
                string.Empty,
                "ee4v-asset-manager__preview-selection-label");
            _selectionLabel.pickingMode = PickingMode.Ignore;
            _selectionOverlay.Add(_selectionLabel);
            _selectionHighlightButton = new UiButton(
                string.Empty,
                ToggleSelectionHighlight,
                variant: UiButtonVariant.Ghost);
            _selectionHighlightButton.AddToClassList(
                "ee4v-asset-manager__preview-selection-toggle");
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

        internal void SetPrefab(GameObject prefab)
        {
            if (_prefab == prefab && _instance != null)
            {
                return;
            }

            _prefab = prefab;
            RebuildPreview();
        }

        internal void SetListSelection(string partKey, Material material)
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
            var tooltip = I18N.Get(_selectionHighlightVisible
                ? "detail.derivedAssetPreviewHideHighlight"
                : "detail.derivedAssetPreviewShowHighlight");
            _selectionHighlightButton.tooltip = tooltip;
            _selectionHighlightButton.SetIcon(FluentUiIcons.CreateState(
                _selectionHighlightVisible ? "eye.png" : "eye_off.png",
                UiSizeTokens.Size18,
                tooltip));
            _selectionOverlay.EnableInClassList(
                "ee4v-asset-manager__preview-selection--highlight-hidden",
                !_selectionHighlightVisible);
        }

        internal void SetScope(
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

        internal void SetHiddenPrefabs(
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

        internal static string GetPartKey(
            int prefabSiblingIndex,
            IReadOnlyList<int> siblingPath)
        {
            var indices = prefabSiblingIndex >= 0
                ? new[] { prefabSiblingIndex }.Concat(siblingPath)
                : siblingPath;
            return string.Join("/", indices);
        }

        internal void SetHiddenParts(IEnumerable<string> hiddenPartKeys)
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

        internal void RefreshPreview()
        {
            RequestPreviewRepaint();
        }

        internal void ReloadPrefab()
        {
            RebuildPreview();
        }

        internal void ReloadPrefabPreservingView(GameObject prefab)
        {
            _orbit.UpdateTransition(EditorApplication.timeSinceStartup);
            _prefab = prefab;
            RebuildPreview(true);
        }

        internal void UpdatePrefabReference(GameObject prefab)
        {
            _prefab = prefab;
        }

        internal void SetPartVisibility(
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

        internal void FocusBodyPart(BodyPartCategory? part)
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

            _primaryViewButton.SetLabel(I18N.Get(sideSelection
                ? "workflow.preview.sideLeft"
                : "workflow.preview.front"));
            _secondaryViewButton.SetLabel(I18N.Get(sideSelection
                ? "workflow.preview.sideRight"
                : "workflow.preview.back"));
            var primaryActive = sideSelection
                ? IsLeftSide(_focusedBodyPart.Value)
                : !IsBackView(_focusedBodyPart);
            _primaryViewButton.EnableInClassList(
                "ee4v-asset-manager__preview-side-button--active",
                primaryActive);
            _secondaryViewButton.EnableInClassList(
                "ee4v-asset-manager__preview-side-button--active",
                !primaryActive);
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
            RefreshSelectionAfterVisibilityChange();
            RebuildMaterialTargets();
            RequestPreviewRepaint();
        }

        internal void SetFlexibleLayout(bool flexible)
        {
            _flexibleLayout = flexible;
            RefreshViewToggle();
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
                _instance = UnityEngine.Object.Instantiate(_prefab);
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

                _utility = new PreviewRenderUtility();
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
                ApplyPickedSelection(renderer, material);
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
            EditorApplication.delayCall += () =>
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
                for (var attempt = 0; attempt < 16; attempt++)
                {
                    if (!TryPickPreviewCandidate(rect, pixelX, pixelY,
                            skipped, readback, out var candidate))
                    {
                        return false;
                    }
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
            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                return false;
            }

            var camera = _utility.camera;
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
                        renderer, GetPickMaterial(shader, id), slot);
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
                _pickMaterialsById.Add(
                    CreateSolidPreviewMaterial(shader, color));
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
            if (_invisiblePreviewMaterial == null)
            {
                var shader = Shader.Find("Hidden/Internal-Colored");
                _invisiblePreviewMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                _invisiblePreviewMaterial.SetColor("_Color", Color.clear);
                _invisiblePreviewMaterial.SetInt("_SrcBlend",
                    (int)BlendMode.Zero);
                _invisiblePreviewMaterial.SetInt("_DstBlend",
                    (int)BlendMode.One);
                _invisiblePreviewMaterial.SetInt("_ZWrite", 0);
                _invisiblePreviewMaterial.SetInt("_ZTest",
                    (int)CompareFunction.Always);
                _invisiblePreviewMaterial.SetInt("_ColorMask", 0);
            }

            var camera = _utility.camera;
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var previewTexture = _previewTexture as RenderTexture;
            RenderTexture savedPreview = null;
            var originalMaterials =
                new List<KeyValuePair<Renderer, Material[]>>();
            var disabledForOutline = new List<Renderer>();
            var rendererWasEnabled = omittedRenderer != null &&
                omittedRenderer.enabled;
            var previewStarted = false;
            if (previewTexture != null)
            {
                savedPreview = RenderTexture.GetTemporary(
                    previewTexture.width, previewTexture.height, 0,
                    RenderTextureFormat.ARGBHalf,
                    RenderTextureReadWrite.Linear);
                Graphics.Blit(previewTexture, savedPreview);
            }
            HideOutOfScopeRenderers();
            try
            {
                foreach (var renderer in _renderers)
                {
                    if (renderer == null || !renderer.enabled)
                    {
                        continue;
                    }
                    var materials = renderer.sharedMaterials;
                    var changed = false;
                    for (var slot = 0; slot < materials.Length; slot++)
                    {
                        if ((renderer == omittedRenderer &&
                             slot == omittedSlot) ||
                            (omittedMaterial != null &&
                             materials[slot] == omittedMaterial) ||
                            _hiddenMaterials.Contains(materials[slot]))
                        {
                            if (!changed)
                            {
                                originalMaterials.Add(
                                    new KeyValuePair<Renderer, Material[]>(
                                        renderer, materials));
                                materials = (Material[])materials.Clone();
                                changed = true;
                            }
                            materials[slot] = _invisiblePreviewMaterial;
                        }
                    }
                    if (changed)
                    {
                        renderer.sharedMaterials = materials;
                    }
                }
                if (omittedRenderer != null && omittedSlot < 0)
                {
                    omittedRenderer.enabled = false;
                }
                if (omittedRenderers != null)
                {
                    foreach (var renderer in omittedRenderers)
                    {
                        if (renderer != null && renderer.enabled)
                        {
                            renderer.enabled = false;
                            disabledForOutline.Add(renderer);
                        }
                    }
                }
                _utility.BeginPreview(
                    new Rect(0f, 0f, target.width, target.height),
                    GUIStyle.none);
                previewStarted = true;
                camera.Render();
                var rendered = _utility.EndPreview();
                previewStarted = false;
                Graphics.Blit(rendered, target);
            }
            finally
            {
                if (previewStarted)
                {
                    _utility.EndPreview();
                }
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (omittedRenderer != null)
                {
                    omittedRenderer.enabled = rendererWasEnabled;
                }
                foreach (var renderer in disabledForOutline)
                {
                    if (renderer != null)
                    {
                        renderer.enabled = true;
                    }
                }
                foreach (var pair in originalMaterials)
                {
                    if (pair.Key != null)
                    {
                        pair.Key.sharedMaterials = pair.Value;
                    }
                }
                RestoreOutOfScopeRenderers();
                if (savedPreview != null)
                {
                    Graphics.Blit(savedPreview, previewTexture);
                    RenderTexture.ReleaseTemporary(savedPreview);
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
                    renderer != null && renderer.enabled &&
                    renderer.gameObject.activeInHierarchy &&
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
                if (renderer == null || !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy ||
                    !IsInScope(renderer) ||
                    _hiddenPartRenderers.Contains(renderer))
                {
                    continue;
                }
                Mesh mesh = null;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    mesh = skinned.sharedMesh;
                }
                else if (renderer is MeshRenderer meshRenderer)
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

        private static Material CreateSolidPreviewMaterial(
            Shader shader,
            Color color)
        {
            var material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            material.SetColor("_Color",
                QualitySettings.activeColorSpace == ColorSpace.Linear
                    ? color.gamma : color);
            material.SetInt("_SrcBlend", (int)BlendMode.One);
            material.SetInt("_DstBlend", (int)BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            material.SetInt("_Cull", (int)CullMode.Off);
            return material;
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
                (!_pickedRenderer.enabled ||
                 !_pickedRenderer.gameObject.activeInHierarchy ||
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

        internal void ResetView()
        {
            if (_instance == null)
            {
                return;
            }

            FrameCurrentSelection(true);
        }

        private void FrameCurrentSelection(bool animate = false)
        {
            if (_instance == null)
            {
                return;
            }

            ApplyPendingUpdates();
            if (!_focusedBodyPart.HasValue ||
                _focusedBodyPart.Value == BodyPartCategory.Other)
            {
                FrameWholeAvatar(animate);
                return;
            }

            var part = _focusedBodyPart.Value;
            var avatarHeight = part == BodyPartCategory.Head ||
                part == BodyPartCategory.Legs
                    ? Mathf.Max(0.2f, _bounds.size.y)
                    : GetAvatarReferenceHeight();
            var leftSide = IsLeftSide(part);
            if (!TryGetBoneFocusBounds(part, avatarHeight, leftSide,
                    out var focusBounds))
            {
                FrameWholeAvatar(animate);
                return;
            }

            var minimumHalfView = avatarHeight * GetMinimumHalfView(part);
            if (part == BodyPartCategory.Head &&
                TryGetHeadFaceFraming(out var faceCenter,
                    out var faceHeight, out var faceWidth))
            {
                var up = _instance.transform.up;
                var right = _instance.transform.right;
                var offset = faceCenter - focusBounds.center;
                focusBounds.center += up * Vector3.Dot(offset, up) +
                    right * Vector3.Dot(offset, right);
                var previewRect = _viewport.PreviewRect;
                var aspect = previewRect.height > 1f
                    ? previewRect.width / previewRect.height
                    : 1f;
                minimumHalfView = Mathf.Max(faceHeight,
                    faceWidth / Mathf.Max(0.01f, aspect)) *
                    HeadFaceViewScale;
            }

            if (part == BodyPartCategory.Chest)
            {
                focusBounds.center += _instance.transform.up *
                    avatarHeight * 0.14f;
            }

            var viewAngles = GetViewAngles(part, leftSide);
            if (IsBackView(part))
            {
                viewAngles.x += 180f;
                if (part == BodyPartCategory.Waist)
                {
                    viewAngles.y = 28f;
                }
            }
            FrameBounds(
                focusBounds,
                minimumHalfView,
                viewAngles,
                animate,
                GetDistanceScale(part));
        }

        private void FrameWholeAvatar(bool animate)
        {
            var bounds = _bounds;
            if (_flexibleLayout && !_scopeSiblingIndex.HasValue)
            {
                bounds.center -= _instance.transform.up *
                    bounds.size.y * AppearanceFullBodyVerticalOffsetScale;
            }
            FrameBounds(
                bounds,
                0f,
                _flexibleLayout && _wholeBackView
                    ? new Vector2(180f, 0f)
                    : Vector2.zero,
                animate,
                _flexibleLayout
                    ? AppearanceFullBodyDistanceScale
                    : 1f);
        }

        private float GetAvatarReferenceHeight()
        {
            var animator = _instance
                .GetComponentsInChildren<Animator>(true)
                .FirstOrDefault(candidate => candidate != null &&
                    IsInFocusScope(candidate.transform) &&
                    candidate.avatar != null &&
                    candidate.avatar.isHuman && candidate.isHuman);
            if (animator != null)
            {
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                var foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                if (head != null && foot != null)
                {
                    var height = Vector3.Dot(
                        head.position - foot.position,
                        _instance.transform.up) + 0.18f;
                    if (height > 0.3f)
                    {
                        return height;
                    }
                }
            }

            return Mathf.Max(0.2f, _bounds.size.y);
        }

        private void FrameBounds(
            Bounds bounds,
            float minimumHalfView,
            Vector2 viewAngles,
            bool animate,
            float distanceScale = 1f)
        {
            if (_utility == null)
            {
                return;
            }

            var previewRect = _viewport.PreviewRect;
            var aspect = previewRect.height > 1f
                ? previewRect.width / previewRect.height
                : 1f;
            var halfViewSize = Mathf.Max(
                minimumHalfView,
                bounds.extents.y,
                bounds.extents.x / Mathf.Max(0.01f, aspect));
            var distance = bounds.extents.z +
                Mathf.Max(0.05f, halfViewSize) /
                Mathf.Tan(_utility.cameraFieldOfView * 0.5f *
                          Mathf.Deg2Rad) * PreviewFitPadding *
                distanceScale;
            if (animate)
            {
                _orbit.AnimateTo(
                    bounds.center,
                    distance,
                    viewAngles.x,
                    viewAngles.y,
                    EditorApplication.timeSinceStartup,
                    CameraTransitionDuration);
                StartCameraAnimation();
            }
            else
            {
                StopCameraAnimation();
                _orbit.SetView(
                    bounds.center,
                    distance,
                    viewAngles.x,
                    viewAngles.y);
            }
        }

        private void AdvanceCameraAnimation()
        {
            if (_instance == null || panel == null)
            {
                StopCameraAnimation();
                return;
            }

            if (_orbit.UpdateTransition(EditorApplication.timeSinceStartup))
            {
                RequestPreviewRepaint();
            }
            if (!_orbit.IsTransitioning)
            {
                StopCameraAnimation();
            }
        }

        private void StartCameraAnimation()
        {
            if (_cameraAnimationSubscribed)
            {
                return;
            }

            EditorApplication.update += AdvanceCameraAnimation;
            _cameraAnimationSubscribed = true;
        }

        private void StopCameraAnimation()
        {
            if (!_cameraAnimationSubscribed)
            {
                return;
            }

            EditorApplication.update -= AdvanceCameraAnimation;
            _cameraAnimationSubscribed = false;
        }

        private bool TryGetBoneFocusBounds(
            BodyPartCategory part,
            float avatarHeight,
            bool leftSide,
            out Bounds bounds)
        {
            bounds = default;
            var animator = _instance
                .GetComponentsInChildren<Animator>(true)
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    IsInFocusScope(candidate.transform) &&
                    HasFocusBone(candidate, part, IsInFocusScope));
            Vector3[] points;
            if (animator != null)
            {
                points = GetFocusBones(part, leftSide)
                    .Select(animator.GetBoneTransform)
                    .Where(bone => bone != null && IsInFocusScope(bone))
                    .Select(bone => bone.position)
                    .ToArray();
                if (points.Length == 0)
                {
                    points = GetFocusBones(part, !leftSide)
                        .Select(animator.GetBoneTransform)
                        .Where(bone => bone != null && IsInFocusScope(bone))
                        .Select(bone => bone.position)
                        .ToArray();
                }
            }
            else
            {
                points = GetSkinnedFocusBones(
                        _instance, part, IsInFocusScope, leftSide)
                    .Select(bone => bone.position)
                    .ToArray();
                if (points.Length == 0)
                {
                    points = GetSkinnedFocusBones(
                            _instance, part, IsInFocusScope, !leftSide)
                        .Select(bone => bone.position)
                        .ToArray();
                }
            }
            if (points.Length == 0)
            {
                return false;
            }

            bounds = new Bounds(points[0], Vector3.zero);
            foreach (var point in points)
            {
                bounds.Encapsulate(point);
            }

            if (part == BodyPartCategory.Head)
            {
                bounds.Encapsulate(points[0] +
                    _instance.transform.up * avatarHeight * 0.1f);
            }
            bounds.Expand(avatarHeight * 0.06f);
            return true;
        }

        private bool TryGetHeadFaceFraming(
            out Vector3 faceCenter,
            out float faceHeight,
            out float faceWidth)
        {
            faceCenter = default;
            faceHeight = 0f;
            faceWidth = 0f;
            var animator = _instance
                .GetComponentsInChildren<Animator>(true)
                .FirstOrDefault(candidate => candidate != null &&
                    IsInFocusScope(candidate.transform) &&
                    candidate.avatar != null &&
                    candidate.avatar.isHuman && candidate.isHuman);
            var head = animator != null
                ? animator.GetBoneTransform(HumanBodyBones.Head)
                : null;
            if (head == null || !IsInFocusScope(head))
            {
                return false;
            }

            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var up = _instance.transform.up;
            var right = _instance.transform.right;
            var bestCount = 0;
            var bestHasHips = false;
            var bestMin = 0f;
            var bestMax = 0f;
            var bestLeft = 0f;
            var bestRight = 0f;
            foreach (var renderer in _skinnedRenderers)
            {
                if (renderer == null || !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy ||
                    !IsInScope(renderer) ||
                    _hiddenPartRenderers.Contains(renderer) ||
                    renderer.sharedMesh == null ||
                    !renderer.sharedMesh.isReadable)
                {
                    continue;
                }

                var bones = renderer.bones;
                var headIndex = Array.IndexOf(bones, head);
                if (headIndex < 0)
                {
                    continue;
                }

                var weights = renderer.sharedMesh.boneWeights;
                if (weights.Length != renderer.sharedMesh.vertexCount)
                {
                    continue;
                }

                var baked = new Mesh();
                var count = 0;
                var minimum = float.MaxValue;
                var maximum = float.MinValue;
                var left = float.MaxValue;
                var rightmost = float.MinValue;
                try
                {
                    renderer.BakeMesh(baked, true);
                    var vertices = baked.vertices;
                    if (vertices.Length != weights.Length)
                    {
                        continue;
                    }
                    for (var index = 0; index < vertices.Length; index++)
                    {
                        var weight = weights[index];
                        var headWeight = 0f;
                        if (weight.boneIndex0 == headIndex)
                        {
                            headWeight += weight.weight0;
                        }
                        if (weight.boneIndex1 == headIndex)
                        {
                            headWeight += weight.weight1;
                        }
                        if (weight.boneIndex2 == headIndex)
                        {
                            headWeight += weight.weight2;
                        }
                        if (weight.boneIndex3 == headIndex)
                        {
                            headWeight += weight.weight3;
                        }
                        if (headWeight < 0.5f)
                        {
                            continue;
                        }

                        var point = renderer.transform.TransformPoint(
                            vertices[index]);
                        var height = Vector3.Dot(point - head.position, up);
                        var horizontal = Vector3.Dot(
                            point - head.position, right);
                        minimum = Mathf.Min(minimum, height);
                        maximum = Mathf.Max(maximum, height);
                        left = Mathf.Min(left, horizontal);
                        rightmost = Mathf.Max(rightmost, horizontal);
                        count++;
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(baked);
                }

                var hasHips = hips != null && Array.IndexOf(bones, hips) >= 0;
                if (count < 32 ||
                    (bestCount > 0 &&
                     (bestHasHips && !hasHips ||
                      bestHasHips == hasHips && count <= bestCount)))
                {
                    continue;
                }

                bestCount = count;
                bestHasHips = hasHips;
                bestMin = minimum;
                bestMax = maximum;
                bestLeft = left;
                bestRight = rightmost;
            }

            if (bestCount == 0 || bestMax - bestMin < 0.01f)
            {
                return false;
            }

            faceCenter = head.position +
                up * ((bestMin + bestMax) * 0.5f) +
                right * ((bestLeft + bestRight) * 0.5f);
            faceHeight = bestMax - bestMin;
            faceWidth = bestRight - bestLeft;
            return true;
        }

        internal static bool HasFocusBone(
            GameObject root,
            BodyPartCategory part,
            Func<Transform, bool> isInScope)
        {
            if (root == null)
            {
                return false;
            }

            return root.GetComponentsInChildren<Animator>(true)
                       .Any(animator => isInScope(animator.transform) &&
                           HasFocusBone(animator, part, isInScope)) ||
                   GetSkinnedFocusBones(root, part, isInScope).Length > 0;
        }

        internal static bool HasFocusBone(
            Animator animator,
            BodyPartCategory part,
            Func<Transform, bool> isInScope)
        {
            if (animator == null || animator.avatar == null ||
                !animator.avatar.isHuman || !animator.isHuman)
            {
                return false;
            }

            return GetFocusBones(part, true)
                .Concat(GetFocusBones(part, false))
                .Select(animator.GetBoneTransform)
                .Any(bone => bone != null && isInScope(bone));
        }

        private static Transform[] GetSkinnedFocusBones(
            GameObject root,
            BodyPartCategory part,
            Func<Transform, bool> isInScope,
            bool? leftSide = null)
        {
            return root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => isInScope(renderer.transform))
                // Clothing renderers can use bones outside their Prefab tab.
                .SelectMany(renderer => renderer.bones ??
                    Array.Empty<Transform>())
                .Where(bone => bone != null &&
                    MatchesFocusBoneName(bone.name, part) &&
                    (!leftSide.HasValue ||
                     MatchesBoneSide(bone.name, leftSide.Value)))
                .Distinct()
                .ToArray();
        }

        private static bool MatchesFocusBoneName(
            string name,
            BodyPartCategory part)
        {
            var words = AssetManagerPrefabUtility.SplitName(name);
            switch (part)
            {
                case BodyPartCategory.Head:
                    return HasBoneWord(words, "head", "neck") ||
                           HasBoneText(name, "頭", "首");
                case BodyPartCategory.Chest:
                    return HasBoneWord(words, "chest", "spine") ||
                           HasBoneText(name, "胸", "背骨");
                case BodyPartCategory.Waist:
                    return HasBoneWord(words, "hips", "hip", "pelvis",
                               "spine", "waist") ||
                           HasBoneText(name, "腰", "骨盤", "背骨");
                case BodyPartCategory.Shoulders:
                case BodyPartCategory.Arms:
                    return HasBoneWord(words, "shoulder", "arm", "elbow",
                               "forearm") ||
                           HasBoneText(name, "肩", "腕", "肘");
                case BodyPartCategory.Hands:
                    return HasBoneWord(words, "hand", "wrist", "finger",
                               "thumb", "index", "middle", "ring",
                               "little") ||
                           HasBoneText(name, "手", "指");
                case BodyPartCategory.Legs:
                    return HasBoneWord(words, "leg", "thigh", "calf",
                               "knee", "shin", "foot") ||
                           HasBoneText(name, "脚", "腿", "膝");
                case BodyPartCategory.Feet:
                    return HasBoneWord(words, "foot", "toe", "toes",
                               "ankle") ||
                           HasBoneText(name, "足", "つま先", "足首");
                default:
                    return false;
            }
        }

        private static bool MatchesBoneSide(string name, bool leftSide)
        {
            var words = AssetManagerPrefabUtility.SplitName(name);
            var left = HasBoneWord(words, "left", "l");
            var right = HasBoneWord(words, "right", "r");
            return left == right || (leftSide ? left : right);
        }

        private static bool HasBoneWord(
            IReadOnlyCollection<string> words,
            params string[] terms)
        {
            return words.Any(word => terms.Contains(word));
        }

        private static bool HasBoneText(
            string name,
            params string[] terms)
        {
            return terms.Any(term => name.IndexOf(
                term, StringComparison.Ordinal) >= 0);
        }

        private bool IsInFocusScope(Transform target)
        {
            return AssetManagerPrefabUtility.IsInScope(
                target,
                _instance == null ? null : _instance.transform,
                _scopeSiblingIndex,
                _prefabSiblingIndices);
        }

        private static HumanBodyBones[] GetFocusBones(
            BodyPartCategory part,
            bool leftSide)
        {
            switch (part)
            {
                case BodyPartCategory.Head:
                    return new[] { HumanBodyBones.Head,
                        HumanBodyBones.Neck };
                case BodyPartCategory.Chest:
                    return new[] { HumanBodyBones.Spine,
                        HumanBodyBones.Chest,
                        HumanBodyBones.UpperChest };
                case BodyPartCategory.Waist:
                    return new[] { HumanBodyBones.Hips,
                        HumanBodyBones.Spine };
                case BodyPartCategory.Shoulders:
                    return leftSide
                        ? new[] { HumanBodyBones.LeftShoulder,
                            HumanBodyBones.LeftUpperArm }
                        : new[] { HumanBodyBones.RightShoulder,
                            HumanBodyBones.RightUpperArm };
                case BodyPartCategory.Arms:
                    return leftSide
                        ? new[] { HumanBodyBones.LeftUpperArm,
                            HumanBodyBones.LeftLowerArm,
                            HumanBodyBones.LeftHand }
                        : new[] { HumanBodyBones.RightUpperArm,
                            HumanBodyBones.RightLowerArm,
                            HumanBodyBones.RightHand };
                case BodyPartCategory.Hands:
                    return leftSide
                        ? new[] { HumanBodyBones.LeftHand,
                            HumanBodyBones.LeftMiddleProximal,
                            HumanBodyBones.LeftMiddleDistal }
                        : new[] { HumanBodyBones.RightHand,
                            HumanBodyBones.RightMiddleProximal,
                            HumanBodyBones.RightMiddleDistal };
                case BodyPartCategory.Legs:
                    return leftSide
                        ? new[] { HumanBodyBones.LeftUpperLeg,
                            HumanBodyBones.LeftLowerLeg,
                            HumanBodyBones.LeftFoot }
                        : new[] { HumanBodyBones.RightUpperLeg,
                            HumanBodyBones.RightLowerLeg,
                            HumanBodyBones.RightFoot };
                case BodyPartCategory.Feet:
                    return leftSide
                        ? new[] { HumanBodyBones.LeftFoot,
                            HumanBodyBones.LeftToes }
                        : new[] { HumanBodyBones.RightFoot,
                            HumanBodyBones.RightToes };
                default:
                    return Array.Empty<HumanBodyBones>();
            }
        }

        private static float GetMinimumHalfView(BodyPartCategory part)
        {
            switch (part)
            {
                case BodyPartCategory.Head:
                    return 0.145f;
                case BodyPartCategory.Chest:
                    return 0.30f;
                case BodyPartCategory.Waist:
                    return 0.21f;
                case BodyPartCategory.Shoulders:
                    return 0.21f;
                case BodyPartCategory.Arms:
                    return 0.21f;
                case BodyPartCategory.Legs:
                    return 0.28f;
                case BodyPartCategory.Hands:
                case BodyPartCategory.Feet:
                    return 0.15f;
                default:
                    return 0.5f;
            }
        }

        private static float GetDistanceScale(BodyPartCategory part)
        {
            switch (part)
            {
                case BodyPartCategory.Chest:
                    return 0.82f;
                case BodyPartCategory.Waist:
                case BodyPartCategory.Shoulders:
                case BodyPartCategory.Arms:
                case BodyPartCategory.Hands:
                case BodyPartCategory.Feet:
                    return 1.045f;
                default:
                    return 1f;
            }
        }

        private static Vector2 GetViewAngles(
            BodyPartCategory part,
            bool leftSide)
        {
            switch (part)
            {
                case BodyPartCategory.Head:
                    return new Vector2(-12f, 3f);
                case BodyPartCategory.Chest:
                    return new Vector2(-30f, 13f);
                case BodyPartCategory.Waist:
                    return new Vector2(-38f, 18f);
                case BodyPartCategory.Shoulders:
                case BodyPartCategory.Arms:
                    return new Vector2(leftSide ? -38f : 38f, -18f);
                case BodyPartCategory.Hands:
                    return new Vector2(leftSide ? -38f : 38f, -18f);
                case BodyPartCategory.Legs:
                    return new Vector2(-13f, 4f);
                case BodyPartCategory.Feet:
                    return new Vector2(-30f, -20f);
                default:
                    return Vector2.zero;
            }
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

    internal sealed class DerivedAssetPrefabPickerWindow
        : CustomPopupWindow
    {
        private static readonly Vector2 PopupSize =
            new Vector2(520f, 420f);

        private IReadOnlyList<GameObject> _candidates;
        private GameObject _selected;
        private Action<GameObject> _select;
        private bool _showAsGrid;
        private SearchField _search;
        private VisualElement _options;
        private IReadOnlyList<GameObject> _filtered =
            Array.Empty<GameObject>();

        internal static void Show(
            VisualElement anchor,
            IReadOnlyList<GameObject> candidates,
            GameObject selected,
            Action<GameObject> select,
            bool showAsGrid = false)
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
            window._showAsGrid = showAsGrid;
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
            _options.EnableInClassList(
                "ee4v-asset-manager__prefab-picker-options--grid",
                _showAsGrid);
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
                        _showAsGrid ? string.Empty : path,
                        selected: prefab == _selected),
                    () => Select(selectedPrefab));
                option.tooltip = path;
                option.AddToClassList(
                    "ee4v-asset-manager__prefab-picker-option");
                option.EnableInClassList(
                    "ee4v-asset-manager__prefab-picker-option--grid",
                    _showAsGrid);
                var preview = new DerivedAssetPrefabPreview(prefab);
                preview.EnableInClassList(
                    "ee4v-asset-manager__prefab-picker-preview--grid",
                    _showAsGrid);
                option.Leading.Add(preview);
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
