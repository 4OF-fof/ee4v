using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class DiffConfirmationOverlayCatalogRegistrar : ICatalogRegistrar
        {
            public int Order => 12;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/UI/Components/Overlays/DiffConfirmationOverlay/diff-confirmation-overlay.uss");
                registry.RegisterStory(new StoryRegistration(
                    "diff-confirmation-overlay",
                    "Overlays",
                    "Diff Confirmation Overlay",
                    "現在値と同期元の値を並べ、上書き前に確認するoverlayです。",
                    "比較内容と文言をstateで受け取り、domain logicには依存しない汎用componentです。",
                    Array.Empty<string>(),
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildDiffConfirmationOverlayStory(parent)));
            }
        }

        private void BuildDiffConfirmationOverlayStory(VisualElement parent)
        {
            var controls = CreatePlainControlsSection(parent, "Openを押すとCatalog全体にsample diffを表示します。");
            controls.Content.Add(new Button(() => DiffConfirmationOverlayApi.Show(
                this,
                new DiffConfirmationState(
                    "Synchronization conflict",
                    "The local item is newer. Review the values before overwriting it.",
                    "Current",
                    "Incoming",
                    "Overwrite",
                    "Cancel",
                    new[]
                    {
                        new DiffConfirmationItemState(
                            "Avatar Package",
                            "Eagle · local 2026-07-21 12:00 / source 2026-07-20 18:00",
                            new[]
                            {
                                new DiffConfirmationFieldState("My Avatar", "Avatar Package"),
                                new DiffConfirmationFieldState("Local notes", "Source description")
                            })
                    }),
                _ => { })) { text = "Open" });
            FinalizeControlsSection(parent, controls);
        }
    }
}
