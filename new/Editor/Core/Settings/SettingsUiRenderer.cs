using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Core.Settings
{
    internal static class SettingsUiRenderer
    {
        private static readonly Dictionary<string, string> ValidationMessages = new Dictionary<string, string>();

        public static void DrawScope(SettingScope scope, string searchContext)
        {
            var definitions = SettingApi.GetDefinitions(scope);
            var grouped = definitions
                .GroupBy(definition => GetGroupKey(definition))
                .OrderBy(group => group.Key, StringComparer.Ordinal);

            foreach (var group in grouped)
            {
                var visibleDefinitions = group.Where(definition => MatchesSearch(definition, searchContext)).ToArray();
                if (visibleDefinitions.Length == 0)
                {
                    continue;
                }

                var firstDefinition = visibleDefinitions[0];
                EditorGUILayout.LabelField(Translate(firstDefinition.SectionKey, firstDefinition.LocalizationScope), EditorStyles.boldLabel);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    for (var i = 0; i < visibleDefinitions.Length; i++)
                    {
                        DrawDefinition(visibleDefinitions[i], searchContext);
                    }
                }

                EditorGUILayout.Space(8f);
            }
        }

        private static void DrawDefinition(SettingDefinitionBase definition, string searchContext)
        {
            var tooltip = string.Empty;
            string translatedTooltip;
            if (I18N.TryGetForScope(definition.LocalizationScope, definition.DescriptionKey, out translatedTooltip))
            {
                tooltip = translatedTooltip;
            }

            var label = new GUIContent(Translate(definition.DisplayNameKey, definition.LocalizationScope), tooltip);
            var currentValue = SettingApi.GetBoxed(definition);

            EditorGUI.BeginChangeCheck();
            var newValue = definition.DrawField(label, currentValue, searchContext);
            if (EditorGUI.EndChangeCheck())
            {
                var validation = definition.ValidateBoxed(newValue);
                if (validation.IsValid)
                {
                    ValidationMessages.Remove(definition.Key);
                    SettingApi.SetBoxed(definition, newValue);
                }
                else
                {
                    ValidationMessages[definition.Key] = validation.Message;
                }
            }

            string error;
            if (ValidationMessages.TryGetValue(definition.Key, out error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
        }

        private static bool MatchesSearch(SettingDefinitionBase definition, string searchContext)
        {
            if (string.IsNullOrWhiteSpace(searchContext))
            {
                return true;
            }

            var needle = searchContext.Trim();
            if (ContainsIgnoreCase(Translate(definition.DisplayNameKey, definition.LocalizationScope), needle) ||
                ContainsIgnoreCase(Translate(definition.SectionKey, definition.LocalizationScope), needle))
            {
                return true;
            }

            return definition.Keywords.Any(keyword => ContainsIgnoreCase(keyword, needle));
        }

        private static bool ContainsIgnoreCase(string source, string needle)
        {
            return !string.IsNullOrEmpty(source) &&
                   source.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetGroupKey(SettingDefinitionBase definition)
        {
            return definition.LocalizationScope + "|" + definition.SectionKey;
        }

        private static string Translate(string key, string localizationScope)
        {
            return string.IsNullOrEmpty(key) ? string.Empty : I18N.GetForScope(localizationScope, key);
        }
    }
}
