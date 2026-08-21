using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.FaceExpression
{
    [Serializable]
    internal sealed class BlendShapeNamePresetState
    {
        public string selectedId;
        public List<BlendShapeNameCustomPreset> customPresets =
            new List<BlendShapeNameCustomPreset>();
    }

    [Serializable]
    internal sealed class BlendShapeNameCustomPreset
    {
        public string id;
        public string name;
        public string pattern;
    }

    internal static class BlendShapeNamePresetSetting
    {
        internal const string UnderscorePresetId = "builtin:underscore";
        internal const string SpaceParenthesesPresetId =
            "builtin:space-parentheses";
        internal const string SpaceParenthesesPattern =
            @"^(?<group>[^_ ]+)_(?<role>.+?)(?:(?: (?<variation>\d+))?_(?<side>L|R)(?: \((?<variation>[^)]+)\))?|(?: (?<variation>\d+))?(?: \((?<variation>[^)]+)\))?)$";

        internal static string DefaultValue => Serialize(
            new BlendShapeNamePresetState
            {
                selectedId = UnderscorePresetId
            });

        internal static void RegisterDrawer(
            SettingDefinition<string> definition)
        {
            SettingDrawerApi.Register(
                definition,
                context => new PresetField(
                    context.Value,
                    context.Tooltip,
                    context.NotifyValueChanged));
        }

        internal static string GetPattern(string value)
        {
            var state = Parse(value);
            if (TryGetBuiltInPattern(state.selectedId, out var builtIn))
            {
                return builtIn;
            }

            return state.customPresets.FirstOrDefault(preset =>
                    string.Equals(
                        preset.id,
                        state.selectedId,
                        StringComparison.Ordinal))
                ?.pattern ?? FaceExpressionSettings.DefaultBlendShapeNamePattern;
        }

        internal static SettingValidationResult Validate(string value)
        {
            var state = Parse(value);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < state.customPresets.Count; index++)
            {
                var preset = state.customPresets[index];
                if (string.IsNullOrWhiteSpace(preset.name) ||
                    !names.Add(preset.name.Trim()))
                {
                    return SettingValidationResult.Error(
                        "Preset names must be unique and non-empty.");
                }

                var patternValidation = BlendShapeNamingRule.Validate(
                    preset.pattern);
                if (!patternValidation.IsValid)
                {
                    return patternValidation;
                }
            }

            return SettingValidationResult.Success;
        }

        internal static BlendShapeNamePresetState Parse(string value)
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (trimmed.StartsWith("{", StringComparison.Ordinal))
            {
                try
                {
                    var parsed = JsonUtility.FromJson<BlendShapeNamePresetState>(
                        trimmed);
                    if (parsed != null)
                    {
                        Normalize(parsed);
                        return parsed;
                    }
                }
                catch (ArgumentException)
                {
                }
            }

            return MigratePattern(value ?? string.Empty);
        }

        internal static string Serialize(BlendShapeNamePresetState state)
        {
            state = state ?? new BlendShapeNamePresetState();
            Normalize(state);
            return JsonUtility.ToJson(state);
        }

        internal static string SaveCustom(
            BlendShapeNamePresetState state,
            string selectedId,
            string name,
            string pattern)
        {
            Normalize(state);
            var preset = state.customPresets.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.id,
                    selectedId,
                    StringComparison.Ordinal));
            if (preset == null)
            {
                preset = new BlendShapeNameCustomPreset
                {
                    id = Guid.NewGuid().ToString("N")
                };
                state.customPresets.Add(preset);
            }

            preset.name = name.Trim();
            preset.pattern = pattern ?? string.Empty;
            state.selectedId = preset.id;
            return preset.id;
        }

        private static BlendShapeNamePresetState MigratePattern(string pattern)
        {
            if (string.Equals(
                    pattern,
                    FaceExpressionSettings.DefaultBlendShapeNamePattern,
                    StringComparison.Ordinal))
            {
                return new BlendShapeNamePresetState
                {
                    selectedId = UnderscorePresetId
                };
            }

            if (string.Equals(
                    pattern,
                    SpaceParenthesesPattern,
                    StringComparison.Ordinal))
            {
                return new BlendShapeNamePresetState
                {
                    selectedId = SpaceParenthesesPresetId
                };
            }

            var state = new BlendShapeNamePresetState();
            SaveCustom(state, null, "Custom", pattern);
            return state;
        }

        private static void Normalize(BlendShapeNamePresetState state)
        {
            state.customPresets = (state.customPresets ??
                    new List<BlendShapeNameCustomPreset>())
                .Where(preset => preset != null &&
                                 !string.IsNullOrEmpty(preset.id))
                .ToList();
            if (!IsBuiltIn(state.selectedId) &&
                state.customPresets.All(preset => !string.Equals(
                    preset.id,
                    state.selectedId,
                    StringComparison.Ordinal)))
            {
                state.selectedId = UnderscorePresetId;
            }
        }

        private static bool TryGetBuiltInPattern(
            string id,
            out string pattern)
        {
            if (string.Equals(id, UnderscorePresetId, StringComparison.Ordinal))
            {
                pattern = FaceExpressionSettings.DefaultBlendShapeNamePattern;
                return true;
            }

            if (string.Equals(
                    id,
                    SpaceParenthesesPresetId,
                    StringComparison.Ordinal))
            {
                pattern = SpaceParenthesesPattern;
                return true;
            }

            pattern = null;
            return false;
        }

        private static bool IsBuiltIn(string id)
        {
            return TryGetBuiltInPattern(id, out _);
        }

        private sealed class PresetChoice
        {
            internal PresetChoice(
                string id,
                string name,
                string pattern,
                bool builtIn)
            {
                Id = id;
                Name = name;
                Pattern = pattern;
                BuiltIn = builtIn;
            }

            internal string Id { get; }
            internal string Name { get; }
            internal string Pattern { get; }
            internal bool BuiltIn { get; }
        }

        private sealed class PresetField : VisualElement
        {
            private readonly Action<string> _notify;
            private readonly PopupField<PresetChoice> _preset;
            private readonly TextField _name;
            private readonly TextField _pattern;
            private readonly UiTextButton _delete;
            private readonly HelpBox _error;
            private BlendShapeNamePresetState _state;
            private bool _rendering;

            internal PresetField(
                string value,
                string tooltip,
                Action<string> notify)
            {
                _notify = notify;
                _state = Parse(value);
                tooltip = tooltip ?? string.Empty;
                UiStyleUtility.AddPackageStyleSheet(
                    this,
                    "Editor/UI/Components/Inputs/ui-button.uss");

                _preset = UiTextFactory.CreatePopupField(
                    I18N.Get("settings.blendShapeNamePattern.preset"),
                    CreateChoices(),
                    0,
                    choice => choice?.Name ?? string.Empty,
                    choice => choice?.Name ?? string.Empty);
                _preset.tooltip = tooltip;
                _preset.RegisterValueChangedCallback(evt =>
                {
                    if (_rendering || evt.newValue == null)
                    {
                        return;
                    }

                    _state.selectedId = evt.newValue.Id;
                    Notify();
                    ShowChoice(evt.newValue);
                });
                Add(_preset);

                _name = UiTextFactory.CreateTextField(
                    I18N.Get("settings.blendShapeNamePattern.name"));
                _name.tooltip = tooltip;
                Add(_name);

                _pattern = UiTextFactory.CreateTextField(
                    I18N.Get("settings.blendShapeNamePattern.pattern"));
                _pattern.multiline = true;
                _pattern.style.minHeight = 64f;
                _pattern.tooltip = tooltip;
                Add(_pattern);

                var actions = new VisualElement();
                actions.style.flexDirection = FlexDirection.Row;
                actions.style.justifyContent = Justify.FlexEnd;
                var save = UiTextFactory.CreateButton(
                    I18N.Get("settings.blendShapeNamePattern.save"),
                    Save);
                actions.Add(save);
                _delete = UiTextFactory.CreateButton(
                    I18N.Get("settings.blendShapeNamePattern.delete"),
                    Delete);
                _delete.style.marginLeft = UiSpacingTokens.Xs;
                actions.Add(_delete);
                Add(actions);

                _error = UiTextFactory.CreateHelpBox(
                    string.Empty,
                    HelpBoxMessageType.Error);
                _error.style.display = DisplayStyle.None;
                Add(_error);
                RefreshChoices();
            }

            private List<PresetChoice> CreateChoices()
            {
                var choices = new List<PresetChoice>
                {
                    new PresetChoice(
                        UnderscorePresetId,
                        I18N.Get(
                            "settings.blendShapeNamePattern.underscorePreset"),
                        FaceExpressionSettings.DefaultBlendShapeNamePattern,
                        true),
                    new PresetChoice(
                        SpaceParenthesesPresetId,
                        I18N.Get(
                            "settings.blendShapeNamePattern.spaceParenthesesPreset"),
                        SpaceParenthesesPattern,
                        true)
                };
                choices.AddRange(_state.customPresets.Select(preset =>
                    new PresetChoice(
                        preset.id,
                        preset.name,
                        preset.pattern,
                        false)));
                return choices;
            }

            private void RefreshChoices()
            {
                var choices = CreateChoices();
                var selected = choices.FirstOrDefault(choice => string.Equals(
                                   choice.Id,
                                   _state.selectedId,
                                   StringComparison.Ordinal)) ??
                               choices[0];
                _rendering = true;
                _preset.choices = choices;
                _preset.SetValueWithoutNotify(selected);
                _rendering = false;
                ShowChoice(selected);
            }

            private void ShowChoice(PresetChoice choice)
            {
                _rendering = true;
                _name.SetValueWithoutNotify(choice.Name);
                _pattern.SetValueWithoutNotify(choice.Pattern);
                _delete.SetEnabled(!choice.BuiltIn);
                _rendering = false;
                HideError();
            }

            private void Save()
            {
                var name = (_name.value ?? string.Empty).Trim();
                if (name.Length == 0)
                {
                    ShowError(I18N.Get(
                        "settings.blendShapeNamePattern.nameRequired"));
                    return;
                }

                var pattern = _pattern.value ?? string.Empty;
                if (!BlendShapeNamingRule.Validate(pattern).IsValid)
                {
                    ShowError(I18N.Get(
                        "settings.blendShapeNamePattern.invalidPattern"));
                    return;
                }

                var selectedCustom = _state.customPresets.FirstOrDefault(
                    preset => string.Equals(
                        preset.id,
                        _state.selectedId,
                        StringComparison.Ordinal));
                var duplicate = CreateChoices().Any(choice =>
                    !string.Equals(
                        choice.Id,
                        selectedCustom?.id,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        choice.Name,
                        name,
                        StringComparison.OrdinalIgnoreCase));
                if (duplicate)
                {
                    ShowError(I18N.Get(
                        "settings.blendShapeNamePattern.duplicateName"));
                    return;
                }

                SaveCustom(
                    _state,
                    selectedCustom?.id,
                    name,
                    pattern);
                Notify();
                RefreshChoices();
            }

            private void Delete()
            {
                var removed = _state.customPresets.RemoveAll(preset =>
                    string.Equals(
                        preset.id,
                        _state.selectedId,
                        StringComparison.Ordinal));
                if (removed == 0)
                {
                    return;
                }

                _state.selectedId = UnderscorePresetId;
                Notify();
                RefreshChoices();
            }

            private void Notify()
            {
                _notify?.Invoke(Serialize(_state));
            }

            private void ShowError(string message)
            {
                UiTextFactory.SetText(_error, message);
                _error.style.display = DisplayStyle.Flex;
            }

            private void HideError()
            {
                UiTextFactory.SetText(_error, string.Empty);
                _error.style.display = DisplayStyle.None;
            }
        }
    }
}
