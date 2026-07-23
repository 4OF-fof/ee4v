using System;
using System.Collections.Generic;

namespace Ee4v.Core.Settings
{
    public sealed class SettingChangedEventArgs : EventArgs
    {
        public SettingChangedEventArgs(SettingDefinitionBase definition, object value)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Value = value;
        }

        public SettingDefinitionBase Definition { get; }

        public object Value { get; }
    }

    public interface ISettingsService
    {
        event EventHandler<SettingChangedEventArgs> Changed;

        void Register(SettingDefinitionBase definition);

        IReadOnlyList<SettingDefinitionBase> GetDefinitions(SettingScope scope);

        void Preload(SettingScope scope);

        T Get<T>(SettingDefinition<T> definition);

        object Get(SettingDefinitionBase definition);

        void Set<T>(SettingDefinition<T> definition, T value, bool saveImmediately = true);

        void Set(SettingDefinitionBase definition, object value, bool saveImmediately = true);

        void Save(SettingScope? scope = null);
    }
}
