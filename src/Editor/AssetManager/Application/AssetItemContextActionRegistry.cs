using System;
using System.Collections.Generic;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.Application
{
    internal sealed class AssetItemContextActionRegistry :
        IAssetItemContextActionRegistry
    {
        private readonly List<IAssetItemContextActionProvider> _providers =
            new List<IAssetItemContextActionProvider>();

        public IDisposable Register(IAssetItemContextActionProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            _providers.Add(provider);
            return new Registration(_providers, provider);
        }

        public IReadOnlyList<AssetItemContextAction> CreateActions(
            AssetItemContextActionRequest request)
        {
            var actions = new List<AssetItemContextAction>();
            for (var i = 0; i < _providers.Count; i++)
            {
                if (_providers[i].TryCreate(request, out var action) &&
                    action != null)
                {
                    actions.Add(action);
                }
            }

            return actions;
        }

        private sealed class Registration : IDisposable
        {
            private List<IAssetItemContextActionProvider> _providers;
            private IAssetItemContextActionProvider _provider;

            internal Registration(
                List<IAssetItemContextActionProvider> providers,
                IAssetItemContextActionProvider provider)
            {
                _providers = providers;
                _provider = provider;
            }

            public void Dispose()
            {
                _providers?.Remove(_provider);
                _providers = null;
                _provider = null;
            }
        }
    }
}
