using System;
using System.Linq;
using Ee4v.UI;
using Ee4v.AvatarEditing;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ee4v.AvatarParts
{
    public sealed partial class AvatarPartsEditor
    {
        public AvatarPartsEditor(AvatarEditingContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void SelectPreviewPart(string partKey)
        {
            var entries = ReadPrefabObjects();
            var entry = entries.FirstOrDefault(candidate =>
                PrefabScenePreview.GetPartKey(
                    candidate.PrefabSiblingIndex,
                    candidate.SiblingPath) == partKey);
            while (entry == null && !string.IsNullOrEmpty(partKey))
            {
                var separator = partKey.LastIndexOf('/');
                partKey = separator < 0
                    ? string.Empty
                    : partKey.Substring(0, separator);
                entry = entries.FirstOrDefault(candidate =>
                    PrefabScenePreview.GetPartKey(
                        candidate.PrefabSiblingIndex,
                        candidate.SiblingPath) == partKey);
            }
            if (entry == null)
            {
                return;
            }

            if (_shapePartsSection == ShapePartsSection.Shape)
            {
                EndBodyScaleDrag();
            }
            _shapePartsSection = ShapePartsSection.Parts;
            if (_context.SelectedBodyPart.HasValue &&
                !FilterPrefabObjectsByBodyPart(
                    entries, _context.SelectedBodyPart.Value).Contains(entry))
            {
                _context.SelectedBodyPart = null;
            }
            var selectedKey = PrefabScenePreview.GetPartKey(
                entry.PrefabSiblingIndex, entry.SiblingPath);
            if (entry.PrefabSiblingIndex >= 0)
            {
                _expandedPartPrefabGroups.Add(entry.PrefabSiblingIndex);
            }
            var separatorIndex = selectedKey.IndexOf('/');
            while (separatorIndex >= 0)
            {
                _expandedObjectGroups.Add(
                    selectedKey.Substring(0, separatorIndex));
                separatorIndex = selectedKey.IndexOf('/', separatorIndex + 1);
            }
            _context.SelectedPartKey = selectedKey;
            _context.Host.InvalidateControls(AvatarEditorPanel.Parts);
            _context.Host.ShowParts();
            ScrollToSelectedPart();
        }

        public void ClearPreviewSelection()
        {
            _context.SelectedPartKey = null;
            foreach (var row in _objectRows.Values)
            { row.Row.EnableInClassList("ee4v-modification-workflow__object-row--selected", false); }
            _context.Host.SyncPreviewSelection();
        }

        public VisualElement BuildControls()
        {
            var content = new VisualElement();
            UiComposition.Prepare(content,
                "Editor/Feature/Shared/AvatarEditing/avatar-editing.uss",
                "Editor/Feature/Avatar/AvatarParts/avatar-parts.uss");
            content.AddToClassList("ee4v-modification-workflow__controls-content");
            AvatarEditingUi.AddFeedback(_context, content);
            content.Add(_shapePartsSection == ShapePartsSection.Shape
                ? BuildBodyScaleControls() : BuildObjectControls());
            return content;
        }

        public void InvalidateParts() { _objectEntriesCache = null; _objectRows.Clear(); }

        public void InvalidateBlendShapes() { _bodyBlendShapesCache = null; }

        public void ClearData() { InvalidateParts(); InvalidateBlendShapes(); }

        public void ResetEditingState()
        {
            _bodyScaleDirty = false;
            _bodyScaleDragging = false;
            ClearPendingBodySizeChange();
            _bodyScaleBaseScales.Clear();
            _advancedBodyScaleExpanded = false;
            _expandedBodyScaleAxes.Clear();
            _expandedBodyBlendShapeGroups.Clear();
            _baseAvatarViewPosition = null;
            _avatarDescriptor = null;
            _expandedPartPrefabGroups.Clear();
            _expandedObjectGroups.Clear();
            ClearData();
        }

        public bool BodyScaleDirty { get => _bodyScaleDirty; set => _bodyScaleDirty = value; }

        public ShapePartsSection Section { get => _shapePartsSection; set => _shapePartsSection = value; }

        public bool HasPendingPartVisibility => _pendingPartVisibility.Count > 0;

        public bool HasPendingBodySizeChange => _pendingBodySizeChange != PendingBodySizeChange.None;

        public void ClearPendingPartVisibility() { _pendingPartVisibility.Clear(); _pendingPartAssetPath = null; }

        public void ClearRows() { _objectRows.Clear(); }

        public void InvalidateShapeTargets()
        {
            _bodyScaleBaseScales.Clear();
            _baseAvatarViewPosition = null;
            _avatarDescriptor = null;
        }

        public void ClearExpandedGroups() { _expandedPartPrefabGroups.Clear(); _expandedObjectGroups.Clear(); }

        public void CancelBodySizeChange() { _bodyScaleDragging = false; ClearPendingBodySizeChange(); }

        public void AddPrefabActiveSelfMenuItem(GenericMenu menu, int prefabSiblingIndex)
        {
            AddActiveSelfMenuItem(menu, CreatePrefabRootEntry(prefabSiblingIndex));
        }

        public void ShowPrefabContextMenu(ContextClickEvent evt, int prefabSiblingIndex)
        {
            ShowActiveSelfContextMenu(evt, CreatePrefabRootEntry(prefabSiblingIndex));
        }
    }
}
