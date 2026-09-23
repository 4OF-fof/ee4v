using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.FaceExpression;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetModificationWorkflowWindow : EditorWindow
    {
        private enum WorkflowCategory
        {
            Appearance,
            ExpressionAnimation,
            Todo
        }

        private enum AppearanceGroup
        {
            Color,
            Texture,
            Adjustment
        }

        private sealed class ShaderPropertyEntry
        {
            internal int Index { get; set; }
            internal string Name { get; set; }
            internal string DisplayName { get; set; }
            internal ShaderPropertyType Type { get; set; }
        }

        private sealed class VariantSourceOption
        {
            internal AssetItem Item { get; set; }
            internal IReadOnlyList<GameObject> Prefabs { get; set; }
        }

        private readonly Dictionary<WorkflowCategory, UiButton>
            _categoryButtons =
                new Dictionary<WorkflowCategory, UiButton>();
        private IAssetManager _manager;
        private DerivedAssetInfo _workingAsset;
        private GameObject _workingObject;
        private Material _selectedMaterial;
        private DerivedAssetPrefabScenePreview _scenePreview;
        private FaceExpressionWindow.EmbeddedEditor _faceExpressionEditor;
        private VisualElement _customizerHost;
        private VisualElement _faceExpressionHost;
        private ScrollView _controlsHost;
        private UiTextElement _previewTitle;
        private UiTextElement _previewHint;
        private WorkflowCategory _currentCategory =
            WorkflowCategory.Appearance;
        private AppearanceGroup _appearanceGroup =
            AppearanceGroup.Color;
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
            DisposeEditors();
        }

        private void ConfigureWindow()
        {
            titleContent = UiTextFactory.CreateGuiContent(
                I18N.Get("workflow.windowTitle"));
            minSize = new Vector2(1040f, 680f);
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
            _previewTitle = UiTextFactory.Create(
                string.Empty,
                UiClassNames.SectionTitle,
                "ee4v-modification-workflow__preview-title");
            pane.Add(_previewTitle);
            _scenePreview = new DerivedAssetPrefabScenePreview();
            _scenePreview.AddToClassList(
                "ee4v-modification-workflow__preview");
            _scenePreview.SetPrefab(_workingObject);
            pane.Add(_scenePreview);
            _previewHint = UiTextFactory.Create(
                string.Empty,
                UiClassNames.SecondaryText,
                "ee4v-modification-workflow__preview-hint");
            _previewHint.SetWhiteSpace(WhiteSpace.Normal);
            _previewHint.SetTextAlign(TextAnchor.MiddleCenter);
            pane.Add(_previewHint);
            return pane;
        }

        private void ShowCategory(
            WorkflowCategory category,
            bool clearFeedback = true)
        {
            if (_customizerHost == null || _faceExpressionHost == null)
            {
                _currentCategory = category;
                return;
            }
            if (_currentCategory != category && clearFeedback)
            {
                _feedback = string.Empty;
            }
            _currentCategory = category;
            foreach (var pair in _categoryButtons)
            {
                pair.Value.EnableInClassList(
                    "ee4v-modification-workflow__category-button--active",
                    pair.Key == category);
            }

            var faceExpression =
                category == WorkflowCategory.ExpressionAnimation;
            _customizerHost.EnableInClassList(
                "ee4v-modification-workflow__hidden",
                faceExpression);
            _faceExpressionHost.EnableInClassList(
                "ee4v-modification-workflow__hidden",
                !faceExpression);
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
            _controlsHost.Clear();
            if (category == WorkflowCategory.Appearance)
            {
                _controlsHost.Add(BuildAppearanceControls());
                SetPreviewText(
                    "workflow.preview.appearanceTitle",
                    "workflow.preview.appearanceHint");
            }
            else
            {
                _controlsHost.Add(BuildTodoControls());
                SetPreviewText(
                    "workflow.preview.todoTitle",
                    "workflow.preview.todoHint");
            }
        }

        private VisualElement BuildAppearanceControls()
        {
            var panel = CreateControlsPanel(
                "workflow.appearance.title",
                "workflow.appearance.description");
            AddFeedback(panel);
            var materials = GetWorkingMaterials();
            if (materials.Count == 0)
            {
                panel.Add(CreateEmptyState(
                    "workflow.appearance.emptyTitle",
                    "workflow.appearance.emptyDescription"));
                return panel;
            }

            if (_selectedMaterial == null ||
                !materials.Contains(_selectedMaterial))
            {
                _selectedMaterial = materials[0];
            }

            panel.Add(CreateSubheading(
                "workflow.appearance.partTitle",
                "workflow.appearance.partDescription"));
            var materialChoices = new VisualElement();
            materialChoices.AddToClassList(
                "ee4v-modification-workflow__choice-grid");
            foreach (var material in materials)
            {
                var choice = new UiButton(
                    material.name,
                    () =>
                    {
                        _selectedMaterial = material;
                        ShowCategory(WorkflowCategory.Appearance, false);
                    },
                    variant: UiButtonVariant.Ghost);
                choice.AddToClassList(
                    "ee4v-modification-workflow__choice-button");
                choice.EnableInClassList(
                    "ee4v-modification-workflow__choice-button--active",
                    material == _selectedMaterial);
                materialChoices.Add(choice);
            }
            panel.Add(materialChoices);

            if (!IsEditableWorkflowMaterial(_selectedMaterial))
            {
                panel.Add(UiTextFactory.CreateHelpBox(
                    I18N.Get("workflow.appearance.protected"),
                    HelpBoxMessageType.Warning));
                return panel;
            }

            panel.Add(CreateSubheading(
                "workflow.appearance.groupTitle",
                "workflow.appearance.groupDescription"));
            var groups = new VisualElement();
            groups.AddToClassList(
                "ee4v-modification-workflow__segment-row");
            AddAppearanceGroupButton(
                groups,
                AppearanceGroup.Color,
                "workflow.appearance.group.color");
            AddAppearanceGroupButton(
                groups,
                AppearanceGroup.Texture,
                "workflow.appearance.group.texture");
            AddAppearanceGroupButton(
                groups,
                AppearanceGroup.Adjustment,
                "workflow.appearance.group.adjustment");
            panel.Add(groups);
            panel.Add(BuildMaterialPropertyGroup(_selectedMaterial));
            return panel;
        }

        private void AddAppearanceGroupButton(
            VisualElement row,
            AppearanceGroup group,
            string labelKey)
        {
            var button = new UiButton(
                I18N.Get(labelKey),
                () =>
                {
                    _appearanceGroup = group;
                    ShowCategory(WorkflowCategory.Appearance, false);
                },
                variant: UiButtonVariant.Ghost);
            button.AddToClassList(
                "ee4v-modification-workflow__segment-button");
            button.EnableInClassList(
                "ee4v-modification-workflow__segment-button--active",
                group == _appearanceGroup);
            row.Add(button);
        }

        private VisualElement BuildMaterialPropertyGroup(Material material)
        {
            var group = new VisualElement();
            group.AddToClassList(
                "ee4v-modification-workflow__setting-list");
            var properties = GetMaterialProperties(
                material,
                _appearanceGroup);
            if (properties.Count == 0)
            {
                group.Add(CreateEmptyState(
                    "workflow.appearance.groupEmptyTitle",
                    "workflow.appearance.groupEmptyDescription"));
                return group;
            }

            foreach (var property in properties)
            {
                group.Add(BuildMaterialProperty(material, property));
            }
            return group;
        }

        private VisualElement BuildMaterialProperty(
            Material material,
            ShaderPropertyEntry property)
        {
            var setting = new VisualElement();
            setting.AddToClassList(
                "ee4v-modification-workflow__setting");
            setting.Add(UiTextFactory.Create(
                property.DisplayName,
                UiClassNames.FormLabel,
                "ee4v-modification-workflow__setting-label"));
            switch (property.Type)
            {
                case ShaderPropertyType.Color:
                {
                    var field = UiTextFactory.CreateColorField();
                    field.SetValueWithoutNotify(
                        material.GetColor(property.Name));
                    field.RegisterValueChangedCallback(evt =>
                        EditMaterial(material, () => material.SetColor(
                            property.Name,
                            evt.newValue)));
                    setting.Add(field);
                    break;
                }
                case ShaderPropertyType.Texture:
                {
                    var field = UiTextFactory.CreateObjectField();
                    field.objectType = typeof(Texture);
                    field.allowSceneObjects = false;
                    field.SetValueWithoutNotify(
                        material.GetTexture(property.Name));
                    field.RegisterValueChangedCallback(evt =>
                        EditMaterial(material, () => material.SetTexture(
                            property.Name,
                            evt.newValue as Texture)));
                    setting.Add(field);
                    break;
                }
                case ShaderPropertyType.Range:
                    setting.Add(BuildMaterialRange(material, property));
                    break;
                default:
                {
                    var field = UiTextFactory.CreateFloatField();
                    field.SetValueWithoutNotify(
                        material.GetFloat(property.Name));
                    field.RegisterValueChangedCallback(evt =>
                        EditMaterial(material, () => material.SetFloat(
                            property.Name,
                            evt.newValue)));
                    setting.Add(field);
                    break;
                }
            }
            return setting;
        }

        private VisualElement BuildMaterialRange(
            Material material,
            ShaderPropertyEntry property)
        {
            var limits = material.shader.GetPropertyRangeLimits(
                property.Index);
            var row = new VisualElement();
            row.AddToClassList(
                "ee4v-modification-workflow__slider-row");
            var slider = new Slider(limits.x, limits.y);
            var number = UiTextFactory.CreateFloatField();
            var value = material.GetFloat(property.Name);
            slider.SetValueWithoutNotify(value);
            number.SetValueWithoutNotify(value);
            slider.RegisterValueChangedCallback(evt =>
            {
                number.SetValueWithoutNotify(evt.newValue);
                EditMaterial(material, () => material.SetFloat(
                    property.Name,
                    evt.newValue));
            });
            number.RegisterValueChangedCallback(evt =>
            {
                var next = Mathf.Clamp(evt.newValue, limits.x, limits.y);
                number.SetValueWithoutNotify(next);
                slider.SetValueWithoutNotify(next);
                EditMaterial(material, () => material.SetFloat(
                    property.Name,
                    next));
            });
            row.Add(slider);
            row.Add(number);
            return row;
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

        private VisualElement CreateSubheading(
            string titleKey,
            string descriptionKey)
        {
            var heading = new VisualElement();
            heading.AddToClassList(
                "ee4v-modification-workflow__subheading");
            heading.Add(UiTextFactory.Create(
                I18N.Get(titleKey),
                UiClassNames.FormLabel));
            var description = UiTextFactory.Create(
                I18N.Get(descriptionKey),
                UiClassNames.SecondaryText);
            description.SetWhiteSpace(WhiteSpace.Normal);
            heading.Add(description);
            return heading;
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
            _selectedMaterial = null;
            _feedback = string.Empty;
            BuildWindow();
        }

        private void ClearDerivedAsset()
        {
            _workingAsset = null;
            _workingObject = null;
            _selectedMaterial = null;
            _feedback = string.Empty;
            BuildWindow();
        }

        private IReadOnlyList<Material> GetWorkingMaterials()
        {
            if (_workingObject == null)
            {
                return Array.Empty<Material>();
            }
            var path = AssetDatabase.GetAssetPath(_workingObject);
            if (string.IsNullOrWhiteSpace(path))
            {
                return Array.Empty<Material>();
            }
            return AssetDatabase.GetDependencies(path, true)
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                .OfType<Material>()
                .Distinct()
                .OrderByDescending(IsEditableWorkflowMaterial)
                .ThenBy(
                    material => material.name,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
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

        private IReadOnlyList<ShaderPropertyEntry> GetMaterialProperties(
            Material material,
            AppearanceGroup group)
        {
            var shader = material?.shader;
            if (shader == null)
            {
                return Array.Empty<ShaderPropertyEntry>();
            }
            var properties = new List<ShaderPropertyEntry>();
            for (var index = 0;
                 index < shader.GetPropertyCount();
                 index++)
            {
                if ((shader.GetPropertyFlags(index) &
                     ShaderPropertyFlags.HideInInspector) != 0)
                {
                    continue;
                }
                var type = shader.GetPropertyType(index);
                var matches = group == AppearanceGroup.Color
                    ? type == ShaderPropertyType.Color
                    : group == AppearanceGroup.Texture
                        ? type == ShaderPropertyType.Texture
                        : type == ShaderPropertyType.Float ||
                          type == ShaderPropertyType.Range;
                if (!matches)
                {
                    continue;
                }
                properties.Add(new ShaderPropertyEntry
                {
                    Index = index,
                    Name = shader.GetPropertyName(index),
                    DisplayName = shader.GetPropertyDescription(index),
                    Type = type
                });
            }
            return properties;
        }

        private void EditMaterial(Material material, Action change)
        {
            Undo.RecordObject(material, "Edit Derived Asset Appearance");
            change();
            EditorUtility.SetDirty(material);
            _scenePreview?.RefreshPreview();
        }

        private void SetPreviewText(
            string titleKey,
            string hintKey)
        {
            _previewTitle?.SetText(I18N.Get(titleKey));
            _previewHint?.SetText(I18N.Get(hintKey));
        }

        private void DisposeEditors()
        {
            _faceExpressionEditor?.Dispose();
            _faceExpressionEditor = null;
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
