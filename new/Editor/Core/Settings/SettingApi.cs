using System;
using System.Collections.Generic;
using System.Linq;

namespace Ee4v.Core.Settings
{
    public static class SettingApi
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, SettingDefinitionBase> Definitions = new Dictionary<string, SettingDefinitionBase>();
        private static readonly Dictionary<string, object> CachedValues = new Dictionary<string, object>();
        private static readonly HashSet<SettingScope> LoadedScopes = new HashSet<SettingScope>();
        private static readonly HashSet<SettingScope> DirtyScopes = new HashSet<SettingScope>();
        private static readonly Dictionary<SettingScope, ISettingStore> Stores = new Dictionary<SettingScope, ISettingStore>
        {
            { SettingScope.User, new EditorPrefsSettingStore() },
            { SettingScope.Project, new ProjectFileSettingStore() }
        };

        public static event Action<SettingDefinitionBase, object> Changed;

        public static void Register(SettingDefinitionBase definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            lock (SyncRoot)
            {
                SettingDefinitionBase existing;
                if (Definitions.TryGetValue(definition.Key, out existing))
                {
                    if (!ReferenceEquals(existing, definition))
                    {
                        throw new InvalidOperationException("Duplicate setting key: " + definition.Key);
                    }

                    return;
                }

                Definitions.Add(definition.Key, definition);

                if (LoadedScopes.Contains(definition.Scope))
                {
                    CachedValues[definition.Key] = LoadValue(definition, Stores[definition.Scope].LoadAll());
                }
            }
        }

        public static IReadOnlyList<SettingDefinitionBase> GetDefinitions(SettingScope scope)
        {
            lock (SyncRoot)
            {
                EnsureScopeLoaded(scope);
                return Definitions.Values
                    .Where(definition => definition.Scope == scope)
                    .OrderBy(definition => definition.SectionKey, StringComparer.Ordinal)
                    .ThenBy(definition => definition.Order)
                    .ThenBy(definition => definition.Key, StringComparer.Ordinal)
                    .ToArray();
            }
        }

        public static void Preload(SettingScope scope)
        {
            lock (SyncRoot)
            {
                EnsureScopeLoaded(scope);
            }
        }

        public static T Get<T>(SettingDefinition<T> definition)
        {
            return (T)GetBoxed(definition);
        }

        public static object GetBoxed(SettingDefinitionBase definition)
        {
            lock (SyncRoot)
            {
                EnsureRegistered(definition);
                EnsureScopeLoaded(definition.Scope);
                EnsureCachedValue(definition);
                return CachedValues[definition.Key];
            }
        }

        public static void Set<T>(SettingDefinition<T> definition, T value, bool saveImmediately = true)
        {
            SetBoxed(definition, value, saveImmediately);
        }

        public static void SetBoxed(SettingDefinitionBase definition, object value, bool saveImmediately = true)
        {
            var changed = false;
            lock (SyncRoot)
            {
                EnsureRegistered(definition);
                EnsureScopeLoaded(definition.Scope);

                var validation = definition.ValidateBoxed(value);
                if (!validation.IsValid)
                {
                    throw new InvalidOperationException(validation.Message);
                }

                object currentValue;
                if (CachedValues.TryGetValue(definition.Key, out currentValue) &&
                    Equals(currentValue, value))
                {
                    if (saveImmediately && DirtyScopes.Contains(definition.Scope))
                    {
                        SaveScope(definition.Scope);
                    }

                    return;
                }

                CachedValues[definition.Key] = value;
                DirtyScopes.Add(definition.Scope);
                changed = true;

                if (saveImmediately)
                {
                    SaveScope(definition.Scope);
                }
            }

            var handler = Changed;
            if (changed && handler != null)
            {
                handler(definition, value);
            }
        }

        public static void Save(SettingScope? scope = null)
        {
            lock (SyncRoot)
            {
                if (scope.HasValue)
                {
                    SaveScope(scope.Value);
                    return;
                }

                var scopes = DirtyScopes.ToArray();
                for (var i = 0; i < scopes.Length; i++)
                {
                    SaveScope(scopes[i]);
                }
            }
        }

        private static void SaveScope(SettingScope scope)
        {
            EnsureScopeLoaded(scope);

            var values = Definitions.Values
                .Where(definition => definition.Scope == scope)
                .ToDictionary(
                    definition => definition.Key,
                    definition => definition.SerializeBoxed(GetCachedValue(definition)));

            Stores[scope].SaveAll(values);
            DirtyScopes.Remove(scope);
        }

        private static void EnsureScopeLoaded(SettingScope scope)
        {
            if (LoadedScopes.Contains(scope))
            {
                return;
            }

            var persisted = Stores[scope].LoadAll();
            foreach (var definition in Definitions.Values.Where(definition => definition.Scope == scope))
            {
                CachedValues[definition.Key] = LoadValue(definition, persisted);
            }

            LoadedScopes.Add(scope);
        }

        private static object LoadValue(SettingDefinitionBase definition, IReadOnlyDictionary<string, string> persisted)
        {
            string rawValue;
            var value = persisted.TryGetValue(definition.Key, out rawValue)
                ? definition.DeserializeBoxed(rawValue)
                : definition.BoxedDefaultValue;

            var validation = definition.ValidateBoxed(value);
            return validation.IsValid ? value : definition.BoxedDefaultValue;
        }

        private static void EnsureRegistered(SettingDefinitionBase definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            SettingDefinitionBase existing;
            if (Definitions.TryGetValue(definition.Key, out existing))
            {
                if (!ReferenceEquals(existing, definition))
                {
                    throw new InvalidOperationException("Duplicate setting key: " + definition.Key);
                }

                return;
            }

            Register(definition);
        }

        private static object GetCachedValue(SettingDefinitionBase definition)
        {
            EnsureCachedValue(definition);
            return CachedValues[definition.Key];
        }

        private static void EnsureCachedValue(SettingDefinitionBase definition)
        {
            if (CachedValues.ContainsKey(definition.Key))
            {
                return;
            }

            var persisted = Stores[definition.Scope].LoadAll();
            CachedValues[definition.Key] = LoadValue(definition, persisted);
            LoadedScopes.Add(definition.Scope);
        }
    }
}
