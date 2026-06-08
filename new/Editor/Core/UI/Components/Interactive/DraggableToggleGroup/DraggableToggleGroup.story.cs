using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class DraggableToggleGroupCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 12; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/Core/UI/Components/Interactive/DraggableToggleGroup/draggable-toggle-group.uss");
                registry.RegisterStory(new StoryRegistration(
                    "draggable-toggle-group",
                    "Interactive",
                    "DraggableToggleGroup",
                    "ドラッグで複数項目をなぞって ON/OFF できる toggle 群です。",
                    "クリックした項目の反転後の値をドラッグ範囲に入った項目へ適用します。ラベルやタグなど、連続した boolean 値を素早く編集する用途を想定しています。",
                    Array.Empty<string>(),
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildDraggableToggleGroupStory(parent)));
            }
        }

        private void BuildDraggableToggleGroupStory(VisualElement parent)
        {
            var enabled = true;
            var itemCount = 12;
            var values = Enumerable.Range(0, itemCount)
                .ToDictionary(index => "item-" + index, index => index % 3 == 0, StringComparer.Ordinal);
            Action refresh = null;

            var controls = CreatePlainControlsSection(parent, "ドラッグ開始した toggle の反転値を、ドラッグ範囲に重なった toggle へ適用します。");
            var enabledToggle = new Toggle("Enabled")
            {
                value = enabled
            };
            enabledToggle.RegisterValueChangedCallback(evt =>
            {
                enabled = evt.newValue;
                refresh();
            });
            controls.Content.Add(enabledToggle);

            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface(true);
            surface.style.width = 240f;
            var group = new DraggableToggleGroup();
            surface.Add(group);
            preview.Body.Add(surface);

            var selectedCard = new InfoCard();
            preview.Body.Add(selectedCard);

            refresh = () =>
            {
                enabledToggle.SetValueWithoutNotify(enabled);
                group.SetState(
                    new DraggableToggleGroupState(
                        Enumerable.Range(0, itemCount)
                            .Select(index =>
                            {
                                var id = "item-" + index;
                                return new DraggableToggleItemState(id, "Option " + (index + 1), values[id], index != 10);
                            })
                            .ToArray(),
                        enabled),
                    (id, value) =>
                    {
                        values[id] = value;
                        selectedCard.SetState(CreateDraggableToggleGroupResult(values));
                    });

                selectedCard.SetState(CreateDraggableToggleGroupResult(values));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }

        private static InfoCardState CreateDraggableToggleGroupResult(IReadOnlyDictionary<string, bool> values)
        {
            var activeItems = values
                .Where(pair => pair.Value)
                .Select(pair => pair.Key)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            return new InfoCardState(
                "Current Values",
                activeItems.Length == 0 ? "ON: none" : "ON: " + string.Join(", ", activeItems),
                "State");
        }
    }
}
