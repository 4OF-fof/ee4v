using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ee4v.Core.Internal;
using Newtonsoft.Json;
using UnityEngine;

namespace Ee4v.Core.Settings
{
    public abstract class SettingDefinitionBase
    {
        protected SettingDefinitionBase(
            string key,
            SettingScope scope,
            string sectionKey,
            string displayNameKey,
            string descriptionKey,
            int order,
            IReadOnlyList<string> keywords,
            string localizationScope)
        {
            Key = key;
            Scope = scope;
            SectionKey = sectionKey;
            DisplayNameKey = displayNameKey;
            DescriptionKey = descriptionKey;
            Order = order;
            Keywords = keywords ?? Array.Empty<string>();
            LocalizationScope = localizationScope ?? string.Empty;
        }

        public string Key { get; }

        public SettingScope Scope { get; }

        public string SectionKey { get; }

        public string DisplayNameKey { get; }

        public string DescriptionKey { get; }

        public int Order { get; }

        public IReadOnlyList<string> Keywords { get; }

        internal string LocalizationScope { get; }

        public abstract Type ValueType { get; }

        internal abstract object BoxedDefaultValue { get; }

        internal abstract SettingValidationResult ValidateBoxed(object value);

        internal abstract object DrawField(GUIContent label, object currentValue, string searchContext);

        internal abstract string SerializeBoxed(object value);

        internal abstract object DeserializeBoxed(string rawValue);
    }

    public sealed class SettingDefinition<T> : SettingDefinitionBase
    {
        private readonly Func<T, SettingValidationResult> _validator;
        private readonly Func<SettingDrawerContext<T>, T> _customDrawer;
        private readonly T _defaultValue;

        public SettingDefinition(
            string key,
            SettingScope scope,
            string sectionKey,
            string displayNameKey,
            string descriptionKey,
            T defaultValue,
            int order = 0,
            Func<T, SettingValidationResult> validator = null,
            Func<SettingDrawerContext<T>, T> customDrawer = null,
            IReadOnlyList<string> keywords = null,
            [CallerFilePath] string definitionSourceFilePath = "")
            : base(
                key,
                scope,
                sectionKey,
                displayNameKey,
                descriptionKey,
                order,
                keywords,
                PackagePathUtility.GetScopeNameForSourceFile(definitionSourceFilePath))
        {
            _defaultValue = defaultValue;
            _validator = validator;
            _customDrawer = customDrawer;
        }

        public override Type ValueType
        {
            get { return typeof(T); }
        }

        internal override object BoxedDefaultValue
        {
            get { return _defaultValue; }
        }

        internal override SettingValidationResult ValidateBoxed(object value)
        {
            if (_validator == null)
            {
                return SettingValidationResult.Success;
            }

            return _validator(value != null ? (T)value : default(T));
        }

        internal override object DrawField(GUIContent label, object currentValue, string searchContext)
        {
            if (_customDrawer != null)
            {
                return _customDrawer(new SettingDrawerContext<T>(label, currentValue != null ? (T)currentValue : default(T), searchContext));
            }

            return SettingFieldRenderer.Draw(typeof(T), label, currentValue);
        }

        internal override string SerializeBoxed(object value)
        {
            return JsonConvert.SerializeObject(value, Formatting.None);
        }

        internal override object DeserializeBoxed(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return _defaultValue;
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(rawValue);
            }
            catch (Exception)
            {
                return _defaultValue;
            }
        }
    }
}
