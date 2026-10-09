using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.EditorIntegration;
using Ee4v.Core.I18n;
using Ee4v.UI;
using static Ee4v.AvatarEditing.AvatarBodyAnalysis;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AvatarMaterials
{
    public sealed partial class AvatarMaterialsEditor
    {
        private NavigationItem BuildMaterialChoice(AvatarMaterialEntry entry)
        {
            var material = entry.Material;
            var choice = new NavigationItem(
                new NavigationItemState(
                    material.name,
                    FormatMaterialUsageSummary(entry),
                    CreateMaterialIcon(material),
                    material == _context.SelectedMaterial),
                () =>
                {
                    _context.SelectedMaterial = material;
                    _context.Host.ShowMaterials();
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
            materialField.SetEnabled(_context.Edits.CanEditPrefab());
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
            if (!_context.Edits.CanEditMaterial(material))
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
            _context.Preview?.SetHiddenMaterials(_hiddenMaterials);
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

            _context.Preview?.SetHiddenMaterials(_hiddenMaterials);
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

        private IReadOnlyList<AvatarMaterialEntry> GetAvatarMaterials()
        {
            if (_context.Root == null)
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
            foreach (var renderer in _context.Root
                         .GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null ||
                    IsEditorOnlyPart(renderer.transform,
                        _context.Root.transform))
                {
                    continue;
                }
                var assigned = renderer.sharedMaterials;
                if (!_context.IsInSelectedPrefabScope(renderer.transform))
                {
                    _materialsOutsideSelectedPrefab.UnionWith(assigned);
                    continue;
                }
                var rendererPath = AnimationUtility.CalculateTransformPath(
                    renderer.transform,
                    _context.Root.transform);
                if (string.IsNullOrWhiteSpace(rendererPath))
                {
                    rendererPath = _context.Root.name;
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
            foreach (var index in _context.PrefabSiblingIndices)
            {
                if (PrefabHierarchyUtility.IsInScope(
                        target,
                        _context.Root.transform,
                        index,
                        _context.PrefabSiblingIndices))
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
                    GetHumanoidMaterialBoneCategories(_context.FindHumanoidAnimator());
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

        private bool TryGetSkinnedMaterialCategories(
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
                    GetMaterialBoneCategory(bone, humanoidCategories,
                        _materialResolvedBoneCategoriesCache))
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

        private static IconState CreateMaterialIcon(Material material)
        {
            var texture = AssetPreview.GetMiniThumbnail(material) ??
                          EditorGUIUtility.ObjectContent(
                              material,
                              typeof(Material)).image;
            return texture != null
                ? IconState.FromTexture(texture, UiSizeTokens.Size31)
                : FluentUiIcons.CreateState(
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
                _context.Feedback = I18N.Get("workflow.appearance.materialReplacementFailed");
                _context.FeedbackType = HelpBoxMessageType.Error;
                _context.Host.ShowMaterials();
            }
        }

        public void ReplaceMaterialAssignments(Material sourceMaterial, Material replacement)
        {
            if (!_context.Edits.FlushChanges())
            {
                throw new InvalidOperationException(
                    "The derived Prefab size could not be saved.");
            }
            if (!_context.Edits.CanEditPrefab() || sourceMaterial == null ||
                replacement == null || !EditorUtility.IsPersistent(replacement))
            {
                throw new InvalidOperationException(
                    "A derived Prefab and a Material asset are required.");
            }
            if (!_context.Edits.FlushChanges())
            {
                throw new InvalidOperationException(
                    "Pending part visibility could not be saved.");
            }
            var root = _context.Root;
            // Apply overrides only when the Variant revision is saved.
            {
                var replaced = false;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null || !_context.IsInSelectedPrefabScope(
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
                _context.Edits.WorkingSceneDirty = true;
                _context.Edits.Changed();
            }
        }

        public void RefreshAfterMaterialReplacement(Material sourceMaterial, Material replacement)
        {
            if (_hiddenMaterials.Contains(sourceMaterial))
            {
                _hiddenMaterials.Add(replacement);
            }
            _context.SelectedMaterial = replacement;
            _context.Feedback = string.Empty;
            _context.Host.ClearCaches();
            _context.Edits.Changed();
            _context.Preview?.SetHiddenMaterials(_hiddenMaterials);
            _context.Preview?.ReloadPrefabPreservingView(_context.Root);
            _context.Host.ShowMaterials();
        }

        public bool IsMaterialSharedOutsideSelectedPrefab(Material material)
        {
            if (!_context.SelectedPrefabSiblingIndex.HasValue ||
                _context.Root == null)
            {
                return false;
            }
            GetAvatarMaterials();
            return _materialsOutsideSelectedPrefab.Contains(material);
        }

        public void RefreshMaterialPreview()
        {
            _context.Edits.MaterialChanged?.Invoke(_context.SelectedMaterial);
            _context.Edits.Changed();
            _context.Preview?.RefreshPreview();
        }

        public void DisposeMaterialEditor()
        {
            _materialInspector?.Dispose();
            _materialInspector = null;
        }
    }
}
