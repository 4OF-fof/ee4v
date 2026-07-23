using System;
using Ee4v.AssetManager.Infrastructure;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.Settings;

namespace Ee4v.AssetManager.Composition
{
    internal sealed class AssetManagerUiPreferencesAdapter : IAssetManagerUiPreferences
    {
        internal AssetManagerUiPreferencesAdapter()
        {
            SettingApi.Changed += OnSettingChanged;
        }

        public event Action<AssetManagerUiPreference> Changed;

        public int ItemsPerRow
        {
            get { return SettingApi.Get(AssetManagerDefinitions.ItemGridItemsPerRow); }
            set { SettingApi.Set(AssetManagerDefinitions.ItemGridItemsPerRow, value); }
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
                    SettingApi.Get(AssetManagerDefinitions.HistoryOverlayMaximumItems)));

        public bool ShowFileTreeImageTooltip =>
            SettingApi.Get(AssetManagerDefinitions.ShowFileTreeImageTooltip);

        public void Preload()
        {
            SettingApi.Preload(SettingScope.User);
        }

        private void OnSettingChanged(SettingDefinitionBase definition, object value)
        {
            if (ReferenceEquals(definition, AssetManagerDefinitions.ItemGridItemsPerRow))
            {
                Changed?.Invoke(AssetManagerUiPreference.ItemsPerRow);
            }
            else if (ReferenceEquals(
                         definition,
                         AssetManagerDefinitions.HistoryOverlayMaximumItems))
            {
                Changed?.Invoke(AssetManagerUiPreference.HistoryOverlayMaximumItems);
            }
            else if (ReferenceEquals(
                         definition,
                         AssetManagerDefinitions.ShowFileTreeImageTooltip))
            {
                Changed?.Invoke(AssetManagerUiPreference.ShowFileTreeImageTooltip);
            }
        }
    }
}
