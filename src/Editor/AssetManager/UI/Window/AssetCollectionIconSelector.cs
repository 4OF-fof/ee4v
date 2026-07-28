using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetCollectionIconSelector : VisualElement
    {
        private const string RootClassName =
            "ee4v-collection-icon-selector";
        private const string CandidateClassName =
            "ee4v-collection-icon-selector__candidate";
        private const string LastCandidateClassName =
            "ee4v-collection-icon-selector__candidate--last";
        private const string SelectedClassName =
            "ee4v-collection-icon-selector__candidate--selected";
        private const string PresetsClassName =
            "ee4v-collection-icon-selector__presets";
        private const string PresetRowClassName =
            "ee4v-collection-icon-selector__preset-row";
        private const int PresetColumns = 15;
        private const string CustomRowClassName =
            "ee4v-collection-icon-selector__custom-row";
        private const string CustomLabelClassName =
            "ee4v-collection-icon-selector__custom-label";
        private const string CustomFieldClassName =
            "ee4v-collection-icon-selector__custom-field";
        private readonly List<Candidate> _candidates =
            new List<Candidate>();
        private readonly ObjectField _customIconField;
        private AssetCollectionIcon _value;
        private string _assetGuid = string.Empty;

        public AssetCollectionIconSelector(
            AssetCollectionIcon value,
            string assetGuid = null)
        {
            AddToClassList(RootClassName);
            var presets = new VisualElement();
            presets.AddToClassList(PresetsClassName);
            var icons = Enum.GetValues(typeof(AssetCollectionIcon))
                .Cast<AssetCollectionIcon>()
                .ToArray();
            VisualElement presetRow = null;
            var index = 0;
            foreach (var icon in icons)
            {
                if (index % PresetColumns == 0)
                {
                    presetRow = new VisualElement();
                    presetRow.AddToClassList(
                        PresetRowClassName);
                    presets.Add(presetRow);
                }

                var capturedIcon = icon;
                var button = new UiButton(
                    new UiButtonState(
                        tooltip: FormatIcon(capturedIcon),
                        iconState: IconState.FromFluentIcon(
                            AssetCollectionIconPresenter.Resolve(
                                capturedIcon),
                            size: UiSizeTokens.Size18),
                        variant: UiButtonVariant.Ghost,
                        size: UiButtonSize.Compact),
                    () => SelectBuiltin(capturedIcon));
                button.AddToClassList(CandidateClassName);
                button.EnableInClassList(
                    LastCandidateClassName,
                    (index + 1) % PresetColumns == 0 ||
                    index == icons.Length - 1);
                presetRow.Add(button);
                _candidates.Add(new Candidate(capturedIcon, button));
                index++;
            }

            Add(presets);

            var customRow = new VisualElement();
            customRow.AddToClassList(CustomRowClassName);
            customRow.Add(UiTextFactory.Create(
                I18N.Get(
                    "assetManager.collectionCreation.customIcon"),
                UiClassNames.FormLabel,
                CustomLabelClassName));
            _customIconField = new ObjectField(string.Empty)
            {
                objectType = typeof(Texture),
                allowSceneObjects = false,
                tooltip = I18N.Get(
                    "assetManager.collectionCreation.customIconTooltip")
            };
            _customIconField.AddToClassList(CustomFieldClassName);
            _customIconField.RegisterValueChangedCallback(
                evt => SetCustomIcon(evt.newValue as Texture));
            customRow.Add(_customIconField);
            Add(customRow);

            _value = value;
            SetAssetGuidWithoutNotify(assetGuid);
            RefreshSelection();
        }

        public event Action<AssetCollectionIcon> ValueChanged;

        public AssetCollectionIcon Value
        {
            get { return _value; }
            set { SelectBuiltin(value); }
        }

        public string AssetGuid
        {
            get { return _assetGuid; }
        }

        public void SetValueWithoutNotify(
            AssetCollectionIcon value)
        {
            _value = value;
            _assetGuid = string.Empty;
            _customIconField.SetValueWithoutNotify(null);
            RefreshSelection();
        }

        private void SelectBuiltin(
            AssetCollectionIcon value)
        {
            var changed =
                _value != value ||
                !string.IsNullOrEmpty(_assetGuid);
            SetValueWithoutNotify(value);
            if (changed)
            {
                ValueChanged?.Invoke(_value);
            }
        }

        private void SetCustomIcon(Texture texture)
        {
            var path = texture != null
                ? AssetDatabase.GetAssetPath(texture)
                : string.Empty;
            _assetGuid = string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(path);
            RefreshSelection();
            ValueChanged?.Invoke(_value);
        }

        private void SetAssetGuidWithoutNotify(
            string assetGuid)
        {
            _assetGuid = assetGuid ?? string.Empty;
            var texture = LoadAssetIcon(_assetGuid);
            if (texture == null)
            {
                _assetGuid = string.Empty;
            }

            _customIconField.SetValueWithoutNotify(texture);
        }

        private void RefreshSelection()
        {
            for (var i = 0; i < _candidates.Count; i++)
            {
                var candidate = _candidates[i];
                var selected =
                    string.IsNullOrEmpty(_assetGuid) &&
                    candidate.Icon == _value;
                candidate.Button.EnableInClassList(
                    SelectedClassName,
                    selected);
                candidate.Button.SetSelected(selected);
            }
        }

        private static Texture LoadAssetIcon(
            string assetGuid)
        {
            if (string.IsNullOrWhiteSpace(assetGuid))
            {
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(assetGuid);
            return string.IsNullOrWhiteSpace(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture>(path);
        }

        private static string FormatIcon(
            AssetCollectionIcon icon)
        {
            return I18N.Get(
                "assetManager.collectionCreation.icon." +
                icon.ToString().ToLowerInvariant());
        }

        private sealed class Candidate
        {
            public Candidate(
                AssetCollectionIcon icon,
                UiButton button)
            {
                Icon = icon;
                Button = button;
            }

            public AssetCollectionIcon Icon { get; }

            public UiButton Button { get; }
        }
    }
}
