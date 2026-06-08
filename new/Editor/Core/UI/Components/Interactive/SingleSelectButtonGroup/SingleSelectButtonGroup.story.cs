using System;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class SingleSelectButtonGroupCatalogRegistrar : ICatalogRegistrar
        {
            public int Order
            {
                get { return 10; }
            }

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/Core/UI/Components/Interactive/SingleSelectButtonGroup/single-select-button-group.uss");
                registry.RegisterStory(new StoryRegistration(
                    "single-select-button-group",
                    "Interactive",
                    "SingleSelectButtonGroup",
                    "縦並びの button 群から 1 件だけを選ぶ、単一選択向けコンポーネントです。",
                    "old AssetManager navigation のように、カテゴリやモードをリストから 1 つ選ぶ用途を想定しています。選択中 item は面色で強調し、他 item と同じ button 操作で切り替えます。",
                    new[]
                    {
                        "Icon"
                    },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildSingleSelectButtonGroupStory(parent)));
            }
        }

        private void BuildSingleSelectButtonGroupStory(VisualElement parent)
        {
            var firstLabel = "All Assets";
            var firstMeta = "24";
            var firstIcon = SingleSelectButtonGroupStoryIconOption.Search;
            var secondLabel = "Booth Items";
            var secondMeta = "12";
            var secondIcon = SingleSelectButtonGroupStoryIconOption.DisclosureClosed;
            var thirdLabel = "Trash";
            var thirdMeta = "3";
            var thirdIcon = SingleSelectButtonGroupStoryIconOption.None;
            var thirdEnabled = true;
            var selectedItemId = "all";
            Action refresh = null;

            var controls = CreatePlainControlsSection(parent, "old AssetManager navigation のような、縦並びの単一選択 button 群を確認します。");
            AddTextField(controls.Content, "項目1", firstLabel, value =>
            {
                firstLabel = value;
                refresh();
            });
            AddTextField(controls.Content, "項目1 Meta", firstMeta, value =>
            {
                firstMeta = value;
                refresh();
            });
            var firstIconField = AddEnumField(controls.Content, "項目1 Icon", firstIcon, value =>
            {
                firstIcon = value;
                refresh();
            });
            AddTextField(controls.Content, "項目2", secondLabel, value =>
            {
                secondLabel = value;
                refresh();
            });
            AddTextField(controls.Content, "項目2 Meta", secondMeta, value =>
            {
                secondMeta = value;
                refresh();
            });
            var secondIconField = AddEnumField(controls.Content, "項目2 Icon", secondIcon, value =>
            {
                secondIcon = value;
                refresh();
            });
            AddTextField(controls.Content, "項目3", thirdLabel, value =>
            {
                thirdLabel = value;
                refresh();
            });
            AddTextField(controls.Content, "項目3 Meta", thirdMeta, value =>
            {
                thirdMeta = value;
                refresh();
            });
            var thirdIconField = AddEnumField(controls.Content, "項目3 Icon", thirdIcon, value =>
            {
                thirdIcon = value;
                refresh();
            });

            var thirdEnabledToggle = new Toggle("項目3 Enabled")
            {
                value = thirdEnabled
            };
            thirdEnabledToggle.RegisterValueChangedCallback(evt =>
            {
                thirdEnabled = evt.newValue;
                refresh();
            });
            controls.Content.Add(thirdEnabledToggle);

            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface(true);
            surface.style.width = 240f;
            var group = new SingleSelectButtonGroup();
            surface.Add(group);
            preview.Body.Add(surface);

            var selectedCard = new InfoCard();
            preview.Body.Add(selectedCard);

            refresh = () =>
            {
                thirdEnabledToggle.SetValueWithoutNotify(thirdEnabled);
                firstIconField.SetValueWithoutNotify((Enum)(object)firstIcon);
                secondIconField.SetValueWithoutNotify((Enum)(object)secondIcon);
                thirdIconField.SetValueWithoutNotify((Enum)(object)thirdIcon);
                group.SetState(
                    new SingleSelectButtonGroupState(
                        new[]
                        {
                            new SingleSelectButtonGroupItemState("all", firstLabel, firstMeta, iconState: CreateSingleSelectButtonGroupStoryIcon(firstIcon)),
                            new SingleSelectButtonGroupItemState("booth", secondLabel, secondMeta, iconState: CreateSingleSelectButtonGroupStoryIcon(secondIcon)),
                            new SingleSelectButtonGroupItemState("trash", thirdLabel, thirdMeta, thirdEnabled, CreateSingleSelectButtonGroupStoryIcon(thirdIcon))
                        },
                        selectedItemId),
                    id =>
                    {
                        selectedItemId = id;
                        refresh();
                    });

                selectedCard.SetState(new InfoCardState(
                    "Current Selection",
                    string.IsNullOrWhiteSpace(selectedItemId) ? "未選択" : selectedItemId,
                    "State"));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }

        private static IconState CreateSingleSelectButtonGroupStoryIcon(SingleSelectButtonGroupStoryIconOption option)
        {
            switch (option)
            {
                case SingleSelectButtonGroupStoryIconOption.Search:
                    return IconState.FromBuiltinIcon(UiBuiltinIcon.Search, size: 12f);
                case SingleSelectButtonGroupStoryIconOption.Close:
                    return IconState.FromBuiltinIcon(UiBuiltinIcon.Close, size: 12f);
                case SingleSelectButtonGroupStoryIconOption.DisclosureClosed:
                    return IconState.FromBuiltinIcon(UiBuiltinIcon.DisclosureClosed, size: 12f);
                case SingleSelectButtonGroupStoryIconOption.DisclosureOpen:
                    return IconState.FromBuiltinIcon(UiBuiltinIcon.DisclosureOpen, size: 12f);
                default:
                    return null;
            }
        }
    }
}
