using System.Collections.Generic;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.Images;
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
                    "AssetManagerView · Main",
                    "AssetManager Main Windowの一覧と詳細操作を確認する画面です。",
                    "実画面と同じMain modeのAssetManagerViewをサンプルデータで表示します。Item一覧、File Tree、Target設定を操作できます。",
                    Build,
                    dependencies: new[]
                    {
                        "UiTextFactory",
                        "InfoCard",
                        "Badge",
                        "AssetDetailComponents",
                        "Fluent UI System Icons"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerStandaloneWindows.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss",
                        "Editor/AssetManager/UI/searchable-file-tree.uss",
                        "Editor/AssetManager/UI/asset-detail.uss"
                    }),
                new UiStory(
                    "asset-manager-grid",
                    "Domain/AssetManager",
                    "AssetItemGridView",
                    "Item一覧の可変列Gridと複数選択を確認するStoryです。",
                    "現行AssetItem向けの行仮想化Gridを、サムネイル有無を含むサンプルデータで表示します。スライダーで1〜12列へ変更し、Ctrl追加選択、Shift範囲選択、Escapeと空白クリックによる選択解除を確認できます。",
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
                    "asset-manager-breadcrumb",
                    "Domain/AssetManager/Components",
                    "AssetManagerBreadcrumb",
                    "現在位置の末尾と、ホバー中のフルパスを確認するStoryです。",
                    "通常時は末尾のItem名だけを表示します。ホバーすると親階層を選択できるフルパスを表示します。",
                    BuildBreadcrumb,
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
                    "AssetManagerView · Separated modes",
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
                        "Editor/AssetManager/UI/asset-manager.uss",
                        "Editor/AssetManager/UI/searchable-file-tree.uss",
                        "Editor/AssetManager/UI/asset-detail.uss"
                    }),
                new UiStory(
                    "asset-manager-collection-popup",
                    "Domain/AssetManager",
                    "AssetCollectionCreationPopup",
                    "コレクションの作成と条件編集を確認するStoryです。",
                    "ボタンから実際のポップアップを開き、入れ子のAND、OR、条件とグループの反転、条件とグループの追加と削除を確認できます。Story内の保存操作はデータを変更しません。",
                    BuildCollectionPopup,
                    dependencies: new[]
                    {
                        "UiTextFactory",
                        "AssetManagerControls"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss"
                    }),
                new UiStory(
                    "asset-manager-tag-field",
                    "Domain/AssetManager/Components",
                    "AssetTagField",
                    "ItemのTagをチップと検索ポップアップで編集するコンポーネントです。",
                    "選択済みTagの削除、既存Tagの検索と選択、新しいTagの作成を確認できます。",
                    BuildTagField,
                    dependencies: new[]
                    {
                        "UiButton",
                        "SearchField",
                        "TagPill"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss"
                    }),
                new UiStory(
                    "asset-manager-file-tree",
                    "Domain/AssetManager",
                    "SearchableFileTree",
                    "Item詳細で使用する検索付きFile Treeです。",
                    "FileをルートにしてZIPやUnityPackageの内容を階層表示し、選択した要素を右ペインへ渡します。",
                    BuildFileTree,
                    dependencies: new[]
                    {
                        "SearchableTreeView",
                        "UiTextFactory"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss",
                        "Editor/AssetManager/UI/searchable-file-tree.uss"
                    }),
                new UiStory(
                    "asset-manager-controls",
                    "Domain/AssetManager/Components",
                    "AssetManagerControls",
                    "AssetManager専用のボタン、入力、通知を確認するStoryです。",
                    "masterの状態表現を参考に、現行画面向けに再設計した内部コンポーネントを一覧表示します。",
                    BuildControls,
                    dependencies: new[]
                    {
                        "UiTextFactory",
                        "Fluent UI System Icons"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.cs",
                        "Editor/AssetManager/UI/AssetCollectionCreationPopup.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss"
                    }),
                new UiStory(
                    "asset-manager-grid-card",
                    "Domain/AssetManager/Components",
                    "AssetItemGridCard",
                    "Assetのサムネイル、名前、選択状態をまとめるCardです。",
                    "サムネイルの有無、選択状態、名前を一枚のCardとして表示し、クリック操作を通知します。",
                    BuildGridCard,
                    dependencies: new[]
                    {
                        "PreviewContainer",
                        "CachedImage",
                        "UiTextFactory"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetItemGridView.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss"
                    }),
                new UiStory(
                    "asset-manager-filter-editor",
                    "Domain/AssetManager/Components",
                    "AssetFilterEditor",
                    "Collection条件を入れ子で編集するパーツです。",
                    "条件GroupをANDまたはORで組み合わせ、反転、追加、削除を編集できます。",
                    BuildFilterEditor,
                    dependencies: new[]
                    {
                        "ActionBar",
                        "FormInput",
                        "UiTextFactory"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetCollectionCreationPopup.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss"
                    }),
                new UiStory(
                    "asset-manager-thumbnail-stack",
                    "Domain/AssetManager/Components",
                    "AssetThumbnailStack",
                    "最大3枚のAsset画像を重ねるPreviewパーツです。",
                    "最大3件のサムネイルをずらして重ね、複数Itemの概要を視覚的に表します。",
                    BuildThumbnailStack,
                    dependencies: new[]
                    {
                        "PreviewContainer",
                        "CachedImage"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss"
                    }),
                new UiStory(
                    "asset-manager-detail-components",
                    "Domain/AssetManager/Components",
                    "AssetDetail components",
                    "Asset詳細を構成するHeader、Section、Fact、設定行です。",
                    "HeaderでAsset概要を示し、Section、Fact、設定行で情報と操作を整理します。",
                    BuildDetailComponents,
                    dependencies: new[]
                    {
                        "SectionHeader",
                        "Badge",
                        "UiTextFactory"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss",
                        "Editor/AssetManager/UI/asset-detail.uss"
                    })
            };
        }

        private static void BuildGridCard(VisualElement parent)
        {
            var cache = new CachedImageCache();
            cache.SetSource(
                "story-grid-card",
                CreateSampleThumbnail());
            var card = new AssetItemGridCard(cache);
            card.SetWidth(180f);
            card.SetState(
                new AssetItemGridEntry(
                    "story-grid-card",
                    "Summer Costume"),
                true);
            card.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                card.Dispose();
                cache.Dispose();
            });
            parent.Add(card);
        }

        private static void BuildFilterEditor(VisualElement parent)
        {
            var editor = new AssetFilterEditor(AssetFilterNode.And(
                AssetFilterNode.Condition(
                    AssetFilterConditionType.HasTag,
                    "Avatar"),
                AssetFilterNode.Not(AssetFilterNode.Condition(
                    AssetFilterConditionType.HasFileExtension,
                    "zip"))));
            editor.style.width = 620f;
            parent.Add(editor);
        }

        private static void BuildThumbnailStack(VisualElement parent)
        {
            var cache = new CachedImageCache();
            var ids = new[] { "story-a", "story-b", "story-c" };
            for (var index = 0; index < ids.Length; index++)
            {
                cache.SetSource(ids[index], CreateSampleThumbnail());
            }

            var stack = new AssetThumbnailStack(cache, ids);
            stack.style.width = 240f;
            stack.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                stack.Dispose();
                cache.Dispose();
            });
            parent.Add(stack);
        }

        private static void BuildDetailComponents(VisualElement parent)
        {
            var surface = new VisualElement();
            surface.AddToClassList("ee4v-asset-manager");
            surface.style.width = 620f;

            var header = new AssetDetailHeader(
                "Summer Costume",
                "Asset",
                "Assets/Avatar/Summer Costume",
                new BadgeState("Imported", UiStatusTone.Passed));
            header.AddAction(AssetManagerControls.CreateButton(
                "Open",
                () => { }));
            surface.Add(header);

            var overview = new AssetDetailSection(
                "Overview",
                "Updated just now");
            var facts = new VisualElement();
            facts.style.flexDirection = FlexDirection.Row;
            facts.Add(new AssetDetailFact("Files", "12"));
            facts.Add(new AssetDetailFact("Tags", "3"));
            overview.Add(facts);
            surface.Add(overview);

            var settings = new AssetDetailSection("Settings");
            var list = new AssetDetailSettingList();
            list.Add(new AssetDetailSettingRow(
                "Target",
                UiTextFactory.Create("Avatar")));
            list.Add(new AssetDetailKeyValueRow(
                "Source",
                "Eagle / Summer Costume"));
            settings.Add(list);
            surface.Add(settings);
            parent.Add(surface);
        }

        private static void BuildFileTree(VisualElement parent)
        {
            var manager = new AssetManagerStoryManager();
            var surface = new VisualElement();
            surface.AddToClassList("ee4v-asset-manager");
            surface.style.width = 360f;
            surface.style.height = 420f;
            var tree = new SearchableFileTree(manager);
            tree.SetItem(
                "item-avatar",
                manager.GetFiles("item-avatar"));
            surface.RegisterCallback<DetachFromPanelEvent>(_ => tree.Dispose());
            surface.Add(tree);
            parent.Add(surface);
        }

        private static void BuildTagField(VisualElement parent)
        {
            var surface = new VisualElement();
            surface.style.width = 300f;
            var field = new AssetTagField();
            field.SetValues(
                new[]
                {
                    new AssetTagOption("avatar", 12),
                    new AssetTagOption("costume", 8),
                    new AssetTagOption("free", 5),
                    new AssetTagOption("gimmick", 3),
                    new AssetTagOption("world", 2)
                },
                new[] { "avatar", "costume" });
            surface.Add(field);
            parent.Add(surface);
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
            grid.SetSelectedItemIds(
                new[] { "summer-costume", "gesture-pack" },
                "gesture-pack");
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

        private static void BuildBreadcrumb(VisualElement parent)
        {
            var surface = new VisualElement();
            surface.AddToClassList("ee4v-asset-manager");
            surface.style.height = 120f;
            surface.style.paddingLeft = 12f;
            surface.style.paddingTop = 12f;

            var breadcrumb = new AssetManagerBreadcrumb();
            breadcrumb.SetItems(new[]
            {
                new AssetManagerBreadcrumbItem("Library", () => { }),
                new AssetManagerBreadcrumbItem("Summer Costume")
            });
            surface.RegisterCallback<DetachFromPanelEvent>(_ =>
                breadcrumb.Dispose());
            surface.Add(breadcrumb);
            parent.Add(surface);
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

        private static void BuildCollectionPopup(VisualElement parent)
        {
            var surface = new VisualElement();
            surface.style.width = 420f;
            surface.AddToClassList("ee4v-asset-manager-controls-story");
            surface.Add(AssetManagerControls.CreateNotice(
                "各ボタンの直下にコレクション編集ポップアップを表示します。"));

            var createButton = AssetManagerControls.CreateButton(
                "新規コレクションを開く",
                () => { });
            createButton.clicked += () =>
                AssetCollectionCreationPopup.Show(
                    createButton,
                    null,
                    (_, __) => true);
            surface.Add(createButton);

            var sample = new AssetCollection
            {
                Id = "collection-story",
                Name = "Avatar favorites",
                Root = AssetFilterNode.Or(
                    AssetFilterNode.And(
                        AssetFilterNode.Condition(
                            AssetFilterConditionType.HasTag,
                            "Avatar"),
                        AssetFilterNode.Or(
                            AssetFilterNode.Condition(
                                AssetFilterConditionType.NameContains,
                                "Summer"),
                            AssetFilterNode.Condition(
                                AssetFilterConditionType.DescriptionContains,
                                "Favorite"))),
                    AssetFilterNode.Not(AssetFilterNode.Condition(
                        AssetFilterConditionType.HasFileExtension,
                        "zip")))
            };
            var editButton = AssetManagerControls.CreateButton(
                "既存コレクションを編集",
                () => { });
            editButton.clicked += () =>
                AssetCollectionCreationPopup.Show(
                    editButton,
                    sample,
                    (_, __) => true);
            surface.Add(editButton);
            parent.Add(surface);
        }

        private static void BuildControls(VisualElement parent)
        {
            var surface = new VisualElement();
            surface.style.width = 520f;
            surface.AddToClassList("ee4v-asset-manager-controls-story");

            var actions = new VisualElement();
            actions.AddToClassList("ee4v-asset-manager__actions");
            actions.Add(AssetManagerControls.CreateSortButton(() => { }));
            actions.Add(AssetManagerControls.CreateButton("Default", () => { }));
            actions.Add(AssetManagerControls.CreateButton(
                "Primary",
                () => { },
                "ee4v-asset-manager__primary-action"));
            actions.Add(AssetManagerControls.CreateDangerButton(
                "Danger",
                () => { }));
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
