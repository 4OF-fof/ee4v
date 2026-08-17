using System.Threading.Tasks;
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
                    "background task の実行中、window右下にspinnerと状態を表示します。",
                    "IBackgroundTaskManagerが管理するライフサイクルと進捗メッセージを描画します。",
                    new string[0],
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildStatusOverlayStory(parent),
                    new[]
                    {
                        "Editor/Core/Presentation/Background/BackgroundStatusOverlayHost.cs"
                    }));
            }
        }

        private void BuildStatusOverlayStory(VisualElement parent)
        {
            IBackgroundTaskHandle task = null;
            var controls = CreatePlainControlsSection(parent, "taskを開始するとCatalog window右下にStatus Overlayを表示します。");
            var start = UiTextFactory.CreateButton("Start", () =>
            {
                task?.Cancel();
                task = CoreBackgroundActivities.Current.Run(
                    "Synchronizing library...",
                    async context =>
                    {
                        for (var step = 1; step <= 5; step++)
                        {
                            context.CancellationToken
                                .ThrowIfCancellationRequested();
                            context.Report(
                                "Synchronizing library... " + step + "/5");
                            await Task.Delay(
                                500,
                                context.CancellationToken);
                        }
                    });
                BackgroundStatusOverlay.Attach(this);
            });
            var stop = UiTextFactory.CreateButton("Stop", () =>
            {
                task?.Cancel();
                task = null;
            });
            controls.Content.Add(start);
            controls.Content.Add(stop);
            BackgroundStatusOverlay.Attach(this);
            FinalizeControlsSection(parent, controls);
        }
    }
}
