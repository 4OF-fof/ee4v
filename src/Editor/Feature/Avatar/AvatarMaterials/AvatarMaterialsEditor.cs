using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.UI;
using Ee4v.AvatarEditing;
using static Ee4v.AvatarEditing.AvatarEditingUi;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AvatarMaterials
{
    public sealed partial class AvatarMaterialsEditor : IDisposable
    {
        public AvatarMaterialsEditor(AvatarEditingContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        private void NavigateToMaterial(Material material)
        {
            var controls = _context.ControlsHost;
            if (_context.SelectedMaterial == null && material != null)
            {
                _materialListScrollOffset = controls?.scrollOffset ?? Vector2.zero;
            }
            if (material == null) { _showOnlySelectedMaterial = false; }
            _context.Preview?.SetListSelection(null, null);
            _context.SelectedMaterial = material;
            _context.Host.InvalidateControls(AvatarEditorPanel.Material);
            _context.Host.ShowMaterials();
            if (controls != null)
            {
                var offset = material != null ? Vector2.zero : _materialListScrollOffset;
                controls.scrollOffset = offset;
                controls.schedule.Execute(() =>
                {
                    if (_context.ControlsHost == controls &&
                        _context.SelectedMaterial == material)
                    {
                        controls.scrollOffset = offset;
                    }
                });
            }
        }

        public void SelectPreviewMaterial(Material material, int prefabSiblingIndex)
        {
            if (material == null) { return; }
            if (_context.SelectedBodyPart.HasValue && !GetAvatarMaterials().Any(entry =>
                entry.Material == material && entry.Usages.Any(usage =>
                    usage.PrefabSiblingIndex == prefabSiblingIndex &&
                    GetMaterialUsageCategories(usage, material).Any(_context.MatchesSelectedBodyPart))))
            { _context.SelectedBodyPart = null; }
            if (prefabSiblingIndex >= 0) { _expandedMaterialPrefabGroups.Add(prefabSiblingIndex); }
            NavigateToMaterial(material);
        }

        public VisualElement BuildControls()
        {
            DisposeMaterialEditor();
            var panel = new VisualElement();
            UiComposition.Prepare(panel,
                "Editor/Feature/Shared/AvatarEditing/avatar-editing.uss",
                "Editor/Feature/Avatar/AvatarMaterials/avatar-materials.uss");
            panel.AddToClassList(
                "ee4v-modification-workflow__controls-content");
            AvatarEditingUi.AddFeedback(_context, panel);
            var allMaterials = GetAvatarMaterials();
            var filteredMaterials = new List<AvatarMaterialEntry>();
            foreach (var entry in allMaterials)
            {
                var usages = entry.Usages.Where(usage =>
                    !_context.SelectedBodyPart.HasValue ||
                    GetMaterialUsageCategories(usage, entry.Material)
                        .Any(_context.MatchesSelectedBodyPart)).ToArray();
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
                _context.SelectedMaterial = null;
                _showOnlySelectedMaterial = false;
                RefreshMaterialVisibility();
                panel.Add(AvatarEditingUi.CreateEmptyState(
                    I18N.Get("workflow.appearance.emptyTitle"),
                    I18N.Get(_context.SelectedBodyPart.HasValue
                        ? "workflow.appearance.emptyPartDescription"
                        : "workflow.appearance.emptyDescription")));
                return panel;
            }

            if (_context.SelectedMaterial != null &&
                !filteredMaterials.Any(entry =>
                    entry.Material == _context.SelectedMaterial))
            {
                _context.SelectedMaterial = null;
            }
            if (_context.SelectedMaterial == null) { _showOnlySelectedMaterial = false; }
            var availableMaterials = new HashSet<Material>(
                filteredMaterials.Select(entry => entry.Material));
            var allAvailableMaterials = new HashSet<Material>(
                allMaterials.Select(entry => entry.Material));
            _hiddenMaterials.RemoveWhere(material => material == null ||
                !allAvailableMaterials.Contains(material));
            RefreshMaterialVisibility();

            if (_context.SelectedMaterial != null)
            {
                var back = new UiButton(
                    I18N.Get("workflow.appearance.backToMaterials"),
                    () => NavigateToMaterial(null),
                    icon: FluentUiIcons.CreateState("arrow_left.png", UiSizeTokens.Size18),
                    variant: UiButtonVariant.Ghost);
                back.AddToClassList("ee4v-modification-workflow__material-back");
                panel.Add(back);
                panel.Add(BuildMaterialEditor(filteredMaterials.First(entry =>
                    entry.Material == _context.SelectedMaterial)));
                return panel;
            }

            var materialChoices = new VisualElement();
            materialChoices.AddToClassList(
                "ee4v-modification-workflow__material-list");
            if (availableMaterials.Count > 0)
            {
                materialChoices.Add(BuildAllMaterialsVisibilityRow(
                    availableMaterials));
            }
            foreach (var prefabSiblingIndex in _context.GetDisplayedPrefabScopes())
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
                if (!_context.SelectedPrefabSiblingIndex.HasValue &&
                    prefabSiblingIndex >= 0)
                {
                    var content = new VisualElement();
                    foreach (var entry in group)
                    {
                        content.Add(BuildMaterialChoice(entry));
                    }
                    materialChoices.Add(_context.Host.BuildPrefabGroup(
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

            return panel;
        }

        public void ClearData()
        {
            DisposeMaterialEditor();
            _avatarMaterialsCache = null;
            _materialBoneCategoriesCache = null;
            _materialResolvedBoneCategoriesCache.Clear();
            _materialGeometryCache.Clear();
            _materialsOutsideSelectedPrefab.Clear();
        }

        public void ResetEditingState()
        {
            _hiddenMaterials.Clear();
            _showOnlySelectedMaterial = false;
            _expandedMaterialPrefabGroups.Clear();
            _materialListScrollOffset = Vector2.zero;
            ClearData();
        }

        public void Dispose() { DisposeMaterialEditor(); }

        public void ClearExpandedGroups() { _expandedMaterialPrefabGroups.Clear(); }

        public HashSet<Material> HiddenMaterials => _hiddenMaterials;

        public IEnumerable<Material> PreviewHiddenMaterials
        {
            get
            {
                if (!_showOnlySelectedMaterial || _context.SelectedMaterial == null ||
                    _context.Root == null)
                {
                    return _hiddenMaterials;
                }
                var hidden = new HashSet<Material>(_hiddenMaterials);
                foreach (var renderer in _context.Root.GetComponentsInChildren<Renderer>(true))
                {
                    hidden.UnionWith(renderer.sharedMaterials.Where(material =>
                        material != null && material != _context.SelectedMaterial));
                }
                hidden.Remove(_context.SelectedMaterial);
                return hidden;
            }
        }
    }
}
