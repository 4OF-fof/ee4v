using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class ContextMenuWindowCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 10; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/Core/UI/Components/Overlays/ContextMenuWindow/context-menu-window.uss");
                registry.RegisterStory(new StoryRegistration(
                    "context-menu-window",
                    "Overlays",
                    "ContextMenuWindow",
                    "old AssetManager の GenericDropdownMenu に近い見た目を UI Toolkit と USS で再現したコンテキストメニューWindowです。",
                    "target VisualElement と panel/world position を渡して開きます。項目、区切り、disabled、icon、shortcut、選択 callback を扱い、幅は項目テキストを測定して決めます。",
                    new[]
                    {
                        "Icon"
                    },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildContextMenuWindowStory(parent)));
            }
        }

        private void BuildContextMenuWindowStory(VisualElement parent)
        {
            var lastAction = "未選択";
            var includeDisabledItem = true;
            Action refresh = null;

            Func<ContextMenuState> createMenuState = () => new ContextMenuState(
                new[]
                {
                    new ContextMenuItemState(
                        "open",
                        "Open",
                        () =>
                        {
                            lastAction = "Open";
                            refresh();
                        },
                        iconState: IconState.FromBuiltinIcon(UiBuiltinIcon.Search, size: 10f),
                        shortcut: "Enter"),
                    new ContextMenuItemState(
                        "rename",
                        "Rename",
                        () =>
                        {
                            lastAction = "Rename";
                            refresh();
                        },
                        shortcut: "F2"),
                    ContextMenuItemState.Separator(),
                    new ContextMenuItemState(
                        "disabled",
                        "Disabled Action",
                        () =>
                        {
                            lastAction = "Disabled Action";
                            refresh();
                        },
                        enabled: !includeDisabledItem),
                    new ContextMenuItemState(
                        "delete",
                        "Delete",
                        () =>
                        {
                            lastAction = "Delete";
                            refresh();
                        },
                        iconState: IconState.FromBuiltinIcon(UiBuiltinIcon.Close, size: 10f),
                        shortcut: "Del")
                });

            var controls = CreatePlainControlsSection(parent, "button click または preview surface の右クリックから、UI Toolkit 製の menu window を開きます。");
            var disabledToggle = new Toggle("Disabled Action を無効にする")
            {
                value = includeDisabledItem
            };
            disabledToggle.RegisterValueChangedCallback(evt =>
            {
                includeDisabledItem = evt.newValue;
                refresh();
            });
            controls.Content.Add(disabledToggle);

            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface(true);
            surface.style.width = 320f;
            surface.style.minHeight = 112f;
            surface.style.alignItems = Align.FlexStart;

            var openButton = new Button();
            openButton.text = "Open Context Menu";
            openButton.clicked += () =>
            {
                var panelPosition = openButton.worldBound.position + new Vector2(0f, openButton.worldBound.height);
                ContextMenuWindow.Show(openButton, panelPosition, createMenuState());
            };

            var hint = UiTextFactory.Create("この面を右クリックしても開きます。");
            hint.SetWhiteSpace(WhiteSpace.Normal);

            surface.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.button != 1)
                {
                    return;
                }

                evt.StopPropagation();
                ContextMenuWindow.Show(surface, surface.LocalToWorld(evt.localPosition), createMenuState());
            });

            surface.Add(openButton);
            surface.Add(hint);
            preview.Body.Add(surface);

            var selectedCard = new InfoCard();
            preview.Body.Add(selectedCard);

            refresh = () =>
            {
                disabledToggle.SetValueWithoutNotify(includeDisabledItem);
                selectedCard.SetState(new InfoCardState("Last Action", lastAction, "ContextMenuWindow"));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }
    }
}
