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
                    "asset-manager-tag-list",
                    "Domain/AssetManager/Collections",
                    "AssetTagListView",
                    "タグPillの折り返し配置、検索、Item数と並び順を確認するStoryです。",
                    "階層prefixと件数を補助文字にした軽いTagPillを横へ並べます。全幅の検索欄、下線付き文字タブ、名前順での頭文字の見出しと漢字の「その他」グループ、表示幅に合わせた折り返しとタグ選択を確認できます。",
                    BuildTagList,
                    dependencies: new[]
                    {
                        "TagPill", "SearchField", "UiButton"
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
                    "asset-manager-grid",
                    "Domain/AssetManager/Collections",
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
                    "Domain/AssetManager/Inputs",
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
                    "コレクションの作成、アイコン選択と条件編集を確認するStoryです。",
                    "ボタンから実際のポップアップを開き、アイコン選択、入れ子のAND、OR、条件とグループの反転、条件とグループの追加と削除を確認できます。保存時はStory内のサンプルだけを更新します。",
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
                    "Domain/AssetManager/Inputs",
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
                    "Domain/AssetManager/Collections",
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
                    "asset-manager-text-field",
                    "Domain/AssetManager/Inputs",
                    "AssetManagerTextField",
                    "AssetManagerで使用するラベル付きの文字入力です。",
                    "単行と複数行の入力を、共通のFormInputとInputFieldを使って表示します。",
                    BuildTextField,
                    dependencies: new[]
                    {
                        "FormInput",
                        "InputField"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.cs",
                        "Editor/AssetManager/UI/AssetCollectionCreationPopup.cs",
                        "Editor/AssetManager/UI/Components/AssetFilterEditor.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss"
                    }),
                new UiStory(
                    "asset-manager-grid-size-slider",
                    "Domain/AssetManager/Inputs",
                    "AssetManagerGridSizeSlider",
                    "Gridの列数を増減する入力です。",
                    "スライダーと両端の増減操作を一つの値入力として公開します。",
                    BuildGridSizeSlider,
                    dependencies: new[]
                    {
                        "UiButton",
                        "UiTextFactory"
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
                    "asset-manager-enum-field",
                    "Domain/AssetManager/Inputs",
                    "AssetManagerEnumField",
                    "Collection条件などの列挙値を選択する入力です。",
                    "ラベルと列挙値の選択欄をFormInputとして表示し、任意の表示名へ変換できます。",
                    BuildEnumField,
                    dependencies: new[]
                    {
                        "FormInput",
                        "UiTextFactory"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/Components/AssetFilterEditor.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss"
                    }),
                new UiStory(
                    "asset-manager-notice",
                    "Domain/AssetManager/Displays",
                    "AssetManagerNotice",
                    "補足や空状態の理由を表示する通知です。",
                    "情報アイコンと折り返し可能な本文をAssetManagerの画面内へ表示します。",
                    BuildNotice,
                    dependencies: new[]
                    {
                        "Icon",
                        "UiTextFactory"
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
                    "asset-manager-grid-card",
                    "Domain/AssetManager/Inputs",
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
                    "Domain/AssetManager/Inputs",
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
                    "Domain/AssetManager/Displays",
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
                    "asset-detail-header",
                    "Domain/AssetManager/Containers",
                    "AssetDetailHeader",
                    "Assetの概要と操作をまとめる詳細ヘッダーです。",
                    "種別、名称、補足、状態に加えて呼び出し側の操作を配置します。",
                    BuildDetailHeader,
                    dependencies: new[]
                    {
                        "Badge",
                        "InfoCard"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss",
                        "Editor/AssetManager/UI/asset-detail.uss"
                    }),
                new UiStory(
                    "asset-detail-section",
                    "Domain/AssetManager/Containers",
                    "AssetDetailSection",
                    "詳細画面の内容を見出し付きでまとめる領域です。",
                    "見出しと任意の注記の下へ、呼び出し側が詳細要素を追加します。",
                    BuildDetailSection,
                    dependencies: new[] { "SectionHeader" },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss",
                        "Editor/AssetManager/UI/asset-detail.uss"
                    }),
                new UiStory(
                    "asset-detail-fact",
                    "Domain/AssetManager/Displays",
                    "AssetDetailFact",
                    "短いラベルと値を強調して表示する概要値です。",
                    "ファイル数やタグ数など、Assetの要点を一組ずつ表示します。",
                    BuildDetailFact,
                    dependencies: new[] { "UiTextFactory" },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss",
                        "Editor/AssetManager/UI/asset-detail.uss"
                    }),
                new UiStory(
                    "asset-detail-setting-list",
                    "Domain/AssetManager/Containers",
                    "AssetDetailSettingList",
                    "複数の設定行を縦に配置する詳細領域です。",
                    "設定行とキー・値行を同じ間隔でまとめます。値の保持や編集は各行が担当します。",
                    BuildDetailSettingList,
                    dependencies: new[]
                    {
                        "AssetDetailSettingRow",
                        "AssetDetailKeyValueRow"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss",
                        "Editor/AssetManager/UI/asset-detail.uss"
                    }),
                new UiStory(
                    "asset-detail-setting-row",
                    "Domain/AssetManager/Inputs",
                    "AssetDetailSettingRow",
                    "ラベル、値、任意操作を横に配置する設定行です。",
                    "表示だけの値と、編集表示へ切り替わる値の両方を構成できます。",
                    BuildDetailSettingRow,
                    dependencies: new[]
                    {
                        "AssetManagerTextField",
                        "UiButton"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.cs"
                    },
                    styleSheetPaths: new[]
                    {
                        "Editor/AssetManager/UI/asset-manager.uss",
                        "Editor/AssetManager/UI/asset-detail.uss"
                    }),
                new UiStory(
                    "asset-detail-key-value-row",
                    "Domain/AssetManager/Displays",
                    "AssetDetailKeyValueRow",
                    "詳細情報のキーと値を一行で表示します。",
                    "Sourceや識別子などの補足情報を、折り返し可能な値として表示します。",
                    BuildDetailKeyValueRow,
                    dependencies: new[] { "UiTextFactory" },
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

        private static void BuildDetailHeader(VisualElement parent)
        {
            var surface = CreateDetailStorySurface();
            var header = new AssetDetailHeader(
                "Summer Costume",
                "Asset",
                "Assets/Avatar/Summer Costume",
                new BadgeState("Imported", UiStatusTone.Passed));
            header.AddAction(AssetManagerControls.CreateButton(
                "Open",
                () => { }));
            surface.Add(header);
            parent.Add(surface);
        }

        private static void BuildDetailSection(VisualElement parent)
        {
            var surface = CreateDetailStorySurface();
            var overview = new AssetDetailSection(
                "Overview",
                "Updated just now");
            var facts = new VisualElement();
            facts.style.flexDirection = FlexDirection.Row;
            facts.Add(new AssetDetailFact("Files", "12"));
            facts.Add(new AssetDetailFact("Tags", "3"));
            overview.Add(facts);
            surface.Add(overview);
            parent.Add(surface);
        }

        private static void BuildDetailFact(VisualElement parent)
        {
            var surface = CreateDetailStorySurface();
            surface.Add(new AssetDetailFact("Files", "12"));
            parent.Add(surface);
        }

        private static void BuildDetailSettingList(VisualElement parent)
        {
            var surface = CreateDetailStorySurface();
            var list = new AssetDetailSettingList();
            list.Add(new AssetDetailSettingRow(
                "Target",
                UiTextFactory.Create("Avatar")));
            list.Add(new AssetDetailKeyValueRow(
                "Source",
                "Eagle / Summer Costume"));
            surface.Add(list);
            parent.Add(surface);
        }

        private static void BuildDetailSettingRow(VisualElement parent)
        {
            var surface = CreateDetailStorySurface();
            var list = new AssetDetailSettingList();
            list.Add(new AssetDetailSettingRow(
                "Target",
                UiTextFactory.Create("Avatar")));

            var editor = AssetManagerControls.CreateTextField(string.Empty);
            editor.value = "Summer Costume";
            var save = AssetManagerControls.CreateButton("Save");
            save.clicked += () => save.SetLabel("Saved");
            list.Add(AssetDetailSettingRow.Editable(
                "Name",
                "Summer Costume",
                editor,
                save,
                "Edit"));
            surface.Add(list);
            parent.Add(surface);
        }

        private static void BuildDetailKeyValueRow(VisualElement parent)
        {
            var surface = CreateDetailStorySurface();
            surface.Add(new AssetDetailKeyValueRow(
                "Source",
                "Eagle / Summer Costume"));
            parent.Add(surface);
        }

        private static VisualElement CreateDetailStorySurface()
        {
            var surface = new VisualElement();
            surface.AddToClassList("ee4v-asset-manager");
            surface.style.width = 620f;
            return surface;
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

        private static void BuildTagList(VisualElement parent)
        {
            var surface = new VisualElement();
            surface.style.height = 480f;
            surface.style.minWidth = 360f;
            var selected = UiTextFactory.Create("Select a tag.");
            var list = new AssetTagListView(path => selected.SetText(path));
            var tags = new[]
            {
                new AssetTag { Path = "avatar" },
                new AssetTag { Path = "avatar/costume" },
                new AssetTag { Path = "avatar/accessory/hair" },
                new AssetTag { Path = "world/lighting" },
                new AssetTag { Path = "shader" },
                new AssetTag { Path = "archived-only" },
                new AssetTag { Path = "衣装" },
                new AssetTag { Path = "髪型" },
                new AssetTag { Path = "あくせさり" },
                new AssetTag { Path = "アバター" }
            };
            list.SetData(tags, new[]
            {
                new AssetItem { Tags = new[] { tags[0], tags[1], tags[2] } },
                new AssetItem { Tags = new[] { tags[1] } },
                new AssetItem { Tags = new[] { tags[3], tags[4] } },
                new AssetItem { Tags = new[] { tags[5] }, IsArchived = true },
                new AssetItem { Tags = new[] { tags[6], tags[7], tags[8], tags[9] } }
            });
            surface.Add(list);
            surface.Add(selected);
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
                    (_, __, ___) => true);
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
                    (name, icon, root) =>
                    {
                        sample.Name = name;
                        sample.Icon = icon;
                        sample.Root = root;
                        return true;
                    });
            surface.Add(editButton);
            parent.Add(surface);
        }

        private static void BuildTextField(VisualElement parent)
        {
            var surface = new VisualElement();
            surface.style.width = 520f;
            surface.AddToClassList("ee4v-asset-manager-controls-story");
            var name = AssetManagerControls.CreateTextField("Name");
            name.value = "Summer Costume";
            surface.Add(name);
            var notes = AssetManagerControls.CreateTextField("Notes");
            notes.multiline = true;
            notes.value = "Preview the component states here.";
            surface.Add(notes);
            parent.Add(surface);
        }

        private static void BuildGridSizeSlider(VisualElement parent)
        {
            var slider = AssetManagerControls.CreateGridSizeSlider(
                6,
                AssetItemGridView.MinimumItemsPerRow,
                AssetItemGridView.MaximumItemsPerRow);
            slider.style.width = 320f;
            parent.Add(slider);
        }

        private static void BuildEnumField(VisualElement parent)
        {
            var field = AssetManagerControls.CreateEnumField(
                "Condition",
                AssetFilterConditionType.HasTag,
                AssetManagerControls.FormatFilterCondition);
            field.style.width = 420f;
            parent.Add(field);
        }

        private static void BuildNotice(VisualElement parent)
        {
            var notice = AssetManagerControls.CreateNotice(
                "No matching assets were found. Change the search or collection filters.");
            notice.style.width = 520f;
            parent.Add(notice);
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
