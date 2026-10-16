using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AvatarEditing;
using Ee4v.Core.EditorIntegration;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AvatarParts
{
    public sealed partial class AvatarPartsEditor
    {
        private void NavigateToPart(string key)
        {
            if (key != null && !_context.Edits.FlushChanges()) { return; }
            if (key == null && _context.SelectedPartKey == null)
            {
                SelectPartRow(null, false);
                return;
            }
            var controls = _context.ControlsHost;
            if (_context.SelectedPartKey == null && key != null)
            {
                _partsListScrollOffset = controls?.scrollOffset ?? Vector2.zero;
            }
            if (key == null) { _context.Host.ClearCaches(); }
            _context.SelectedPartKey = key;
            _context.Host.InvalidateControls(AvatarEditorPanel.Parts);
            _context.Host.ShowParts();
            _context.Host.SyncPreviewSelection();
            if (controls == null) { return; }
            var offset = key == null ? _partsListScrollOffset : Vector2.zero;
            controls.scrollOffset = offset;
            controls.schedule.Execute(() =>
            {
                if (_context.ControlsHost == controls && _context.SelectedPartKey == key)
                {
                    controls.scrollOffset = offset;
                }
            });
        }

        private VisualElement BuildPartDetails(
            PrefabObjectEntry entry,
            IReadOnlyList<PrefabObjectEntry> displayed)
        {
            var panel = new VisualElement();
            panel.AddToClassList("ee4v-part-details");
            var header = BuildObjectRow(entry, isDetailsHeader: true);
            header.AddToClassList("ee4v-part-details__header");
            var back = new UiButton(string.Empty,
                () => NavigateToPart(null),
                tooltip: I18N.Get("workflow.objects.backToParts"),
                icon: FluentUiIcons.CreateState("arrow_left.png", UiSizeTokens.Size18),
                variant: UiButtonVariant.Ghost);
            back.AddToClassList("ee4v-part-details__back");
            header.Insert(0, back);
            panel.Add(header);
            GameObject target;
            try { target = ResolvePartObject(_context.Root, entry); }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                panel.Add(UiTextFactory.CreateMessagePanel(I18N.Get("workflow.assets.readFailed"),
                    MessageSeverity.Error));
                return panel;
            }
            TrackPartDetailsRow(panel, entry, target);
            var children = displayed.Where(child =>
                child.PrefabSiblingIndex == entry.PrefabSiblingIndex &&
                child.SiblingPath.Length == entry.SiblingPath.Length + 1 &&
                IsAncestorPath(entry.SiblingPath, child.SiblingPath)).ToArray();
            if (children.Length > 0)
            {
                var section = new VisualElement();
                section.AddToClassList("ee4v-part-details__children");
                section.Add(UiTextFactory.Create(I18N.Get("workflow.objects.children"),
                    UiClassNames.SectionTitle, "ee4v-part-details__children-title"));
                var list = new VisualElement();
                list.AddToClassList("ee4v-modification-workflow__objects-list");
                foreach (var child in children)
                {
                    list.Add(BuildObjectRow(child));
                    TrackPartDetailsRow(panel, child, ResolvePartObject(_context.Root, child));
                }
                AvatarEditingUi.MarkLastListItem(list);
                section.Add(list);
                panel.Add(section);
            }
            foreach (var component in target.GetComponents<Component>())
            {
                if (component == null)
                {
                    panel.Add(UiTextFactory.CreateMessagePanel(I18N.Get("workflow.objects.missingScript"),
                        MessageSeverity.Warning));
                    continue;
                }
                panel.Add(new PartComponentInspector(component,
                    () => _context.Edits.CanEditPrefab() && _context.Root != null &&
                        target != null && target.transform.IsChildOf(_context.Root.transform) &&
                        (component.hideFlags & HideFlags.NotEditable) == 0,
                    enabledOnly => PartComponentChanged(component, enabledOnly)));
            }
            return panel;
        }

        private void TrackPartDetailsRow(
            VisualElement panel,
            PrefabObjectEntry entry,
            GameObject target)
        {
            var state = _objectRows[entry];
            panel.schedule.Execute(() =>
            {
                if (target == null) { return; }
                var visible = !target.CompareTag("EditorOnly");
                var activeSelf = target.activeSelf;
                var parentActive = target.transform.parent == null ||
                    target.transform.parent.gameObject.activeInHierarchy;
                var activeInHierarchy = target.activeInHierarchy;
                if (entry.IsVisible == visible && entry.IsActiveSelf == activeSelf &&
                    entry.ParentActiveInHierarchy == parentActive &&
                    entry.IsVisibleInHierarchy == activeInHierarchy) { return; }
                entry.IsVisible = visible;
                entry.IsActiveSelf = activeSelf;
                entry.ParentActiveInHierarchy = parentActive;
                entry.IsVisibleInHierarchy = activeInHierarchy;
                UpdateObjectRow(entry, state);
            }).Every(200);
        }

        private void PartComponentChanged(Component component, bool enabledOnly)
        {
            if (component == null || !_context.Edits.CanEditPrefab()) { return; }
            _context.Edits.WorkingSceneDirty = true;
            ClearData();
            InvalidateShapeTargets();
            _context.Host.InvalidateMaterialData();
            _context.Host.InvalidateControls(AvatarEditorPanel.Shape);
            _context.Edits.Changed();
            if (enabledOnly) { _context.Preview?.RefreshPreview(); }
            else { _context.Preview?.ReloadTargetPreservingView(_context.Root); }
            _context.Host.SyncPreviewSelection();
        }

        private sealed class PartComponentInspector : VisualElement
        {
            private readonly Component _component;
            private readonly Func<bool> _canEdit;
            private readonly Action<bool> _changed;
            private readonly VisualElement _header;
            private readonly UiTextElement _title;
            private readonly VisualElement _body;
            private Toggle _enabled;
            private Editor _editor;
            private bool _changePending;
            private bool _pendingEnabledOnly;

            internal PartComponentInspector(Component component, Func<bool> canEdit, Action<bool> changed)
            {
                _component = component;
                _canEdit = canEdit;
                _changed = changed;
                AddToClassList("ee4v-part-component");
                _header = new VisualElement();
                _header.AddToClassList("ee4v-part-component__header");
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("ee4v-part-component__icon");
                var texture = EditorGUIUtility.ObjectContent(component, component.GetType()).image;
                icon.Add(new Icon(texture != null
                    ? IconState.FromTexture(texture, UiSizeTokens.Size18)
                    : FluentUiIcons.CreateState("cube.png", UiSizeTokens.Size18)));
                _header.Add(icon);
                _title = UiTextFactory.Create(ObjectNames.GetInspectorTitle(component),
                    UiClassNames.SectionTitle, "ee4v-part-component__title");
                _title.tooltip = ObjectNames.GetInspectorTitle(component);
                _header.Add(_title);
                Add(_header);
                _body = new VisualElement();
                _body.AddToClassList("ee4v-part-component__body");
                Add(_body);
                RegisterCallback<AttachToPanelEvent>(_ => BuildInspector());
                RegisterCallback<DetachFromPanelEvent>(_ => ReleaseInspector());
                schedule.Execute(UpdateEditable).Every(200);
            }

            private void BuildInspector()
            {
                if (_editor != null || _component == null) { return; }
                try
                {
                    _editor = Editor.CreateEditor(_component);
                    var inspector = UiTextFactory.CreateInspector(_editor);
                    var enabledProperty = _editor.serializedObject.FindProperty("m_Enabled");
                    if (enabledProperty != null && enabledProperty.propertyType == SerializedPropertyType.Boolean)
                    {
                        _enabled = UiTextFactory.CreateSwitch();
                        _enabled.tooltip = I18N.Get("workflow.objects.componentEnabled");
                        _enabled.RegisterValueChangedCallback(evt => UpdateHeader(evt.newValue));
                        _enabled.SetValueWithoutNotify(enabledProperty.boolValue);
                        _enabled.BindProperty(enabledProperty);
                        _header.Add(_enabled);
                        _header.TrackPropertyValue(enabledProperty, property => UpdateHeader(property.boolValue));
                    }
                    _body.Add(inspector);
                    Undo.postprocessModifications += OnUndoModifications;
                    Undo.undoRedoPerformed += RefreshEnabledHeader;
                    RefreshEnabledHeader();
                    UpdateEditable();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    ReleaseInspector();
                    _body.Add(UiTextFactory.CreateMessagePanel(I18N.Get("workflow.objects.editorUnavailable"),
                        MessageSeverity.Error));
                }
            }

            private UndoPropertyModification[] OnUndoModifications(UndoPropertyModification[] modifications)
            {
                if (_component == null || _editor == null || !_canEdit()) { return modifications; }
                foreach (var modification in modifications)
                {
                    var current = modification.currentValue;
                    if (current == null || current.target != _component) { continue; }
                    var previous = modification.previousValue;
                    if (previous != null && previous.objectReference == current.objectReference &&
                        string.Equals(previous.value, current.value, StringComparison.Ordinal)) { continue; }
                    EditorSceneApi.SaveAfterEdit(_component.gameObject.scene);
                    if (!_changePending)
                    {
                        _changePending = true;
                        _pendingEnabledOnly = true;
                        EditorApplication.delayCall += NotifyChange;
                    }
                    _pendingEnabledOnly &= current.propertyPath == "m_Enabled";
                }
                return modifications;
            }

            private void NotifyChange()
            {
                if (!_changePending) { return; }
                var enabledOnly = _pendingEnabledOnly;
                _changePending = false;
                EditorApplication.delayCall -= NotifyChange;
                RefreshEnabledHeader();
                if (_component != null && _canEdit()) { _changed(enabledOnly); }
            }

            private void RefreshEnabledHeader()
            {
                if (_editor == null || _component == null) { return; }
                var serializedObject = _editor.serializedObject;
                serializedObject.UpdateIfRequiredOrScript();
                var property = serializedObject.FindProperty("m_Enabled");
                var enabled = property == null || property.propertyType != SerializedPropertyType.Boolean || property.boolValue;
                _enabled?.SetValueWithoutNotify(enabled);
                UpdateHeader(enabled);
            }

            private void UpdateHeader(bool enabled)
            {
                _header.EnableInClassList("ee4v-part-component__header--inactive", !enabled);
                _title.SetColor(enabled ? UiColorTokens.TextPrimary : UiColorTokens.TextDisabled);
            }

            private void ReleaseInspector()
            {
                EditorApplication.delayCall -= NotifyChange;
                _changePending = false;
                Undo.postprocessModifications -= OnUndoModifications;
                Undo.undoRedoPerformed -= RefreshEnabledHeader;
                this.Unbind();
                _enabled?.RemoveFromHierarchy();
                _enabled = null;
                _body.Clear();
                if (_editor == null) { return; }
                UnityEngine.Object.DestroyImmediate(_editor);
                _editor = null;
            }

            private void UpdateEditable()
            {
                var editable = _component != null && _canEdit();
                _body.SetEnabled(editable);
                _enabled?.SetEnabled(editable);
            }
        }
    }
}
