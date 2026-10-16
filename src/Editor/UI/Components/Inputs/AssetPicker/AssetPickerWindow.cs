using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ee4v.Core.EditorIntegration;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Ee4v.UI
{
    internal sealed partial class AssetPickerWindow : CustomPopupWindow
    {
        private Type _type;
        private Object _current;
        private GameObject _context;
        private IReadOnlyDictionary<string, AssetPickerOrigin> _origins;
        private readonly List<AssetPickerCandidate> _candidates = new List<AssetPickerCandidate>();
        private readonly List<AssetPickerCandidate> _textureMaterials = new List<AssetPickerCandidate>();
        private MaterialUvUsageIndex _uv;
        private MaterialUvUsageIndex _projectUv;
        private IEnumerator<IReadOnlyList<AssetPickerCandidate>> _assets;
        private IEnumerator<IReadOnlyList<AssetPickerCandidate>> _materialAssets;
        private IEnumerator<bool> _usage;
        private IVisualElementScheduledItem _loading;
        private SearchField _search;
        private CustomPopup _popup;
        private ObjectField _referenceField;
        private PopupField<AssetPickerAnimationFilter> _animationFilterField;
        private ListView _list;
        private UiTextElement _status;
        private StatusOverlay _busy;
        private VisualElement _busyHost;
        private double _nextRefresh;
        private bool _failed;
        private bool _initialized;
        private bool _scanningProject;
        private bool _scanningContext;
        private int _cacheVersion;
        private Object _previewAsset;
        private PreviewPane _previewPane;
        private Image _previewImage;
        private UiTextElement _previewPlaceholder;
        private UiTextElement _previewDetails;
        private VisualElement _previewDetailsHost;
        private VisualElement _previewStage;
        private VisualElement _animationDetails;
        private UiTextElement _animationSummary;
        private ScrollView _animationProperties;
        private AnimationClip _detailsClip;
        private int _detailsVersion = -1;
        private UiButton _showInProject;
        private UiButton _materialView;
        private UiButton _textureView;
        private bool _showTexture;
        private IVisualElementScheduledItem _previewRefresh;
        private int _previewAttempts;
        private const string RelatedGroup = "related";
        private string _groupKey;
        private string _overviewQuery = string.Empty;
        private string _folder = string.Empty;
        private VisualElement _sourceHeader;
        private PickerBrowser _browser;
        private ScrollView _overview;
        private VisualElement _overviewPane;
        private VisualElement _cards;
        private VisualElement _candidatePane;
        private UiTextElement _candidateCount;
        private MaterialEditor _materialEditor;
        private IMGUIContainer _materialPreview;
        private IVisualElementScheduledItem _thumbnailRefresh;
        private readonly List<ThumbnailRequest> _thumbnails = new List<ThumbnailRequest>();
        private readonly List<CandidateRow> _visibleRows = new List<CandidateRow>();
        private readonly Dictionary<Object, Texture> _thumbnailTextures = new Dictionary<Object, Texture>();
        private bool _selectFirstInGroup;
        private CancellationTokenSource _sourceThumbnailCancellation;
        private readonly Dictionary<string, SourcePreview> _sourcePreviews = new Dictionary<string, SourcePreview>();
        private readonly Dictionary<string, Texture2D> _sourceTextures = new Dictionary<string, Texture2D>();
        private readonly HashSet<string> _missingSourceThumbnails = new HashSet<string>();
        private int _sourceThumbnailLoads;
        private bool _refreshingList;
        private AssetPickerAnimationFilter _animationFilter;

        private bool BrowsingTextures => typeof(Texture).IsAssignableFrom(_type);
        private bool BrowsingAllTypes => _type == typeof(Object);
        private Object ReferencePreview => BrowsingTextures ? (_current as Material)?.mainTexture : _current;

        private static string TypeName(Type type) => type == typeof(Object)
            ? UiLocalization.Get("ui.assetPicker.allTypes") : type.Name;

        private sealed class SourcePreview
        {
            internal NavigationItem Card;
            internal PreviewContainer Frame;
            internal Image Image;
            internal AssetPickerOrigin Origin;
            internal AssetPickerCandidate Fallback;
            internal bool FixedIcon;
        }

        private sealed class ThumbnailRequest
        {
            internal Object Asset;
            internal Image Image;
            internal int Attempts;
        }

        private sealed class CandidateRow
        {
            internal AssetPickerCandidate Candidate;
            internal string Folder;
            internal int Count;
        }

        internal static void Show(VisualElement anchor, Type type, Object current, GameObject context,
            AssetPickerAnimationFilter animationFilter = AssetPickerAnimationFilter.All)
        {
            Create(type, current, context, animationFilter).ShowAsPopup(anchor, new Vector2(1040f, 720f));
        }

        internal static void Show(Vector2 screenPosition, Type type, Object current, GameObject context,
            AssetPickerAnimationFilter animationFilter = AssetPickerAnimationFilter.All, string groupKey = null)
        {
            Create(type, current, context, animationFilter, groupKey).ShowAsPopup(screenPosition, new Vector2(1040f, 720f));
        }

        private static AssetPickerWindow Create(Type type, Object current, GameObject context,
            AssetPickerAnimationFilter animationFilter, string groupKey = null)
        {
            foreach (var existing in Resources.FindObjectsOfTypeAll<AssetPickerWindow>()) existing.Close();
            var window = CreateInstance<AssetPickerWindow>();
            window._type = type;
            window._animationFilter = typeof(AnimationClip).IsAssignableFrom(type)
                ? animationFilter : AssetPickerAnimationFilter.All;
            var referenceType = AssetPicker.GetReferenceType(type);
            window._current = current != null && referenceType != null && referenceType.IsInstanceOfType(current)
                ? current : null;
            window._previewAsset = window.ReferencePreview;
            window._context = context;
            window._groupKey = groupKey != null && groupKey != RelatedGroup ? groupKey :
                window._current == null ? null : RelatedGroup;
            return window;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            _sourceThumbnailCancellation = new CancellationTokenSource();
            root.Clear();
            UiComposition.Prepare(root);
            root.RegisterCallback<GeometryChangedEvent>(evt =>
                root.EnableInClassList("ee4v-ui-asset-picker--compact", evt.newRect.width < 720f));
            ConfigureCloseAndSubmitKeys(root);
            _popup = new CustomPopup(UiLocalization.Get("ui.assetPicker.title") + " · " + TypeName(_type),
                showFooter: false,
                closeTooltip: UiLocalization.Get("ui.dismiss.tooltip"));
            var body = new VisualElement();
            body.AddToClassList("ee4v-ui-asset-picker__body");
            var toolbar = new VisualElement();
            toolbar.AddToClassList("ee4v-ui-asset-picker__toolbar");
            _sourceHeader = new VisualElement();
            _sourceHeader.AddToClassList("ee4v-ui-asset-picker__source");
            toolbar.Add(_sourceHeader);
            toolbar.Add(BuildReferenceControls());
            _search = new SearchField(new SearchFieldState(
                placeholder: UiLocalization.Get("ui.assetPicker.search"),
                searchIconState: FluentUiIcons.CreateState("search.png", UiSizeTokens.Size14),
                clearIconState: FluentUiIcons.CreateState("dismiss.png", UiSizeTokens.Size10)));
            _search.ValueChanged += _ => RefreshOptions();
            toolbar.Add(_search);
            body.Add(toolbar);
            _busy = new StatusOverlay(new StatusOverlayState(true, UiLocalization.Get("ui.assetPicker.loading")));
            _busy.AddToClassList("ee4v-ui-asset-picker__busy");
            _busyHost = new VisualElement { pickingMode = PickingMode.Ignore };
            _busyHost.AddToClassList("ee4v-ui-asset-picker__busy-host");
            _busyHost.Add(_busy);
            _status = UiTextFactory.Create(string.Empty);
            _status.AddToClassList("ee4v-ui-asset-picker__status--hidden");
            body.Add(_status);
            _browser = new PickerBrowser();
            _browser.AddToClassList("ee4v-ui-asset-picker__split");
            _overviewPane = new VisualElement();
            _overviewPane.AddToClassList("ee4v-ui-asset-picker__overview-pane");
            _overview = new ScrollView(ScrollViewMode.Vertical);
            _overview.AddToClassList("ee4v-ui-asset-picker__overview");
            _cards = new VisualElement();
            _cards.AddToClassList("ee4v-ui-asset-picker__cards");
            _overview.Add(_cards);
            _cards.RegisterCallback<GeometryChangedEvent>(_ => UpdateCardSizes());
            _overviewPane.Add(_overview);
            _candidatePane = new VisualElement();
            _candidatePane.AddToClassList("ee4v-ui-asset-picker__candidate-pane");
            _candidateCount = UiTextFactory.Create(string.Empty);
            _candidateCount.AddToClassList("ee4v-ui-asset-picker__count");
            _candidatePane.Add(_candidateCount);
            _list = new ListView
            {
                itemsSource = _visibleRows,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                fixedItemHeight = 76,
                selectionType = SelectionType.Single,
                makeItem = () =>
                {
                    var container = new VisualElement();
                    var heading = UiTextFactory.Create(string.Empty, UiClassNames.SecondaryText,
                        "ee4v-ui-asset-picker__folder-heading");
                    heading.name = "folderHeading";
                    heading.SetWhiteSpace(WhiteSpace.Normal);
                    heading.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                    container.Add(heading);
                    var item = new NavigationItem { name = "assetOption" };
                    item.AddToClassList("ee4v-ui-asset-picker__option");
                    var thumbnail = new Image { name = "assetThumbnail", scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                    thumbnail.AddToClassList("ee4v-ui-asset-picker__thumbnail");
                    item.Leading.Add(thumbnail);
                    item.clicked += () =>
                    {
                        if (item.userData is CandidateRow row)
                        {
                            _list.SetSelection(_visibleRows.IndexOf(row));
                        }
                    };
                    item.AddManipulator(new AssetDragManipulator(
                        () => (item.userData as CandidateRow)?.Candidate?.Asset));
                    container.Add(item);
                    return container;
                },
                bindItem = (element, index) => BindCandidateRow(element, _visibleRows[index])
            };
            _list.selectionChanged += selection =>
            {
                if (_refreshingList) return;
                var candidate = selection.OfType<CandidateRow>().FirstOrDefault()?.Candidate;
                if (candidate != null) ShowPreview(candidate.Asset);
            };
            _list.AddToClassList("ee4v-ui-asset-picker__list");
            _candidatePane.Add(_list);
            _browser.SetPanels(_overviewPane, _candidatePane, BuildPreview(), _cards);
            body.Add(_browser);
            _popup.Content.Add(body);
            SetPopup(_popup);
            ShowPreview(_previewAsset);
            RefreshBrowserVisibility();
            _loading = root.schedule.Execute(LoadNext).StartingIn(16).Every(16);
            _thumbnailRefresh = root.schedule.Execute(RefreshThumbnails).Every(100);
            _search.schedule.Execute(() => _search.Q<TextField>()?.Focus());
        }

        private void BindCandidateRow(VisualElement element, CandidateRow row)
        {
            var heading = element.Q<UiTextElement>("folderHeading");
            heading.EnableInClassList("ee4v-ui-asset-picker__hidden", row.Count == 0);
            heading.SetText(row.Count == 0 ? string.Empty : row.Folder + " · " + row.Count);
            heading.tooltip = row.Folder;
            var item = element.Q<NavigationItem>("assetOption");
            item.userData = row;
            var thumbnail = item.Leading.Q<Image>("assetThumbnail");
            var candidate = row.Candidate;
            var usage = _uv?.DescribeUsage(candidate.Key);
            var description = _groupKey == RelatedGroup
                ? (candidate.Match == AssetPickerMatch.Other ? string.Empty :
                    UiLocalization.Get("ui.assetPicker." + candidate.Match) + " · ") + candidate.Folder
                : BrowsingAllTypes ? candidate.AssetTypeName : string.Empty;
            item.SetState(new NavigationItemState(candidate.Name, description,
                selected: candidate.IsAsset(_previewAsset)));
            thumbnail.tintColor = Color.white;
            BindThumbnail(thumbnail, candidate.Asset);
            item.tooltip = OriginName(candidate) + "\n" + candidate.Path + "\n" + candidate.Guid + ":" + candidate.LocalId +
                (string.IsNullOrEmpty(usage) ? string.Empty : "\n" + usage);
        }

        private VisualElement BuildReferenceControls()
        {
            var controls = new VisualElement();
            controls.AddToClassList("ee4v-ui-asset-picker__reference-controls");
            var types = new List<Type> { typeof(Object), typeof(Material), typeof(Texture), typeof(AnimationClip) };
            if (!types.Contains(_type)) types.Add(_type);
            var typePicker = UiTextFactory.CreatePopupField(UiLocalization.Get("ui.assetPicker.assetType"),
                types, types.IndexOf(_type), TypeName, TypeName);
            typePicker.AddToClassList("ee4v-ui-asset-picker__type-filter");
            typePicker.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == _type) return;
                SwitchType(evt.newValue);
            });
            controls.Add(typePicker);
            var choices = new List<AssetPickerAnimationFilter> { AssetPickerAnimationFilter.All, AssetPickerAnimationFilter.Facial };
            Func<AssetPickerAnimationFilter, string> label = value => UiLocalization.Get("ui.assetPicker.animation" + value);
            _animationFilterField = UiTextFactory.CreatePopupField(UiLocalization.Get("ui.assetPicker.animationFilter"),
                choices, choices.IndexOf(_animationFilter), label, label,
                "ee4v-ui-asset-picker__animation-filter");
            _animationFilterField.RegisterValueChangedCallback(evt =>
            {
                _animationFilter = evt.newValue;
                _selectFirstInGroup = true;
                RefreshOptions();
            });
            controls.Add(_animationFilterField);
            _referenceField = UiTextFactory.CreateObjectField(UiLocalization.Get("ui.assetPicker.reference"),
                "ee4v-ui-asset-picker__reference");
            _referenceField.allowSceneObjects = false;
            _referenceField.RegisterValueChangedCallback(evt =>
            {
                _current = evt.newValue;
                _groupKey = _current == null ? null : RelatedGroup;
                _folder = string.Empty;
                _selectFirstInGroup = true;
                ShowPreview(ReferencePreview);
                RefreshOptions();
            });
            controls.Add(_referenceField);
            RefreshReferenceControls();
            return controls;
        }

        private void RefreshReferenceControls()
        {
            var referenceType = AssetPicker.GetReferenceType(_type);
            _referenceField.objectType = referenceType ?? typeof(Object);
            _referenceField.SetValueWithoutNotify(_current);
            _referenceField.EnableInClassList("ee4v-ui-asset-picker__hidden", referenceType == null);
            _animationFilterField.SetValueWithoutNotify(_animationFilter);
            _animationFilterField.EnableInClassList("ee4v-ui-asset-picker__hidden",
                !typeof(AnimationClip).IsAssignableFrom(_type));
        }

        private void SwitchType(Type type)
        {
            var preview = _previewAsset;
            ResetInvestigation();
            _type = type;
            var referenceType = AssetPicker.GetReferenceType(type);
            if (_current != null && (referenceType == null || !referenceType.IsInstanceOfType(_current)))
                _current = null;
            if (!typeof(AnimationClip).IsAssignableFrom(type)) _animationFilter = AssetPickerAnimationFilter.All;
            if (_groupKey == RelatedGroup && _current == null) _groupKey = null;
            _folder = string.Empty;
            _popup.SetTitle(UiLocalization.Get("ui.assetPicker.title") + " · " + TypeName(type));
            RefreshReferenceControls();
            ShowPreview(preview != null && type.IsInstanceOfType(preview) ? preview : ReferencePreview);
            _selectFirstInGroup = _previewAsset == null && _groupKey != null;
            try { InitializeInvestigation(); }
            catch (Exception exception) { Fail(exception); }
            RefreshOptions();
        }

        private VisualElement BuildPreview()
        {
            _previewPane = new PreviewPane(UiLocalization.Get("ui.assetPicker.preview"));
            _previewPane.AddToClassList("ee4v-ui-asset-picker__preview");
            _showInProject = new UiButton(UiLocalization.Get("ui.assetPicker.showInProject"),
                () => EditorGUIUtility.PingObject(_previewAsset), UiLocalization.Get("ui.assetPicker.dragHint"),
                variant: UiButtonVariant.Ghost);
            _showInProject.AddManipulator(new AssetDragManipulator(() => _previewAsset));
            _previewPane.Actions.Add(_showInProject);
            _materialView = new UiButton(UiLocalization.Get("ui.assetPicker.materialView"), () =>
            {
                _showTexture = false;
                ShowPreview(_previewAsset);
            }, variant: UiButtonVariant.Ghost);
            _textureView = new UiButton(UiLocalization.Get("ui.assetPicker.textureView"), () =>
            {
                _showTexture = true;
                ShowPreview(_previewAsset);
            }, variant: UiButtonVariant.Ghost);
            _previewPane.Actions.Add(_materialView);
            _previewPane.Actions.Add(_textureView);
            var stage = new VisualElement();
            stage.AddToClassList("ee4v-ui-asset-picker__stage");
            _previewStage = stage;
            _previewImage = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            _previewImage.AddToClassList("ee4v-ui-asset-picker__image");
            stage.Add(_previewImage);
            _materialPreview = new IMGUIContainer(DrawMaterialPreview);
            _materialPreview.AddToClassList("ee4v-ui-asset-picker__image");
            _materialPreview.AddToClassList("ee4v-ui-asset-picker__hidden");
            stage.Add(_materialPreview);
            _previewPlaceholder = UiTextFactory.Create(UiLocalization.Get("ui.assetPicker.previewEmpty"));
            _previewPlaceholder.AddToClassList("ee4v-ui-asset-picker__placeholder");
            _previewPlaceholder.SetWhiteSpace(WhiteSpace.Normal);
            stage.Add(_previewPlaceholder);
            _previewPane.Content.Add(stage);
            var details = new ScrollView(ScrollViewMode.Vertical);
            details.AddToClassList("ee4v-ui-asset-picker__details");
            _previewDetails = UiTextFactory.Create(string.Empty);
            _previewDetails.SetWhiteSpace(WhiteSpace.Normal);
            details.Add(_previewDetails);
            _previewPane.Content.Add(details);
            _previewDetailsHost = details;
            _animationDetails = BuildAnimationDetails();
            _previewPane.Content.Add(_animationDetails);
            return _previewPane;
        }

        private void ShowPreview(Object asset)
        {
            _previewRefresh?.Pause();
            _previewRefresh = null;
            _previewAsset = asset;
            _previewAttempts = 0;
            if (_materialEditor != null && (_materialEditor.target != asset || _showTexture))
            {
                DestroyImmediate(_materialEditor);
                _materialEditor = null;
            }
            _materialPreview.AddToClassList("ee4v-ui-asset-picker__hidden");
            if (!(asset is Material textureMaterial) || !(textureMaterial.mainTexture is Texture2D)) _showTexture = false;
            _previewPane.SetTitle(asset == null ? UiLocalization.Get("ui.assetPicker.preview") : asset.name);
            _showInProject?.SetEnabled(asset != null && EditorUtility.IsPersistent(asset));
            _textureView?.SetEnabled(asset is Material material && material.mainTexture is Texture2D);
            _materialView?.EnableInClassList("ee4v-ui-asset-picker__hidden", !(asset is Material));
            _textureView?.EnableInClassList("ee4v-ui-asset-picker__hidden", !(asset is Material));
            _previewStage.EnableInClassList("ee4v-ui-asset-picker__hidden", asset is AnimationClip);
            _previewDetailsHost?.EnableInClassList("ee4v-ui-asset-picker__hidden",
                asset == null || asset is Material || asset is AnimationClip);
            _animationDetails?.EnableInClassList("ee4v-ui-asset-picker__hidden", !(asset is AnimationClip));
            _materialView?.EnableInClassList("ee4v-ui-asset-picker__view--selected", !_showTexture);
            _textureView?.EnableInClassList("ee4v-ui-asset-picker__view--selected", _showTexture);
            RefreshPreviewDetails();
            _list?.RefreshItems();
            if (asset == null)
            {
                SetPreviewTexture(null);
                return;
            }
            if (asset is AnimationClip)
            {
                SetPreviewTexture(null);
                return;
            }
            var texture = asset as Texture2D;
            if (_showTexture && asset is Material selectedMaterial) texture = selectedMaterial.mainTexture as Texture2D;
            if (texture != null)
            {
                SetPreviewTexture(texture);
                return;
            }
            if (asset is Material)
            {
                if (_materialEditor == null) _materialEditor = Editor.CreateEditor(asset, typeof(MaterialEditor)) as MaterialEditor;
                SetPreviewTexture(null);
                if (_materialEditor != null)
                {
                    _previewPlaceholder.style.display = DisplayStyle.None;
                    _materialPreview.RemoveFromClassList("ee4v-ui-asset-picker__hidden");
                    _materialPreview.MarkDirtyRepaint();
                }
                return;
            }
            SetPreviewTexture(AssetPreview.GetAssetPreview(asset));
            if (_previewImage.image == null)
                _previewRefresh = rootVisualElement.schedule.Execute(PollPreview).Every(100);
        }

        private void DrawMaterialPreview()
        {
            if (_materialEditor == null || _materialEditor.target == null || _showTexture) return;
            var rect = _materialPreview.contentRect;
            if (rect.width < 2f || rect.height < 2f) return;
            try
            {
                // MaterialEditor renders at the viewport's pixel size and supports rotation.
                _materialEditor.OnInteractivePreviewGUI(rect, GUIStyle.none);
            }
            catch (Exception exception)
            {
                DestroyImmediate(_materialEditor);
                _materialEditor = null;
                _materialPreview.AddToClassList("ee4v-ui-asset-picker__hidden");
                SetPreviewTexture(null);
                UnityEngine.Debug.LogException(exception);
            }
        }

        private void BindThumbnail(Image image, Object asset)
        {
            if (asset == null)
            {
                image.image = AssetPreview.GetMiniTypeThumbnail(_type);
                image.userData = null;
                _thumbnails.RemoveAll(request => request.Image == image);
                return;
            }
            // A Material thumbnail must show its shaded preview, never its main texture.
            var texture = asset as Texture;
            if (texture == null && _thumbnailTextures.TryGetValue(asset, out var cached) && cached != null)
                texture = cached;
            if (texture == null) texture = AssetPreview.GetAssetPreview(asset);
            if (texture != null)
            {
                _thumbnailTextures[asset] = texture;
                image.image = texture;
                _thumbnails.RemoveAll(request => request.Image == image);
            }
            else
            {
                if (image.userData as Object != asset)
                {
                    image.image = AssetPreview.GetMiniThumbnail(asset);
                    _thumbnails.RemoveAll(request => request.Image == image);
                }
                if (!_thumbnails.Any(request => request.Image == image))
                    _thumbnails.Add(new ThumbnailRequest { Asset = asset, Image = image });
            }
            image.userData = asset;
        }

        private void ResetInvestigation()
        {
            _assets?.Dispose();
            _materialAssets?.Dispose();
            _usage?.Dispose();
            _assets = null;
            _materialAssets = null;
            _usage = null;
            _uv = null;
            _projectUv = null;
            _candidates.Clear();
            _textureMaterials.Clear();
            _thumbnails.Clear();
            _initialized = false;
            _failed = false;
            _scanningProject = false;
            _scanningContext = false;
            _nextRefresh = 0;
            _loading.Resume();
        }

        private void RefreshThumbnails()
        {
            if (_initialized && _cacheVersion != AssetPickerCache.Version)
            {
                _thumbnailTextures.Clear();
                ResetInvestigation();
            }
            RefreshSourceThumbnails();
            var checkedCount = 0;
            for (var i = _thumbnails.Count - 1; i >= 0; i--)
            {
                var request = _thumbnails[i];
                if (request.Asset == null || request.Image.userData as Object != request.Asset ||
                    request.Image.panel == null || request.Attempts > 50)
                {
                    _thumbnails.RemoveAt(i);
                    continue;
                }
                var viewport = _groupKey == null ? _overview.worldBound : _list.worldBound;
                if (!viewport.Overlaps(request.Image.worldBound)) continue;
                if (++checkedCount > 12) break;
                request.Attempts++;
                var texture = AssetPreview.GetAssetPreview(request.Asset);
                if (texture == null) continue;
                _thumbnailTextures[request.Asset] = texture;
                request.Image.image = texture;
                _thumbnails.RemoveAt(i);
            }
            if (_materialEditor != null && _groupKey != null && !_showTexture)
                _materialPreview.MarkDirtyRepaint();
        }

        private void PollPreview()
        {
            if (_previewAsset == null) { _previewRefresh?.Pause(); return; }
            var texture = AssetPreview.GetAssetPreview(_previewAsset);
            if (texture != null) SetPreviewTexture(texture);
            if (texture != null || ++_previewAttempts >= 50) _previewRefresh?.Pause();
        }

        private void SetPreviewTexture(Texture texture)
        {
            _previewImage.image = texture;
            _previewImage.EnableInClassList("ee4v-ui-asset-picker__hidden", texture == null);
            _previewPlaceholder.SetText(UiLocalization.Get(_previewAsset == null
                ? "ui.assetPicker.previewEmpty" : "ui.assetPicker.previewUnavailable"));
            _previewPlaceholder.style.display = texture != null ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void RefreshPreviewDetails()
        {
            if (_previewDetails == null) return;
            if (_previewAsset is AnimationClip clip)
            {
                RefreshAnimationDetails(clip);
                return;
            }
            _detailsClip = null;
            if (_previewAsset == null) { _previewDetails.SetText(string.Empty); return; }
            var candidate = _candidates.FirstOrDefault(entry => entry.IsAsset(_previewAsset));
            var usage = _uv?.DescribeUsage(_previewAsset as Material);
            var text = (candidate == null ? string.Empty : OriginName(candidate) + "\n") +
                AssetDatabase.GetAssetPath(_previewAsset);
            if (candidate != null && candidate.Match != AssetPickerMatch.Other)
                text += "\n" + UiLocalization.Get("ui.assetPicker." + candidate.Match);
            if (!string.IsNullOrEmpty(usage)) text += "\n" + usage;
            if (_previewAsset is Material material && material.shader != null)
                text += "\n" + material.shader.name;
            if (_previewAsset is Texture texture) text += "\n" + texture.width + " × " + texture.height;
            _previewDetails.SetText(text);
        }

        private void InitializeInvestigation()
        {
            _initialized = true;
            _cacheVersion = AssetPickerCache.Version;
            try { _origins = AssetPickerCatalog.GetOrigins(); }
            catch (Exception exception)
            {
                _origins = new Dictionary<string, AssetPickerOrigin>();
                UnityEngine.Debug.LogException(exception);
            }
            if (AssetPickerCache.TryGetCandidates(_type, _origins, out var cachedCandidates))
                _candidates.AddRange(cachedCandidates);
            else
                _assets = AssetPickerCatalog.Find(_type, _origins).GetEnumerator();
            if (BrowsingTextures)
            {
                if (AssetPickerCache.TryGetCandidates(typeof(Material), _origins, out var cachedMaterials))
                    _textureMaterials.AddRange(cachedMaterials);
                else _materialAssets = AssetPickerCatalog.Find(typeof(Material), _origins).GetEnumerator();
            }
            if (typeof(Material).IsAssignableFrom(_type) || BrowsingTextures)
            {
                if (AssetPickerCache.TryGetUvUsage(out var cachedUsage))
                {
                    _projectUv = cachedUsage;
                }
                else
                {
                    _scanningProject = true;
                    _projectUv = new MaterialUvUsageIndex();
                }
                _uv = _projectUv.CreateContextIndex();
                _scanningContext = true;
                _usage = _uv.ScanContext(_context).GetEnumerator();
            }
        }

        private void LoadNext()
        {
            try
            {
                if (!_initialized) InitializeInvestigation();
                var timer = Stopwatch.StartNew();
                do
                {
                    if (_assets != null && !_scanningContext)
                    {
                        if (_assets.MoveNext()) _candidates.AddRange(_assets.Current);
                        else
                        {
                            _assets.Dispose();
                            _assets = null;
                            AssetPickerCache.StoreCandidates(_type, _candidates, _cacheVersion);
                        }
                    }
                    else if (_materialAssets != null && !_scanningContext)
                    {
                        if (_materialAssets.MoveNext()) _textureMaterials.AddRange(_materialAssets.Current);
                        else
                        {
                            _materialAssets.Dispose();
                            _materialAssets = null;
                            AssetPickerCache.StoreCandidates(typeof(Material), _textureMaterials, _cacheVersion);
                        }
                    }
                    else if (_usage != null)
                    {
                        if (!_usage.MoveNext())
                        {
                            _usage.Dispose();
                            _usage = null;
                            if (_scanningContext)
                            {
                                _scanningContext = false;
                                if (_scanningProject) _usage = _projectUv.ScanProject().GetEnumerator();
                            }
                            else if (_scanningProject)
                            {
                                AssetPickerCache.StoreUvUsage(_projectUv, _cacheVersion);
                                _scanningProject = false;
                            }
                        }
                    }
                    else { _loading.Pause(); break; }
                } while (timer.ElapsedMilliseconds < 6);
                if (_assets == null && _materialAssets == null && _usage == null || EditorApplication.timeSinceStartup >= _nextRefresh)
                {
                    RefreshOptions();
                    _nextRefresh = EditorApplication.timeSinceStartup + 0.25;
                }
            }
            catch (Exception exception) { Fail(exception); }
        }

        private void RefreshOptions()
        {
            if (_origins == null) return;
            var query = (_search.Value ?? string.Empty).Trim();
            AssetPickerCatalog.UpdateMatches(_candidates, _current, _origins, _uv,
                BrowsingTextures ? _textureMaterials : null);
            var available = _candidates.Where(candidate => _animationFilter == AssetPickerAnimationFilter.All ||
                candidate.ClipKind == AssetPickerClipKind.Facial).ToList();
            var loading = !_failed && (!_initialized || _assets != null || _materialAssets != null || _usage != null);
            if (!loading && _groupKey != null && _groupKey != RelatedGroup &&
                !available.Any(candidate => GroupKey(candidate) == _groupKey)) _groupKey = null;
            if (!loading && _groupKey == RelatedGroup && !available.Any(IsRelated)) _groupKey = null;
            var scope = available.Where(candidate => _groupKey == null ||
                (_groupKey == RelatedGroup ? IsRelated(candidate) : GroupKey(candidate) == _groupKey)).ToList();
            if (_folder.Length > 0 && !scope.Any(candidate => candidate.Folder == _folder)) _folder = string.Empty;
            var filtered = scope.Where(candidate =>
                (_folder.Length == 0 || candidate.Folder == _folder) &&
                (query.Length == 0 || (candidate.Name + " " + OriginName(candidate) + " " + candidate.Path + " " + candidate.Guid)
                    .IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(candidate => candidate.Match)
                .ThenBy(candidate => candidate.Origin?.Name ?? candidate.Folder, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.Origin?.Id, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Folder, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.Path, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.LocalId).ToList();
            RefreshBrowserVisibility();
            RebuildSourceHeader(scope);
            if (_groupKey == null) RebuildCards(filtered);
            _candidateCount.SetText(UiLocalization.Get("ui.assetPicker.candidateCount") + " · " + filtered.Count);
            var selected = _previewAsset;
            var showFirst = false;
            var selectedIndex = -1;
            _refreshingList = true;
            try
            {
                RefreshCandidateRows(filtered);
                var virtualization = _groupKey == RelatedGroup ? CollectionVirtualizationMethod.FixedHeight
                    : CollectionVirtualizationMethod.DynamicHeight;
                if (_list.virtualizationMethod != virtualization) _list.virtualizationMethod = virtualization;
                _list.RefreshItems();
                selectedIndex = _visibleRows.FindIndex(row => row.Candidate.IsAsset(selected));
                var firstCandidate = _visibleRows.FindIndex(row => row.Candidate != null);
                showFirst = firstCandidate >= 0 && (_previewAsset == null || _selectFirstInGroup);
                if (showFirst)
                {
                    _selectFirstInGroup = false;
                    if (selectedIndex < 0) selectedIndex = firstCandidate;
                }
                if (selectedIndex >= 0) _list.SetSelectionWithoutNotify(new[] { selectedIndex });
                else _list.ClearSelection();
            }
            finally { _refreshingList = false; }
            if (showFirst) ShowPreview(_visibleRows[selectedIndex].Candidate.Asset);
            RefreshPreviewDetails();
            _busy.SetState(new StatusOverlayState(loading, UiLocalization.Get("ui.assetPicker.loading")));
            _status.SetText(_failed ? UiLocalization.Get("ui.assetPicker.failed") :
                filtered.Count == 0 ? UiLocalization.Get("ui.assetPicker.noMatch") :
                UiLocalization.Get("ui.assetPicker.ready"));
            _status.RemoveFromClassList("ee4v-ui-asset-picker__status--hidden");
            _status.style.display = !_failed && (loading || filtered.Count > 0) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void RefreshCandidateRows(List<AssetPickerCandidate> candidates)
        {
            _visibleRows.Clear();
            if (_groupKey == null) return;
            if (_groupKey == RelatedGroup)
            {
                _visibleRows.AddRange(candidates.Select(candidate => new CandidateRow { Candidate = candidate }));
                return;
            }
            foreach (var group in candidates.GroupBy(candidate => candidate.Folder, StringComparer.Ordinal)
                         .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(group => group.Key, StringComparer.Ordinal))
            {
                var first = true;
                foreach (var candidate in group.OrderBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
                             .ThenBy(candidate => candidate.Path, StringComparer.Ordinal).ThenBy(candidate => candidate.LocalId))
                {
                    _visibleRows.Add(new CandidateRow { Candidate = candidate, Folder = group.Key, Count = first ? group.Count() : 0 });
                    first = false;
                }
            }
        }

        private void RefreshBrowserVisibility()
        {
            var browsing = _groupKey == null;
            _browser.ShowDetails(!browsing);
            var loadingPane = browsing ? _overviewPane : _candidatePane;
            if (_busyHost.parent != loadingPane) loadingPane.Add(_busyHost);
        }

        private void ShowGroup(string key)
        {
            if (_groupKey == null) _overviewQuery = _search.Value;
            _groupKey = key;
            _selectFirstInGroup = key != null;
            _folder = string.Empty;
            _search.SetValueWithoutNotify(key == null ? _overviewQuery : string.Empty);
            RefreshOptions();
            _list.ScrollToItem(0);
            _search.Q<TextField>()?.Focus();
        }

        private void RebuildSourceHeader(List<AssetPickerCandidate> scope)
        {
            _sourceHeader.Clear();
            _sourceHeader.style.display = _groupKey == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (_groupKey == null)
            {
                return;
            }
            _sourceHeader.Add(new UiButton(UiLocalization.Get("ui.assetPicker.backToSources"), () => ShowGroup(null),
                icon: FluentUiIcons.CreateState("arrow_left.png", UiSizeTokens.Size14)));
            var sourceName = scope.Count > 0 ? GroupName(scope[0]) : UiLocalization.Get("ui.assetPicker.unregistered");
            if (_groupKey.StartsWith("origin:", StringComparison.Ordinal) &&
                _origins.TryGetValue(_groupKey.Substring("origin:".Length), out var origin)) sourceName = origin.Name;
            if (_groupKey == RelatedGroup) sourceName = UiLocalization.Get("ui.assetPicker.related");
            var title = UiTextFactory.Create(sourceName, UiClassNames.SectionTitle);
            title.SetFontSize(13);
            title.AddToClassList("ee4v-ui-asset-picker__source-name");
            _sourceHeader.Add(title);
            var folders = new List<string> { string.Empty };
            folders.AddRange(scope.Select(candidate => candidate.Folder).Distinct().OrderBy(value => value, StringComparer.Ordinal));
            if (!folders.Contains(_folder)) _folder = string.Empty;
            if (folders.Count <= 2) return;
            string FormatFolder(string value) => value.Length == 0 ? UiLocalization.Get("ui.assetPicker.allFolders") : value;
            var filter = UiTextFactory.CreatePopupField(string.Empty, folders, folders.IndexOf(_folder), FormatFolder, FormatFolder);
            filter.AddToClassList("ee4v-ui-asset-picker__folder-filter");
            filter.RegisterValueChangedCallback(evt => { _folder = evt.newValue; RefreshOptions(); });
            _sourceHeader.Add(filter);
        }

        private void RebuildCards(List<AssetPickerCandidate> candidates)
        {
            var keys = new List<string>();
            var related = candidates.Where(IsRelated).ToList();
            if (related.Count > 0)
            {
                AddGroupCard(RelatedGroup, UiLocalization.Get("ui.assetPicker.related"),
                    UiLocalization.Get("ui.assetPicker.relatedDescription"), related);
                keys.Add(RelatedGroup);
            }
            foreach (var group in candidates.GroupBy(GroupKey)
                         .OrderBy(group => group.First().Origin == null)
                         .ThenBy(group => GroupName(group.First()), StringComparer.OrdinalIgnoreCase))
            {
                var entries = group.ToList();
                AddGroupCard(group.Key, GroupName(entries[0]), TypeName(_type), entries);
                keys.Add(group.Key);
            }
            foreach (var key in _sourcePreviews.Keys.Where(key => !keys.Contains(key)).ToArray())
            {
                _sourcePreviews[key].Card.RemoveFromHierarchy();
                _sourcePreviews.Remove(key);
            }
            for (var index = 0; index < keys.Count; index++)
            {
                var card = _sourcePreviews[keys[index]].Card;
                if (_cards.IndexOf(card) != index) _cards.Insert(index, card);
            }
            UpdateCardSizes();
        }

        private void AddGroupCard(string key, string name, string description, List<AssetPickerCandidate> candidates)
        {
            var fixedIcon = key == RelatedGroup || candidates[0].Origin == null;
            var state = new NavigationItemState(name, description + " · " + candidates.Count);
            var tooltip = name + "\n" + string.Join("\n", candidates.Select(candidate => candidate.Folder).Distinct());
            if (_sourcePreviews.TryGetValue(key, out var existing))
            {
                existing.Card.SetState(state);
                existing.Card.tooltip = tooltip;
                existing.Origin = key == RelatedGroup ? null : candidates[0].Origin;
                existing.Fallback = fixedIcon ? null : candidates[0];
                return;
            }
            var card = new PickerBrowserCard(state, () => ShowGroup(key));
            card.AddToClassList("ee4v-ui-asset-picker__card");
            card.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                ShowGroup(key);
                evt.StopPropagation();
            });
            var frame = card.Preview;
            frame.AddToClassList("ee4v-ui-asset-picker__card-preview");
            var fallbackAsset = fixedIcon ? null : candidates[0];
            if (fixedIcon)
            {
                var icon = new Icon(FluentUiIcons.CreateState(key == RelatedGroup ? "search.png" : "folder.png", 64f,
                    tintColor: UiColorTokens.TextMuted)) { pickingMode = PickingMode.Ignore };
                frame.Placeholder.Add(icon);
            }
            else
            {
                var fallback = new Image
                {
                    image = AssetPreview.GetMiniTypeThumbnail(_type),
                    scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore
                };
                fallback.AddToClassList("ee4v-ui-asset-picker__mini-thumbnail");
                frame.Placeholder.Add(fallback);
            }
            var image = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            image.AddToClassList("ee4v-ui-asset-picker__source-image");
            frame.Content.Add(image);
            if (!fixedIcon && _sourceTextures.TryGetValue(key, out var texture)) image.image = texture;
            frame.SetHasContent(image.image != null);
            _sourcePreviews[key] = new SourcePreview
            {
                Card = card,
                Frame = frame, Image = image,
                Origin = key == RelatedGroup ? null : candidates[0].Origin,
                Fallback = fallbackAsset, FixedIcon = fixedIcon
            };
            card.tooltip = tooltip;
            _cards.Add(card);
        }

        private void UpdateCardSizes()
        {
            if (_groupKey != null) return;
            _browser.RefreshCardSizes();
        }

        private readonly HashSet<string> _pendingSourceThumbnails = new HashSet<string>();

        private void RefreshSourceThumbnails()
        {
            if (_groupKey != null || _sourceThumbnailCancellation == null) return;
            var viewport = _overview.contentViewport.worldBound;
            viewport.yMin -= 240f;
            viewport.yMax += 240f;
            foreach (var entry in _sourcePreviews)
            {
                var preview = entry.Value;
                if (preview.FixedIcon) continue;
                if (!preview.Frame.worldBound.Overlaps(viewport)) continue;
                if (_sourceTextures.TryGetValue(entry.Key, out var texture))
                {
                    preview.Image.image = texture;
                    preview.Frame.SetHasContent(true);
                    continue;
                }
                if (preview.Origin != null && !_missingSourceThumbnails.Contains(entry.Key))
                {
                    if (_sourceThumbnailLoads < 4 && _pendingSourceThumbnails.Add(entry.Key))
                        _ = LoadSourceThumbnail(entry.Key, preview.Origin, _sourceThumbnailCancellation.Token);
                    continue;
                }
                if (preview.Fallback == null) continue;
                var asset = preview.Fallback.Asset;
                if (asset == null) continue;
                var fallback = asset as Texture ?? AssetPreview.GetAssetPreview(asset);
                if (fallback == null) continue;
                preview.Image.image = fallback;
                preview.Frame.SetHasContent(true);
            }
        }

        private async Task LoadSourceThumbnail(string key, AssetPickerOrigin origin, CancellationToken cancellationToken)
        {
            _sourceThumbnailLoads++;
            Texture2D pendingTexture = null;
            try
            {
                await Task.Yield();
                var bytes = await origin.GetThumbnailAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (bytes == null || bytes.Length == 0) { _missingSourceThumbnails.Add(key); return; }
                pendingTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                if (!pendingTexture.LoadImage(bytes, true))
                {
                    _missingSourceThumbnails.Add(key);
                    return;
                }
                var texture = pendingTexture;
                _sourceTextures[key] = texture;
                pendingTexture = null;
                if (_sourcePreviews.TryGetValue(key, out var preview))
                {
                    preview.Image.image = texture;
                    preview.Frame.SetHasContent(true);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _missingSourceThumbnails.Add(key);
                    UnityEngine.Debug.LogException(exception);
                }
            }
            finally
            {
                if (pendingTexture != null) DestroyImmediate(pendingTexture);
                _sourceThumbnailLoads--;
                _pendingSourceThumbnails.Remove(key);
            }
        }

        private static bool IsRelated(AssetPickerCandidate candidate) => candidate.Match != AssetPickerMatch.Other;

        private static string GroupKey(AssetPickerCandidate candidate) => candidate.Origin == null
            ? "unregistered" : "origin:" + candidate.Origin.Id;

        private static string GroupName(AssetPickerCandidate candidate) => OriginName(candidate);

        private static string OriginName(AssetPickerCandidate candidate) =>
            candidate.Origin?.Name ?? UiLocalization.Get("ui.assetPicker.unregistered");

        private sealed class AssetDragManipulator : PointerManipulator
        {
            private readonly Func<Object> _getAsset;
            private Object _asset;
            private Vector2 _start;
            private int _pointerId;

            internal AssetDragManipulator(Func<Object> getAsset) => _getAsset = getAsset;

            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
                target.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
                target.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
                target.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
                target.RegisterCallback<DetachFromPanelEvent>(OnDetach);
            }

            protected override void UnregisterCallbacksFromTarget()
            {
                target.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
                target.UnregisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
                target.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
                target.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
                target.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
                _asset = null;
            }

            private void OnPointerDown(PointerDownEvent evt)
            {
                _asset = null;
                if (evt.button != (int)MouseButton.LeftMouse) return;
                var asset = _getAsset();
                if (asset == null || !EditorUtility.IsPersistent(asset)) return;
                _asset = asset;
                _start = evt.position;
                _pointerId = evt.pointerId;
            }

            private void OnPointerMove(PointerMoveEvent evt)
            {
                if (_asset == null || evt.pointerId != _pointerId || (evt.pressedButtons & 1) == 0 ||
                    Vector2.Distance(_start, evt.position) < 4f) return;
                var asset = _asset;
                _asset = null;
                target.ReleasePointer(evt.pointerId);
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new[] { asset };
                DragAndDrop.StartDrag(asset.name);
                evt.StopImmediatePropagation();
            }

            private void OnPointerUp(PointerUpEvent evt) => _asset = null;
            private void OnCaptureOut(PointerCaptureOutEvent evt) => _asset = null;
            private void OnDetach(DetachFromPanelEvent evt) => _asset = null;
        }

        protected override void OnLostFocus() { }

        private void Fail(Exception exception)
        {
            _failed = true;
            _loading?.Pause();
            _busy.SetState(new StatusOverlayState(false, string.Empty));
            _status.RemoveFromClassList("ee4v-ui-asset-picker__status--hidden");
            _status.SetText(UiLocalization.Get("ui.assetPicker.failed"));
            _status.style.display = DisplayStyle.Flex;
            UnityEngine.Debug.LogException(exception);
        }

        protected override void OnDisable()
        {
            _sourceThumbnailCancellation?.Cancel();
            _sourceThumbnailCancellation?.Dispose();
            _sourceThumbnailCancellation = null;
            foreach (var preview in _sourcePreviews.Values) preview.Image.image = null;
            _sourcePreviews.Clear();
            foreach (var texture in _sourceTextures.Values) if (texture != null) DestroyImmediate(texture);
            _sourceTextures.Clear();
            _thumbnailRefresh?.Pause();
            _thumbnails.Clear();
            _thumbnailTextures.Clear();
            _visibleRows.Clear();
            if (_materialEditor != null) DestroyImmediate(_materialEditor);
            _materialEditor = null;
            _previewRefresh?.Pause();
            if (_previewImage != null) _previewImage.image = null;
            _previewAsset = null;
            _detailsClip = null;
            _animationPropertyRows.Clear();
            _selectedAnimationProperty = null;
            _animationGraph?.SetProperty(null, 0f);
            _loading?.Pause();
            _assets?.Dispose();
            _materialAssets?.Dispose();
            _usage?.Dispose();
            _assets = null;
            _materialAssets = null;
            _usage = null;
            _uv = null;
            _projectUv = null;
            _origins = null;
            _context = null;
            _current = null;
            _candidates.Clear();
            _textureMaterials.Clear();
            base.OnDisable();
        }
    }
}
