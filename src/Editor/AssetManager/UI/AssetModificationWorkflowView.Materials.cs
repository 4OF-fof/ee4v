using Ee4v.Core.EditorIntegration;
using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetProtection;
using Ee4v.AssetManager.Contracts;
using Ee4v.AssetManager.Simulation;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using Ee4v.FaceExpression;
using Ee4v.UI;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed partial class AssetModificationWorkflowView
    {
        private VisualElement BuildAppearanceControls()
        {
            var panel = new VisualElement();
            panel.AddToClassList(
                "ee4v-modification-workflow__controls-content");
            AddFeedback(panel);
            if (_currentCategory == WorkflowCategory.ShapeParts)
            {
                panel.Add(_shapePartsSection == ShapePartsSection.Shape
                    ? BuildBodyScaleControls()
                    : BuildObjectControls());
                return panel;
            }

            var allMaterials = GetAvatarMaterials();
            var filteredMaterials = new List<AvatarMaterialEntry>();
            foreach (var entry in allMaterials)
            {
                var usages = entry.Usages.Where(usage =>
                    !_selectedBodyPart.HasValue ||
                    GetMaterialUsageCategories(usage, entry.Material)
                        .Any(MatchesSelectedBodyPart)).ToArray();
                if (usages.Length == 0)
                {
                    continue;
                }
                var filtered = new AvatarMaterialEntry
                {
                    Material = entry.Material
                };
                filtered.Usages.AddRange(usages);
                filteredMaterials.Add(filtered);
            }
            _materialVisibilityButtons.Clear();
            _allMaterialsVisibilityButton = null;
            if (filteredMaterials.Count == 0)
            {
                _selectedMaterial = null;
                panel.Add(CreateEmptyState(
                    "workflow.appearance.emptyTitle",
                    _selectedBodyPart.HasValue
                        ? "workflow.appearance.emptyPartDescription"
                        : "workflow.appearance.emptyDescription"));
                return panel;
            }

            if (_selectedMaterial != null &&
                !filteredMaterials.Any(entry =>
                    entry.Material == _selectedMaterial))
            {
                _selectedMaterial = null;
            }
            var availableMaterials = new HashSet<Material>(
                filteredMaterials.Select(entry => entry.Material));
            var allAvailableMaterials = new HashSet<Material>(
                allMaterials.Select(entry => entry.Material));
            _hiddenMaterials.RemoveWhere(material => material == null ||
                !allAvailableMaterials.Contains(material));
            _scenePreview?.SetHiddenMaterials(_hiddenMaterials);

            var materialChoices = new VisualElement();
            materialChoices.AddToClassList(
                "ee4v-modification-workflow__material-list");
            if (availableMaterials.Count > 0)
            {
                materialChoices.Add(BuildAllMaterialsVisibilityRow(
                    availableMaterials));
            }
            foreach (var prefabSiblingIndex in GetDisplayedPrefabScopes())
            {
                var group = filteredMaterials.Select(entry =>
                {
                    var scoped = new AvatarMaterialEntry
                    {
                        Material = entry.Material
                    };
                    scoped.Usages.AddRange(entry.Usages.Where(usage =>
                        usage.PrefabSiblingIndex == prefabSiblingIndex));
                    return scoped;
                }).Where(entry => entry.Usages.Count > 0).ToArray();
                if (group.Length == 0)
                {
                    continue;
                }
                if (!_selectedPrefabSiblingIndex.HasValue &&
                    prefabSiblingIndex >= 0)
                {
                    var content = new VisualElement();
                    foreach (var entry in group)
                    {
                        content.Add(BuildMaterialChoice(entry));
                    }
                    materialChoices.Add(BuildCollapsiblePrefabGroup(
                        prefabSiblingIndex,
                        content,
                        _expandedMaterialPrefabGroups,
                        false));
                    continue;
                }
                foreach (var entry in group)
                {
                    materialChoices.Add(BuildMaterialChoice(entry));
                }
            }
            MarkLastListItem(materialChoices);
            RefreshMaterialVisibilityButtons(availableMaterials);
            panel.Add(materialChoices);

            if (_selectedMaterial == null)
            {
                return panel;
            }

            if (!IsEditableWorkflowMaterial(_selectedMaterial))
            {
                if (IsEditableWorkflowPrefab())
                {
                    var sourceMaterial = _selectedMaterial;
                    var makeEditable = new UiButton(
                        I18N.Get("workflow.appearance.makeEditable"),
                        () => CreateEditableMaterialVariant(sourceMaterial));
                    makeEditable.AddToClassList(
                        "ee4v-modification-workflow__make-material-editable");
                    panel.Add(makeEditable);
                }
                return panel;
            }

            panel.Add(BuildMaterialEditor(_selectedMaterial));
            return panel;
        }

        private NavigationItem BuildMaterialChoice(AvatarMaterialEntry entry)
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
                    ShowCategory(WorkflowCategory.Material, false);
                });
            choice.AddToClassList(
                "ee4v-modification-workflow__material-item");
            choice.userData = new KeyValuePair<Material, int>(
                material, entry.Usages[0].PrefabSiblingIndex);
            choice.tooltip = FormatMaterialUsageTooltip(entry);
            var materialField = UiTextFactory.CreateObjectField(
                string.Empty,
                "ee4v-modification-workflow__material-slot");
            materialField.objectType = typeof(Material);
            materialField.allowSceneObjects = false;
            materialField.SetValueWithoutNotify(material);
            materialField.SetEnabled(IsEditableWorkflowPrefab());
            materialField.RegisterCallback<PointerDownEvent>(
                evt => evt.StopPropagation());
            materialField.RegisterCallback<ClickEvent>(
                evt => evt.StopPropagation());
            materialField.RegisterValueChangedCallback(evt =>
            {
                var replacement = evt.newValue as Material;
                if (replacement == null || replacement == material ||
                    !EditorUtility.IsPersistent(replacement))
                {
                    materialField.SetValueWithoutNotify(material);
                    return;
                }
                ReplaceWorkflowMaterial(material, replacement);
            });
            var titleContainer = choice.Row.TitleText.parent;
            titleContainer.Insert(0, materialField);
            choice.Row.TitleText.RemoveFromHierarchy();
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
            if (!_materialVisibilityButtons.TryGetValue(
                    material, out var buttons))
            {
                buttons = new List<UiButton>();
                _materialVisibilityButtons.Add(material, buttons);
            }
            buttons.Add(visibility);
            return choice;
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
            if (materials.All(material =>
                    _hiddenMaterials.Contains(material)))
            {
                _hiddenMaterials.ExceptWith(materials);
            }
            else
            {
                _hiddenMaterials.UnionWith(materials);
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
                var isVisible = !_hiddenMaterials.Contains(material);
                var tooltipKey = isVisible
                    ? "workflow.appearance.hideMaterial"
                    : "workflow.appearance.showMaterial";
                var tooltip = I18N.Get(tooltipKey);
                foreach (var button in pair.Value)
                {
                    button.tooltip = tooltip;
                    button.SetIcon(FluentUiIcons.CreateState(
                        isVisible
                            ? "eye.png"
                            : "eye_off.png",
                        UiSizeTokens.Size18,
                        tooltip));
                }
            }

            var allVisible = materials.Count > 0 &&
                             !_hiddenMaterials.Overlaps(materials);
            var allTooltip = I18N.Get(allVisible
                ? "workflow.appearance.hideAll"
                : "workflow.appearance.showAll");
            _allMaterialsVisibilityButton?.SetIcon(
                FluentUiIcons.CreateState(
                    allVisible
                        ? "eye.png"
                        : "eye_off.png",
                    UiSizeTokens.Size18,
                    allTooltip));
            if (_allMaterialsVisibilityButton != null)
            {
                _allMaterialsVisibilityButton.tooltip = allTooltip;
            }
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

        private IReadOnlyList<AvatarMaterialEntry> GetAvatarMaterials()
        {
            if (_workingObject == null)
            {
                return Array.Empty<AvatarMaterialEntry>();
            }

            if (_avatarMaterialsCache != null)
            {
                return _avatarMaterialsCache;
            }

            var entries = new List<AvatarMaterialEntry>();
            var byMaterial = new Dictionary<Material, AvatarMaterialEntry>();
            _materialsOutsideSelectedPrefab.Clear();
            foreach (var renderer in _workingObject
                         .GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null ||
                    IsEditorOnlyPart(renderer.transform,
                        _workingObject.transform))
                {
                    continue;
                }
                var assigned = renderer.sharedMaterials;
                if (!IsInSelectedPrefabScope(renderer.transform))
                {
                    _materialsOutsideSelectedPrefab.UnionWith(assigned);
                    continue;
                }
                var rendererPath = AnimationUtility.CalculateTransformPath(
                    renderer.transform,
                    _workingObject.transform);
                if (string.IsNullOrWhiteSpace(rendererPath))
                {
                    rendererPath = _workingObject.name;
                }

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
                        Renderer = renderer,
                        RendererPath = rendererPath,
                        PrefabSiblingIndex = GetPrefabSiblingIndex(
                            renderer.transform),
                        SlotIndex = index
                    });
                }
            }

            _avatarMaterialsCache = entries;
            return _avatarMaterialsCache;
        }

        private int GetPrefabSiblingIndex(Transform target)
        {
            foreach (var index in _prefabSiblingIndices)
            {
                if (PrefabHierarchyUtility.IsInScope(
                        target,
                        _workingObject.transform,
                        index,
                        _prefabSiblingIndices))
                {
                    return index;
                }
            }
            return -1;
        }

        private static bool IsEditorOnlyPart(Transform target, Transform root)
        {
            for (var current = target;
                 current != null;
                 current = current.parent)
            {
                if (string.Equals(current.gameObject.tag, "EditorOnly",
                        StringComparison.Ordinal))
                {
                    return true;
                }
                if (current == root)
                {
                    break;
                }
            }
            return false;
        }

        private IReadOnlyCollection<BodyPartCategory> GetMaterialUsageCategories(
            MaterialUsage usage, Material material)
        {
            if (usage.Categories == null)
            {
                _materialBoneCategoriesCache = _materialBoneCategoriesCache ??
                    GetHumanoidMaterialBoneCategories(FindHumanoidAnimator());
                usage.Categories = ClassifyMaterialUsage(
                    usage.Renderer, material, usage.RendererPath,
                    usage.SlotIndex, _materialBoneCategoriesCache);
            }
            return usage.Categories;
        }

        private IReadOnlyCollection<BodyPartCategory>
            ClassifyMaterialUsage(
            Renderer renderer,
            Material material,
            string rendererPath,
            int slotIndex,
            IReadOnlyDictionary<Transform, BodyPartCategory> boneCategories)
        {
            if (renderer is SkinnedMeshRenderer skinned &&
                TryGetCachedSkinnedMaterialCategories(
                    skinned,
                    slotIndex,
                    boneCategories,
                    out var skinnedCategories))
            {
                return skinnedCategories;
            }

            var materialPart = ClassifyBodyPart(
                material?.name,
                string.Empty);
            if (materialPart != BodyPartCategory.Other)
            {
                return new[] { materialPart };
            }

            var rendererPart = ClassifyBodyPart(
                renderer?.name,
                renderer is SkinnedMeshRenderer meshRenderer
                    ? meshRenderer.sharedMesh?.name
                    : string.Empty);
            var category = rendererPart != BodyPartCategory.Other
                ? rendererPart
                : ClassifyBodyPart(rendererPath, string.Empty);
            return new[] { category };
        }

        private bool TryGetCachedSkinnedMaterialCategories(
            SkinnedMeshRenderer renderer,
            int slotIndex,
            IReadOnlyDictionary<Transform, BodyPartCategory> boneCategories,
            out IReadOnlyCollection<BodyPartCategory> categories)
        {
            categories = null;
            var mesh = renderer.sharedMesh;
            if (mesh == null || slotIndex < 0 ||
                slotIndex >= mesh.subMeshCount)
            {
                return false;
            }

            if (!_materialGeometryCache.TryGetValue(
                    renderer, out var cached) ||
                cached.Mesh != mesh ||
                cached.Slots.Length != mesh.subMeshCount)
            {
                cached = new MaterialGeometryCacheEntry
                {
                    Mesh = mesh,
                    Slots = new IReadOnlyCollection<BodyPartCategory>[
                        mesh.subMeshCount]
                };
                _materialGeometryCache[renderer] = cached;
            }

            categories = cached.Slots[slotIndex];
            if (categories != null)
            {
                return categories.Count > 0;
            }

            if (TryGetSkinnedMaterialCategories(
                    renderer,
                    slotIndex,
                    boneCategories,
                    out categories))
            {
                cached.Slots[slotIndex] = categories;
                return true;
            }

            cached.Slots[slotIndex] = Array.Empty<BodyPartCategory>();
            return false;
        }

        private static bool TryGetSkinnedMaterialCategories(
            SkinnedMeshRenderer renderer,
            int slotIndex,
            IReadOnlyDictionary<Transform, BodyPartCategory> humanoidCategories,
            out IReadOnlyCollection<BodyPartCategory> categories)
        {
            categories = null;
            var mesh = renderer.sharedMesh;
            var bones = renderer.bones;
            if (mesh == null || bones == null || bones.Length == 0 ||
                slotIndex < 0 || slotIndex >= mesh.subMeshCount)
            {
                return false;
            }

            BoneWeight[] weights;
            int[] indices;
            try
            {
                weights = mesh.boneWeights;
                indices = mesh.GetIndices(slotIndex);
            }
            catch (UnityException)
            {
                return false;
            }
            if (weights == null || weights.Length != mesh.vertexCount ||
                indices == null || indices.Length == 0)
            {
                return false;
            }

            var boneParts = bones.Select(bone =>
                    GetMaterialBoneCategory(bone, humanoidCategories))
                .ToArray();
            var usedVertices = new bool[weights.Length];
            var counts = new int[Enum.GetValues(typeof(BodyPartCategory)).Length];
            var classifiedCount = 0;
            foreach (var vertexIndex in indices)
            {
                if (vertexIndex < 0 || vertexIndex >= weights.Length ||
                    usedVertices[vertexIndex])
                {
                    continue;
                }

                usedVertices[vertexIndex] = true;
                var weight = weights[vertexIndex];
                var part = BodyPartCategory.Other;
                var strongestWeight = 0f;
                ConsiderBoneWeight(weight.boneIndex0, weight.weight0);
                ConsiderBoneWeight(weight.boneIndex1, weight.weight1);
                ConsiderBoneWeight(weight.boneIndex2, weight.weight2);
                ConsiderBoneWeight(weight.boneIndex3, weight.weight3);
                if (part == BodyPartCategory.Other)
                {
                    continue;
                }

                counts[(int)part]++;
                classifiedCount++;

                void ConsiderBoneWeight(int boneIndex, float value)
                {
                    if (value <= strongestWeight ||
                        boneIndex < 0 || boneIndex >= boneParts.Length ||
                        boneParts[boneIndex] == BodyPartCategory.Other)
                    {
                        return;
                    }

                    strongestWeight = value;
                    part = boneParts[boneIndex];
                }
            }

            if (classifiedCount == 0)
            {
                return false;
            }

            var minimumVertices = Mathf.Max(
                3,
                Mathf.CeilToInt(classifiedCount * 0.001f));
            var result = new List<BodyPartCategory>();
            foreach (BodyPartCategory part in Enum.GetValues(
                         typeof(BodyPartCategory)))
            {
                if (part != BodyPartCategory.Other &&
                    counts[(int)part] >= minimumVertices)
                {
                    result.Add(part);
                }
            }

            if (result.Count == 0)
            {
                return false;
            }

            categories = result;
            return true;
        }

        private static BodyPartCategory GetMaterialBoneCategory(
            Transform bone,
            IReadOnlyDictionary<Transform, BodyPartCategory> humanoidCategories)
        {
            for (var current = bone; current != null;
                 current = current.parent)
            {
                if (humanoidCategories.TryGetValue(
                        current, out var category))
                {
                    return category;
                }

                category = ClassifyBodyPart(current.name, string.Empty);
                if (category != BodyPartCategory.Other)
                {
                    return category;
                }
            }

            return BodyPartCategory.Other;
        }

        private static IReadOnlyDictionary<Transform, BodyPartCategory>
            GetHumanoidMaterialBoneCategories(Animator animator)
        {
            var categories = new Dictionary<Transform, BodyPartCategory>();
            if (animator == null)
            {
                return categories;
            }

            Add(HumanBodyBones.Head, BodyPartCategory.Head);
            Add(HumanBodyBones.Neck, BodyPartCategory.Head);
            Add(HumanBodyBones.UpperChest, BodyPartCategory.Chest);
            Add(HumanBodyBones.Chest, BodyPartCategory.Chest);
            Add(HumanBodyBones.Spine, BodyPartCategory.Waist);
            Add(HumanBodyBones.Hips, BodyPartCategory.Waist);
            Add(HumanBodyBones.LeftShoulder, BodyPartCategory.Shoulders);
            Add(HumanBodyBones.RightShoulder, BodyPartCategory.Shoulders);
            Add(HumanBodyBones.LeftUpperArm, BodyPartCategory.Arms);
            Add(HumanBodyBones.RightUpperArm, BodyPartCategory.Arms);
            Add(HumanBodyBones.LeftLowerArm, BodyPartCategory.Arms);
            Add(HumanBodyBones.RightLowerArm, BodyPartCategory.Arms);
            Add(HumanBodyBones.LeftHand, BodyPartCategory.Hands);
            Add(HumanBodyBones.RightHand, BodyPartCategory.Hands);
            Add(HumanBodyBones.LeftUpperLeg, BodyPartCategory.Legs);
            Add(HumanBodyBones.RightUpperLeg, BodyPartCategory.Legs);
            Add(HumanBodyBones.LeftLowerLeg, BodyPartCategory.Legs);
            Add(HumanBodyBones.RightLowerLeg, BodyPartCategory.Legs);
            Add(HumanBodyBones.LeftFoot, BodyPartCategory.Feet);
            Add(HumanBodyBones.RightFoot, BodyPartCategory.Feet);
            Add(HumanBodyBones.LeftToes, BodyPartCategory.Feet);
            Add(HumanBodyBones.RightToes, BodyPartCategory.Feet);
            return categories;

            void Add(HumanBodyBones bone, BodyPartCategory category)
            {
                var transform = animator.GetBoneTransform(bone);
                if (transform != null)
                {
                    categories[transform] = category;
                }
            }
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

        private void ReplaceWorkflowMaterial(Material sourceMaterial, Material replacement)
        {
            if (sourceMaterial == null || replacement == null ||
                sourceMaterial == replacement)
            {
                return;
            }
            try
            {
                ReplaceMaterialAssignments(sourceMaterial, replacement);
                RefreshAfterMaterialReplacement(sourceMaterial, replacement);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _feedback = I18N.Get("workflow.appearance.materialReplacementFailed");
                _feedbackType = HelpBoxMessageType.Error;
                ShowCategory(WorkflowCategory.Material, false);
            }
        }

        private void ReplaceMaterialAssignments(Material sourceMaterial, Material replacement)
        {
            EndBodyScaleDrag();
            SaveBodyScalePrefab();
            if (_bodyScaleDirty)
            {
                throw new InvalidOperationException(
                    "The derived Prefab size could not be saved.");
            }
            if (!IsEditableWorkflowPrefab() || sourceMaterial == null ||
                replacement == null || !EditorUtility.IsPersistent(replacement))
            {
                throw new InvalidOperationException(
                    "A derived Prefab and a Material asset are required.");
            }
            if (!FlushPendingPartVisibility())
            {
                throw new InvalidOperationException(
                    "Pending part visibility could not be saved.");
            }
            var root = _workingObject;
            // Apply overrides only when the Variant revision is saved.
            {
                var replaced = false;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null || !IsInSelectedPrefabScope(
                            renderer.transform, root.transform))
                    {
                        continue;
                    }
                    var materials = renderer.sharedMaterials;
                    var changed = false;
                    for (var index = 0; index < materials.Length; index++)
                    {
                        if (materials[index] != sourceMaterial)
                        {
                            continue;
                        }
                        materials[index] = replacement;
                        changed = true;
                    }
                    if (!changed)
                    {
                        continue;
                    }
                    Undo.RecordObject(renderer,
                        "Replace Variant Material");
                    renderer.sharedMaterials = materials;
                    if (PrefabUtility.IsPartOfPrefabInstance(renderer))
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    }
                    EditorUtility.SetDirty(renderer);
                    replaced = true;
                }
                if (!replaced)
                {
                    throw new InvalidOperationException(
                        "The selected Material is no longer assigned.");
                }
                _workingSceneDirty = true;
                InvalidateVariantSaveStatus();
            }
        }

        private void RefreshAfterMaterialReplacement(Material sourceMaterial, Material replacement)
        {
            if (_hiddenMaterials.Contains(sourceMaterial))
            {
                _hiddenMaterials.Add(replacement);
            }
            _selectedMaterial = replacement;
            _feedback = string.Empty;
            _avatarDescriptor = null;
            ClearAppearanceCaches();
            InvalidateVariantSaveStatus();
            _scenePreview?.SetHiddenMaterials(_hiddenMaterials);
            _scenePreview?.ReloadPrefabPreservingView(_workingObject);
            ShowCategory(WorkflowCategory.Material, false);
        }

        private void CreateEditableMaterialVariant(Material sourceMaterial)
        {
            if (!IsEditableWorkflowPrefab() || sourceMaterial == null)
            {
                return;
            }
            Material variant = null;
            string materialPath = null;
            var assigned = false;
            try
            {
                var prefabPath = GetWorkingAssetPath();
                var variantFolder = System.IO.Path
                    .GetDirectoryName(prefabPath)?.Replace('\\', '/');
                if (string.IsNullOrEmpty(variantFolder))
                {
                    throw new InvalidOperationException(
                        "The derived asset folder could not be found.");
                }
                var assetsFolder = variantFolder + "/Assets";
                if (!AssetDatabase.IsValidFolder(assetsFolder))
                {
                    AssetDatabase.CreateFolder(variantFolder, "Assets");
                }
                var materialsFolder = assetsFolder + "/Materials";
                if (!AssetDatabase.IsValidFolder(materialsFolder))
                {
                    AssetDatabase.CreateFolder(assetsFolder, "Materials");
                }
                var safeName = new string(sourceMaterial.name
                    .Select(character =>
                        char.IsLetterOrDigit(character) ||
                        character == ' ' ||
                        character == '_' ||
                        character == '-'
                            ? character
                            : '_')
                    .Take(80)
                    .ToArray());
                if (string.IsNullOrWhiteSpace(safeName))
                {
                    safeName = "Material";
                }
                materialPath = AssetDatabase.GenerateUniqueAssetPath(
                    materialsFolder + "/" + safeName + ".mat");
                variant = new Material(sourceMaterial)
                {
                    name = sourceMaterial.name,
                    parent = sourceMaterial,
                    hideFlags = HideFlags.None
                };
                AssetDatabase.CreateAsset(variant, materialPath);
                AssetDatabase.SaveAssets();
                ReplaceMaterialAssignments(sourceMaterial, variant);
                assigned = true;
                RefreshAfterMaterialReplacement(sourceMaterial, variant);
            }
            catch (Exception exception)
            {
                if (!assigned && variant != null)
                {
                    if (AssetDatabase.Contains(variant))
                    {
                        AssetDatabase.DeleteAsset(materialPath);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(variant);
                    }
                }
                Debug.LogException(exception);
                _feedback = I18N.Get(
                    "workflow.appearance.materialVariantFailed");
                _feedbackType = HelpBoxMessageType.Error;
                ShowCategory(WorkflowCategory.Material, false);
            }
        }

        private bool IsEditableWorkflowMaterial(Material material)
        {
            if (material == null || _workingObject == null)
            {
                return false;
            }
            var path = AssetDatabase.GetAssetPath(material);
            var prefabPath = GetWorkingAssetPath();
            var variantFolder = System.IO.Path
                .GetDirectoryName(prefabPath)?.Replace('\\', '/');
            return !string.IsNullOrEmpty(path) &&
                   !string.IsNullOrEmpty(variantFolder) &&
                   path.StartsWith(variantFolder + "/",
                       StringComparison.OrdinalIgnoreCase) &&
                   (material.hideFlags & HideFlags.NotEditable) == 0 &&
                   !AssetProtectionModule.IsProtected(path) &&
                   AssetDatabase.IsOpenForEdit(material, StatusQueryOptions.UseCachedIfPossible) &&
                   !IsMaterialSharedOutsideSelectedPrefab(material);
        }

        private bool IsMaterialSharedOutsideSelectedPrefab(Material material)
        {
            if (!_selectedPrefabSiblingIndex.HasValue ||
                _workingObject == null)
            {
                return false;
            }
            GetAvatarMaterials();
            return _materialsOutsideSelectedPrefab.Contains(material);
        }

        private void RefreshMaterialPreview()
        {
            InvalidateVariantSaveStatus();
            _scenePreview?.RefreshPreview();
        }

        private void RefreshAfterUndoRedo()
        {
            InvalidateVariantSaveStatus();
            ClearAppearanceCaches();
            if (_currentCategory != WorkflowCategory.ShapeParts ||
                _shapePartsSection != ShapePartsSection.Shape)
            {
                RefreshMaterialPreview();
                ShowCategory(_currentCategory, false);
                return;
            }

            if (IsEditableWorkflowPrefab()) { _workingSceneDirty = true; }

            _scenePreview?.ReloadPrefab();
            if (_controlsHost == null)
            {
                return;
            }
            ShowCategory(_currentCategory, false);
        }

        private void DisposeMaterialEditor()
        {
            _materialInspector?.Dispose();
            _materialInspector = null;
        }
    }
}
