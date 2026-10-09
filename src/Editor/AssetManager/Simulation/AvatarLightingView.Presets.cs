using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.Simulation
{
    public sealed partial class AvatarLightingView
    {
        private readonly List<LightingPreset> _savedPresets = new List<LightingPreset>();
        private readonly List<Action> _refreshPreviewSettings = new List<Action>();
        private readonly LightingPreset _customChoice = new LightingPreset { Pattern = -1 };
        private PopupField<LightingPreset> _presetField;
        private InputField _presetName;
        private UiButton _savePreset;
        private UiButton _deletePreset;
        private UiButton _addPreview;
        private VisualElement _previewSettings;
        private VisualElement _directionalSettings;
        private VisualElement _volumeSettings;
        private bool _presetReadFailed;

        private LightingPreset CreateBuiltin(int pattern)
        {
            return LightingPresetStore.CreateBuiltin(pattern);
        }

        private string PresetLabel(LightingPreset preset) =>
            preset.Pattern == -1 ? Text("custom") : !string.IsNullOrEmpty(preset.BuiltinKey) ? Text(preset.BuiltinKey) :
            string.IsNullOrEmpty(preset.Name) ? Text("custom") : preset.Name + (preset.Modified ? " *" : string.Empty);

        private void AddPreview()
        {
            if (_previews.Count >= MaxPreviewCount) { return; }
            _previews.Add(_previews[ActivePreview].Copy());
            SetPreviewCount(_previews.Count);
            SelectPreview(_previews.Count - 1);
        }

        protected override void AddPreviewActions(VisualElement header, int index, int count)
        {
            var remove = new UiButton(string.Empty, () => RemovePreview(index),
                icon: FluentUiIcons.CreateState("dismiss.png", UiSizeTokens.Size12), variant: UiButtonVariant.Ghost);
            remove.tooltip = Text("removePreview");
            remove.AddToClassList("ee4v-inspection__remove-preview");
            remove.SetEnabled(count > 1);
            header.Add(remove);
        }

        private void RemovePreview(int index)
        {
            if (_previews.Count <= 1) { return; }
            var selected = ActivePreview;
            _previews.RemoveAt(index);
            if (selected > index) { selected--; }
            SetPreviewCount(_previews.Count, selected);
        }

        private void BuildPresetControls()
        {
            _presetField = UiTextFactory.CreatePopupField(Text("preset"),
                new List<LightingPreset> { _customChoice }, 0, PresetLabel, PresetLabel);
            _presetField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == _customChoice) { RefreshPresetControls(); return; }
                _previews[ActivePreview] = evt.newValue.Copy();
                _presetName.SetValueWithoutNotify(_previews[ActivePreview].Name);
                RefreshSelection();
                Repaint();
            });
            Controls.Add(_presetField);
            _presetName = new InputField(new InputFieldState(placeholder: Text("presetName")));
            _presetName.ValueChanged += _ => RefreshPresetActions();
            Controls.Add(_presetName);
            var actions = new VisualElement();
            actions.AddToClassList("ee4v-lighting__preset-actions");
            _savePreset = new UiButton(Text("savePreset"), SavePreset);
            _deletePreset = new UiButton(Text("deletePreset"), DeletePreset);
            actions.Add(_savePreset);
            actions.Add(_deletePreset);
            Controls.Add(actions);
            ReloadPresets();
            LightingPresetStore.Changed += ReloadPresets;
            GlobalDataSettings.PathChanged += ReloadPresets;
        }

        private void ReloadPresets()
        {
            _savedPresets.Clear();
            _presetReadFailed = false;
            try
            {
                _savedPresets.AddRange(LightingPresetStore.Read());
            }
            catch (Exception exception) { _presetReadFailed = true; ShowPresetError(exception); }
            RefreshPresetControls();
        }

        private void RefreshPresetControls()
        {
            if (_presetField == null) { return; }
            var choices = new List<LightingPreset> { _customChoice };
            for (var i = 1; i <= (_volumesAvailable ? 6 : 5); i++) { choices.Add(CreateBuiltin(i)); }
            choices.AddRange(_savedPresets.Where(preset => _volumesAvailable || preset.Pattern != 6));
            _presetField.choices = choices;
            var active = _previews[ActivePreview];
            var selected = active.Modified ? null : choices.Skip(1).FirstOrDefault(preset =>
                preset.BuiltinKey == active.BuiltinKey && preset.Name == active.Name && preset.Matches(active));
            _presetField.SetValueWithoutNotify(selected ?? _customChoice);
            RefreshPresetActions();
        }

        private void RefreshPresetActions()
        {
            var active = _previews[ActivePreview];
            var name = _presetName.Value.Trim();
            _savePreset.SetEnabled(!_presetReadFailed && name.Length > 0 &&
                !_patternNames.Any(key => string.Equals(Text(key), name, StringComparison.Ordinal)));
            _savePreset.SetLabel(Text(_savedPresets.Any(preset => preset.Name == name) ? "overwritePreset" : "savePreset"));
            _deletePreset.SetEnabled(!_presetReadFailed && string.IsNullOrEmpty(active.BuiltinKey) &&
                _savedPresets.Any(preset => preset.Name == active.Name));
        }

        private void SavePreset()
        {
            var preset = _previews[ActivePreview].Copy();
            preset.Name = _presetName.Value.Trim();
            preset.BuiltinKey = null;
            preset.Modified = false;
            if (!preset.IsValid) { return; }
            try { LightingPresetStore.Upsert(preset); Status.SetText(string.Empty); }
            catch (Exception exception) { ShowPresetError(exception); return; }
            _previews[ActivePreview] = preset;
            RefreshSelection();
        }

        private void DeletePreset()
        {
            var name = _previews[ActivePreview].Name;
            try { LightingPresetStore.Delete(name); Status.SetText(string.Empty); }
            catch (Exception exception) { ShowPresetError(exception); return; }
            foreach (var preview in _previews.Where(preview => preview.Name == name && string.IsNullOrEmpty(preview.BuiltinKey)))
            { preview.Name = string.Empty; preview.Modified = true; }
            _presetName.SetValueWithoutNotify(string.Empty);
            RefreshSelection();
        }

        private void ShowPresetError(Exception exception) =>
            Status.SetText(I18N.Get("workflow.lighting.presetFailed", exception.Message));

        private void BuildPreviewSettings()
        {
            _previewSettings = new VisualElement();
            _previewSettings.AddToClassList("ee4v-lighting__settings");
            Controls.Add(_previewSettings);
            AddPreviewColor(_previewSettings, "ambient", preset => preset.AmbientColor, (preset, value) => preset.AmbientColor = value);
            AddPreviewFloat(_previewSettings, "ambientIntensity", preset => preset.AmbientIntensity, (preset, value) => preset.AmbientIntensity = value);
            AddPreviewFloat(_previewSettings, "reflection", preset => preset.ReflectionIntensity, (preset, value) => preset.ReflectionIntensity = value);
            _directionalSettings = new VisualElement();
            _previewSettings.Add(_directionalSettings);
            AddPreviewColor(_directionalSettings, "color", preset => preset.LightColor, (preset, value) => preset.LightColor = value);
            AddPreviewFloat(_directionalSettings, "intensity", preset => preset.LightIntensity, (preset, value) => preset.LightIntensity = value);
            AddPreviewFloat(_directionalSettings, "pitch", preset => preset.Pitch, (preset, value) => preset.Pitch = value, false);
            AddPreviewFloat(_directionalSettings, "yaw", preset => preset.Yaw, (preset, value) => preset.Yaw = value, false);
            if (!_volumesAvailable) { return; }
            _volumeSettings = new VisualElement();
            _previewSettings.Add(_volumeSettings);
            AddPreviewColor(_volumeSettings, "lvColor", preset => preset.VolumeColor, (preset, value) => preset.VolumeColor = value);
            AddPreviewFloat(_volumeSettings, "intensity", preset => preset.LightIntensity, (preset, value) => preset.LightIntensity = value);
            AddPreviewFloat(_volumeSettings, "pitch", preset => preset.Pitch, (preset, value) => preset.Pitch = value, false);
            AddPreviewFloat(_volumeSettings, "yaw", preset => preset.Yaw, (preset, value) => preset.Yaw = value, false);
            _volumeSettings.Add(UiTextFactory.Create(Text("lvHint"), UiClassNames.SecondaryText));
        }

        private void PreviewSettingChanged()
        {
            var preset = _previews[ActivePreview];
            preset.BuiltinKey = null;
            preset.Modified = true;
            RefreshSelection();
            Repaint();
        }

        private void AddPreviewColor(VisualElement parent, string key, Func<LightingPreset, Color> get, Action<LightingPreset, Color> set)
        {
            var field = UiTextFactory.CreateColorField(Text(key));
            field.showAlpha = false;
            _refreshPreviewSettings.Add(() => field.SetValueWithoutNotify(get(_previews[ActivePreview])));
            field.RegisterValueChangedCallback(evt => { set(_previews[ActivePreview], evt.newValue); PreviewSettingChanged(); });
            parent.Add(field);
        }

        private void AddPreviewFloat(VisualElement parent, string key, Func<LightingPreset, float> get,
            Action<LightingPreset, float> set, bool nonnegative = true)
        {
            var field = UiTextFactory.CreateFloatField(Text(key));
            _refreshPreviewSettings.Add(() => field.SetValueWithoutNotify(get(_previews[ActivePreview])));
            field.RegisterValueChangedCallback(evt =>
            {
                if (float.IsNaN(evt.newValue) || float.IsInfinity(evt.newValue))
                { field.SetValueWithoutNotify(get(_previews[ActivePreview])); return; }
                set(_previews[ActivePreview], nonnegative ? Mathf.Max(0, evt.newValue) : evt.newValue);
                PreviewSettingChanged();
            });
            parent.Add(field);
        }
    }
}
