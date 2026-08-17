using System;
using System.Collections.Generic;

namespace Ee4v.Core.Settings
{
    public abstract class SettingDefinitionBase
    {
        protected SettingDefinitionBase(
            string key,
            SettingScope scope,
            string localizationScope,
            string sectionKey,
            string displayNameKey,
            string descriptionKey,
            int order,
            IReadOnlyList<string> keywords)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Setting key is required.", nameof(key));
            }

            if (string.IsNullOrWhiteSpace(localizationScope))
            {
                throw new ArgumentException("Localization scope is required.", nameof(localizationScope));
            }

            Key = key;
            Scope = scope;
            LocalizationScope = localizationScope;
            SectionKey = sectionKey ?? string.Empty;
            DisplayNameKey = displayNameKey ?? string.Empty;
            DescriptionKey = descriptionKey ?? string.Empty;
            Order = order;
            Keywords = keywords ?? Array.Empty<string>();
        }

        public string Key { get; }

        public SettingScope Scope { get; }

        public string LocalizationScope { get; }

        public string SectionKey { get; }

        public string DisplayNameKey { get; }

        public string DescriptionKey { get; }

        public int Order { get; }

        public IReadOnlyList<string> Keywords { get; }

        public abstract Type ValueType { get; }

        public abstract object DefaultValue { get; }

        public abstract SettingValidationResult Validate(object value);
    }

    public sealed class SettingDefinition<T> : SettingDefinitionBase
    {
        private readonly Func<T, SettingValidationResult> _validator;
        private readonly T _defaultValue;
        private readonly SettingRange<T> _range;

        public SettingDefinition(
            string key,
            SettingScope scope,
            string localizationScope,
            string sectionKey,
            string displayNameKey,
            string descriptionKey,
            T defaultValue,
            int order = 0,
            Func<T, SettingValidationResult> validator = null,
            SettingRange<T> range = null,
            IReadOnlyList<string> keywords = null)
            : base(
                key,
                scope,
                localizationScope,
                sectionKey,
                displayNameKey,
                descriptionKey,
                order,
                keywords)
        {
            _defaultValue = defaultValue;
            _validator = validator;
            _range = range;
            if (_range != null && !_range.Contains(_defaultValue))
            {
                throw new ArgumentOutOfRangeException(nameof(defaultValue));
            }
        }

        public SettingRange<T> Range
        {
            get { return _range; }
        }

        public override Type ValueType
        {
            get { return typeof(T); }
        }

        public override object DefaultValue
        {
            get { return _defaultValue; }
        }

        public override SettingValidationResult Validate(object value)
        {
            var typedValue = value != null ? (T)value : default(T);
            if (_range != null && !_range.Contains(typedValue))
            {
                return _range.CreateOutOfRangeResult();
            }

            return _validator != null
                ? _validator(typedValue)
                : SettingValidationResult.Success;
        }
    }
}
