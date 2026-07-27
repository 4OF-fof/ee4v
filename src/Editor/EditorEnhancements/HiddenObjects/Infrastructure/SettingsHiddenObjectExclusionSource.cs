using System;
using Ee4v.Core.Settings;

namespace Ee4v.HiddenObjects
{
    internal sealed class SettingsHiddenObjectExclusionSource
        : IHiddenObjectExclusionSource
    {
        private readonly ISettingsService _settings;

        public SettingsHiddenObjectExclusionSource(
            ISettingsService settings)
        {
            _settings = settings ??
                throw new ArgumentNullException(nameof(settings));
        }

        public HiddenObjectExclusionRules Load()
        {
            return new HiddenObjectExclusionRules(
                HiddenObjectExclusionPolicy.ParsePatterns(
                    _settings.Get(
                        HiddenObjectsDefinitions
                            .ExcludedScenePatterns)),
                HiddenObjectExclusionPolicy.ParsePatterns(
                    _settings.Get(
                        HiddenObjectsDefinitions
                            .ExcludedObjectPatterns)));
        }
    }
}
