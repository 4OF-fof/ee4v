using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private void BuildSearchFieldStory(VisualElement parent)
        {
            var value = string.Empty;
            var placeholder = "suite 名、説明、テスト名で検索";
            Action refresh = null;

            var controls = CreatePlainControlsSection(parent, "placeholder と入力値を変えながら、一覧絞り込み用の単体 search field を確認します。");
            var valueField = AddTextField(controls.Content, "値", value, nextValue =>
            {
                value = nextValue;
                refresh();
            });
            var placeholderField = AddTextField(controls.Content, "Placeholder", placeholder, nextValue =>
            {
                placeholder = nextValue;
                refresh();
            });

            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface(true);
            var searchField = new SearchField();
            surface.Add(searchField);
            preview.Body.Add(surface);

            refresh = () =>
            {
                valueField.SetValueWithoutNotify(value);
                placeholderField.SetValueWithoutNotify(placeholder);
                searchField.SetState(new SearchFieldState(value, placeholder));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }

        private void BuildTabCardStory(VisualElement parent)
        {
            var firstLabel = "基本";
            var secondLabel = "詳細";
            var thirdLabel = "空状態";
            var selectedTabId = "basic";
            Action refresh = null;

            var controls = CreatePlainControlsSection(parent, "タブ名と、選択中タブで表示する内容を編集します。");
            AddTextField(controls.Content, "タブ1", firstLabel, value =>
            {
                firstLabel = value;
                refresh();
            });
            AddTextField(controls.Content, "タブ2", secondLabel, value =>
            {
                secondLabel = value;
                refresh();
            });
            AddTextField(controls.Content, "タブ3", thirdLabel, value =>
            {
                thirdLabel = value;
                refresh();
            });

            var preview = CreatePreviewSection(parent);
            var tabCard = new TabCard();
            preview.Body.Add(tabCard);

            refresh = () =>
            {
                tabCard.SetState(
                    new TabCardState(
                        new[]
                        {
                            new TabCardTabState("basic", firstLabel),
                            new TabCardTabState("detail", secondLabel),
                            new TabCardTabState("empty", thirdLabel)
                        },
                        selectedTabId),
                    id =>
                    {
                        selectedTabId = id;
                        refresh();
                    });

                tabCard.Content.Clear();
                var previewCard = new InfoCard(new InfoCardState(
                    selectedTabId == "basic" ? "基本表示" : selectedTabId == "detail" ? "詳細表示" : "空状態表示",
                    selectedTabId == "basic"
                        ? "タブ切り替え後の content slot に任意の UI を配置できます。"
                        : selectedTabId == "detail"
                            ? "複数のフォーム、説明文、ステータスなどを任意に構成できます。"
                            : "コンポーネント未選択時やデータ空状態の panel としても使えます。",
                    null,
                    selectedTabId.ToUpperInvariant()));
                previewCard.AddToClassList("ee4v-ui-catalog-preview-card--flush");
                tabCard.Content.Add(previewCard);
            };

            refresh();
            FinalizeControlsSection(parent, controls);
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
