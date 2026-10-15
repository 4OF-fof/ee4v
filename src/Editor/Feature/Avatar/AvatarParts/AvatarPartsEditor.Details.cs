using System;
using Ee4v.AvatarEditing;
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
                _context.Host.SyncPreviewSelection();
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

        private VisualElement BuildPartDetails(PrefabObjectEntry entry)
        {
            var panel = new VisualElement();
            panel.AddToClassList("ee4v-part-details");
            var back = new UiButton(I18N.Get("workflow.objects.backToParts"),
                () => NavigateToPart(null),
                icon: FluentUiIcons.CreateState("arrow_left.png", UiSizeTokens.Size18),
                variant: UiButtonVariant.Ghost);
            back.AddToClassList("ee4v-part-details__back");
            panel.Add(back);
            panel.Add(UiTextFactory.Create(entry.Name, UiClassNames.SectionTitle));
            var path = UiTextFactory.Create(entry.Path, UiClassNames.SecondaryText,
                "ee4v-part-details__path");
            path.SetWhiteSpace(WhiteSpace.Normal);
            panel.Add(path);
            GameObject target;
            try { target = ResolvePartObject(_context.Root, entry); }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                panel.Add(UiTextFactory.CreateHelpBox(I18N.Get("workflow.assets.readFailed"),
                    HelpBoxMessageType.Error));
                return panel;
            }
            foreach (var component in target.GetComponents<Component>())
            {
                if (component == null)
                {
                    panel.Add(UiTextFactory.CreateHelpBox(I18N.Get("workflow.objects.missingScript"),
                        HelpBoxMessageType.Warning));
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
            else { _context.Preview?.ReloadPrefabPreservingView(_context.Root); }
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
                    _body.Add(UiTextFactory.CreateHelpBox(I18N.Get("workflow.objects.editorUnavailable"),
                        HelpBoxMessageType.Error));
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
