using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.Images;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Ee4v.UI
{
    internal sealed class CommonComponentsStoryProvider : IUiStoryProvider
    {
        private enum SampleChoice { NameContains, HasTag, HasFileExtension }
        public int Order => 30;
        public IReadOnlyList<UiStory> GetStories()
        {
            return new[]
            {
                new UiStory("scalar-input", "Inputs", "ScalarInput",
                    "Slider、数値入力、単位とリセットを同じ行へ配置します。",
                    "値の保存・丸め・検証は利用側が担当します。", BuildScalar,
                    usageLocations: new[] {
                        "Editor/Feature/Avatar/AvatarParts/AvatarPartsEditor.Shape.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionView.cs" }),
                new UiStory(
                    "image-grid",
                    "Collections",
                    "ImageGridView",
                    "Item一覧の可変列Gridと複数選択を確認するStoryです。",
                    "汎用の行仮想化Gridを、サムネイル有無を含むサンプルデータで表示します。スライダーで1〜12列へ変更し、Ctrl追加選択、Shift範囲選択、Escapeと空白クリックによる選択解除を確認できます。",
                    BuildGrid,
                    dependencies: new[] { "UiTextFactory", "ListView" },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.cs"
                    }),
                new UiStory(
                    "breadcrumb",
                    "Inputs",
                    "Breadcrumb",
                    "現在位置の末尾と、ホバー中のフルパスを確認するStoryです。",
                    "通常時は末尾のItem名だけを表示します。ホバーすると親階層を選択できるフルパスを表示します。",
                    BuildBreadcrumb,
                    dependencies: new[] { "UiTextFactory" },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.Navigation.cs"
                    }),
                new UiStory(
                    "tag-field",
                    "Inputs",
                    "TagField",
                    "ItemのTagをチップと検索ポップアップで編集するコンポーネントです。",
                    "Tagチップのクリック、選択済みTagの削除、ロックしたTagの削除不可、既存Tagの検索と選択、新しいTagの作成を確認できます。",
                    BuildTagField,
                    dependencies: new[]
                    {
                        "UiButton",
                        "SearchField",
                        "TagPill"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.Items.cs",
                        "Editor/AssetManager/UI/AssetCollectionCreationPopup.cs"
                    }),
                new UiStory(
                    "stepper-slider",
                    "Inputs",
                    "StepperSlider",
                    "範囲付き整数をスライダーと増減ボタンで編集する入力です。",
                    "スライダーと両端の増減操作を一つの値入力として公開します。",
                    BuildGridSizeSlider,
                    dependencies: new[]
                    {
                        "UiButton",
                        "UiTextFactory"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.Navigation.cs"
                    }),
                new UiStory(
                    "enum-form-input",
                    "Inputs",
                    "EnumFormInput",
                    "列挙値をラベル付きで選択する入力です。",
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
                    }),
                new UiStory(
                    "image-grid-card",
                    "Inputs",
                    "ImageGridCard",
                    "画像、名前、選択状態をまとめるCardです。",
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
                        "Editor/UI/Components/Collections/ImageGridView/ImageGridView.cs"
                    }),
                new UiStory(
                    "thumbnail-stack",
                    "Displays",
                    "ThumbnailStack",
                    "最大3枚の画像を重ねるPreviewパーツです。",
                    "最大3件のサムネイルをずらして重ね、複数Itemの概要を視覚的に表します。",
                    BuildThumbnailStack,
                    dependencies: new[]
                    {
                        "PreviewContainer",
                        "CachedImage"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.Items.cs",
                        "Editor/AssetManager/UI/AssetManagerView.Images.cs"
                    }),
                new UiStory(
                    "detail-header",
                    "Containers",
                    "DetailHeader",
                    "対象の概要と操作をまとめる詳細ヘッダーです。",
                    "種別、名称、補足、状態に加えて呼び出し側の操作を配置します。",
                    BuildDetailHeader,
                    dependencies: new[]
                    {
                        "Badge",
                        "InfoCard"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.Files.cs"
                    }),
                new UiStory(
                    "detail-section",
                    "Containers",
                    "DetailSection",
                    "詳細画面の内容を見出し付きでまとめる領域です。",
                    "見出しと任意の注記の下へ、呼び出し側が詳細要素を追加します。",
                    BuildDetailSection,
                    dependencies: new[] { "SectionHeader" },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.Items.cs",
                        "Editor/AssetManager/UI/AssetManagerView.Files.cs",
                        "Editor/AssetManager/UI/AssetManagerView.Variants.cs"
                    }),
                new UiStory(
                    "metric-display",
                    "Displays",
                    "MetricDisplay",
                    "短いラベルと値を強調して表示する概要値です。",
                    "件数など、対象の要点を一組ずつ表示します。",
                    BuildDetailFact,
                    dependencies: new[] { "UiTextFactory" },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.Items.cs"
                    }),
                new UiStory(
                    "setting-list",
                    "Containers",
                    "SettingList",
                    "複数の設定行を縦に配置する詳細領域です。",
                    "設定行とキー・値行を同じ間隔でまとめます。値の保持や編集は各行が担当します。",
                    BuildDetailSettingList,
                    dependencies: new[]
                    {
                        "SettingRow",
                        "KeyValueRow"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.Items.cs",
                        "Editor/AssetManager/UI/AssetManagerView.Files.cs"
                    }),
                new UiStory(
                    "setting-row",
                    "Inputs",
                    "SettingRow",
                    "ラベル、値、任意操作を横に配置する設定行です。",
                    "表示だけの値と、編集表示へ切り替わる値の両方を構成できます。",
                    BuildDetailSettingRow,
                    dependencies: new[]
                    {
                        "FormInput<InputField>",
                        "UiButton"
                    },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.Items.cs",
                        "Editor/AssetManager/UI/AssetManagerView.Files.cs"
                    }),
                new UiStory(
                    "key-value-row",
                    "Displays",
                    "KeyValueRow",
                    "詳細情報のキーと値を一行で表示します。",
                    "Sourceや識別子などの補足情報を、折り返し可能な値として表示します。",
                    BuildDetailKeyValueRow,
                    dependencies: new[] { "UiTextFactory" },
                    usageLocations: new[]
                    {
                        "Editor/AssetManager/UI/AssetManagerView.Items.cs",
                        "Editor/AssetManager/UI/AssetManagerView.Variants.cs"
                    })
            }.Select(Decorate).ToArray();
        }

        private static UiStory Decorate(UiStory story)
        {
            return new UiStory(story.Id, story.Group, story.Title, story.Description,
                "ダミーデータだけで表示と操作を確認します。保存・外部ライブラリの変更は行いません。", parent =>
                {
                    var controls = new InfoCard(new InfoCardState("コントロール"));
                    var preview = new InfoCard(new InfoCardState("プレビュー"));
                    var surface = new VisualElement();
                    var enabled = UiTextFactory.CreateToggle();
                    enabled.SetValueWithoutNotify(true);
                    enabled.RegisterValueChangedCallback(evt => surface.SetEnabled(evt.newValue));
                    controls.Body.Add(new FormInput("有効", enabled));
                    var width = UiTextFactory.CreateSlider(lowValue: 320, highValue: 1200);
                    width.showInputField = true;
                    width.SetValueWithoutNotify(960);
                    width.RegisterValueChangedCallback(evt => surface.style.width = evt.newValue);
                    controls.Body.Add(new FormInput("表示幅", width));
                    surface.style.width = 960;
                    story.Build(surface);
                    var stepper = surface.Q<StepperSlider>();
                    if (stepper != null)
                    {
                        var minimum = UiTextFactory.CreateIntegerField();
                        var maximum = UiTextFactory.CreateIntegerField();
                        minimum.SetValueWithoutNotify(stepper.Minimum);
                        maximum.SetValueWithoutNotify(stepper.Maximum);
                        minimum.RegisterValueChangedCallback(_ => stepper.SetRangeWithoutNotify(minimum.value, maximum.value));
                        maximum.RegisterValueChangedCallback(_ => stepper.SetRangeWithoutNotify(minimum.value, maximum.value));
                        controls.Body.Add(new FormInput("最小値", minimum));
                        controls.Body.Add(new FormInput("最大値", maximum));
                    }
                    var detail = surface.Q<DetailHeader>();
                    if (detail != null)
                    {
                        var title = new InputField(new InputFieldState(detail.TitleText.Text));
                        title.ValueChanged += detail.TitleText.SetText;
                        controls.Body.Add(new FormInput("見出し", title));
                    }
                    preview.Body.Add(surface);
                    parent.Add(controls);
                    parent.Add(preview);
                }, story.Dependencies, story.UsageLocations);
        }

        private static void RecordAction(VisualElement parent, string message)
        {
            var output = parent.Q<UiTextElement>("commonStoryResult");
            if (output == null)
            {
                output = UiTextFactory.Create(string.Empty, UiClassNames.SecondaryText);
                output.name = "commonStoryResult";
                parent.Add(output);
            }
            output.SetText(message);
        }

        private static void BuildScalar(VisualElement parent)
        {
            ScalarInput input = null;
            input = new ScalarInput(0, 100, "Reset", () => input.Slider.value = 0, "%");
            var result = UiTextFactory.Create("0", UiClassNames.SecondaryText);
            input.Slider.RegisterValueChangedCallback(evt =>
            {
                input.ValueField.SetValueWithoutNotify(evt.newValue);
                result.SetText(evt.newValue.ToString("0.##"));
            });
            input.ValueField.RegisterValueChangedCallback(evt => input.Slider.value = evt.newValue);
            parent.Add(input);
            parent.Add(result);
        }

        private static UiButton CreateButton(string label, Action action = null, params string[] classNames)
        {
            var button = new UiButton(label, action);
            foreach (var className in classNames) { button.AddToClassList(className); }
            return button;
        }

        private static UiButton CreateDangerButton(string label, Action action)
        {
            var button = new UiButton(label, action);
            button.SetTone(UiButtonTone.Danger);
            return button;
        }

        private static FormInput<InputField> CreateTextInput(string label)
        {
            return new FormInput<InputField>(label, new InputField());
        }

        private static void BuildGridCard(VisualElement parent)
        {
            var cache = new CachedImageCache();
            cache.SetSource(
                "story-grid-card",
                CreateSampleThumbnail());
            var card = new ImageGridCard(cache);
            card.SetWidth(180f);
            card.SetState(
                new ImageGridEntry(
                    "story-grid-card",
                    "Summer Costume"),
                true);
            card.Clicked += (_, __, ___) => RecordAction(parent, "Summer Costumeを選択しました");
            var createCard = new ImageGridCard(cache);
            createCard.SetWidth(180f);
            createCard.SetPlaceholderIcon(FluentUiIcons.CreateState(
                "add.png", UiSizeTokens.Size24, "Create item", UiColorTokens.TextMuted));
            createCard.SetState(new ImageGridEntry("story-create-card", "新規作成")
            {
                Activate = () => RecordAction(parent, "操作しました")
            }, selected: false);
            createCard.Clicked += (_, __, ___) => RecordAction(parent, "作成操作を選択しました");
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            createCard.style.marginRight = UiSpacingTokens.Medium;
            row.Add(createCard);
            row.Add(card);
            row.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                card.Dispose();
                createCard.Dispose();
                cache.Dispose();
            });
            parent.Add(row);
        }

        private static void BuildThumbnailStack(VisualElement parent)
        {
            var cache = new CachedImageCache();
            var ids = new[] { "story-a", "story-b", "story-c" };
            for (var index = 0; index < ids.Length; index++)
            {
                cache.SetSource(ids[index], CreateSampleThumbnail());
            }

            var stack = new ThumbnailStack(cache, ids);
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
            var header = new DetailHeader(
                "Summer Costume",
                "Asset",
                "Assets/Avatar/Summer Costume",
                new BadgeState("Imported", UiStatusTone.Passed));
            header.AddAction(CreateButton(
                "Open",
                () => RecordAction(parent, "操作しました")));
            header.AddHeaderAction(CreateDangerButton(
                "Archive",
                () => RecordAction(parent, "操作しました")));
            surface.Add(header);
            parent.Add(surface);
        }

        private static void BuildDetailSection(VisualElement parent)
        {
            var surface = CreateDetailStorySurface();
            var overview = new DetailSection(
                "Overview",
                "Updated just now");
            var facts = new VisualElement();
            facts.style.flexDirection = FlexDirection.Row;
            facts.Add(new MetricDisplay("Files", "12"));
            facts.Add(new MetricDisplay("Tags", "3"));
            overview.Add(facts);
            surface.Add(overview);
            parent.Add(surface);
        }

        private static void BuildDetailFact(VisualElement parent)
        {
            var surface = CreateDetailStorySurface();
            surface.Add(new MetricDisplay("Files", "12"));
            parent.Add(surface);
        }

        private static void BuildDetailSettingList(VisualElement parent)
        {
            var surface = CreateDetailStorySurface();
            var list = new SettingList();
            list.AddRow(new SettingRow(
                "Target",
                UiTextFactory.Create("Avatar")));
            list.AddRow(new SettingRow(
                "Source",
                UiTextFactory.Create("Eagle / Summer Costume")));
            surface.Add(list);
            parent.Add(surface);
        }

        private static void BuildDetailSettingRow(VisualElement parent)
        {
            var surface = CreateDetailStorySurface();
            var list = new SettingList();
            list.AddRow(new SettingRow(
                "Target",
                UiTextFactory.Create("Avatar")));

            var editor = CreateTextInput(string.Empty);
            editor.Input.Value = "Summer Costume";
            var save = CreateButton("Save");
            save.clicked += () => save.SetLabel("Saved");
            list.AddRow(SettingRow.Editable(
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
            surface.Add(new KeyValueRow(
                "Source",
                "Eagle / Summer Costume"));
            parent.Add(surface);
        }

        private static VisualElement CreateDetailStorySurface()
        {
            var surface = new VisualElement();
            surface.AddToClassList("ee4v-ui-common-component-story");
            surface.style.width = 620f;
            return surface;
        }

        private static void BuildTagField(VisualElement parent)
        {
            var surface = new VisualElement();
            surface.style.width = 300f;
            var selected = UiTextFactory.Create("Select a tag.");
            var field = new TagField(path => selected.SetText(path));
            field.SetValues(
                new[]
                {
                    new TagOption("avatar", 12),
                    new TagOption("costume", 8),
                    new TagOption("free", 5),
                    new TagOption("gimmick", 3),
                    new TagOption("world", 2)
                },
                new[] { "avatar", "costume" },
                new[] { "avatar" });
            surface.Add(field);
            surface.Add(selected);
            parent.Add(surface);
        }

        private static void BuildGrid(VisualElement parent)
        {
            var thumbnail = CreateSampleThumbnail();
            var surface = new VisualElement();
            surface.style.minWidth = 760f;
            var grid = new ImageGridView();
            grid.style.height = 500f;
            grid.SetItems(new[]
            {
                new ImageGridEntry(
                    "avatar-base",
                    "Avatar Base",
                    thumbnail),
                new ImageGridEntry(
                    "summer-costume",
                    "Summer Costume",
                    thumbnail),
                new ImageGridEntry(
                    "city-stage",
                    "City Stage"),
                new ImageGridEntry(
                    "gesture-pack",
                    "Gesture Pack"),
                new ImageGridEntry(
                    "shader-library",
                    "Shader Library"),
                new ImageGridEntry(
                    "sound-effects",
                    "Sound Effects")
            });
            grid.SetSelectedItemIds(
                new[] { "summer-costume", "gesture-pack" },
                "gesture-pack");
            var slider = new StepperSlider(
                grid.ItemsPerRow,
                grid.RecommendedMinimumItemsPerRow,
                ImageGridView.MaximumItemsPerRow);
            slider.tooltip = "Grid columns";
            slider.ValueChanged += grid.SetItemsPerRow;
            grid.RecommendedMinimumItemsPerRowChanged += value =>
            {
                slider.SetRangeWithoutNotify(
                    value,
                    ImageGridView.MaximumItemsPerRow);
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
            surface.AddToClassList("ee4v-ui-common-component-story");
            surface.style.height = 120f;
            surface.style.paddingLeft = 12f;
            surface.style.paddingTop = 12f;

            var breadcrumb = new Breadcrumb();
            breadcrumb.SetItems(new[]
            {
                new BreadcrumbItem("Library", () => RecordAction(parent, "操作しました")),
                new BreadcrumbItem("Summer Costume")
            });
            surface.RegisterCallback<DetachFromPanelEvent>(_ =>
                breadcrumb.Dispose());
            surface.Add(breadcrumb);
            parent.Add(surface);
        }

        private static void BuildGridSizeSlider(VisualElement parent)
        {
            var slider = new StepperSlider(
                6,
                ImageGridView.MinimumItemsPerRow,
                ImageGridView.MaximumItemsPerRow);
            slider.style.width = 320f;
            parent.Add(slider);
        }

        private static void BuildEnumField(VisualElement parent)
        {
            var field = new EnumFormInput(
                "Condition",
                SampleChoice.HasTag,
                value => (SampleChoice)value == SampleChoice.HasTag ? "タグを含む"
                    : (SampleChoice)value == SampleChoice.NameContains ? "名前を含む" : "拡張子を含む");
            field.style.width = 420f;
            parent.Add(field);
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
