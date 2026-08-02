using System;
using Ee4v.AssetManager.Contracts;
using Ee4v.AvatarModify.Application;
using Ee4v.AvatarModify.Infrastructure.Unity;
using Ee4v.AvatarModify.UI;
using Ee4v.Core.I18n;
using Ee4v.Core.Internal;
using Ee4v.Core.Settings;

namespace Ee4v.AvatarModify.Composition
{
    public static class AvatarModifyBootstrap
    {
        private static bool _initialized;
        private static IDisposable _contextActionRegistration;

        public static void Initialize(
            IAssetManager assetManager,
            IAssetManagerAssetDerivationService derivationService,
            IAssetItemContextActionRegistry contextActions)
        {
            if (_initialized)
            {
                return;
            }

            if (assetManager == null)
            {
                throw new ArgumentNullException(nameof(assetManager));
            }

            if (derivationService == null)
            {
                throw new ArgumentNullException(nameof(derivationService));
            }

            if (contextActions == null)
            {
                throw new ArgumentNullException(nameof(contextActions));
            }

            var settings = CoreSettings.Current;
            FeatureBootstrapContract.Initialize(
                "AvatarModify",
                typeof(AvatarModifyDefinitions),
                () => AvatarModifyDefinitions.RegisterAll(settings),
                () => InitializeModule(
                    assetManager,
                    derivationService,
                    contextActions,
                    settings));
            _initialized = true;
        }

        private static void InitializeModule(
            IAssetManager assetManager,
            IAssetManagerAssetDerivationService derivationService,
            IAssetItemContextActionRegistry contextActions,
            ISettingsService settings)
        {
            var service = new AvatarModifyService(
                assetManager,
                new UnityAvatarAssetGateway(),
                new UnityAvatarVariantGateway(
                    derivationService));
            _contextActionRegistration =
                contextActions.Register(
                    new ContextActionProvider(
                        service,
                        () => settings.Get(
                            AvatarModifyDefinitions.VariantRoot)));
        }

        private sealed class ContextActionProvider :
            IAssetItemContextActionProvider
        {
            private readonly AvatarModifyService _service;
            private readonly Func<string> _getVariantRoot;

            internal ContextActionProvider(
                AvatarModifyService service,
                Func<string> getVariantRoot)
            {
                _service = service;
                _getVariantRoot = getVariantRoot;
            }

            public bool TryCreate(
                AssetItemContextActionRequest request,
                out AssetItemContextAction action)
            {
                action = null;
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.ItemId))
                {
                    return false;
                }

                var enabled =
                    _service.IsImportedItem(request.ItemId);
                action = new AssetItemContextAction(
                    "create-avatar-variant",
                    I18N.Get("context.createVariant"),
                    () => AvatarVariantCreationPopup.Open(
                        _service,
                        request.ItemId,
                        _getVariantRoot(),
                        request.ScreenX,
                        request.ScreenY),
                    enabled);
                return true;
            }
        }
    }
}
