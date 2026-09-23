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
            AppearanceSection.Material;
        private bool _creatingDerivedAsset;
        private string _creationItemId = string.Empty;
        private GameObject _creationPrefab;
        private string _derivedName = string.Empty;
        private string _derivedDescription = string.Empty;
        private string _feedback = string.Empty;
        private HelpBoxMessageType _feedbackType =
            HelpBoxMessageType.Info;

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
            Undo.undoRedoPerformed -= RefreshMaterialPreview;
            Undo.undoRedoPerformed += RefreshMaterialPreview;
            ConfigureWindow();
        }

        private void CreateGUI()
        {
            BuildWindow();
        }

        private void OnDisable()
        {
            I18N.Reloaded -= Rebuild;
            AssetManagerWindowSession.ManagerInvalidated -=
                OnManagerInvalidated;
            Undo.undoRedoPerformed -= RefreshMaterialPreview;
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
                panel.Add(CreateTodoCard(
                    "workflow.appearance.sizeTodoTitle",
                    "workflow.appearance.sizeTodoDescription"));
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

        private VisualElement BuildAppearanceSectionTabs()
        {
            var tabs = new VisualElement();
            tabs.AddToClassList(
                "ee4v-modification-workflow__appearance-tabs");
            AddAppearanceSectionButton(
                tabs,
                AppearanceSection.Material,
                "workflow.appearance.section.material");
            AddAppearanceSectionButton(
                tabs,
                AppearanceSection.Size,
                "workflow.appearance.section.size");
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
            _workingAsset = asset;
            _workingObject = asset.Prefab;
            _creatingDerivedAsset = false;
            _currentCategory = WorkflowCategory.Appearance;
            _appearanceSection = AppearanceSection.Material;
            _selectedMaterial = null;
            _hiddenMaterials.Clear();
            _feedback = string.Empty;
            BuildWindow();
        }

        private void ClearDerivedAsset()
        {
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
