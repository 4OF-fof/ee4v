using System;
using System.Collections.Generic;
using System.Linq;

namespace Ee4v.Core.Settings
{
    public sealed class SettingsService : ISettingsService
    {
        private readonly object _syncRoot = new object();
        private readonly Dictionary<string, SettingDefinitionBase> _definitions =
            new Dictionary<string, SettingDefinitionBase>();
        private readonly Dictionary<string, object> _cachedValues =
            new Dictionary<string, object>();
        private readonly HashSet<SettingScope> _loadedScopes =
            new HashSet<SettingScope>();
        private readonly HashSet<SettingScope> _dirtyScopes =
            new HashSet<SettingScope>();
        private readonly IReadOnlyDictionary<SettingScope, ISettingStore> _stores;
        private readonly ISettingValueSerializer _serializer;

        public SettingsService(
            IReadOnlyDictionary<SettingScope, ISettingStore> stores,
            ISettingValueSerializer serializer)
        {
            _stores = stores ?? throw new ArgumentNullException(nameof(stores));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

            foreach (SettingScope scope in Enum.GetValues(typeof(SettingScope)))
            {
                if (!_stores.ContainsKey(scope) || _stores[scope] == null)
                {
                    throw new ArgumentException("A setting store is required for scope: " + scope, nameof(stores));
                }
            }
        }

        public event EventHandler<SettingChangedEventArgs> Changed;

        public void Register(SettingDefinitionBase definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            lock (_syncRoot)
            {
                if (_definitions.TryGetValue(definition.Key, out var existing))
                {
                    if (!ReferenceEquals(existing, definition))
                    {
                        throw new InvalidOperationException("Duplicate setting key: " + definition.Key);
                    }

                    return;
                }

                _definitions.Add(definition.Key, definition);
                if (_loadedScopes.Contains(definition.Scope))
                {
                    _cachedValues[definition.Key] =
                        LoadValue(definition, _stores[definition.Scope].LoadAll());
                }
            }
        }

        public IReadOnlyList<SettingDefinitionBase> GetDefinitions(SettingScope scope)
        {
            lock (_syncRoot)
            {
                EnsureScopeLoaded(scope);
                return _definitions.Values
                    .Where(definition => definition.Scope == scope)
                    .OrderBy(definition => definition.LocalizationScope, StringComparer.Ordinal)
                    .ThenBy(definition => definition.SectionKey, StringComparer.Ordinal)
                    .ThenBy(definition => definition.Order)
                    .ThenBy(definition => definition.Key, StringComparer.Ordinal)
                    .ToArray();
            }
        }

        public void Preload(SettingScope scope)
        {
            lock (_syncRoot)
            {
                EnsureScopeLoaded(scope);
            }
        }

        public T Get<T>(SettingDefinition<T> definition)
        {
            return (T)Get((SettingDefinitionBase)definition);
        }

        public object Get(SettingDefinitionBase definition)
        {
            lock (_syncRoot)
            {
                EnsureRegistered(definition);
                EnsureScopeLoaded(definition.Scope);
                EnsureCachedValue(definition);
                return _cachedValues[definition.Key];
            }
        }

        public void Set<T>(SettingDefinition<T> definition, T value, bool saveImmediately = true)
        {
            Set((SettingDefinitionBase)definition, value, saveImmediately);
        }

        public void Set(SettingDefinitionBase definition, object value, bool saveImmediately = true)
        {
            var changed = false;
            lock (_syncRoot)
            {
                EnsureRegistered(definition);
                EnsureScopeLoaded(definition.Scope);

                var validation = definition.Validate(value);
                if (!validation.IsValid)
                {
                    throw new InvalidOperationException(validation.Message);
                }

                if (_cachedValues.TryGetValue(definition.Key, out var currentValue) &&
                    Equals(currentValue, value))
                {
                    if (saveImmediately && _dirtyScopes.Contains(definition.Scope))
                    {
                        SaveScope(definition.Scope);
                    }

                    return;
                }

                _cachedValues[definition.Key] = value;
                _dirtyScopes.Add(definition.Scope);
                changed = true;

                if (saveImmediately)
                {
                    SaveScope(definition.Scope);
                }
            }

            if (changed)
            {
                Changed?.Invoke(this, new SettingChangedEventArgs(definition, value));
            }
        }

        public void Save(SettingScope? scope = null)
        {
            lock (_syncRoot)
            {
                if (scope.HasValue)
                {
                    SaveScope(scope.Value);
                    return;
                }

                foreach (var dirtyScope in _dirtyScopes.ToArray())
                {
                    SaveScope(dirtyScope);
                }
            }
        }

        private void SaveScope(SettingScope scope)
        {
            EnsureScopeLoaded(scope);

            var values = _definitions.Values
                .Where(definition => definition.Scope == scope)
                .ToDictionary(
                    definition => definition.Key,
                    definition => _serializer.Serialize(
                        definition.ValueType,
                        GetCachedValue(definition)));

            _stores[scope].SaveAll(values);
            _dirtyScopes.Remove(scope);
        }

        private void EnsureScopeLoaded(SettingScope scope)
        {
            if (_loadedScopes.Contains(scope))
            {
                return;
            }

            var persisted = _stores[scope].LoadAll();
            foreach (var definition in _definitions.Values.Where(definition => definition.Scope == scope))
            {
                _cachedValues[definition.Key] = LoadValue(definition, persisted);
            }

            _loadedScopes.Add(scope);
        }

        private object LoadValue(
            SettingDefinitionBase definition,
            IReadOnlyDictionary<string, string> persisted)
        {
            object value = definition.DefaultValue;
            if (persisted.TryGetValue(definition.Key, out var rawValue) &&
                _serializer.TryDeserialize(definition.ValueType, rawValue, out var deserialized))
            {
                value = deserialized;
            }

            var validation = definition.Validate(value);
            return validation.IsValid ? value : definition.DefaultValue;
        }

        private void EnsureRegistered(SettingDefinitionBase definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (_definitions.TryGetValue(definition.Key, out var existing))
            {
                if (!ReferenceEquals(existing, definition))
                {
                    throw new InvalidOperationException("Duplicate setting key: " + definition.Key);
                }

                return;
            }

            Register(definition);
        }

        private object GetCachedValue(SettingDefinitionBase definition)
        {
            EnsureCachedValue(definition);
            return _cachedValues[definition.Key];
        }

        private void EnsureCachedValue(SettingDefinitionBase definition)
        {
            if (_cachedValues.ContainsKey(definition.Key))
            {
                return;
            }

            _cachedValues[definition.Key] =
                LoadValue(definition, _stores[definition.Scope].LoadAll());
            _loadedScopes.Add(definition.Scope);
        }
    }
}
