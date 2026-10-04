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

        private void ScrollToMaterial(
            Material material,
            int prefabSiblingIndex)
        {
            var row = _context.ControlsHost.Query<NavigationItem>(
                    className: "ee4v-modification-workflow__material-item")
                .ToList()
                .FirstOrDefault(item =>
                    item.userData is KeyValuePair<Material, int> choice &&
                    choice.Key == material &&
                    choice.Value == prefabSiblingIndex);
            if (row != null)
            {
                _context.ControlsHost.schedule.Execute(() =>
                {
                    if (row.panel != null)
                    {
                        _context.ControlsHost.ScrollTo(row);
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
            _context.SelectedMaterial = material;
            if (prefabSiblingIndex >= 0) { _expandedMaterialPrefabGroups.Add(prefabSiblingIndex); }
            _context.InvalidateControls(AvatarEditorPanel.Material);
            _context.ShowMaterials();
            ScrollToMaterial(material, prefabSiblingIndex);
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
            var availableMaterials = new HashSet<Material>(
                filteredMaterials.Select(entry => entry.Material));
            var allAvailableMaterials = new HashSet<Material>(
                allMaterials.Select(entry => entry.Material));
            _hiddenMaterials.RemoveWhere(material => material == null ||
                !allAvailableMaterials.Contains(material));
            _context.Preview?.SetHiddenMaterials(_hiddenMaterials);

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
                    materialChoices.Add(_context.BuildPrefabGroup(
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

            if (_context.SelectedMaterial == null)
            {
                return panel;
            }

            if (!_context.CanEditMaterial(_context.SelectedMaterial))
            {
                if (_context.CanEditPrefab())
                {
                    var sourceMaterial = _context.SelectedMaterial;
                    var makeEditable = new UiButton(
                        I18N.Get("workflow.appearance.makeEditable"),
                        () => _context.CreateMaterialVariant(sourceMaterial));
                    makeEditable.AddToClassList(
                        "ee4v-modification-workflow__make-material-editable");
                    panel.Add(makeEditable);
                }
                return panel;
            }

            panel.Add(BuildMaterialEditor(_context.SelectedMaterial));
            return panel;
        }

        public void ClearData()
        {
            DisposeMaterialEditor();
            _avatarMaterialsCache = null;
            _materialBoneCategoriesCache = null;
            _materialGeometryCache.Clear();
            _materialsOutsideSelectedPrefab.Clear();
        }

        public void ResetEditingState()
        {
            _hiddenMaterials.Clear();
            _expandedMaterialPrefabGroups.Clear();
            ClearData();
        }

        public void Dispose() { DisposeMaterialEditor(); }

        public void ClearExpandedGroups() { _expandedMaterialPrefabGroups.Clear(); }

        public HashSet<Material> HiddenMaterials { get => _hiddenMaterials; }
    }
}
