using System;
using Ee4v.Core.Background;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class StatusOverlayCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 11; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/UI/Components/Overlays/StatusOverlay/status-overlay.uss");
                registry.RegisterStory(new StoryRegistration(
                    "status-overlay",
                    "Overlays",
                    "Status Overlay",
                    "background activity が存在する間、window右下にspinnerと状態を表示します。",
                    "IBackgroundActivityTrackerの状態だけを描画し、同期処理そのものには依存しない汎用overlayです。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildStatusOverlayStory(parent)));
            }
        }

        private void BuildStatusOverlayStory(VisualElement parent)
        {
            IDisposable activity = null;
            var controls = CreatePlainControlsSection(parent, "activityを開始するとCatalog window右下にStatus Overlayを表示します。");
            var start = new Button(() =>
            {
                activity?.Dispose();
                activity = CoreBackgroundActivities.Current.Begin(
                    "Synchronizing library...");
                BackgroundStatusOverlayApi.EnsureHost(this);
            }) { text = "Start" };
            var stop = new Button(() =>
            {
                activity?.Dispose();
                activity = null;
            }) { text = "Stop" };
            controls.Content.Add(start);
            controls.Content.Add(stop);
            BackgroundStatusOverlayApi.EnsureHost(this);
            FinalizeControlsSection(parent, controls);
        }
    }
}
