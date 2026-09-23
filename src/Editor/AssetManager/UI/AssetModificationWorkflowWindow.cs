using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.FaceExpression;
using Ee4v.PhysBoneCollider;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetModificationWorkflowWindow : EditorWindow
    {
        private const float MinimumBodyScale = 0.5f;
        private const float MaximumBodyScale = 2f;
        private const float MinimumBodyBlendShapeWeight = 0f;
        private const float MaximumBodyBlendShapeWeight = 100f;
        private const string AvatarDescriptorTypeName =
            "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor";

        private static readonly string[] HeadBlendShapeTerms =
        {
            "head", "neck", "face", "facial", "hair", "ear",
            "頭", "首", "顔", "髪", "耳"
        };
        private static readonly string[] ChestBlendShapeTerms =
        {
            "chest", "breast", "bust", "torso", "rib",
            "胸", "バスト", "乳", "胴"
        };
        private static readonly string[] WaistBlendShapeTerms =
        {
            "waist", "hip", "pelvis", "belly", "stomach", "abdomen",
            "腰", "尻", "お尻", "腹", "お腹"
        };
        private static readonly string[] ShoulderBlendShapeTerms =
            { "shoulder", "肩" };
        private static readonly string[] ArmBlendShapeTerms =
            { "arm", "elbow", "腕", "肘" };
        private static readonly string[] HandBlendShapeTerms =
            { "hand", "finger", "wrist", "nail", "手", "指", "手首" };
        private static readonly string[] LegBlendShapeTerms =
        {
            "leg", "thigh", "calf", "knee", "shin",
            "脚", "太もも", "腿", "膝", "ふくらはぎ", "すね"
        };
        private static readonly string[] FootBlendShapeTerms =
            { "foot", "feet", "toe", "ankle", "足", "つま先", "足首" };

        private enum WorkflowCategory
        {
            Appearance,
            ExpressionAnimation,
            PhysBone,
            Todo
        }

        private enum AppearanceSection
        {
            Material,
            Size
        }

        private enum BodyPartCategory
        {
            Head,
            Chest,
            Waist,
            Shoulders,
            Arms,
            Hands,
            Legs,
            Feet,
            Other
        }

        private enum PendingBodySizeChange
        {
            None,
            Scale,
            BlendShape
        }

        private sealed class AvatarMaterialEntry
        {
            internal Material Material { get; set; }
            internal List<MaterialUsage> Usages { get; } =
                new List<MaterialUsage>();
        }

        private sealed class MaterialUsage
        {
            internal string RendererPath { get; set; }
            internal int SlotIndex { get; set; }
        }

        private sealed class BodyScaleDefinition
        {
            internal BodyScaleDefinition(
                string localizationKey,
                bool usesAvatarRoot,
                bool usesFirstAvailableBone,
                params HumanBodyBones[] bones)
            {
                LocalizationKey = localizationKey;
                UsesAvatarRoot = usesAvatarRoot;
                UsesFirstAvailableBone = usesFirstAvailableBone;
                Bones = bones ?? Array.Empty<HumanBodyBones>();
            }

            internal string LocalizationKey { get; }
            internal bool UsesAvatarRoot { get; }
            internal bool UsesFirstAvailableBone { get; }
            internal IReadOnlyList<HumanBodyBones> Bones { get; }
        }

        private sealed class BodyBlendShapeDefinition
        {
            internal string RendererPath { get; set; }
            internal string ShapeName { get; set; }
            internal string DisplayName { get; set; }
            internal BodyPartCategory Category { get; set; }
            internal float Value { get; set; }
            internal float BaseValue { get; set; }
        }

        private static readonly IReadOnlyList<BodyScaleDefinition>
            BodyScaleDefinitions = new[]
            {
                new BodyScaleDefinition(
                    "workflow.appearance.sizePart.wholeBody",
                    true,
                    false),
                new BodyScaleDefinition(
                    "workflow.appearance.sizePart.head",
                    false,
                    false,
                    HumanBodyBones.Head),
                new BodyScaleDefinition(
                    "workflow.appearance.sizePart.chest",
                    false,
                    true,
                    HumanBodyBones.UpperChest,
                    HumanBodyBones.Chest,
                    HumanBodyBones.Spine),
                new BodyScaleDefinition(
                    "workflow.appearance.sizePart.waist",
                    false,
                    false,
                    HumanBodyBones.Hips),
                new BodyScaleDefinition(
                    "workflow.appearance.sizePart.shoulders",
                    false,
                    false,
                    HumanBodyBones.LeftShoulder,
                    HumanBodyBones.RightShoulder),
                new BodyScaleDefinition(
                    "workflow.appearance.sizePart.arms",
                    false,
                    false,
                    HumanBodyBones.LeftUpperArm,
                    HumanBodyBones.RightUpperArm),
                new BodyScaleDefinition(
                    "workflow.appearance.sizePart.hands",
                    false,
                    false,
                    HumanBodyBones.LeftHand,
                    HumanBodyBones.RightHand),
                new BodyScaleDefinition(
                    "workflow.appearance.sizePart.legs",
                    false,
                    false,
                    HumanBodyBones.LeftUpperLeg,
                    HumanBodyBones.RightUpperLeg),
                new BodyScaleDefinition(
                    "workflow.appearance.sizePart.feet",
                    false,
                    false,
                    HumanBodyBones.LeftFoot,
                    HumanBodyBones.RightFoot)
            };

        private sealed class EmbeddedMaterialInspector
            : VisualElement, IDisposable
        {
            private const float HorizontalPadding = 12f;

            private readonly Material _material;
            private readonly Action _onChanged;
            private readonly IMGUIContainer _container;
            private MaterialEditor _editor;

            internal bool IsAvailable => _editor != null;

            internal EmbeddedMaterialInspector(
                Material material,
                Action onChanged)
            {
                _material = material;
                _onChanged = onChanged;
                AddToClassList(
                    "ee4v-modification-workflow__material-editor");

                _editor = Editor.CreateEditor(material) as MaterialEditor;
                if (_editor == null)
                {
                    return;
                }

                _container = new IMGUIContainer(DrawInspector);
                _container.AddToClassList(
                    "ee4v-modification-workflow__material-editor-imgui");
                _container.RegisterCallback<GeometryChangedEvent>(
                    OnGeometryChanged);
                Add(_container);
            }

            public void Dispose()
            {
                if (_container != null)
                {
                    _container.UnregisterCallback<GeometryChangedEvent>(
                        OnGeometryChanged);
                }

                if (_editor == null)
                {
                    return;
                }

                UnityEngine.Object.DestroyImmediate(_editor);
                _editor = null;
            }

            private void OnGeometryChanged(GeometryChangedEvent evt)
            {
                if (Mathf.Abs(
                        evt.newRect.width - evt.oldRect.width) < 0.5f)
                {
                    return;
                }

                _container?.MarkDirtyRepaint();
            }

            private void DrawInspector()
            {
                if (_editor == null || _material == null)
                {
                    return;
                }

                var containerWidth = Mathf.Floor(
                    _container.contentRect.width);
                if (containerWidth < 2f)
                {
                    return;
                }

                var contentWidth = Mathf.Max(
                    1f,
                    containerWidth - HorizontalPadding * 2f);
                var previousWideMode = EditorGUIUtility.wideMode;
                var previousLabelWidth = EditorGUIUtility.labelWidth;
                var previousHierarchyMode =
                    EditorGUIUtility.hierarchyMode;
                var previousIndentLevel = EditorGUI.indentLevel;
                var materialChanged = false;
                EditorGUI.BeginChangeCheck();
                try
                {
                    EditorGUIUtility.wideMode = contentWidth > 330f;
                    EditorGUIUtility.labelWidth = Mathf.Clamp(
                        contentWidth * 0.45f,
                        120f,
                        220f);
                    EditorGUIUtility.hierarchyMode = true;
                    EditorGUI.indentLevel = 0;

                    using (new GUILayout.HorizontalScope(
                               GUILayout.Width(containerWidth)))
                    {
                        GUILayout.Space(HorizontalPadding);
                        using (new GUILayout.VerticalScope(
                                   GUILayout.Width(contentWidth)))
                        {
                            DrawMaterialInspector(_editor, _material);
                        }
                        GUILayout.Space(HorizontalPadding);
                    }
                }
                finally
                {
                    materialChanged = EditorGUI.EndChangeCheck();
                    EditorGUIUtility.wideMode = previousWideMode;
                    EditorGUIUtility.labelWidth = previousLabelWidth;
                    EditorGUIUtility.hierarchyMode =
                        previousHierarchyMode;
                    EditorGUI.indentLevel = previousIndentLevel;
                }

                if (!materialChanged)
                {
                    return;
                }

                EditorUtility.SetDirty(_material);
                _onChanged?.Invoke();
            }

            private static void DrawMaterialInspector(
                MaterialEditor materialEditor,
                Material material)
            {
                var shaderGui = materialEditor.customShaderGUI;
                if (shaderGui == null)
                {
                    materialEditor.PropertiesGUI();
                    return;
                }

                var properties = MaterialEditor.GetMaterialProperties(
                    new UnityEngine.Object[] { material });
                shaderGui.OnGUI(materialEditor, properties);
            }
        }

        private sealed class VariantSourceOption
        {
            internal AssetItem Item { get; set; }
            internal IReadOnlyList<GameObject> Prefabs { get; set; }
        }

        private readonly Dictionary<WorkflowCategory, UiButton>
            _categoryButtons =
                new Dictionary<WorkflowCategory, UiButton>();
        private readonly Dictionary<Material, UiButton>
            _materialVisibilityButtons =
                new Dictionary<Material, UiButton>();
        private readonly HashSet<Material> _hiddenMaterials =
            new HashSet<Material>();
        private IAssetManager _manager;
        private DerivedAssetInfo _workingAsset;
        private GameObject _workingObject;
        private Material _selectedMaterial;
        private UiButton _allMaterialsVisibilityButton;
        private EmbeddedMaterialInspector _materialInspector;
        private DerivedAssetPrefabScenePreview _scenePreview;
        private FaceExpressionWindow.EmbeddedEditor _faceExpressionEditor;
        private PhysBoneColliderWindow.EmbeddedEditor _physBoneEditor;
        private VisualElement _customizerHost;
        private VisualElement _faceExpressionHost;
        private VisualElement _physBoneHost;
        private ScrollView _controlsHost;
        private UiTextElement _previewTitle;
        private WorkflowCategory _currentCategory =
            WorkflowCategory.Appearance;
        private AppearanceSection _appearanceSection =
            AppearanceSection.Size;
        private bool _creatingDerivedAsset;
        private string _creationItemId = string.Empty;
        private GameObject _creationPrefab;
        private string _derivedName = string.Empty;
        private string _derivedDescription = string.Empty;
        private string _feedback = string.Empty;
        private HelpBoxMessageType _feedbackType =
            HelpBoxMessageType.Info;
        private bool _bodyScaleDragging;
        private PendingBodySizeChange _pendingBodySizeChange;
        private IReadOnlyList<string> _pendingBodyScaleTargetPaths;
        private Vector3 _pendingBodyScaleMultipliers;
        private bool _pendingBodyScaleUpdatesViewPosition;
        private string _pendingBodyBlendShapeRendererPath;
        private string _pendingBodyBlendShapeName;
        private float _pendingBodyBlendShapeWeight;
        private bool _bodyScaleDirty;
        private bool _advancedBodyScaleExpanded;
        private readonly HashSet<string> _expandedBodyScaleAxes =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector3> _bodyScaleBaseScales =
            new Dictionary<string, Vector3>(StringComparer.Ordinal);
        private readonly List<KeyValuePair<string, Transform>>
            _resolvedBodyScaleTargets =
                new List<KeyValuePair<string, Transform>>();
        private readonly Dictionary<string, Vector3>
            _bodyScalePreviewScales =
                new Dictionary<string, Vector3>(StringComparer.Ordinal);
        private Vector3? _baseAvatarViewPosition;
        private Component _avatarDescriptor;

        [MenuItem("ee4v/Window/Asset Manager/Modification Workflow")]
        private static void ShowWindow()
        {
            var window = GetWindow<AssetModificationWorkflowWindow>();
            window.ConfigureWindow();
            window.Show();
        }

        internal static void ShowFor(DerivedAssetInfo asset)
        {
            if (asset?.Prefab == null)
            {
                return;
            }
            var window = GetWindow<AssetModificationWorkflowWindow>();
            window.SelectDerivedAsset(asset);
            window.Show();
        }

        private void OnEnable()
        {
            I18N.Reloaded -= Rebuild;
            I18N.Reloaded += Rebuild;
            AssetManagerWindowSession.ManagerInvalidated -=
                OnManagerInvalidated;
            AssetManagerWindowSession.ManagerInvalidated +=
                OnManagerInvalidated;
            Undo.undoRedoPerformed -= RefreshAfterUndoRedo;
            Undo.undoRedoPerformed += RefreshAfterUndoRedo;
            ConfigureWindow();
        }

        private void CreateGUI()
        {
            BuildWindow();
        }

        private void OnDisable()
        {
            EndBodyScaleDrag(false);
            I18N.Reloaded -= Rebuild;
            AssetManagerWindowSession.ManagerInvalidated -=
                OnManagerInvalidated;
            Undo.undoRedoPerformed -= RefreshAfterUndoRedo;
            DisposeEditors();
        }

        private void ConfigureWindow()
        {
            titleContent = UiTextFactory.CreateGuiContent(
                I18N.Get("workflow.windowTitle"));
            minSize = new Vector2(1180f, 720f);
        }

        private void Rebuild()
        {
            if (rootVisualElement.panel != null)
            {
                BuildWindow();
            }
        }

        private void OnManagerInvalidated()
        {
            _manager = null;
            Rebuild();
        }

        private void BuildWindow()
        {
            ConfigureWindow();
            DisposeEditors();
            var root = rootVisualElement;
            root.Clear();
            AssetManagerWindowSession.PrepareWorkflowRoot(root);
            root.AddToClassList("ee4v-modification-workflow");

            if (_workingObject == null ||
                string.IsNullOrEmpty(AssetDatabase.GetAssetPath(
                    _workingObject)))
            {
                _workingAsset = null;
                _workingObject = null;
                root.Add(_creatingDerivedAsset
                    ? BuildDerivedAssetCreation()
                    : BuildDerivedAssetSelection());
                return;
            }

            root.Add(BuildWorkspaceHeader());
            var body = new VisualElement();
            body.AddToClassList("ee4v-modification-workflow__body");
            body.Add(BuildCategoryRail());

            _customizerHost = new VisualElement();
            _customizerHost.AddToClassList(
                "ee4v-modification-workflow__customizer-host");
            _customizerHost.Add(BuildPreviewPane());
            _controlsHost = new ScrollView(ScrollViewMode.Vertical);
            _controlsHost.horizontalScrollerVisibility =
                ScrollerVisibility.Hidden;
            _controlsHost.AddToClassList(
                "ee4v-modification-workflow__controls");
            _customizerHost.Add(_controlsHost);
            body.Add(_customizerHost);

            _faceExpressionHost = new VisualElement();
            _faceExpressionHost.AddToClassList(
                "ee4v-modification-workflow__face-expression-host");
            _faceExpressionHost.AddToClassList(
                "ee4v-modification-workflow__hidden");
            body.Add(_faceExpressionHost);

            _physBoneHost = new VisualElement();
            _physBoneHost.AddToClassList(
                "ee4v-modification-workflow__physbone-host");
            _physBoneHost.AddToClassList(
                "ee4v-modification-workflow__hidden");
            body.Add(_physBoneHost);
            root.Add(body);
            ShowCategory(_currentCategory, false);
        }

        private VisualElement BuildDerivedAssetSelection()
        {
            var page = new VisualElement();
            page.AddToClassList(
                "ee4v-modification-workflow__selection-page");
            var header = BuildSelectionHeader(
                "workflow.selection.title",
                "workflow.selection.description");
            header.Add(AssetManagerControls.CreateIconTextButton(
                I18N.Get("workflow.selection.createNew"),
                "add.png",
                StartDerivedAssetCreation,
                "ee4v-modification-workflow__primary-action",
                "ee4v-modification-workflow__selection-new"));
            page.Add(header);

            var content = new ScrollView(ScrollViewMode.Vertical);
            content.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            content.AddToClassList(
                "ee4v-modification-workflow__selection-scroll");
            var assets = DerivedAssetCreator.FindAll();
            if (assets.Count == 0)
            {
                content.Add(CreateEmptyState(
                    "workflow.selection.emptyTitle",
                    "workflow.selection.emptyDescription"));
                content.Add(AssetManagerControls.CreateIconTextButton(
                    I18N.Get("workflow.selection.openManager"),
                    "library.png",
                    AssetManagerMainWindow.ShowWindow,
                    "ee4v-modification-workflow__selection-manager"));
                page.Add(content);
                return page;
            }

            var grid = new VisualElement();
            grid.AddToClassList(
                "ee4v-modification-workflow__selection-grid");
            foreach (var asset in assets)
            {
                grid.Add(BuildDerivedAssetCard(asset));
            }
            content.Add(grid);
            page.Add(content);
            return page;
        }

        private VisualElement BuildDerivedAssetCreation()
        {
            var page = new VisualElement();
            page.AddToClassList(
                "ee4v-modification-workflow__selection-page");
            var header = BuildSelectionHeader(
                "workflow.selection.createTitle",
                "workflow.selection.createDescription");
            header.Insert(0, AssetManagerControls.CreateIconButton(
                I18N.Get("workflow.selection.back"),
                "arrow_left.png",
                CancelDerivedAssetCreation,
                "ee4v-modification-workflow__selection-back"));
            page.Add(header);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.AddToClassList(
                "ee4v-modification-workflow__creation-scroll");
            var sources = GetVariantSources();
            if (sources.Count == 0)
            {
                scroll.Add(CreateEmptyState(
                    "workflow.selection.noSourceTitle",
                    "workflow.selection.noSourceDescription"));
                scroll.Add(AssetManagerControls.CreateIconTextButton(
                    I18N.Get("workflow.selection.openManager"),
                    "library.png",
                    AssetManagerMainWindow.ShowWindow,
                    "ee4v-modification-workflow__selection-manager"));
                page.Add(scroll);
                return page;
            }

            var selectedSource = sources.FirstOrDefault(source =>
                string.Equals(
                    source.Item.Id,
                    _creationItemId,
                    StringComparison.Ordinal)) ?? sources[0];
            if (!string.Equals(
                    _creationItemId,
                    selectedSource.Item.Id,
                    StringComparison.Ordinal))
            {
                _creationItemId = selectedSource.Item.Id;
                _creationPrefab = null;
                _derivedName = selectedSource.Item.Name + " Variant";
            }
            if (_creationPrefab == null ||
                !selectedSource.Prefabs.Contains(_creationPrefab))
            {
                _creationPrefab = selectedSource.Prefabs[0];
            }

            var sourceChoices = sources.ToList();
            var sourceField = UiTextFactory.CreatePopupField(
                string.Empty,
                sourceChoices,
                Mathf.Max(0, sourceChoices.IndexOf(selectedSource)),
                FormatVariantSource,
                FormatVariantSource);
            sourceField.RegisterValueChangedCallback(evt =>
                SelectVariantSource(evt.newValue));
            var sourceInput = new FormInput(
                I18N.Get("workflow.selection.sourceItem"),
                sourceField);
            sourceInput.AddToClassList(
                "ee4v-modification-workflow__creation-source");
            scroll.Add(sourceInput);

            var layout = new VisualElement();
            layout.AddToClassList(
                "ee4v-modification-workflow__creation-layout");
            var previewColumn = new VisualElement();
            previewColumn.AddToClassList(
                "ee4v-modification-workflow__creation-preview-column");
            var preview = new DerivedAssetPrefabScenePreview();
            preview.SetPrefab(_creationPrefab);
            previewColumn.Add(preview);
            var selector = new DerivedAssetPrefabSelector(
                selectedSource.Prefabs);
            selector.SetValueWithoutNotify(_creationPrefab);
            selector.ValueChanged += prefab =>
            {
                _creationPrefab = prefab;
                preview.SetPrefab(prefab);
            };
            var prefabInput = new FormInput(
                I18N.Get("workflow.selection.sourcePrefab"),
                selector);
            previewColumn.Add(prefabInput);
            layout.Add(previewColumn);

            var fields = new VisualElement();
            fields.AddToClassList(
                "ee4v-modification-workflow__creation-fields");
            var name = AssetManagerControls.CreateTextField(
                I18N.Get("workflow.selection.name"));
            name.value = _derivedName;
            fields.Add(name);
            var description = AssetManagerControls.CreateTextField(
                I18N.Get("workflow.selection.assetDescription"));
            description.value = _derivedDescription;
            description.SetMultiline(true, 144f);
            fields.Add(description);
            AddFeedback(fields);
            fields.Add(AssetManagerControls.CreateIconTextButton(
                I18N.Get("workflow.selection.create"),
                "add.png",
                () =>
                {
                    _derivedName = name.value;
                    _derivedDescription = description.value;
                    CreateDerivedAsset();
                },
                "ee4v-modification-workflow__primary-action",
                "ee4v-modification-workflow__creation-submit"));
            layout.Add(fields);
            scroll.Add(layout);
            page.Add(scroll);
            return page;
        }

        private VisualElement BuildSelectionHeader(
            string titleKey,
            string descriptionKey)
        {
            var header = new VisualElement();
            header.AddToClassList(
                "ee4v-modification-workflow__selection-header");
            var text = new VisualElement();
            text.AddToClassList(
                "ee4v-modification-workflow__selection-header-text");
            text.Add(UiTextFactory.Create(
                I18N.Get(titleKey),
                UiClassNames.SectionTitle,
                "ee4v-modification-workflow__selection-title"));
            var description = UiTextFactory.Create(
                I18N.Get(descriptionKey),
                UiClassNames.SecondaryText,
                "ee4v-modification-workflow__selection-description");
            description.SetWhiteSpace(WhiteSpace.Normal);
            text.Add(description);
            header.Add(text);
            return header;
        }

        private VisualElement BuildDerivedAssetCard(DerivedAssetInfo asset)
        {
            var card = new VisualElement();
            card.AddToClassList(
                "ee4v-modification-workflow__selection-card");
            var preview = new DerivedAssetPrefabPreview(asset.Prefab);
            preview.AddToClassList(
                "ee4v-modification-workflow__selection-preview");
            card.Add(preview);
            var text = new VisualElement();
            text.AddToClassList(
                "ee4v-modification-workflow__selection-card-text");
            text.Add(UiTextFactory.Create(
                asset.Name,
                UiClassNames.SectionTitle,
                "ee4v-modification-workflow__selection-card-title"));
            var description = UiTextFactory.Create(
                string.IsNullOrWhiteSpace(asset.Description)
                    ? I18N.Get("workflow.selection.noDescription")
                    : asset.Description,
                UiClassNames.SecondaryText,
                "ee4v-modification-workflow__selection-card-description");
            description.SetWhiteSpace(WhiteSpace.Normal);
            text.Add(description);
            card.Add(text);
            card.Add(AssetManagerControls.CreateIconTextButton(
                I18N.Get("workflow.selection.choose"),
                "arrow_right.png",
                () => SelectDerivedAsset(asset),
                "ee4v-modification-workflow__primary-action",
                "ee4v-modification-workflow__selection-choose"));
            return card;
        }

        private VisualElement BuildWorkspaceHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("ee4v-modification-workflow__header");
            var text = new VisualElement();
            text.AddToClassList(
                "ee4v-modification-workflow__header-text");
            text.Add(UiTextFactory.Create(
                I18N.Get("workflow.title"),
                UiClassNames.SectionTitle,
                "ee4v-modification-workflow__title"));
            var subtitle = UiTextFactory.Create(
                I18N.Get("workflow.subtitle"),
                UiClassNames.SecondaryText,
                "ee4v-modification-workflow__subtitle");
            subtitle.SetWhiteSpace(WhiteSpace.Normal);
            text.Add(subtitle);
            header.Add(text);

            var asset = new VisualElement();
            asset.AddToClassList(
                "ee4v-modification-workflow__asset-chip");
            asset.Add(new Icon(
                AssetManagerControls.LoadFluentIconState(
                    "cube.png",
                    UiSizeTokens.Size18,
                    tintColor: UiColorTokens.TextMuted)));
            asset.Add(UiTextFactory.Create(
                _workingAsset?.Name ?? _workingObject.name,
                "ee4v-modification-workflow__asset-name"));
            asset.Add(new UiButton(
                I18N.Get("workflow.asset.change"),
                ClearDerivedAsset,
                variant: UiButtonVariant.Ghost));
            header.Add(asset);
            return header;
        }

        private VisualElement BuildCategoryRail()
        {
            _categoryButtons.Clear();
            var rail = new VisualElement();
            rail.AddToClassList(
                "ee4v-modification-workflow__category-rail");
            rail.Add(UiTextFactory.Create(
                I18N.Get("workflow.categories"),
                UiClassNames.SecondaryText,
                "ee4v-modification-workflow__category-heading"));
            AddCategoryButton(
                rail,
                WorkflowCategory.Appearance,
                "workflow.category.appearance",
                "image.png");
            AddCategoryButton(
                rail,
                WorkflowCategory.ExpressionAnimation,
                "workflow.category.expressionAnimation",
                "star.png");
            AddCategoryButton(
                rail,
                WorkflowCategory.PhysBone,
                "workflow.category.physBone",
                "cube.png");
            AddCategoryButton(
                rail,
                WorkflowCategory.Todo,
                "workflow.category.todo",
                "document.png");
            return rail;
        }

        private void AddCategoryButton(
            VisualElement rail,
            WorkflowCategory category,
            string labelKey,
            string icon)
        {
            var button = new UiButton(
                I18N.Get(labelKey),
                () => ShowCategory(category),
                icon: AssetManagerControls.LoadFluentIconState(
                    icon,
                    UiSizeTokens.Size18),
                variant: UiButtonVariant.Ghost,
                labelTypographyClassName:
                    UiClassNames.NavigationItemLabel);
            button.AddToClassList(
                "ee4v-modification-workflow__category-button");
            _categoryButtons[category] = button;
            rail.Add(button);
        }

        private VisualElement BuildPreviewPane()
        {
            var pane = new VisualElement();
            pane.AddToClassList(
                "ee4v-modification-workflow__preview-pane");
            var toolbar = new VisualElement();
            toolbar.AddToClassList(
                "ee4v-modification-workflow__preview-toolbar");
            _previewTitle = UiTextFactory.Create(
                string.Empty,
                UiClassNames.SectionTitle,
                "ee4v-modification-workflow__preview-title");
            toolbar.Add(_previewTitle);
            pane.Add(toolbar);

            var viewport = new VisualElement();
            viewport.AddToClassList(
                "ee4v-modification-workflow__preview-viewport");
            _scenePreview = new DerivedAssetPrefabScenePreview();
            _scenePreview.SetFlexibleLayout(true);
            _scenePreview.AddToClassList(
                "ee4v-modification-workflow__preview");
            _scenePreview.SetPrefab(_workingObject);
            viewport.Add(_scenePreview);
            pane.Add(viewport);
            return pane;
        }

        private void ShowCategory(
            WorkflowCategory category,
            bool clearFeedback = true)
        {
            if (_currentCategory == WorkflowCategory.Appearance &&
                _appearanceSection == AppearanceSection.Size &&
                category != WorkflowCategory.Appearance)
            {
                EndBodyScaleDrag();
            }
            if (_customizerHost == null ||
                _faceExpressionHost == null ||
                _physBoneHost == null)
            {
                _currentCategory = category;
                return;
            }
            if (_currentCategory != category && clearFeedback)
            {
                _feedback = string.Empty;
            }
            _currentCategory = category;
            _scenePreview?.SetHiddenMaterials(
                category == WorkflowCategory.Appearance &&
                _appearanceSection == AppearanceSection.Material
                    ? _hiddenMaterials
                    : null);
            foreach (var pair in _categoryButtons)
            {
                pair.Value.EnableInClassList(
                    "ee4v-modification-workflow__category-button--active",
                    pair.Key == category);
            }

            var faceExpression =
                category == WorkflowCategory.ExpressionAnimation;
            var physBone = category == WorkflowCategory.PhysBone;
            DisposeMaterialEditor();
            _customizerHost.EnableInClassList(
                "ee4v-modification-workflow__hidden",
                faceExpression || physBone);
            _faceExpressionHost.EnableInClassList(
                "ee4v-modification-workflow__hidden",
                !faceExpression);
            _physBoneHost.EnableInClassList(
                "ee4v-modification-workflow__hidden",
                !physBone);
            if (faceExpression)
            {
                if (_faceExpressionEditor == null)
                {
                    _faceExpressionEditor = FaceExpressionWindow.Embed(
                        _faceExpressionHost,
                        _workingObject,
                        Repaint);
                }
                _faceExpressionEditor.SetActive(true);
                return;
            }

            _faceExpressionEditor?.SetActive(false);
            if (physBone)
            {
                if (_physBoneEditor == null)
                {
                    _physBoneEditor = PhysBoneColliderWindow.Embed(
                        _physBoneHost,
                        _workingObject,
                        RefreshMaterialPreview);
                }
                return;
            }

            _controlsHost.Clear();
            if (category == WorkflowCategory.Appearance)
            {
                _controlsHost.Add(BuildAppearanceControls());
                SetPreviewTitle(
                    _appearanceSection == AppearanceSection.Material
                        ? "workflow.preview.appearanceTitle"
                        : "workflow.preview.sizeTitle");
            }
            else
            {
                _controlsHost.Add(BuildTodoControls());
                SetPreviewTitle("workflow.preview.todoTitle");
            }
        }

        private VisualElement BuildAppearanceControls()
        {
            var panel = new VisualElement();
            panel.AddToClassList(
                "ee4v-modification-workflow__controls-content");
            AddFeedback(panel);
            panel.Add(BuildAppearanceSectionTabs());
            if (_appearanceSection == AppearanceSection.Size)
            {
                panel.Add(BuildBodyScaleControls());
                return panel;
            }

            var materials = GetAvatarMaterials();
            _materialVisibilityButtons.Clear();
            _allMaterialsVisibilityButton = null;
            if (materials.Count == 0)
            {
                _hiddenMaterials.Clear();
                _scenePreview?.SetHiddenMaterials(null);
                panel.Add(CreateEmptyState(
                    "workflow.appearance.emptyTitle",
                    "workflow.appearance.emptyDescription"));
                return panel;
            }

            if (_selectedMaterial == null ||
                !materials.Any(entry =>
                    entry.Material == _selectedMaterial))
            {
                _selectedMaterial = materials[0].Material;
            }
            var availableMaterials = new HashSet<Material>(
                materials.Select(entry => entry.Material));
            _hiddenMaterials.RemoveWhere(material =>
                material == null || !availableMaterials.Contains(material));
            _scenePreview?.SetHiddenMaterials(_hiddenMaterials);

            var materialChoices = new VisualElement();
            materialChoices.AddToClassList(
                "ee4v-modification-workflow__material-list");
            materialChoices.Add(BuildAllMaterialsVisibilityRow(
                availableMaterials));
            foreach (var entry in materials)
            {
                var material = entry.Material;
                var choice = new NavigationItem(
                    new NavigationItemState(
                        material.name,
                        FormatMaterialUsageSummary(entry),
                        CreateMaterialIcon(material),
                        material == _selectedMaterial),
                    () =>
                    {
                        _selectedMaterial = material;
                        ShowCategory(WorkflowCategory.Appearance, false);
                    });
                choice.AddToClassList(
                    "ee4v-modification-workflow__material-item");
                choice.tooltip = FormatMaterialUsageTooltip(entry);
                if (!IsEditableWorkflowMaterial(material))
                {
                    choice.Trailing.Add(new Badge(
                        I18N.Get("workflow.appearance.readOnly")));
                }
                var visibility = new UiButton(
                    string.Empty,
                    () => ToggleMaterialVisibility(material),
                    variant: UiButtonVariant.Ghost);
                visibility.AddToClassList(
                    "ee4v-modification-workflow__material-visibility");
                visibility.RegisterCallback<ClickEvent>(
                    evt => evt.StopPropagation());
                choice.Trailing.Add(visibility);
                _materialVisibilityButtons[material] = visibility;
                materialChoices.Add(choice);
            }
            RefreshMaterialVisibilityButtons(availableMaterials);
            panel.Add(materialChoices);

            if (!IsEditableWorkflowMaterial(_selectedMaterial))
            {
                panel.Add(UiTextFactory.CreateHelpBox(
                    I18N.Get("workflow.appearance.protected"),
                    HelpBoxMessageType.Warning));
                return panel;
            }

            panel.Add(BuildMaterialEditor(_selectedMaterial));
            return panel;
        }

        private VisualElement BuildBodyScaleControls()
        {
            var content = new VisualElement();
            content.AddToClassList(
                "ee4v-modification-workflow__size-content");

            if (!IsEditableWorkflowPrefab())
            {
                content.Add(UiTextFactory.CreateHelpBox(
                    I18N.Get("workflow.appearance.sizeProtected"),
                    HelpBoxMessageType.Warning));
                return content;
            }

            var animator = FindHumanoidAnimator();
            var primaryScaleList = new VisualElement();
            primaryScaleList.AddToClassList(
                "ee4v-modification-workflow__size-list");
            var wholeBody = BodyScaleDefinitions.First(definition =>
                definition.UsesAvatarRoot);
            var wholeBodyTargets = ResolveBodyScaleTargets(
                wholeBody,
                animator);
            if (TryGetWorkingAvatarViewPosition(
                    out var avatarDescriptor,
                    out var currentViewPosition))
            {
                var baseViewPosition = GetBaseAvatarViewPosition(
                    avatarDescriptor,
                    currentViewPosition);
                primaryScaleList.Add(BuildBodyHeightControl(
                    wholeBody,
                    GetTransformPaths(wholeBodyTargets),
                    currentViewPosition,
                    baseViewPosition));
            }
            else
            {
                primaryScaleList.Add(UiTextFactory.CreateHelpBox(
                    I18N.Get("workflow.appearance.heightDescriptorRequired"),
                    HelpBoxMessageType.Warning));
            }
            content.Add(primaryScaleList);

            var bodyShapes = GetBodyBlendShapes();
            var bodyShapeHeader = new SectionHeader(
                I18N.Get("workflow.appearance.bodyShapeTitle"));
            bodyShapeHeader.AddToClassList(
                "ee4v-modification-workflow__size-section-header");
            content.Add(bodyShapeHeader);
            if (bodyShapes.Count == 0)
            {
                content.Add(UiTextFactory.CreateHelpBox(
                    I18N.Get("workflow.appearance.bodyShapeEmpty"),
                    HelpBoxMessageType.Info));
            }
            else
            {
                var bodyShapeList = new VisualElement();
                bodyShapeList.AddToClassList(
                    "ee4v-modification-workflow__size-list");
                foreach (BodyPartCategory category in Enum.GetValues(
                             typeof(BodyPartCategory)))
                {
                    var categoryShapes = bodyShapes
                        .Where(definition =>
                            definition.Category == category)
                        .ToArray();
                    if (categoryShapes.Length == 0)
                    {
                        continue;
                    }

                    var categoryLabel = UiTextFactory.Create(
                        I18N.Get(GetBodyPartCategoryLocalizationKey(category)),
                        UiClassNames.SecondaryText,
                        "ee4v-modification-workflow__size-group-title");
                    bodyShapeList.Add(categoryLabel);
                    foreach (var bodyShape in categoryShapes)
                    {
                        bodyShapeList.Add(
                            BuildBodyBlendShapeControl(bodyShape));
                    }
                }
                content.Add(bodyShapeList);
            }

            var advancedList = new VisualElement();
            advancedList.AddToClassList(
                "ee4v-modification-workflow__size-list");
            var hasBodyPartControls = false;
            foreach (var definition in BodyScaleDefinitions.Where(
                         definition => !definition.UsesAvatarRoot))
            {
                var targets = ResolveBodyScaleTargets(definition, animator);
                if (targets.Count == 0)
                {
                    continue;
                }

                hasBodyPartControls = true;
                advancedList.Add(BuildBodyScaleControl(
                    definition,
                    GetTransformPaths(targets),
                    GetBodyScaleMultipliers(targets)));
            }

            if (hasBodyPartControls)
            {
                content.Add(BuildAdvancedBodyScaleFoldout(advancedList));
            }
            else
            {
                content.Add(UiTextFactory.CreateHelpBox(
                    I18N.Get("workflow.appearance.sizeHumanoidRequired"),
                    HelpBoxMessageType.Warning));
            }
            return content;
        }

        private IReadOnlyList<string> GetTransformPaths(
            IReadOnlyList<Transform> targets)
        {
            return targets
                .Where(target => target != null)
                .Select(target =>
                    AnimationUtility.CalculateTransformPath(
                        target,
                        _workingObject.transform))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private VisualElement BuildAdvancedBodyScaleFoldout(
            VisualElement advancedList)
        {
            var foldout = new VisualElement();
            foldout.AddToClassList(
                "ee4v-modification-workflow__advanced-size");

            UiButton toggle = null;
            toggle = new UiButton(
                I18N.Get("workflow.appearance.advancedSizeTitle"),
                () =>
                {
                    _advancedBodyScaleExpanded =
                        !_advancedBodyScaleExpanded;
                    UpdateAdvancedBodyScaleFoldout(toggle, advancedList);
                },
                icon: AssetManagerControls.LoadFluentIconState(
                    _advancedBodyScaleExpanded
                        ? "chevron_down.png"
                        : "chevron_right.png",
                    UiSizeTokens.Size12),
                variant: UiButtonVariant.Ghost);
            toggle.AddToClassList(
                "ee4v-modification-workflow__advanced-size-toggle");
            foldout.Add(toggle);

            foldout.Add(advancedList);
            UpdateAdvancedBodyScaleFoldout(toggle, advancedList);
            return foldout;
        }

        private void UpdateAdvancedBodyScaleFoldout(
            UiButton toggle,
            VisualElement content)
        {
            toggle?.SetIcon(AssetManagerControls.LoadFluentIconState(
                _advancedBodyScaleExpanded
                    ? "chevron_down.png"
                    : "chevron_right.png",
                UiSizeTokens.Size12));
            content?.EnableInClassList(
                "ee4v-modification-workflow__hidden",
                !_advancedBodyScaleExpanded);
        }

        private VisualElement BuildBodyHeightControl(
            BodyScaleDefinition definition,
            IReadOnlyList<string> targetPaths,
            Vector3 currentViewPosition,
            Vector3 baseViewPosition)
        {
            var group = new VisualElement();
            group.AddToClassList(
                "ee4v-modification-workflow__size-scale-group");
            var baseHeightMeters = baseViewPosition.y;
            var minimumHeight = Mathf.Max(0.01f, baseHeightMeters - 1f);
            var maximumHeight = baseHeightMeters + 1f;
            var row = CreateSizeControlRow(
                I18N.Get(definition.LocalizationKey),
                out var controls);
            var slider = new Slider(minimumHeight, maximumHeight);
            slider.AddToClassList(
                "ee4v-modification-workflow__size-slider");
            slider.tooltip = I18N.Get(
                "workflow.appearance.heightSliderTooltip",
                FormatBodyHeight(minimumHeight),
                FormatBodyHeight(maximumHeight));
            controls.Add(slider);
            var value = UiTextFactory.CreateFloatField();
            value.AddToClassList(
                "ee4v-modification-workflow__size-value");
            controls.Add(value);
            controls.Add(CreateSizeUnit("m"));
            var reset = CreateSizeResetButton(() =>
                slider.value = baseHeightMeters);
            controls.Add(reset);

            var rendering = false;
            var appliedHeight = Mathf.Clamp(
                Mathf.Round(currentViewPosition.y * 100f) / 100f,
                minimumHeight,
                maximumHeight);
            void SetHeight(float height, bool apply)
            {
                var normalized = Mathf.Clamp(
                    Mathf.Round(height * 100f) / 100f,
                    minimumHeight,
                    maximumHeight);
                rendering = true;
                slider.SetValueWithoutNotify(normalized);
                value.SetValueWithoutNotify(normalized);
                rendering = false;
                reset.SetEnabled(!Mathf.Approximately(
                    normalized,
                    baseHeightMeters));
                if (apply &&
                    !Mathf.Approximately(normalized, appliedHeight))
                {
                    appliedHeight = normalized;
                    var multiplier = normalized / baseHeightMeters;
                    ApplyBodyScale(
                        targetPaths,
                        Vector3.one * multiplier,
                        updateAvatarViewPosition: true);
                }
            }

            slider.RegisterCallback<PointerDownEvent>(_ =>
                BeginBodyScaleDrag());
            slider.RegisterCallback<PointerUpEvent>(_ =>
                EndBodyScaleDrag());
            slider.RegisterCallback<PointerCaptureOutEvent>(_ =>
                EndBodyScaleDrag());
            slider.RegisterValueChangedCallback(evt =>
            {
                if (!rendering)
                {
                    SetHeight(evt.newValue, true);
                }
            });
            value.RegisterValueChangedCallback(evt =>
            {
                if (!rendering &&
                    !float.IsNaN(evt.newValue) &&
                    !float.IsInfinity(evt.newValue))
                {
                    SetHeight(evt.newValue, true);
                }
            });
            SetHeight(currentViewPosition.y, false);
            group.Add(row);
            return group;
        }

        private VisualElement BuildBodyScaleControl(
            BodyScaleDefinition definition,
            IReadOnlyList<string> targetPaths,
            Vector3 initialMultipliers)
        {
            var group = new VisualElement();
            group.AddToClassList(
                "ee4v-modification-workflow__size-scale-group");
            var partName = I18N.Get(definition.LocalizationKey);
            var multipliers = RoundBodyScaleMultipliers(initialMultipliers);
            var axisRenderers = new Action<float>[3];
            var axisRows = new VisualElement();
            axisRows.AddToClassList(
                "ee4v-modification-workflow__size-axis-rows");
            UiButton axisToggle = null;
            void ToggleAxes()
            {
                if (!_expandedBodyScaleAxes.Add(
                        definition.LocalizationKey))
                {
                    _expandedBodyScaleAxes.Remove(
                        definition.LocalizationKey);
                }
                UpdateBodyScaleAxisFoldout(
                    axisToggle,
                    axisRows,
                    definition.LocalizationKey,
                    partName);
            }
            axisToggle = AssetManagerControls.CreateIconButton(
                string.Empty,
                "chevron_right.png",
                UiSizeTokens.Size12,
                UiButtonVariant.Ghost,
                ToggleAxes,
                "ee4v-modification-workflow__size-axis-toggle");
            var row = BuildScalePercentControl(
                partName,
                I18N.Get(
                    "workflow.appearance.sizeSliderTooltip",
                    partName),
                AverageVectorComponent(multipliers) * 100f,
                isChild: false,
                percent =>
                {
                    var multiplier = percent / 100f;
                    multipliers = Vector3.one * multiplier;
                    foreach (var renderAxis in axisRenderers)
                    {
                        renderAxis?.Invoke(percent);
                    }
                    ApplyBodyScale(targetPaths, multipliers);
                },
                out var renderUniform,
                axisToggle,
                ToggleAxes);
            group.Add(row);

            for (var axis = 0; axis < 3; axis++)
            {
                var capturedAxis = axis;
                var axisName = GetScaleAxisName(axis);
                var axisRow = BuildScalePercentControl(
                    axisName,
                    I18N.Get(
                        "workflow.appearance.axisScaleSliderTooltip",
                        partName,
                        axisName),
                    GetVectorComponent(multipliers, axis) * 100f,
                    isChild: true,
                    percent =>
                    {
                        multipliers = SetVectorComponent(
                            multipliers,
                            capturedAxis,
                            percent / 100f);
                        renderUniform(
                            AverageVectorComponent(multipliers) * 100f);
                        ApplyBodyScale(targetPaths, multipliers);
                    },
                    out axisRenderers[axis]);
                axisRows.Add(axisRow);
            }
            group.Add(axisRows);
            UpdateBodyScaleAxisFoldout(
                axisToggle,
                axisRows,
                definition.LocalizationKey,
                partName);
            return group;
        }

        private void UpdateBodyScaleAxisFoldout(
            UiButton toggle,
            VisualElement axisRows,
            string key,
            string partName)
        {
            var expanded = _expandedBodyScaleAxes.Contains(key);
            toggle?.SetIcon(AssetManagerControls.LoadFluentIconState(
                expanded
                    ? "chevron_down.png"
                    : "chevron_right.png",
                UiSizeTokens.Size12));
            if (toggle != null)
            {
                toggle.tooltip = I18N.Get(
                    expanded
                        ? "workflow.appearance.sizeAxisCollapse"
                        : "workflow.appearance.sizeAxisExpand",
                    partName);
            }
            axisRows?.EnableInClassList(
                "ee4v-modification-workflow__hidden",
                !expanded);
        }

        private VisualElement BuildScalePercentControl(
            string label,
            string tooltip,
            float initialPercent,
            bool isChild,
            Action<float> changed,
            out Action<float> render,
            VisualElement leading = null,
            Action labelClicked = null)
        {
            var row = CreateSizeControlRow(
                label,
                out var controls,
                isChild,
                leading,
                labelClicked);
            var slider = new Slider(
                MinimumBodyScale * 100f,
                MaximumBodyScale * 100f);
            slider.AddToClassList(
                "ee4v-modification-workflow__size-slider");
            slider.tooltip = tooltip;
            controls.Add(slider);
            var value = UiTextFactory.CreateFloatField();
            value.AddToClassList(
                "ee4v-modification-workflow__size-value");
            controls.Add(value);
            controls.Add(CreateSizeUnit("%"));
            var reset = CreateSizeResetButton(() => slider.value = 100f);
            controls.Add(reset);

            var rendering = false;
            var renderedPercent = Mathf.Round(initialPercent);
            void RenderScalePercent(float percent)
            {
                var normalized = Mathf.Round(percent);
                var sliderValue = Mathf.Clamp(
                    normalized,
                    MinimumBodyScale * 100f,
                    MaximumBodyScale * 100f);
                rendering = true;
                slider.SetValueWithoutNotify(sliderValue);
                value.SetValueWithoutNotify(normalized);
                rendering = false;
                renderedPercent = normalized;
                reset.SetEnabled(!Mathf.Approximately(normalized, 100f));
            }

            void SetScalePercent(float percent)
            {
                var normalized = Mathf.Round(percent);
                var changedValue = !Mathf.Approximately(
                    normalized,
                    renderedPercent);
                RenderScalePercent(normalized);
                if (changedValue)
                {
                    changed?.Invoke(normalized);
                }
            }

            slider.RegisterCallback<PointerDownEvent>(_ =>
                BeginBodyScaleDrag());
            slider.RegisterCallback<PointerUpEvent>(_ =>
                EndBodyScaleDrag());
            slider.RegisterCallback<PointerCaptureOutEvent>(_ =>
                EndBodyScaleDrag());
            slider.RegisterValueChangedCallback(evt =>
            {
                if (!rendering)
                {
                    SetScalePercent(evt.newValue);
                }
            });
            value.RegisterValueChangedCallback(evt =>
            {
                if (!rendering &&
                    !float.IsNaN(evt.newValue) &&
                    !float.IsInfinity(evt.newValue))
                {
                    SetScalePercent(evt.newValue);
                }
            });
            render = RenderScalePercent;
            RenderScalePercent(initialPercent);
            return row;
        }

        private VisualElement BuildBodyBlendShapeControl(
            BodyBlendShapeDefinition definition)
        {
            var row = CreateSizeControlRow(
                definition.DisplayName,
                out var controls);
            var slider = new Slider(
                MinimumBodyBlendShapeWeight,
                MaximumBodyBlendShapeWeight);
            slider.AddToClassList(
                "ee4v-modification-workflow__size-slider");
            slider.tooltip = I18N.Get(
                "workflow.appearance.bodyShapeSliderTooltip",
                definition.DisplayName);
            controls.Add(slider);
            var value = UiTextFactory.CreateFloatField();
            value.AddToClassList(
                "ee4v-modification-workflow__size-value");
            controls.Add(value);
            var reset = CreateSizeResetButton(() =>
                slider.value = definition.BaseValue);
            controls.Add(reset);

            var rendering = false;
            var appliedWeight = Mathf.Clamp(
                Mathf.Round(definition.Value),
                MinimumBodyBlendShapeWeight,
                MaximumBodyBlendShapeWeight);
            void SetWeight(float weight, bool apply)
            {
                var normalized = Mathf.Clamp(
                    Mathf.Round(weight),
                    MinimumBodyBlendShapeWeight,
                    MaximumBodyBlendShapeWeight);
                rendering = true;
                slider.SetValueWithoutNotify(normalized);
                value.SetValueWithoutNotify(normalized);
                rendering = false;
                reset.SetEnabled(!Mathf.Approximately(
                    normalized,
                    definition.BaseValue));
                if (apply &&
                    !Mathf.Approximately(normalized, appliedWeight))
                {
                    appliedWeight = normalized;
                    ApplyBodyBlendShape(
                        definition.RendererPath,
                        definition.ShapeName,
                        normalized);
                }
            }

            slider.RegisterCallback<PointerDownEvent>(_ =>
                BeginBodyScaleDrag());
            slider.RegisterCallback<PointerUpEvent>(_ =>
                EndBodyScaleDrag());
            slider.RegisterCallback<PointerCaptureOutEvent>(_ =>
                EndBodyScaleDrag());
            slider.RegisterValueChangedCallback(evt =>
            {
                if (!rendering)
                {
                    SetWeight(evt.newValue, true);
                }
            });
            value.RegisterValueChangedCallback(evt =>
            {
                if (!rendering &&
                    !float.IsNaN(evt.newValue) &&
                    !float.IsInfinity(evt.newValue))
                {
                    SetWeight(evt.newValue, true);
                }
            });
            SetWeight(definition.Value, false);
            return row;
        }

        private VisualElement CreateSizeControlRow(
            string label,
            out VisualElement controls,
            bool isChild = false,
            VisualElement leading = null,
            Action labelClicked = null)
        {
            var row = new VisualElement();
            row.AddToClassList(
                "ee4v-modification-workflow__size-control");
            row.EnableInClassList(
                "ee4v-modification-workflow__size-control--child",
                isChild);
            if (leading != null)
            {
                row.Add(leading);
            }
            else if (isChild)
            {
                var indent = new VisualElement();
                indent.AddToClassList(
                    "ee4v-modification-workflow__size-axis-indent");
                row.Add(indent);
            }
            var name = UiTextFactory.Create(
                label,
                "ee4v-modification-workflow__size-control-name");
            name.tooltip = label ?? string.Empty;
            if (labelClicked != null)
            {
                name.RegisterCallback<ClickEvent>(evt =>
                {
                    labelClicked();
                    evt.StopPropagation();
                });
            }
            row.Add(name);
            controls = new VisualElement();
            controls.AddToClassList(
                "ee4v-modification-workflow__size-control-row");
            row.Add(controls);
            return row;
        }

        private UiTextElement CreateSizeUnit(string unit)
        {
            var label = UiTextFactory.Create(
                unit,
                UiClassNames.SecondaryText,
                "ee4v-modification-workflow__size-unit");
            label.SetTextAlign(TextAnchor.MiddleLeft);
            return label;
        }

        private UiButton CreateSizeResetButton(Action reset)
        {
            var button = new UiButton(
                I18N.Get("workflow.appearance.sizeReset"),
                reset,
                variant: UiButtonVariant.Ghost);
            button.AddToClassList(
                "ee4v-modification-workflow__size-reset");
            return button;
        }

        private IReadOnlyList<BodyBlendShapeDefinition>
            GetBodyBlendShapes()
        {
            if (_workingObject == null)
            {
                return Array.Empty<BodyBlendShapeDefinition>();
            }

            var renderers = _workingObject
                .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer =>
                    renderer != null &&
                    renderer.sharedMesh != null &&
                    renderer.sharedMesh.blendShapeCount > 0 &&
                    !IsBodyMesh(renderer))
                .ToArray();
            if (renderers.Length == 0)
            {
                return Array.Empty<BodyBlendShapeDefinition>();
            }

            var result = new List<BodyBlendShapeDefinition>();
            var separators = FaceExpressionSettings.GetSeparators();
            foreach (var renderer in renderers)
            {
                var rendererPath = AnimationUtility.CalculateTransformPath(
                    renderer.transform,
                    _workingObject.transform);
                for (var shapeIndex = 0;
                     shapeIndex < renderer.sharedMesh.blendShapeCount;
                     shapeIndex++)
                {
                    var shapeName = renderer.sharedMesh
                        .GetBlendShapeName(shapeIndex);
                    if (FaceExpressionClipEditor.TryGetHeader(
                            shapeName,
                            separators,
                            out _))
                    {
                        continue;
                    }
                    result.Add(new BodyBlendShapeDefinition
                    {
                        RendererPath = rendererPath,
                        ShapeName = shapeName,
                        DisplayName = GetBodyBlendShapeDisplayName(shapeName),
                        Category = ClassifyBodyPart(
                            shapeName,
                            renderer.name),
                        Value = Mathf.Clamp(
                            renderer.GetBlendShapeWeight(shapeIndex),
                            MinimumBodyBlendShapeWeight,
                            MaximumBodyBlendShapeWeight),
                        BaseValue = GetBaseBlendShapeWeight(
                            renderer,
                            shapeName)
                    });
                }
            }
            return result;
        }

        private static string GetBodyBlendShapeDisplayName(string shapeName)
        {
            var name = (shapeName ?? string.Empty).Trim();
            var separator = name.IndexOf('/');
            if (separator < 0 || separator >= name.Length - 1)
            {
                return name;
            }

            var role = name.Substring(separator + 1).Trim();
            return role.Length > 0 ? role : name;
        }

        private static bool IsBodyMesh(SkinnedMeshRenderer renderer)
        {
            return renderer != null &&
                   (string.Equals(
                        NormalizeBlendShapeName(renderer.name),
                        "body",
                        StringComparison.Ordinal) ||
                    string.Equals(
                        NormalizeBlendShapeName(renderer.sharedMesh?.name),
                        "body",
                        StringComparison.Ordinal));
        }

        private static BodyPartCategory ClassifyBodyPart(
            string shapeName,
            string rendererName)
        {
            var normalized = NormalizeBlendShapeName(shapeName) +
                             NormalizeBlendShapeName(rendererName);
            if (ContainsBlendShapeTerm(normalized, HeadBlendShapeTerms))
            {
                return BodyPartCategory.Head;
            }
            if (ContainsBlendShapeTerm(normalized, ChestBlendShapeTerms))
            {
                return BodyPartCategory.Chest;
            }
            if (ContainsBlendShapeTerm(normalized, WaistBlendShapeTerms))
            {
                return BodyPartCategory.Waist;
            }
            if (ContainsBlendShapeTerm(
                    normalized,
                    ShoulderBlendShapeTerms))
            {
                return BodyPartCategory.Shoulders;
            }
            if (ContainsBlendShapeTerm(normalized, HandBlendShapeTerms))
            {
                return BodyPartCategory.Hands;
            }
            if (ContainsBlendShapeTerm(normalized, ArmBlendShapeTerms))
            {
                return BodyPartCategory.Arms;
            }
            if (ContainsBlendShapeTerm(normalized, FootBlendShapeTerms))
            {
                return BodyPartCategory.Feet;
            }
            if (ContainsBlendShapeTerm(normalized, LegBlendShapeTerms))
            {
                return BodyPartCategory.Legs;
            }
            return BodyPartCategory.Other;
        }

        private static bool ContainsBlendShapeTerm(
            string normalized,
            IEnumerable<string> terms)
        {
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            return terms.Any(term => normalized.IndexOf(
                NormalizeBlendShapeName(term),
                StringComparison.Ordinal) >= 0);
        }

        private static string GetBodyPartCategoryLocalizationKey(
            BodyPartCategory category)
        {
            switch (category)
            {
                case BodyPartCategory.Head:
                    return "workflow.appearance.bodyShapePart.head";
                case BodyPartCategory.Chest:
                    return "workflow.appearance.bodyShapePart.chest";
                case BodyPartCategory.Waist:
                    return "workflow.appearance.bodyShapePart.waist";
                case BodyPartCategory.Shoulders:
                    return "workflow.appearance.bodyShapePart.shoulders";
                case BodyPartCategory.Arms:
                    return "workflow.appearance.bodyShapePart.arms";
                case BodyPartCategory.Hands:
                    return "workflow.appearance.bodyShapePart.hands";
                case BodyPartCategory.Legs:
                    return "workflow.appearance.bodyShapePart.legs";
                case BodyPartCategory.Feet:
                    return "workflow.appearance.bodyShapePart.feet";
                default:
                    return "workflow.appearance.bodyShapePart.other";
            }
        }

        private static string NormalizeBlendShapeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return new string(value
                .Trim()
                .ToLowerInvariant()
                .Where(character =>
                    !char.IsWhiteSpace(character) &&
                    character != '_' &&
                    character != '-' &&
                    character != '.')
                .ToArray());
        }

        private static float GetBaseBlendShapeWeight(
            SkinnedMeshRenderer renderer,
            string shapeName)
        {
            var source = PrefabUtility
                .GetCorrespondingObjectFromSource(renderer) as
                SkinnedMeshRenderer;
            var index = source?.sharedMesh == null
                ? -1
                : source.sharedMesh.GetBlendShapeIndex(shapeName);
            return index < 0
                ? MinimumBodyBlendShapeWeight
                : Mathf.Clamp(
                    source.GetBlendShapeWeight(index),
                    MinimumBodyBlendShapeWeight,
                    MaximumBodyBlendShapeWeight);
        }

        private static bool TryGetAvatarViewPosition(
            GameObject avatar,
            out Component descriptor,
            out Vector3 viewPosition)
        {
            descriptor = avatar == null
                ? null
                : avatar.GetComponentsInChildren<Component>(true)
                    .FirstOrDefault(component =>
                        component != null &&
                        string.Equals(
                            component.GetType().FullName,
                            AvatarDescriptorTypeName,
                            StringComparison.Ordinal));
            return TryReadAvatarViewPosition(descriptor, out viewPosition) &&
                   viewPosition.y > 0.01f;
        }

        private bool TryGetWorkingAvatarViewPosition(
            out Component descriptor,
            out Vector3 viewPosition)
        {
            if (_avatarDescriptor != null &&
                TryReadAvatarViewPosition(
                    _avatarDescriptor,
                    out viewPosition) &&
                viewPosition.y > 0.01f)
            {
                descriptor = _avatarDescriptor;
                return true;
            }

            var found = TryGetAvatarViewPosition(
                _workingObject,
                out descriptor,
                out viewPosition);
            _avatarDescriptor = found ? descriptor : null;
            return found;
        }

        private static bool TryReadAvatarViewPosition(
            Component descriptor,
            out Vector3 viewPosition)
        {
            viewPosition = Vector3.zero;
            if (descriptor == null)
            {
                return false;
            }

            var serialized = new SerializedObject(descriptor);
            var property = serialized.FindProperty("ViewPosition");
            if (property == null ||
                property.propertyType != SerializedPropertyType.Vector3)
            {
                return false;
            }

            viewPosition = property.vector3Value;
            return true;
        }

        private Vector3 GetBaseAvatarViewPosition(
            Component descriptor,
            Vector3 currentViewPosition)
        {
            if (_baseAvatarViewPosition.HasValue)
            {
                return _baseAvatarViewPosition.Value;
            }

            var source = PrefabUtility
                .GetCorrespondingObjectFromSource(descriptor) as Component;
            if (!TryReadAvatarViewPosition(source, out var baseViewPosition) ||
                baseViewPosition.y <= 0.01f)
            {
                baseViewPosition = currentViewPosition;
            }
            _baseAvatarViewPosition = baseViewPosition;
            return baseViewPosition;
        }

        private Animator FindHumanoidAnimator()
        {
            if (_workingObject == null)
            {
                return null;
            }

            return _workingObject
                .GetComponentsInChildren<Animator>(true)
                .FirstOrDefault(animator =>
                    animator != null &&
                    animator.avatar != null &&
                    animator.avatar.isHuman &&
                    animator.isHuman);
        }

        private IReadOnlyList<Transform> ResolveBodyScaleTargets(
            BodyScaleDefinition definition,
            Animator animator)
        {
            if (definition.UsesAvatarRoot)
            {
                return _workingObject != null
                    ? new[] { _workingObject.transform }
                    : Array.Empty<Transform>();
            }
            if (animator == null)
            {
                return Array.Empty<Transform>();
            }

            var targets = new List<Transform>();
            foreach (var bone in definition.Bones)
            {
                var target = animator.GetBoneTransform(bone);
                if (target == null)
                {
                    continue;
                }

                targets.Add(target);
                if (definition.UsesFirstAvailableBone)
                {
                    break;
                }
            }
            return targets;
        }

        private static Vector3 GetBodyScaleMultipliers(
            IReadOnlyList<Transform> targets)
        {
            var sum = Vector3.zero;
            var count = 0;
            foreach (var target in targets)
            {
                if (target == null)
                {
                    continue;
                }

                var baseScale = GetBaseLocalScale(target);
                sum += new Vector3(
                    GetScaleRatio(target.localScale.x, baseScale.x),
                    GetScaleRatio(target.localScale.y, baseScale.y),
                    GetScaleRatio(target.localScale.z, baseScale.z));
                count++;
            }

            return count == 0
                ? Vector3.one
                : RoundBodyScaleMultipliers(sum / count);
        }

        private static float GetScaleRatio(float value, float baseValue)
        {
            return Mathf.Abs(baseValue) < 0.0001f
                ? 1f
                : value / baseValue;
        }

        private static Vector3 RoundBodyScaleMultipliers(Vector3 value)
        {
            return new Vector3(
                Mathf.Round(value.x * 100f) / 100f,
                Mathf.Round(value.y * 100f) / 100f,
                Mathf.Round(value.z * 100f) / 100f);
        }

        private static float AverageVectorComponent(Vector3 value)
        {
            return (value.x + value.y + value.z) / 3f;
        }

        private static float GetVectorComponent(Vector3 value, int axis)
        {
            return axis == 0 ? value.x : axis == 1 ? value.y : value.z;
        }

        private static Vector3 SetVectorComponent(
            Vector3 value,
            int axis,
            float component)
        {
            if (axis == 0)
            {
                value.x = component;
            }
            else if (axis == 1)
            {
                value.y = component;
            }
            else
            {
                value.z = component;
            }
            return value;
        }

        private static string GetScaleAxisName(int axis)
        {
            return I18N.Get(
                axis == 0
                    ? "workflow.appearance.sizeAxis.x"
                    : axis == 1
                        ? "workflow.appearance.sizeAxis.y"
                        : "workflow.appearance.sizeAxis.z");
        }

        private static Vector3 GetBaseLocalScale(Transform target)
        {
            var source = PrefabUtility
                .GetCorrespondingObjectFromSource(target) as Transform;
            return source != null ? source.localScale : Vector3.one;
        }

        private Vector3 GetCachedBaseLocalScale(
            string path,
            Transform target)
        {
            var key = path ?? string.Empty;
            if (_bodyScaleBaseScales.TryGetValue(key, out var scale))
            {
                return scale;
            }

            scale = GetBaseLocalScale(target);
            _bodyScaleBaseScales[key] = scale;
            return scale;
        }

        private void ApplyBodyScale(
            IReadOnlyList<string> targetPaths,
            Vector3 multipliers,
            bool updateAvatarViewPosition = false,
            bool rebuildOnFailure = true)
        {
            if (_workingObject == null ||
                targetPaths == null ||
                targetPaths.Count == 0)
            {
                return;
            }

            var targets = _resolvedBodyScaleTargets;
            targets.Clear();
            foreach (var path in targetPaths)
            {
                var target = string.IsNullOrEmpty(path)
                    ? _workingObject.transform
                    : _workingObject.transform.Find(path);
                if (target != null)
                {
                    targets.Add(new KeyValuePair<string, Transform>(
                        path,
                        target));
                }
            }
            if (targets.Count == 0)
            {
                return;
            }

            var previewScales = _bodyScalePreviewScales;
            previewScales.Clear();
            foreach (var pair in targets)
            {
                previewScales[pair.Key] = Vector3.Scale(
                    GetCachedBaseLocalScale(pair.Key, pair.Value),
                    multipliers);
            }

            if (_bodyScaleDragging)
            {
                _scenePreview?.SetTransformScales(
                    previewScales,
                    recalculateBounds: false);
                _pendingBodySizeChange = PendingBodySizeChange.Scale;
                _pendingBodyScaleTargetPaths = targetPaths;
                _pendingBodyScaleMultipliers = multipliers;
                _pendingBodyScaleUpdatesViewPosition =
                    updateAvatarViewPosition;
                return;
            }
            if (!IsEditableWorkflowPrefab())
            {
                return;
            }

            try
            {
                Component avatarDescriptor = null;
                var baseViewPosition = Vector3.zero;
                if (updateAvatarViewPosition &&
                    TryGetWorkingAvatarViewPosition(
                        out avatarDescriptor,
                        out var currentViewPosition))
                {
                    baseViewPosition = GetBaseAvatarViewPosition(
                        avatarDescriptor,
                        currentViewPosition);
                }
                var undoObjects = targets
                    .Select(pair => (UnityEngine.Object)pair.Value)
                    .ToList();
                if (avatarDescriptor != null)
                {
                    undoObjects.Add(avatarDescriptor);
                }
                Undo.RecordObjects(
                    undoObjects.Distinct().ToArray(),
                    I18N.Get("workflow.appearance.sizeUndo"));
                foreach (var pair in targets)
                {
                    var scale = previewScales[pair.Key];
                    pair.Value.localScale = scale;
                    if (PrefabUtility.IsPartOfPrefabInstance(pair.Value))
                    {
                        PrefabUtility
                            .RecordPrefabInstancePropertyModifications(
                                pair.Value);
                    }
                    EditorUtility.SetDirty(pair.Value);
                }
                if (avatarDescriptor != null)
                {
                    var serialized = new SerializedObject(avatarDescriptor);
                    var viewPosition = serialized.FindProperty("ViewPosition");
                    if (viewPosition != null)
                    {
                        viewPosition.vector3Value = Vector3.Scale(
                            baseViewPosition,
                            multipliers);
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                    if (PrefabUtility.IsPartOfPrefabInstance(
                            avatarDescriptor))
                    {
                        PrefabUtility
                            .RecordPrefabInstancePropertyModifications(
                                avatarDescriptor);
                    }
                    EditorUtility.SetDirty(avatarDescriptor);
                }
                EditorUtility.SetDirty(_workingObject);
                _bodyScaleDirty = true;
                _scenePreview?.SetTransformScales(
                    previewScales,
                    recalculateBounds: true);
                SaveBodyScalePrefab(rebuildOnFailure);
            }
            catch (Exception exception)
            {
                ReportBodyScaleFailure(exception, rebuildOnFailure);
            }
        }

        private void ApplyBodyBlendShape(
            string rendererPath,
            string shapeName,
            float weight,
            bool rebuildOnFailure = true)
        {
            if (_workingObject == null ||
                string.IsNullOrEmpty(shapeName))
            {
                return;
            }

            if (_bodyScaleDragging)
            {
                _scenePreview?.SetBlendShapeWeight(
                    rendererPath,
                    shapeName,
                    weight,
                    recalculateBounds: false);
                _pendingBodySizeChange =
                    PendingBodySizeChange.BlendShape;
                _pendingBodyBlendShapeRendererPath = rendererPath;
                _pendingBodyBlendShapeName = shapeName;
                _pendingBodyBlendShapeWeight = weight;
                return;
            }
            if (!IsEditableWorkflowPrefab())
            {
                return;
            }

            var target = string.IsNullOrEmpty(rendererPath)
                ? _workingObject.transform
                : _workingObject.transform.Find(rendererPath);
            var renderer = target == null
                ? null
                : target.GetComponent<SkinnedMeshRenderer>();
            var shapeIndex = renderer?.sharedMesh == null
                ? -1
                : renderer.sharedMesh.GetBlendShapeIndex(shapeName);
            if (renderer == null || shapeIndex < 0)
            {
                return;
            }

            try
            {
                Undo.RecordObject(
                    renderer,
                    I18N.Get("workflow.appearance.sizeUndo"));
                renderer.SetBlendShapeWeight(shapeIndex, weight);
                if (PrefabUtility.IsPartOfPrefabInstance(renderer))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(
                        renderer);
                }
                EditorUtility.SetDirty(renderer);
                EditorUtility.SetDirty(_workingObject);
                _bodyScaleDirty = true;
                _scenePreview?.SetBlendShapeWeight(
                    rendererPath,
                    shapeName,
                    weight,
                    recalculateBounds: true);
                SaveBodyScalePrefab(rebuildOnFailure);
            }
            catch (Exception exception)
            {
                ReportBodyScaleFailure(exception, rebuildOnFailure);
            }
        }

        private void BeginBodyScaleDrag()
        {
            if (_bodyScaleDragging)
            {
                return;
            }

            _bodyScaleDragging = true;
            ClearPendingBodySizeChange();
        }

        private void EndBodyScaleDrag(bool rebuildOnFailure = true)
        {
            if (!_bodyScaleDragging &&
                _pendingBodySizeChange == PendingBodySizeChange.None)
            {
                return;
            }

            var pendingChange = _pendingBodySizeChange;
            var targetPaths = _pendingBodyScaleTargetPaths;
            var multipliers = _pendingBodyScaleMultipliers;
            var updateAvatarViewPosition =
                _pendingBodyScaleUpdatesViewPosition;
            var rendererPath = _pendingBodyBlendShapeRendererPath;
            var shapeName = _pendingBodyBlendShapeName;
            var weight = _pendingBodyBlendShapeWeight;
            ClearPendingBodySizeChange();
            _bodyScaleDragging = false;
            if (pendingChange == PendingBodySizeChange.Scale)
            {
                ApplyBodyScale(
                    targetPaths,
                    multipliers,
                    updateAvatarViewPosition,
                    rebuildOnFailure);
            }
            else if (pendingChange == PendingBodySizeChange.BlendShape)
            {
                ApplyBodyBlendShape(
                    rendererPath,
                    shapeName,
                    weight,
                    rebuildOnFailure);
            }
            _scenePreview?.FlushUpdates(recalculateBounds: true);
            if (pendingChange == PendingBodySizeChange.None)
            {
                SaveBodyScalePrefab(rebuildOnFailure);
            }
        }

        private void ClearPendingBodySizeChange()
        {
            _pendingBodySizeChange = PendingBodySizeChange.None;
            _pendingBodyScaleTargetPaths = null;
            _pendingBodyScaleUpdatesViewPosition = false;
            _pendingBodyBlendShapeRendererPath = null;
            _pendingBodyBlendShapeName = null;
        }

        private void SaveBodyScalePrefab(bool rebuildOnFailure = true)
        {
            if (!_bodyScaleDirty || !IsEditableWorkflowPrefab())
            {
                return;
            }

            try
            {
                var savedPrefab = PrefabUtility.SavePrefabAsset(
                    _workingObject,
                    out var savedSuccessfully);
                if (!savedSuccessfully || savedPrefab == null)
                {
                    throw new InvalidOperationException(
                        "The derived Prefab size could not be saved.");
                }

                _workingObject = savedPrefab;
                if (_workingAsset != null)
                {
                    _workingAsset.Prefab = savedPrefab;
                }
                _scenePreview?.SetPrefab(savedPrefab);
                _bodyScaleDirty = false;
                _feedback = string.Empty;
            }
            catch (Exception exception)
            {
                ReportBodyScaleFailure(exception, rebuildOnFailure);
            }
        }

        private void ReportBodyScaleFailure(
            Exception exception,
            bool rebuildControls)
        {
            Debug.LogException(exception);
            _feedback = I18N.Get(
                "workflow.appearance.sizeSaveFailed");
            _feedbackType = HelpBoxMessageType.Error;
            if (rebuildControls && rootVisualElement.panel != null)
            {
                rootVisualElement.schedule.Execute(() =>
                    ShowCategory(WorkflowCategory.Appearance, false));
            }
        }

        private bool IsEditableWorkflowPrefab()
        {
            if (_workingObject == null)
            {
                return false;
            }

            var path = AssetDatabase.GetAssetPath(_workingObject);
            return !string.IsNullOrEmpty(path) &&
                   path.StartsWith(
                       DerivedAssetCreator.VariantRoot + "/",
                       StringComparison.OrdinalIgnoreCase) &&
                   PrefabUtility.IsPartOfPrefabAsset(_workingObject);
        }

        private static string FormatBodyHeight(float value)
        {
            return value.ToString("0.00") + " m";
        }

        private VisualElement BuildAppearanceSectionTabs()
        {
            var tabs = new VisualElement();
            tabs.AddToClassList(
                "ee4v-modification-workflow__appearance-tabs");
            AddAppearanceSectionButton(
                tabs,
                AppearanceSection.Size,
                "workflow.appearance.section.size");
            AddAppearanceSectionButton(
                tabs,
                AppearanceSection.Material,
                "workflow.appearance.section.material");
            return tabs;
        }

        private void AddAppearanceSectionButton(
            VisualElement tabs,
            AppearanceSection section,
            string labelKey)
        {
            var button = new UiButton(
                I18N.Get(labelKey),
                () =>
                {
                    if (_appearanceSection == AppearanceSection.Size &&
                        section != AppearanceSection.Size)
                    {
                        EndBodyScaleDrag();
                    }
                    _appearanceSection = section;
                    ShowCategory(WorkflowCategory.Appearance, false);
                },
                variant: UiButtonVariant.Ghost);
            button.AddToClassList(
                "ee4v-modification-workflow__appearance-tab");
            button.EnableInClassList(
                "ee4v-modification-workflow__appearance-tab--active",
                section == _appearanceSection);
            tabs.Add(button);
        }

        private VisualElement BuildMaterialEditor(Material material)
        {
            var section = new VisualElement();
            section.AddToClassList(
                "ee4v-modification-workflow__material-editor-section");

            var header = new ItemRow(new ItemRowState(
                material.name,
                icon: CreateMaterialIcon(material)));
            header.AddToClassList(
                "ee4v-modification-workflow__material-editor-header");

            var surface = new VisualElement();
            surface.AddToClassList(
                "ee4v-modification-workflow__material-editor-surface");
            surface.Add(header);

            _materialInspector = new EmbeddedMaterialInspector(
                material,
                RefreshMaterialPreview);
            if (!_materialInspector.IsAvailable)
            {
                _materialInspector.Dispose();
                _materialInspector = null;
                surface.Add(UiTextFactory.CreateHelpBox(
                    I18N.Get("workflow.appearance.editorUnavailable"),
                    HelpBoxMessageType.Error));
                section.Add(surface);
                return section;
            }

            surface.Add(_materialInspector);
            section.Add(surface);
            return section;
        }

        private VisualElement BuildAllMaterialsVisibilityRow(
            IReadOnlyCollection<Material> materials)
        {
            var row = new ItemRow(new ItemRowState(
                I18N.Get("workflow.appearance.allMaterials")));
            row.AddToClassList(
                "ee4v-modification-workflow__material-visibility-all");
            _allMaterialsVisibilityButton = new UiButton(
                string.Empty,
                () => ToggleAllMaterialsVisibility(materials),
                variant: UiButtonVariant.Ghost);
            _allMaterialsVisibilityButton.AddToClassList(
                "ee4v-modification-workflow__material-visibility");
            row.Trailing.Add(_allMaterialsVisibilityButton);
            return row;
        }

        private void ToggleMaterialVisibility(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (!_hiddenMaterials.Add(material))
            {
                _hiddenMaterials.Remove(material);
            }
            _scenePreview?.SetHiddenMaterials(_hiddenMaterials);
            RefreshMaterialVisibilityButtons(
                _materialVisibilityButtons.Keys.ToArray());
        }

        private void ToggleAllMaterialsVisibility(
            IReadOnlyCollection<Material> materials)
        {
            if (_hiddenMaterials.Count == 0)
            {
                _hiddenMaterials.UnionWith(materials);
            }
            else
            {
                _hiddenMaterials.Clear();
            }

            _scenePreview?.SetHiddenMaterials(_hiddenMaterials);
            RefreshMaterialVisibilityButtons(materials);
        }

        private void RefreshMaterialVisibilityButtons(
            IReadOnlyCollection<Material> materials)
        {
            foreach (var pair in _materialVisibilityButtons)
            {
                var material = pair.Key;
                var button = pair.Value;
                var isVisible = !_hiddenMaterials.Contains(material);
                var tooltipKey = isVisible
                    ? "workflow.appearance.hideMaterial"
                    : "workflow.appearance.showMaterial";
                var tooltip = I18N.Get(tooltipKey);
                button.tooltip = tooltip;
                button.SetIcon(IconState.FromBuiltinIcon(
                    isVisible
                        ? UiBuiltinIcon.VisibilityVisible
                        : UiBuiltinIcon.VisibilityHidden,
                    UiSizeTokens.Size18,
                    tooltip));
            }

            var allVisible = materials.Count > 0 &&
                             !_hiddenMaterials.Overlaps(materials);
            var allTooltip = I18N.Get(allVisible
                ? "workflow.appearance.hideAll"
                : "workflow.appearance.showAll");
            _allMaterialsVisibilityButton?.SetIcon(
                IconState.FromBuiltinIcon(
                    allVisible
                        ? UiBuiltinIcon.VisibilityVisible
                        : UiBuiltinIcon.VisibilityHidden,
                    UiSizeTokens.Size18,
                    allTooltip));
            if (_allMaterialsVisibilityButton != null)
            {
                _allMaterialsVisibilityButton.tooltip = allTooltip;
            }
        }

        private VisualElement BuildTodoControls()
        {
            var panel = CreateControlsPanel(
                "workflow.todo.title",
                "workflow.todo.description");
            panel.Add(CreateTodoCard(
                "workflow.todo.materialPresetTitle",
                "workflow.todo.materialPresetDescription"));
            panel.Add(CreateTodoCard(
                "workflow.todo.expressionPresetTitle",
                "workflow.todo.expressionPresetDescription"));
            panel.Add(CreateTodoCard(
                "workflow.todo.applyTitle",
                "workflow.todo.applyDescription"));
            panel.Add(CreateTodoCard(
                "workflow.todo.sessionTitle",
                "workflow.todo.sessionDescription"));
            return panel;
        }

        private VisualElement CreateControlsPanel(
            string titleKey,
            string descriptionKey)
        {
            var panel = new VisualElement();
            panel.AddToClassList(
                "ee4v-modification-workflow__controls-content");
            panel.Add(UiTextFactory.Create(
                I18N.Get(titleKey),
                UiClassNames.SectionTitle,
                "ee4v-modification-workflow__controls-title"));
            var description = UiTextFactory.Create(
                I18N.Get(descriptionKey),
                UiClassNames.SecondaryText,
                "ee4v-modification-workflow__controls-description");
            description.SetWhiteSpace(WhiteSpace.Normal);
            panel.Add(description);
            return panel;
        }

        private void AddFeedback(VisualElement panel)
        {
            if (string.IsNullOrWhiteSpace(_feedback))
            {
                return;
            }
            panel.Add(UiTextFactory.CreateHelpBox(
                _feedback,
                _feedbackType,
                "ee4v-modification-workflow__feedback"));
        }

        private VisualElement CreateEmptyState(
            string titleKey,
            string descriptionKey)
        {
            var empty = new VisualElement();
            empty.AddToClassList("ee4v-ui-empty-state");
            empty.AddToClassList(
                "ee4v-modification-workflow__empty-state");
            empty.Add(UiTextFactory.Create(
                I18N.Get(titleKey),
                UiClassNames.SectionTitle,
                "ee4v-ui-empty-state__title"));
            var description = UiTextFactory.Create(
                I18N.Get(descriptionKey),
                UiClassNames.SecondaryText,
                "ee4v-ui-empty-state__description");
            description.SetWhiteSpace(WhiteSpace.Normal);
            empty.Add(description);
            return empty;
        }

        private VisualElement CreateTodoCard(
            string titleKey,
            string descriptionKey)
        {
            var card = new InfoCard(new InfoCardState(
                I18N.Get(titleKey),
                I18N.Get(descriptionKey),
                I18N.Get("workflow.todo.badge")));
            card.AddToClassList(
                "ee4v-modification-workflow__todo-card");
            return card;
        }

        private void StartDerivedAssetCreation()
        {
            _creatingDerivedAsset = true;
            _feedback = string.Empty;
            BuildWindow();
        }

        private void CancelDerivedAssetCreation()
        {
            _creatingDerivedAsset = false;
            _feedback = string.Empty;
            BuildWindow();
        }

        private IReadOnlyList<VariantSourceOption> GetVariantSources()
        {
            try
            {
                _manager = _manager ?? AssetManagerWindowSession.GetManager();
                return _manager.SearchItems(new AssetItemQuery())
                    .Items
                    .Where(item => item != null && !item.IsArchived)
                    .Select(item => new VariantSourceOption
                    {
                        Item = item,
                        Prefabs = DerivedAssetCreator.FindPrefabCandidates(
                            _manager.GetItemImportedAssetGuids(item.Id))
                    })
                    .Where(source => source.Prefabs.Count > 0)
                    .OrderBy(
                        source => source.Item.Name,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return Array.Empty<VariantSourceOption>();
            }
        }

        private void SelectVariantSource(VariantSourceOption source)
        {
            if (source?.Item == null || source.Prefabs.Count == 0)
            {
                return;
            }
            _creationItemId = source.Item.Id;
            _creationPrefab = source.Prefabs[0];
            _derivedName = source.Item.Name + " Variant";
            _derivedDescription = string.Empty;
            _feedback = string.Empty;
            BuildWindow();
        }

        private void CreateDerivedAsset()
        {
            if (!DerivedAssetCreator.IsValidName(_derivedName))
            {
                SetCreationFeedback(
                    I18N.Get("notice.derivedAssetNameInvalid"),
                    HelpBoxMessageType.Error);
                return;
            }
            if (string.IsNullOrEmpty(_creationItemId) ||
                _creationPrefab == null)
            {
                SetCreationFeedback(
                    I18N.Get("notice.derivedAssetPrefabInvalid"),
                    HelpBoxMessageType.Error);
                return;
            }
            if (AssetDatabase.IsValidFolder(
                    DerivedAssetCreator.GetVariantFolder(_derivedName)))
            {
                SetCreationFeedback(
                    I18N.Get("notice.derivedAssetAlreadyExists"),
                    HelpBoxMessageType.Error);
                return;
            }

            try
            {
                var result = DerivedAssetCreator.Create(
                    new DerivedAssetCreationRequest
                    {
                        ParentItemId = _creationItemId,
                        Name = _derivedName,
                        Description = _derivedDescription,
                        Prefab = _creationPrefab
                    });
                _creatingDerivedAsset = false;
                SelectDerivedAsset(result);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetCreationFeedback(
                    I18N.Get("notice.derivedAssetCreateFailed"),
                    HelpBoxMessageType.Error);
            }
        }

        private void SetCreationFeedback(
            string message,
            HelpBoxMessageType type)
        {
            _feedback = message ?? string.Empty;
            _feedbackType = type;
            BuildWindow();
        }

        private void SelectDerivedAsset(DerivedAssetInfo asset)
        {
            if (asset?.Prefab == null)
            {
                return;
            }
            EndBodyScaleDrag();
            _bodyScaleDirty = false;
            _bodyScaleDragging = false;
            ClearPendingBodySizeChange();
            _bodyScaleBaseScales.Clear();
            _advancedBodyScaleExpanded = false;
            _expandedBodyScaleAxes.Clear();
            _baseAvatarViewPosition = null;
            _avatarDescriptor = null;
            _workingAsset = asset;
            _workingObject = asset.Prefab;
            _creatingDerivedAsset = false;
            _currentCategory = WorkflowCategory.Appearance;
            _appearanceSection = AppearanceSection.Size;
            _selectedMaterial = null;
            _hiddenMaterials.Clear();
            _feedback = string.Empty;
            BuildWindow();
        }

        private void ClearDerivedAsset()
        {
            EndBodyScaleDrag();
            _bodyScaleDirty = false;
            _bodyScaleDragging = false;
            ClearPendingBodySizeChange();
            _bodyScaleBaseScales.Clear();
            _advancedBodyScaleExpanded = false;
            _expandedBodyScaleAxes.Clear();
            _baseAvatarViewPosition = null;
            _avatarDescriptor = null;
            _workingAsset = null;
            _workingObject = null;
            _selectedMaterial = null;
            _hiddenMaterials.Clear();
            _feedback = string.Empty;
            BuildWindow();
        }

        private IReadOnlyList<AvatarMaterialEntry> GetAvatarMaterials()
        {
            if (_workingObject == null)
            {
                return Array.Empty<AvatarMaterialEntry>();
            }

            var entries = new List<AvatarMaterialEntry>();
            var byMaterial = new Dictionary<Material, AvatarMaterialEntry>();
            foreach (var renderer in _workingObject
                         .GetComponentsInChildren<Renderer>(true))
            {
                var rendererPath = AnimationUtility.CalculateTransformPath(
                    renderer.transform,
                    _workingObject.transform);
                if (string.IsNullOrWhiteSpace(rendererPath))
                {
                    rendererPath = _workingObject.name;
                }

                var assigned = renderer.sharedMaterials;
                for (var index = 0; index < assigned.Length; index++)
                {
                    var material = assigned[index];
                    if (material == null)
                    {
                        continue;
                    }

                    if (!byMaterial.TryGetValue(material, out var entry))
                    {
                        entry = new AvatarMaterialEntry
                        {
                            Material = material
                        };
                        byMaterial.Add(material, entry);
                        entries.Add(entry);
                    }

                    entry.Usages.Add(new MaterialUsage
                    {
                        RendererPath = rendererPath,
                        SlotIndex = index
                    });
                }
            }

            return entries;
        }

        private static IconState CreateMaterialIcon(Material material)
        {
            var texture = AssetPreview.GetMiniThumbnail(material) ??
                          EditorGUIUtility.ObjectContent(
                              material,
                              typeof(Material)).image;
            return texture != null
                ? IconState.FromTexture(texture, UiSizeTokens.Size31)
                : AssetManagerControls.LoadFluentIconState(
                    "image.png",
                    UiSizeTokens.Size31,
                    tintColor: UiColorTokens.TextMuted);
        }

        private static string FormatMaterialUsageSummary(
            AvatarMaterialEntry entry)
        {
            if (entry == null || entry.Usages.Count == 0)
            {
                return string.Empty;
            }

            var first = FormatMaterialUsage(entry.Usages[0]);
            return entry.Usages.Count > 1
                ? first + " " + I18N.Get(
                    "workflow.appearance.moreUsages",
                    entry.Usages.Count - 1)
                : first;
        }

        private static string FormatMaterialUsageTooltip(
            AvatarMaterialEntry entry)
        {
            return entry == null
                ? string.Empty
                : string.Join(
                    "\n",
                    entry.Usages.Select(FormatMaterialUsage));
        }

        private static string FormatMaterialUsage(MaterialUsage usage)
        {
            return I18N.Get(
                "workflow.appearance.materialUsage",
                usage?.RendererPath ?? string.Empty,
                (usage?.SlotIndex ?? 0) + 1);
        }

        private static bool IsEditableWorkflowMaterial(Material material)
        {
            var path = AssetDatabase.GetAssetPath(material);
            return material != null &&
                   !string.IsNullOrEmpty(path) &&
                   path.StartsWith(
                       DerivedAssetCreator.VariantRoot + "/",
                       StringComparison.OrdinalIgnoreCase);
        }

        private void RefreshMaterialPreview()
        {
            _scenePreview?.RefreshPreview();
        }

        private void RefreshAfterUndoRedo()
        {
            if (_currentCategory != WorkflowCategory.Appearance ||
                _appearanceSection != AppearanceSection.Size)
            {
                RefreshMaterialPreview();
                return;
            }

            if (IsEditableWorkflowPrefab())
            {
                try
                {
                    var savedPrefab = PrefabUtility.SavePrefabAsset(
                        _workingObject,
                        out var savedSuccessfully);
                    if (savedSuccessfully && savedPrefab != null)
                    {
                        _workingObject = savedPrefab;
                        if (_workingAsset != null)
                        {
                            _workingAsset.Prefab = savedPrefab;
                        }
                        _bodyScaleDirty = false;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            _scenePreview?.ReloadPrefab();
            if (_controlsHost == null)
            {
                return;
            }
            _controlsHost.Clear();
            _controlsHost.Add(BuildAppearanceControls());
        }

        private void DisposeMaterialEditor()
        {
            _materialInspector?.Dispose();
            _materialInspector = null;
        }

        private void SetPreviewTitle(string titleKey)
        {
            _previewTitle?.SetText(I18N.Get(titleKey));
        }

        private void DisposeEditors()
        {
            DisposeMaterialEditor();
            _faceExpressionEditor?.Dispose();
            _faceExpressionEditor = null;
            _physBoneEditor?.Dispose();
            _physBoneEditor = null;
            _scenePreview?.Dispose();
            _scenePreview = null;
        }

        private static string FormatVariantSource(
            VariantSourceOption source)
        {
            return source?.Item?.Name ?? string.Empty;
        }
    }
}
