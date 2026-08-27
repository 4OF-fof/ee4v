using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.Core.Settings
{
    internal sealed class SettingsStoryProvider : IUiStoryProvider
    {
        public int Order => 160;

        public IReadOnlyList<UiStory> GetStories()
        {
            return new[]
            {
                new UiStory(
                    "settings-ui",
                    "Domain/Core",
                    "Settings UI",
                    "Preferences と Project Settings で共用する設定画面です。",
                    "型ごとの field、section、検証表示を保存しないサンプル設定で表示します。",
                    Build,
                    dependencies: new[]
                    {
                        "InputField",
                        "UiTextFactory",
                        "SettingDrawerApi"
                    },
                    usageLocations: new[]
                    {
                        "Editor/Core/Presentation/Settings/RegisteredSettingsProviders.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/UI/Components/Inputs/ui-button.uss",
                        "Editor/UI/Components/Inputs/InputField/input-field.uss",
                        "Editor/UI/Components/Inputs/StringListField/string-list-field.uss",
                        "Editor/Core/Presentation/Settings/settings-ui.uss"
                    })
            };
        }

        private static void Build(VisualElement parent)
        {
            var settings = new StorySettingsService();
            settings.Register(new SettingDefinition<string>(
                "story.core.language",
                SettingScope.User,
                "Core",
                "settings.section.localization",
                "settings.language.label",
                "settings.language.tooltip",
                "ja-JP",
                order: 0));
            settings.Register(new SettingDefinition<string>(
                "story.core.fallbackLanguage",
                SettingScope.User,
                "Core",
                "settings.section.localization",
                "settings.fallbackLanguage.label",
                "settings.fallbackLanguage.tooltip",
                "en-US",
                order: 1));
            settings.Register(new SettingDefinition<string>(
                "story.core.assetRoot",
                SettingScope.User,
                "Core",
                "settings.section.assets",
                "settings.assetRootFolderName.label",
                "settings.assetRootFolderName.tooltip",
                "!ee4vAsset"));

            var surface = new VisualElement();
            surface.style.width = 720f;
            surface.style.height = 440f;
            parent.Add(surface);
            SettingsUiRenderer.BuildScope(
                surface,
                settings,
                SettingScope.User,
                string.Empty);
        }

        private sealed class StorySettingsService : ISettingsService
        {
            private readonly List<SettingDefinitionBase> _definitions =
                new List<SettingDefinitionBase>();
            private readonly Dictionary<string, object> _values =
                new Dictionary<string, object>(StringComparer.Ordinal);

            public event EventHandler<SettingChangedEventArgs> Changed;

            public void Register(SettingDefinitionBase definition)
            {
                if (definition == null ||
                    _definitions.Any(item => item.Key == definition.Key))
                {
                    return;
                }

                _definitions.Add(definition);
                _values[definition.Key] = definition.DefaultValue;
            }

            public IReadOnlyList<SettingDefinitionBase> GetDefinitions(
                SettingScope scope)
            {
                return _definitions
                    .Where(definition => definition.Scope == scope)
                    .ToArray();
            }

            public void Preload(SettingScope scope)
            {
            }

            public T Get<T>(SettingDefinition<T> definition)
            {
                return (T)Get((SettingDefinitionBase)definition);
            }

            public object Get(SettingDefinitionBase definition)
            {
                return _values[definition.Key];
            }

            public void Set<T>(
                SettingDefinition<T> definition,
                T value,
                bool saveImmediately = true)
            {
                Set((SettingDefinitionBase)definition, value, saveImmediately);
            }

            public void Set(
                SettingDefinitionBase definition,
                object value,
                bool saveImmediately = true)
            {
                _values[definition.Key] = value;
                Changed?.Invoke(
                    this,
                    new SettingChangedEventArgs(definition, value));
            }

            public void Save(SettingScope? scope = null)
            {
            }
        }
    }
}
