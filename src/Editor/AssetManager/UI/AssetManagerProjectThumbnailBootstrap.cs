using Ee4v.Core.Injector;
using UnityEditor;
using UnityEngine;

namespace Ee4v.AssetManager.UI
{
    [InitializeOnLoad]
    internal static class AssetManagerProjectThumbnailBootstrap
    {
        private static AssetManagerProjectThumbnailPresenter _presenter;

        static AssetManagerProjectThumbnailBootstrap()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            InjectorApi.Register(
                new ItemInjectionRegistration(
                    "asset-manager.project-thumbnails",
                    InjectionChannel.ProjectItem,
                    context => _presenter?.Draw(context),
                    priority: -100,
                    isEnabled: () => AssetManagerSettings
                        .ShowProjectWindowIconsEnabled));
            AssetManagerWindowSession.ManagerInvalidated +=
                RefreshPresenter;
            AssetManagerSettings.ProjectWindowIconsChanged +=
                RefreshPresenter;
            RefreshPresenter();
        }

        private static void RefreshPresenter()
        {
            _presenter?.Dispose();
            _presenter = null;
            if (!AssetManagerSettings.ShowProjectWindowIconsEnabled)
            {
                RepaintProjectWindow();
                return;
            }

            _presenter = new AssetManagerProjectThumbnailPresenter(
                AssetManagerWindowSession.GetManager());
            RepaintProjectWindow();
        }

        private static void RepaintProjectWindow()
        {
            InjectorApi.Repaint(InjectionChannel.ProjectItem);
        }
    }
}
