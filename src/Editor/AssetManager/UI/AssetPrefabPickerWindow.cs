using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.AvatarEvaluation;
using Ee4v.Core.I18n;
using Ee4v.Core.Images;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VariantSourceOption = Ee4v.AssetManager.UI.AssetModificationWorkflowView.VariantSourceOption;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetPrefabPickerWindow : CustomPopupWindow
    {
        private const float CardSpacing = 12f;
        private const int MaximumCachedPreviews = 8;

        private sealed class ItemPreview
        {
            internal PreviewContainer Frame;
            internal CachedImage Image;
        }

        private readonly Dictionary<string, ItemPreview> _itemPreviews =
            new Dictionary<string, ItemPreview>(StringComparer.Ordinal);
        private readonly Dictionary<string, RenderTexture> _previewTextures =
            new Dictionary<string, RenderTexture>(StringComparer.Ordinal);
        private readonly LinkedList<string> _previewOrder = new LinkedList<string>();
        private IReadOnlyList<VariantSourceOption> _sources;
        private IAssetManager _manager;
        private CachedImageCache _imageCache;
        private CancellationTokenSource _thumbnailCancellation;
        private IVisualElementScheduledItem _searchRebuild;
        private IVisualElementScheduledItem _thumbnailLoading;
        private IVisualElementScheduledItem _previewRendering;
        private VariantSourceOption _source;
        private GameObject _selectedPrefab;
        private string _itemQuery = string.Empty;
        private Action<GameObject> _select;
        private SearchField _search;
        private VisualElement _sourceHeader;
        private VisualElement _browser;
        private ScrollView _scroll;
        private VisualElement _previewPanel;
        private PreviewContainer _previewFrame;
        private Image _previewImage;
        private UiTextElement _previewStatus;
        private UiTextElement _previewName;
        private UiTextElement _previewPath;
        private UiButton _addButton;
        private IReadOnlyList<VariantSourceOption> _filteredItems = Array.Empty<VariantSourceOption>();
        private IReadOnlyList<GameObject> _filteredPrefabs = Array.Empty<GameObject>();

        internal static void Show(VisualElement anchor,
            IReadOnlyList<VariantSourceOption> sources, IAssetManager manager, Action<GameObject> select)
        {
            if (anchor == null || sources == null || sources.Count == 0 || manager == null || select == null)
            {
                return;
            }
            foreach (var existing in Resources.FindObjectsOfTypeAll<AssetPrefabPickerWindow>())
            {
                existing.Close();
            }
            var window = CreateInstance<AssetPrefabPickerWindow>();
            window._sources = sources;
            window._manager = manager;
            window._imageCache = new CachedImageCache();
            window._select = select;
            window.ShowAsPopup(anchor, new Vector2(1040f, 720f));
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            AssetManagerWindowSession.PrepareWorkflowRoot(root);
            ConfigureCloseAndSubmitKeys(root, SubmitSelection);
            var popup = new CustomPopup(I18N.Get("workflow.assets.pickerTitle"),
                closeTooltip: I18N.Get("workflow.assets.closePicker"));
            var body = new VisualElement();
            body.AddToClassList("ee4v-asset-prefab-picker__body");
            _sourceHeader = new VisualElement();
            _sourceHeader.AddToClassList("ee4v-asset-prefab-picker__source");
            body.Add(_sourceHeader);
            var toolbar = new VisualElement();
            toolbar.AddToClassList("ee4v-asset-prefab-picker__toolbar");
            _search = new SearchField();
            _search.AddToClassList("ee4v-asset-prefab-picker__search");
            _search.ValueChanged += _ =>
            {
                _searchRebuild?.Pause();
                _searchRebuild = _search.schedule.Execute(RebuildOptions).StartingIn(150);
            };
            toolbar.Add(_search);
            body.Add(toolbar);
            _browser = new VisualElement();
            _browser.AddToClassList("ee4v-asset-prefab-picker__browser");
            _scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden
            };
            _scroll.AddToClassList("ee4v-asset-prefab-picker__content");
            _scroll.RegisterCallback<GeometryChangedEvent>(_ => ScheduleVisibleThumbnails());
            _scroll.contentContainer.AddToClassList("ee4v-asset-prefab-picker__options");
            _scroll.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => UpdateCardSizes());
            _scroll.verticalScroller.valueChanged += _ => ScheduleVisibleThumbnails();
            _browser.Add(_scroll);
            BuildPreviewPanel();
            _browser.Add(_previewPanel);
            body.Add(_browser);
            popup.Content.Add(body);
            SetPopup(popup);
            ShowItems();
        }

        private void BuildPreviewPanel()
        {
            _previewPanel = new VisualElement();
            _previewPanel.AddToClassList("ee4v-asset-prefab-picker__detail");
            _previewFrame = new PreviewContainer();
            _previewFrame.AddToClassList("ee4v-asset-prefab-picker__large-preview");
            _previewImage = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            _previewImage.AddToClassList("ee4v-asset-prefab-picker__image");
            _previewFrame.Content.Add(_previewImage);
            _previewFrame.Placeholder.Add(new Icon(FluentUiIcons.CreateState(
                "cube.png", UiSizeTokens.Size31, tintColor: UiColorTokens.TextMuted)));
            _previewStatus = UiTextFactory.Create(string.Empty, UiClassNames.SecondaryText,
                "ee4v-asset-prefab-picker__preview-status");
            _previewStatus.SetWhiteSpace(WhiteSpace.Normal);
            _previewFrame.Placeholder.Add(_previewStatus);
            _previewPanel.Add(_previewFrame);
            var footer = new VisualElement();
            footer.AddToClassList("ee4v-asset-prefab-picker__detail-footer");
            var metadata = new VisualElement();
            metadata.AddToClassList("ee4v-asset-prefab-picker__metadata");
            _previewName = UiTextFactory.Create(string.Empty, UiClassNames.SectionTitle,
                "ee4v-asset-prefab-picker__preview-name");
            _previewPath = UiTextFactory.Create(string.Empty, UiClassNames.SecondaryText,
                "ee4v-asset-prefab-picker__preview-path");
            metadata.Add(_previewName);
            metadata.Add(_previewPath);
            footer.Add(metadata);
            _addButton = AssetManagerControls.CreateIconTextButton(
                I18N.Get("workflow.assets.addToVariant"), "add.png", () => Select(_selectedPrefab),
                "ee4v-asset-prefab-picker__add");
            footer.Add(_addButton);
            _previewPanel.Add(footer);
        }

        private void ShowItems()
        {
            _source = null;
            _sourceHeader.Clear();
            _sourceHeader.EnableInClassList("ee4v-asset-prefab-picker__source--visible", false);
            _search.SetPlaceholder(I18N.Get("workflow.assets.searchItems"));
            _search.SetValueWithoutNotify(_itemQuery);
            RebuildOptions();
            FocusSearch();
        }

        private void ShowPrefabs(VariantSourceOption source)
        {
            _itemQuery = _search.Value;
            _source = source;
            _sourceHeader.Clear();
            _sourceHeader.EnableInClassList("ee4v-asset-prefab-picker__source--visible", true);
            _sourceHeader.Add(AssetManagerControls.CreateIconTextButton(
                I18N.Get("workflow.assets.backToItems"), "arrow_left.png", ShowItems));
            var name = UiTextFactory.Create(source.Item.Name, UiClassNames.SectionTitle,
                "ee4v-asset-prefab-picker__item-name");
            name.tooltip = source.Item.Name;
            _sourceHeader.Add(name);
            _search.SetPlaceholder(I18N.Get("workflow.assets.searchPrefabs"));
            _search.SetValueWithoutNotify(string.Empty);
            _selectedPrefab = null;
            RebuildOptions();
            FocusSearch();
        }

        private void RebuildOptions()
        {
            _searchRebuild?.Pause();
            _searchRebuild = null;
            _thumbnailLoading?.Pause();
            CancelThumbnailLoading();
            ClearItemPreviews();
            var options = _scroll.contentContainer;
            options.Clear();
            _scroll.scrollOffset = Vector2.zero;
            var query = _search.Value.Trim();
            var prefabStage = _source != null;
            _browser.EnableInClassList("ee4v-asset-prefab-picker__browser--prefabs", prefabStage);
            _filteredItems = Array.Empty<VariantSourceOption>();
            _filteredPrefabs = Array.Empty<GameObject>();
            if (!prefabStage)
            {
                SetSelectedPrefab(null);
                _filteredItems = _sources.Where(source => Matches(source.Item.Name, query)).ToArray();
                foreach (var source in _filteredItems)
                {
                    var option = new NavigationItem(new NavigationItemState(source.Item.Name,
                        I18N.Get("workflow.assets.prefabCount", source.Prefabs.Count)), () => ShowPrefabs(source));
                    option.tooltip = source.Item.Name;
                    BindSubmit(option, () => ShowPrefabs(source));
                    option.AddToClassList("ee4v-asset-prefab-picker__card");
                    option.Row.TitleText.SetWhiteSpace(WhiteSpace.Normal);
                    var frame = new PreviewContainer { pickingMode = PickingMode.Ignore };
                    frame.AddToClassList("ee4v-asset-prefab-picker__thumbnail");
                    var fallback = new Image
                    {
                        image = AssetPreview.GetMiniThumbnail(source.Prefabs.FirstOrDefault()),
                        scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore
                    };
                    fallback.AddToClassList("ee4v-asset-prefab-picker__mini-thumbnail");
                    frame.Placeholder.Add(fallback);
                    var image = new CachedImage(_imageCache);
                    image.AddToClassList("ee4v-asset-prefab-picker__image");
                    image.SetSource(source.Item.Id);
                    frame.Content.Add(image);
                    frame.SetHasContent(image.DisplayedTexture != null);
                    _itemPreviews[source.Item.Id] = new ItemPreview { Frame = frame, Image = image };
                    option.Leading.Add(frame);
                    options.Add(option);
                }
            }
            else
            {
                _filteredPrefabs = _source.Prefabs.Where(prefab => prefab != null)
                    .GroupBy(AssetDatabase.GetAssetPath, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .Where(prefab => Matches(prefab.name, query) || Matches(AssetDatabase.GetAssetPath(prefab), query))
                    .OrderBy(prefab => prefab.name, StringComparer.OrdinalIgnoreCase).ToArray();
                foreach (var prefab in _filteredPrefabs)
                {
                    var option = new NavigationItem(new NavigationItemState(prefab.name),
                        () => SetSelectedPrefab(prefab));
                    option.userData = prefab;
                    BindSubmit(option, () => SetSelectedPrefab(prefab));
                    option.tooltip = AssetDatabase.GetAssetPath(prefab);
                    option.AddToClassList("ee4v-asset-prefab-picker__prefab-row");
                    var icon = new Image
                    {
                        image = AssetPreview.GetMiniThumbnail(prefab), scaleMode = ScaleMode.ScaleToFit,
                        pickingMode = PickingMode.Ignore
                    };
                    icon.AddToClassList("ee4v-asset-prefab-picker__row-icon");
                    option.Leading.Add(icon);
                    options.Add(option);
                }
                SetSelectedPrefab(_filteredPrefabs.Contains(_selectedPrefab)
                    ? _selectedPrefab : _filteredPrefabs.FirstOrDefault());
            }
            if (options.childCount == 0)
            {
                var empty = new EmptyState(new EmptyStateState(string.Empty,
                    I18N.Get(prefabStage ? "workflow.assets.noMatchingPrefabs" : "workflow.assets.noMatchingItems")));
                empty.AddToClassList("ee4v-asset-prefab-picker__empty");
                options.Add(empty);
            }
            UpdateCardSizes();
        }

        private void SetSelectedPrefab(GameObject prefab)
        {
            _previewRendering?.Pause();
            _selectedPrefab = prefab;
            foreach (var option in _scroll.contentContainer.Children().OfType<NavigationItem>())
            {
                option.SetSelected(prefab != null && ReferenceEquals(option.userData, prefab));
            }
            _previewImage.image = null;
            _previewFrame.SetHasContent(false);
            _previewName.SetText(prefab != null ? prefab.name : string.Empty);
            _previewPath.SetText(AssetDatabase.GetAssetPath(prefab));
            _previewPath.tooltip = AssetDatabase.GetAssetPath(prefab);
            _addButton.SetEnabled(prefab != null);
            if (prefab == null)
            {
                _previewStatus.SetText(I18N.Get("workflow.assets.choosePrefab"));
                return;
            }
            var key = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
            if (_previewTextures.TryGetValue(key, out var cached))
            {
                _previewOrder.Remove(key);
                _previewOrder.AddLast(key);
                _previewImage.image = cached;
                _previewFrame.SetHasContent(true);
                return;
            }
            _previewStatus.SetText(I18N.Get("workflow.assets.loadingPreview"));
            _previewRendering = _previewFrame.schedule.Execute(() => RenderSelectedPrefab(prefab, key)).StartingIn(120);
        }

        private void RenderSelectedPrefab(GameObject prefab, string key)
        {
            if (prefab == null || _selectedPrefab != prefab) { return; }
            try
            {
                var texture = RenderPrefabPreview(prefab);
                if (texture == null)
                {
                    _previewStatus.SetText(I18N.Get("workflow.assets.noPreview"));
                    return;
                }
                _previewTextures.Add(key, texture);
                _previewOrder.AddLast(key);
                _previewImage.image = texture;
                _previewFrame.SetHasContent(true);
                while (_previewOrder.Count > MaximumCachedPreviews)
                {
                    var oldest = _previewOrder.First.Value;
                    _previewOrder.RemoveFirst();
                    ReleasePreviewTexture(_previewTextures[oldest]);
                    _previewTextures.Remove(oldest);
                }
            }
            catch (Exception exception)
            {
                _previewStatus.SetText(I18N.Get("workflow.assets.noPreview"));
                Debug.LogException(exception);
            }
        }

        private static RenderTexture RenderPrefabPreview(GameObject prefab)
        {
            using (var renderer = new AvatarPreviewRenderer(prefab, isolatedSnapshot: true))
            {
                var root = renderer.Root;
                var meshes = root.GetComponentsInChildren<Renderer>(true)
                    .Where(mesh => renderer.IsRendererVisible(mesh) &&
                        (mesh is MeshRenderer || mesh is SkinnedMeshRenderer)).ToArray();
                if (meshes.Length == 0) { return null; }
                var bounds = renderer.GetBounds(meshes[0]);
                foreach (var mesh in meshes.Skip(1)) { bounds.Encapsulate(renderer.GetBounds(mesh)); }
                var camera = renderer.Camera;
                camera.transform.rotation = Quaternion.LookRotation(-root.transform.forward, root.transform.up);
                var cameraBounds = new Bounds();
                for (var corner = 0; corner < 8; corner++)
                {
                    var point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (corner & 1) == 0 ? -1 : 1,
                        (corner & 2) == 0 ? -1 : 1,
                        (corner & 4) == 0 ? -1 : 1));
                    var relative = Quaternion.Inverse(camera.transform.rotation) * (point - bounds.center);
                    if (corner == 0) { cameraBounds = new Bounds(relative, Vector3.zero); }
                    else { cameraBounds.Encapsulate(relative); }
                }
                var halfView = Mathf.Max(0.01f, cameraBounds.extents.x, cameraBounds.extents.y);
                var distance = cameraBounds.extents.z +
                    halfView / Mathf.Tan(renderer.FieldOfView * 0.5f * Mathf.Deg2Rad) * 1.08f;
                camera.transform.position = bounds.center + root.transform.forward * distance;
                camera.aspect = 1f;
                camera.nearClipPlane = Mathf.Max(0.001f, distance - cameraBounds.extents.z - halfView);
                camera.farClipPlane = distance + Mathf.Max(10f, bounds.size.magnitude * 2f);
                var source = renderer.Render(new Rect(0f, 0f, 1024f, 1024f));
                if (source == null) { return null; }
                var texture = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear
                };
                var previousTarget = RenderTexture.active;
                var previousSrgb = GL.sRGBWrite;
                try
                {
                    GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                    Graphics.Blit(source, texture);
                    return texture;
                }
                catch
                {
                    ReleasePreviewTexture(texture);
                    throw;
                }
                finally
                {
                    RenderTexture.active = previousTarget;
                    GL.sRGBWrite = previousSrgb;
                }
            }
        }

        private static void ReleasePreviewTexture(RenderTexture texture)
        {
            if (texture == null) { return; }
            texture.Release();
            DestroyImmediate(texture);
        }

        private static void BindSubmit(NavigationItem option, Action submit)
        {
            option.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) { return; }
                submit();
                evt.StopPropagation();
                evt.PreventDefault();
            });
        }

        private void UpdateCardSizes()
        {
            if (_source != null) { return; }
            var width = _scroll.contentContainer.contentRect.width;
            if (float.IsNaN(width) || width <= 0f) { return; }
            var columns = Mathf.Clamp(Mathf.FloorToInt((width + CardSpacing) / 224f), 1, 5);
            var cardWidth = Mathf.Max(1f, (width - columns * CardSpacing) / columns);
            foreach (var card in _scroll.contentContainer.Children().OfType<NavigationItem>())
            {
                card.style.width = cardWidth;
                var preview = card.Leading.Q<PreviewContainer>();
                if (preview != null) { preview.style.height = Mathf.Clamp(cardWidth - 16f, 120f, 220f); }
            }
            ScheduleVisibleThumbnails();
        }

        private void ScheduleVisibleThumbnails()
        {
            if (_source != null) { return; }
            _thumbnailLoading?.Pause();
            CancelThumbnailLoading();
            _thumbnailLoading = _scroll.schedule.Execute(() =>
            {
                var viewport = _scroll.contentViewport.worldBound;
                viewport.yMin -= 240f;
                viewport.yMax += 240f;
                var visibleIds = _itemPreviews.Where(pair => pair.Value.Frame.worldBound.Overlaps(viewport))
                    .Select(pair => pair.Key).ToArray();
                _ = LoadItemThumbnailsAsync(visibleIds);
            }).StartingIn(80);
        }

        private async Task LoadItemThumbnailsAsync(IReadOnlyList<string> itemIds)
        {
            var pending = itemIds.Where(id => !_imageCache.HasSource(id)).ToArray();
            if (pending.Length == 0) { return; }
            var manager = _manager;
            var cache = _imageCache;
            var cancellation = new CancellationTokenSource();
            _thumbnailCancellation = cancellation;
            try
            {
                await Task.Yield();
                cancellation.Token.ThrowIfCancellationRequested();
                using (var gate = new SemaphoreSlim(4, 4))
                {
                    await Task.WhenAll(pending.Select(async id =>
                    {
                        await gate.WaitAsync(cancellation.Token);
                        try
                        {
                            var thumbnail = await manager.GetThumbnail(id, cancellation.Token);
                            if (!ReferenceEquals(_thumbnailCancellation, cancellation)) { return; }
                            cache.SetSource(id, thumbnail != null && thumbnail.Found ? thumbnail.Data : null);
                            if (_itemPreviews.TryGetValue(id, out var preview))
                            {
                                preview.Image.SetSource(id);
                                preview.Frame.SetHasContent(preview.Image.DisplayedTexture != null);
                            }
                        }
                        finally { gate.Release(); }
                    }));
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { Debug.LogException(exception); }
            finally
            {
                if (ReferenceEquals(_thumbnailCancellation, cancellation)) { _thumbnailCancellation = null; }
                cancellation.Dispose();
            }
        }

        private void CancelThumbnailLoading()
        {
            _thumbnailCancellation?.Cancel();
            _thumbnailCancellation = null;
        }

        private void ClearItemPreviews()
        {
            foreach (var preview in _itemPreviews.Values) { preview.Image.Dispose(); }
            _itemPreviews.Clear();
        }

        private static bool Matches(string value, string query)
        {
            return string.IsNullOrEmpty(query) ||
                (value ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void FocusSearch()
        {
            _search.schedule.Execute(() => _search.Q<TextField>()?.Focus());
        }

        private void SubmitSelection()
        {
            if (_searchRebuild != null) { RebuildOptions(); }
            if (_source == null && _filteredItems.Count > 0) { ShowPrefabs(_filteredItems[0]); }
            else if (_source != null) { Select(_selectedPrefab); }
        }

        private void Select(GameObject prefab)
        {
            if (prefab == null) { return; }
            var select = _select;
            Close();
            select?.Invoke(prefab);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            CancelThumbnailLoading();
            ClearItemPreviews();
            _searchRebuild?.Pause();
            _thumbnailLoading?.Pause();
            _previewRendering?.Pause();
            if (_previewImage != null) { _previewImage.image = null; }
            foreach (var texture in _previewTextures.Values) { ReleasePreviewTexture(texture); }
            _previewTextures.Clear();
            _previewOrder.Clear();
            _imageCache?.Dispose();
            _imageCache = null;
            _manager = null;
            _select = null;
            _sources = null;
            _source = null;
            _selectedPrefab = null;
            _filteredItems = Array.Empty<VariantSourceOption>();
            _filteredPrefabs = Array.Empty<GameObject>();
        }
    }
}
