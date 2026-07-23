using System;
using Ee4v.AssetManager.Infrastructure;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.Settings;

namespace Ee4v.AssetManager.Composition
{
    internal sealed class AssetManagerUiPreferencesAdapter : IAssetManagerUiPreferences
    {
        private readonly ISettingsService _settings;

        internal AssetManagerUiPreferencesAdapter(ISettingsService settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _settings.Changed += OnSettingChanged;
        }

        public event Action<AssetManagerUiPreference> Changed;

        public int ItemsPerRow
        {
            get { return _settings.Get(AssetManagerDefinitions.ItemGridItemsPerRow); }
            set { _settings.Set(AssetManagerDefinitions.ItemGridItemsPerRow, value); }
        }

        public int MinimumItemsPerRow =>
            AssetManagerDefinitions.ItemGridItemsPerRow.Range.Minimum;

        public int MaximumItemsPerRow =>
            AssetManagerDefinitions.ItemGridItemsPerRow.Range.Maximum;

        public int HistoryOverlayMaximumItems =>
            Math.Min(
                20,
                Math.Max(
                    1,
                    _settings.Get(AssetManagerDefinitions.HistoryOverlayMaximumItems)));

        public bool ShowFileTreeImageTooltip =>
            _settings.Get(AssetManagerDefinitions.ShowFileTreeImageTooltip);

        public void Preload()
        {
            _settings.Preload(SettingScope.User);
        }

        private void OnSettingChanged(object sender, SettingChangedEventArgs args)
        {
            if (ReferenceEquals(args.Definition, AssetManagerDefinitions.ItemGridItemsPerRow))
            {
                Changed?.Invoke(AssetManagerUiPreference.ItemsPerRow);
            }
            else if (ReferenceEquals(
                         args.Definition,
                         AssetManagerDefinitions.HistoryOverlayMaximumItems))
            {
                Changed?.Invoke(AssetManagerUiPreference.HistoryOverlayMaximumItems);
            }
            else if (ReferenceEquals(
                         args.Definition,
                         AssetManagerDefinitions.ShowFileTreeImageTooltip))
            {
                Changed?.Invoke(AssetManagerUiPreference.ShowFileTreeImageTooltip);
            }
        }
    }
}
