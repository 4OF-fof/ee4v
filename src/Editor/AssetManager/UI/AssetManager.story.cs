using System.Collections.Generic;
using Ee4v.AssetManager.Contracts;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetManagerStoryProvider : IUiStoryProvider
    {
        public int Order => 200;

        public IReadOnlyList<UiStory> GetStories()
        {
            return new[]
            {
                new UiStory(
                    "asset-manager-view",
                    "Domain/AssetManager",
                    "AssetManagerView",
                    "AssetManager のナビゲーション、一覧、詳細操作を確認する画面です。",
                    "実画面と同じ AssetManagerView をサンプルデータで表示します。Library、Collection、未所属ファイル、Source 画面を操作できます。",
                    Build,
                    dependencies: new[]
                    {
                        "UiTextFactory",
                        "Icon"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerWindow.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss"
                    }),
                new UiStory(
                    "asset-manager-grid",
                    "Domain/AssetManager",
                    "AssetItemGridView",
                    "Item一覧の可変列Gridと選択状態を確認するStoryです。",
                    "現行AssetItem向けの行仮想化Gridを、サムネイル有無を含むサンプルデータで表示します。スライダーで1〜12列へ変更し、表示領域に応じた最小値、カード幅、行高、選択状態を確認できます。",
                    BuildGrid,
                    dependencies: new[] { "UiTextFactory", "ListView" },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss"
                    }),
                new UiStory(
                    "asset-manager-three-pane",
                    "Domain/AssetManager",
                    "AssetManagerThreePaneLayout",
                    "masterに近い固定幅の3ペイン構成を確認するStoryです。",
                    "左ナビゲーション240px、右情報300pxを固定し、中央一覧だけが残り幅へ追従する構成を確認できます。",
                    BuildThreePane,
                    dependencies: new[] { "UiTextFactory" },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss"
                    }),
                new UiStory(
                    "asset-manager-separated-windows",
                    "Domain/AssetManager",
                    "AssetManagerSeparatedWindows",
                    "分離した3ウィンドウの連動状態を確認するStoryです。",
                    "同じIAssetManagerと表示状態を共有するNavigation、Main、Informationを並べ、ページ切替と選択が別ペインへ反映されることを確認できます。",
                    BuildSeparatedWindows,
                    dependencies: new[]
                    {
                        "AssetManagerView",
                        "AssetManagerViewState"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerStandaloneWindows.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss"
                    }),
                new UiStory(
                    "asset-manager-controls",
                    "Domain/AssetManager",
                    "AssetManagerControls",
                    "AssetManager専用のボタン、入力、通知、カードを確認するStoryです。",
                    "masterの状態表現を参考に、現行画面向けに再設計した内部コンポーネントを一覧表示します。",
                    BuildControls,
                    dependencies: new[]
                    {
                        "UiTextFactory",
                        "Icon"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss"
                    })
            };
        }

        private static void Build(VisualElement parent)
        {
            var surface = new VisualElement();
            surface.style.height = 620f;
            surface.style.minWidth = 920f;
            var view = new AssetManagerView(new AssetManagerStoryManager());
            view.RegisterCallback<DetachFromPanelEvent>(_ => view.Dispose());
            surface.Add(view);
            parent.Add(surface);
        }

        private static void BuildGrid(VisualElement parent)
        {
            var thumbnail = CreateSampleThumbnail();
            var surface = new VisualElement();
            surface.style.minWidth = 760f;
            var grid = new AssetItemGridView();
            grid.style.height = 500f;
            grid.SetItems(new[]
            {
                new AssetItemGridEntry(
                    "avatar-base",
                    "Avatar Base",
                    thumbnail),
                new AssetItemGridEntry(
                    "summer-costume",
                    "Summer Costume",
                    thumbnail),
                new AssetItemGridEntry(
                    "city-stage",
                    "City Stage"),
                new AssetItemGridEntry(
                    "gesture-pack",
                    "Gesture Pack"),
                new AssetItemGridEntry(
                    "shader-library",
                    "Shader Library"),
                new AssetItemGridEntry(
                    "sound-effects",
                    "Sound Effects")
            });
            grid.SetSelectedItemId("summer-costume");
            var slider = AssetManagerControls.CreateGridSizeSlider(
                grid.ItemsPerRow,
                grid.RecommendedMinimumItemsPerRow,
                AssetItemGridView.MaximumItemsPerRow);
            slider.tooltip = "Grid columns";
            slider.ValueChanged += grid.SetItemsPerRow;
            grid.RecommendedMinimumItemsPerRowChanged += value =>
            {
                slider.SetRangeWithoutNotify(
                    value,
                    AssetItemGridView.MaximumItemsPerRow);
                slider.SetValueWithoutNotify(grid.ItemsPerRow);
            };
            grid.RegisterCallback<DetachFromPanelEvent>(_ => grid.Dispose());
            surface.Add(slider);
            surface.Add(grid);
            parent.Add(surface);
        }

        private static void BuildThreePane(VisualElement parent)
        {
            var layout = new AssetManagerThreePaneLayout();
            layout.style.height = 520f;
            layout.style.minWidth = 920f;
            layout.LeftToolbarContent.Add(UiTextFactory.Create(
                "ASSET MANAGER",
                "ee4v-asset-manager__brand"));
            layout.MainToolbarContent.Add(UiTextFactory.Create(
                "Library",
                "ee4v-asset-manager__title"));
            layout.RightToolbarContent.Add(UiTextFactory.Create(
                "INFORMATION",
                "ee4v-asset-manager__pane-title"));

            var navigation = new ScrollView();
            navigation.AddToClassList("ee4v-asset-manager__navigation");
            navigation.Add(AssetManagerControls.CreateButton(
                "Library",
                () => { },
                "ee4v-asset-manager__nav-button",
                "ee4v-asset-manager__nav-button--selected"));
            navigation.Add(AssetManagerControls.CreateButton(
                "Archived",
                () => { },
                "ee4v-asset-manager__nav-button"));
            navigation.Add(UiTextFactory.Create(
                "COLLECTIONS",
                "ee4v-asset-manager__nav-section"));
            navigation.Add(AssetManagerControls.CreateButton(
                "Favorites",
                () => { },
                "ee4v-asset-manager__nav-button"));
            layout.LeftContent.Add(navigation);

            var main = new VisualElement();
            main.AddToClassList("ee4v-asset-manager__content");
            var grid = new AssetItemGridView();
            grid.SetItems(new[]
            {
                new AssetItemGridEntry("avatar-base", "Avatar Base"),
                new AssetItemGridEntry("summer-costume", "Summer Costume"),
                new AssetItemGridEntry("city-stage", "City Stage")
            });
            grid.RegisterCallback<DetachFromPanelEvent>(_ => grid.Dispose());
            main.Add(grid);
            layout.MainContent.Add(main);

            var information = new ScrollView();
            information.AddToClassList("ee4v-asset-manager__detail");
            information.Add(UiTextFactory.Create(
                "Summer Costume",
                "ee4v-asset-manager__detail-title"));
            information.Add(AssetManagerControls.CreateNotice(
                "Select an item to edit its metadata and files."));
            layout.RightContent.Add(information);
            parent.Add(layout);
        }

        private static void BuildSeparatedWindows(VisualElement parent)
        {
            var surface = new VisualElement();
            surface.style.height = 620f;
            surface.style.minWidth = 1120f;
            surface.style.flexDirection = FlexDirection.Row;

            var manager = new AssetManagerStoryManager();
            var state = new AssetManagerViewState();
            var navigation = new AssetManagerView(
                manager,
                state,
                AssetManagerViewMode.Navigation);
            navigation.style.width = 240f;
            navigation.style.flexShrink = 0f;
            var main = new AssetManagerView(
                manager,
                state,
                AssetManagerViewMode.Main);
            main.style.flexGrow = 1f;
            var information = new AssetManagerView(
                manager,
                state,
                AssetManagerViewMode.Information);
            information.style.width = 300f;
            information.style.flexShrink = 0f;

            surface.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                navigation.Dispose();
                main.Dispose();
                information.Dispose();
            });
            surface.Add(navigation);
            surface.Add(main);
            surface.Add(information);
            state.SelectItem("item-avatar");
            parent.Add(surface);
        }

        private static void BuildControls(VisualElement parent)
        {
            var surface = new VisualElement();
            surface.style.width = 520f;
            surface.AddToClassList("ee4v-asset-manager-controls-story");

            var actions = new AssetManagerActionRow();
            actions.Add(AssetManagerControls.CreateButton("Default", () => { }));
            actions.Add(AssetManagerControls.CreateButton(
                "Primary",
                () => { },
                "ee4v-asset-manager__primary-action"));
            actions.Add(AssetManagerControls.CreateButton(
                "Danger",
                () => { },
                "ee4v-asset-manager__danger-action"));
            surface.Add(actions);

            var name = AssetManagerControls.CreateTextField("Name");
            name.value = "Summer Costume";
            surface.Add(name);
            surface.Add(AssetManagerControls.CreateSearchField(
                "Search items"));
            surface.Add(AssetManagerControls.CreateGridSizeSlider(
                6,
                AssetItemGridView.MinimumItemsPerRow,
                AssetItemGridView.MaximumItemsPerRow));
            var notes = AssetManagerControls.CreateTextField("Notes");
            notes.multiline = true;
            notes.value = "Preview the component states here.";
            surface.Add(notes);
            surface.Add(AssetManagerControls.CreateEnumField(
                "Condition",
                AssetFilterConditionType.HasTag));
            surface.Add(AssetManagerControls.CreateNotice(
                "These controls are local to AssetManager."));

            var foldout = AssetManagerControls.CreateFoldout(
                "Optional metadata",
                true);
            foldout.Add(AssetManagerControls.CreateTextField("Display name"));
            surface.Add(foldout);

            var card = new AssetManagerCard(
                "Import source",
                "Card and form spacing are owned by AssetManager.");
            card.Add(AssetManagerControls.CreateTextField("Source path"));
            card.Add(AssetManagerControls.CreateButton(
                "Import",
                () => { },
                "ee4v-asset-manager__primary-action"));
            surface.Add(card);
            parent.Add(surface);
        }

        private static byte[] CreateSampleThumbnail()
        {
            var texture = new Texture2D(2, 2);
            texture.SetPixels(new[]
            {
                new Color(0.16f, 0.38f, 0.62f),
                new Color(0.22f, 0.54f, 0.72f),
                new Color(0.42f, 0.26f, 0.58f),
                new Color(0.64f, 0.38f, 0.68f)
            });
            texture.Apply();
            var data = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return data;
        }
    }
}
