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

    internal sealed class DerivedAssetPrefabPreview : PreviewSurface
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
                "ee4v-asset-manager__prefab-preview-surface");
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
        private static readonly int PreviewControlHash =
            nameof(DerivedAssetPrefabScenePreview).GetHashCode();
        private const int GridTextureSize = 64;
        private const int GridCellSize = 16;
        private const float PreviewFitPadding = 1.05f;
        private const float MinimumPreviewSize = 320f;
        private const float MaximumPreviewSize = 560f;

        private readonly PreviewSurface _surface;
        private readonly IMGUIContainer _preview;
        private readonly Icon _placeholder;
        private readonly UiButton _backgroundToggle;
        private readonly UiButton _resetView;
        private readonly PreviewOrbitController _orbit;
        private PreviewRenderUtility _utility;
        private Texture2D _gridTexture;
        private GameObject _prefab;
        private GameObject _instance;
        private Bounds _bounds;
        private bool _lightBackground;

        internal DerivedAssetPrefabScenePreview()
        {
            AddToClassList(
                "ee4v-asset-manager__prefab-scene-preview");

            _surface = new PreviewSurface();
            _surface.AddToClassList(
                "ee4v-asset-manager__prefab-scene-preview-surface");
            _surface.Overlay.AddToClassList(
                "ee4v-asset-manager__prefab-scene-preview-overlay");
            _placeholder = new Icon(
                AssetManagerControls.LoadFluentIconState(
                    "cube.png",
                    UiSizeTokens.Size31,
                    tintColor: UiColorTokens.TextMuted));
            _placeholder.AddToClassList(
                "ee4v-asset-manager__prefab-scene-preview-placeholder");
            _surface.Overlay.Add(_placeholder);
            var actions = new VisualElement();
            actions.AddToClassList(
                "ee4v-asset-manager__prefab-scene-preview-actions");
            _lightBackground = !EditorGUIUtility.isProSkin;
            _backgroundToggle = AssetManagerControls.CreateIconButton(
                I18N.Get("detail.derivedAssetPreviewBackground"),
                "weather_sunny.png",
                UiSizeTokens.Size18,
                UiButtonVariant.Ghost,
                ToggleBackground,
                "ee4v-asset-manager__prefab-scene-preview-action",
                "ee4v-asset-manager__prefab-scene-preview-background");
            actions.Add(_backgroundToggle);
            _resetView = AssetManagerControls.CreateIconButton(
                I18N.Get("detail.derivedAssetPreviewReset"),
                "arrow_clockwise.png",
                UiSizeTokens.Size18,
                UiButtonVariant.Ghost,
                ResetView,
                "ee4v-asset-manager__prefab-scene-preview-action",
                "ee4v-asset-manager__prefab-scene-preview-reset");
            actions.Add(_resetView);
            _surface.Overlay.Add(actions);
            RefreshBackgroundToggle();

            _preview = new IMGUIContainer(DrawPreview);
            _orbit = new PreviewOrbitController(
                PreviewControlHash,
                _preview.MarkDirtyRepaint);
            _preview.AddToClassList(
                "ee4v-asset-manager__prefab-scene-preview-render");
            _surface.Content.Add(_preview);
            Add(_surface);
            _surface.SetHasContent(true);
            SetPreviewAvailable(false);

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                EnsureGridTexture();
                RebuildPreview();
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                CleanupPreview();
                DestroyGridTexture();
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

        public void Dispose()
        {
            CleanupPreview();
            DestroyGridTexture();
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
                EnsureGridTexture();
                _instance = UnityEngine.Object.Instantiate(_prefab);
                _instance.name =
                    _prefab.name + " (Derived Asset Preview)";
                SetHideFlags(_instance.transform);
                foreach (var renderer in _instance
                             .GetComponentsInChildren<
                                 SkinnedMeshRenderer>(true))
                {
                    renderer.forceMatrixRecalculationPerRender = true;
                }

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
                SetPreviewAvailable(true);
                ResetView();
            }
            catch (Exception exception)
            {
                CleanupPreview();
                Debug.LogException(exception);
            }
        }

        private void DrawPreview()
        {
            var rect = GUILayoutUtility.GetRect(
                1f,
                10000f,
                1f,
                10000f,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
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
            if (current.type != EventType.Repaint)
            {
                return;
            }

            DrawGrid(rect);
            if (_utility == null || _instance == null)
            {
                return;
            }

            ConfigureCamera();
            _utility.BeginPreview(rect, GUIStyle.none);
            _utility.camera.Render();
            var texture = _utility.EndPreview();
            GUI.DrawTexture(
                rect,
                texture,
                ScaleMode.StretchToFill,
                true);
        }

        internal void ResetView()
        {
            if (_instance == null)
            {
                return;
            }

            var previewRect = _preview.contentRect;
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
        }

        private void ConfigureCamera()
        {
            _orbit.ConfigureCamera(
                _utility.camera,
                _instance.transform.rotation);
        }

        private void DrawGrid(Rect rect)
        {
            if (_gridTexture == null)
            {
                return;
            }

            GUI.DrawTextureWithTexCoords(
                rect,
                _gridTexture,
                new Rect(
                    0f,
                    0f,
                    rect.width / GridTextureSize,
                    rect.height / GridTextureSize),
                false);
        }

        private void ToggleBackground()
        {
            _lightBackground = !_lightBackground;
            DestroyGridTexture();
            EnsureGridTexture();
            RefreshBackgroundToggle();
            _preview.MarkDirtyRepaint();
        }

        private void RefreshBackgroundToggle()
        {
            _backgroundToggle.SetIcon(
                AssetManagerControls.LoadFluentIconState(
                    _lightBackground
                        ? "weather_moon.png"
                        : "weather_sunny.png",
                    UiSizeTokens.Size18,
                    tintColor: UiColorTokens.TextOnState));
            _backgroundToggle.EnableInClassList(
                "ee4v-asset-manager__prefab-scene-preview-background--light",
                _lightBackground);
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            var size = Mathf.Clamp(
                evt.newRect.width,
                MinimumPreviewSize,
                MaximumPreviewSize);
            if (Mathf.Abs(evt.newRect.height - size) < 0.5f)
            {
                return;
            }

            style.height = size;
            _preview.MarkDirtyRepaint();
        }

        private static Bounds CalculateBounds(GameObject instance)
        {
            var bounds = new Bounds(
                instance.transform.position,
                Vector3.one * 0.2f);
            var hasBounds = false;
            foreach (var renderer in instance
                         .GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled)
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

        private static void SetHideFlags(Transform transform)
        {
            transform.gameObject.hideFlags = HideFlags.HideAndDontSave;
            for (var index = 0; index < transform.childCount; index++)
            {
                SetHideFlags(transform.GetChild(index));
            }
        }

        private void EnsureGridTexture()
        {
            if (_gridTexture != null)
            {
                return;
            }

            var baseColor = _lightBackground
                ? new Color32(96, 100, 111, 255)
                : new Color32(31, 33, 36, 255);
            var minorColor = _lightBackground
                ? new Color32(109, 113, 123, 255)
                : new Color32(43, 46, 51, 255);
            var majorColor = _lightBackground
                ? new Color32(136, 139, 150, 255)
                : new Color32(61, 65, 72, 255);
            var pixels = new Color32[
                GridTextureSize * GridTextureSize];
            for (var y = 0; y < GridTextureSize; y++)
            {
                for (var x = 0; x < GridTextureSize; x++)
                {
                    var major = x == 0 || y == 0;
                    var minor = x % GridCellSize == 0 ||
                                y % GridCellSize == 0;
                    pixels[(y * GridTextureSize) + x] = major
                        ? majorColor
                        : minor
                            ? minorColor
                            : baseColor;
                }
            }

            _gridTexture = new Texture2D(
                GridTextureSize,
                GridTextureSize,
                TextureFormat.RGBA32,
                false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            _gridTexture.SetPixels32(pixels);
            _gridTexture.Apply(false, true);
        }

        private void DestroyGridTexture()
        {
            if (_gridTexture == null)
            {
                return;
            }

            UnityEngine.Object.DestroyImmediate(_gridTexture);
            _gridTexture = null;
        }

        private void SetPreviewAvailable(bool available)
        {
            _placeholder.style.display = available
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            _resetView.SetEnabled(available);
        }

        private void CleanupPreview()
        {
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
            _preview.MarkDirtyRepaint();
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
