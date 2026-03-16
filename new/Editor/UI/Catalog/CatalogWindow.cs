using System;
using System.Collections.Generic;
using Ee4v.Core.I18n;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class CatalogWindow : EditorWindow
    {
        private enum ComponentImplementationKind
        {
            UiToolkit,
            Imgui
        }

        private enum UiCardStoryPreset
        {
            Default,
            Section,
            EmptyState
        }

        private readonly List<StoryDefinition> _stories = new List<StoryDefinition>();

        private VisualElement _navigatorHost;
        private VisualElement _contentHost;
        private StoryDefinition _selectedStory;
        private TreeView _navigatorTree;
        private readonly Dictionary<string, int> _navigatorStoryIds = new Dictionary<string, int>(StringComparer.Ordinal);
        private bool _isSyncingNavigatorSelection;

        [MenuItem("Debug/UI Catalog")]
        private static void ShowWindow()
        {
            var window = GetWindow<CatalogWindow>();
            window.minSize = new Vector2(UiTokens.WindowMinWidth, UiTokens.WindowMinHeight);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(I18N.Get("catalog.window.title"));
            EnsureStories();
        }

        private void CreateGUI()
        {
            RebuildWindow();
        }

        private void EnsureStories()
        {
            if (_stories.Count > 0)
            {
                return;
            }

            _stories.Add(new StoryDefinition("window-page", "Layout", "UiWindowPage", "タイトル、ツールバー、スクロール可能な本文を持つページ骨格です。", ComponentImplementationKind.UiToolkit, BuildWindowPageStory));
            _stories.Add(new StoryDefinition("toolbar-row", "Layout", "UiToolbarRow", "左右スロットを持つツールバー行です。", ComponentImplementationKind.UiToolkit, BuildToolbarRowStory));
            _stories.Add(new StoryDefinition("action-row", "Layout", "UiActionRow", "ボタン群を整列表示するアクション行です。", ComponentImplementationKind.UiToolkit, BuildActionRowStory));
            _stories.Add(new StoryDefinition("card", "Surface", "UiCard", "eyebrow と本文を持つカード面です。", ComponentImplementationKind.UiToolkit, BuildCardStory));
            _stories.Add(new StoryDefinition("alerts", "Feedback", "Alerts", "情報、警告、エラーを出し分けるアラートです。", ComponentImplementationKind.UiToolkit, BuildAlertsStory));
            _stories.Add(new StoryDefinition("status-badge", "Status", "StatusBadge", "状態を短く表示するコンパクトなバッジです。", ComponentImplementationKind.UiToolkit, BuildStatusBadgeStory));
            _stories.Add(new StoryDefinition("meta-list", "Data", "UiMetaList", "label/value を縦に並べるメタ情報リストです。", ComponentImplementationKind.UiToolkit, BuildMetaListStory));
            _stories.Add(new StoryDefinition("reference-row", "Results", "ReferenceRow", "Jump 操作付きの結果行です。", ComponentImplementationKind.UiToolkit, BuildReferenceRowStory));
            _stories.Add(new StoryDefinition("grouped-result-list", "Results", "GroupedResultList", "locale や scope ごとにまとめて表示する結果リストです。", ComponentImplementationKind.UiToolkit, BuildGroupedResultListStory));
            _stories.Add(new StoryDefinition("analyzer-result-section", "Results", "AnalyzerResultSection", "件数付きの結果セクションと空状態表示をまとめた部品です。", ComponentImplementationKind.UiToolkit, BuildAnalyzerResultSectionStory));
            _stories.Add(new StoryDefinition("feature-test-suite-card", "Testing", "FeatureTestSuiteCard", "実行ボタンと結果要約を含む suite カードです。", ComponentImplementationKind.UiToolkit, BuildFeatureTestSuiteCardStory));

            if (_selectedStory == null && _stories.Count > 0)
            {
                _selectedStory = _stories[0];
            }
        }

        private void RebuildWindow()
        {
            EnsureStories();
            titleContent = new GUIContent(I18N.Get("catalog.window.title"));

            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList(UiClassNames.Root);
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Styles/ee4v-ui.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Styles/catalog-window.uss");

            var shell = new VisualElement();
            shell.AddToClassList("ee4v-ui-catalog-shell");

            _navigatorHost = new VisualElement();
            _navigatorHost.AddToClassList("ee4v-ui-catalog-shell__navigator");

            _contentHost = new VisualElement();
            _contentHost.AddToClassList("ee4v-ui-catalog-shell__content");

            shell.Add(_navigatorHost);
            shell.Add(_contentHost);
            root.Add(shell);

            BuildNavigator();
            ShowStory(_selectedStory);
        }

        private void BuildNavigator()
        {
            _navigatorHost.Clear();

            var title = new Label(I18N.Get("catalog.window.title"));
            title.AddToClassList("ee4v-ui-catalog-shell__navigator-title");
            _navigatorHost.Add(title);

            var subtitle = new Label(I18N.Get("catalog.window.navigatorSubtitle"));
            subtitle.AddToClassList("ee4v-ui-catalog-shell__navigator-subtitle");
            _navigatorHost.Add(subtitle);

            _navigatorTree = new TreeView();
            _navigatorTree.AddToClassList("ee4v-ui-catalog-shell__navigator-tree");
            _navigatorTree.viewDataKey = "ee4v-ui-catalog-navigator-tree";
            _navigatorTree.selectionType = SelectionType.Single;
            _navigatorTree.fixedItemHeight = 20;
            _navigatorTree.makeItem = CreateNavigatorTreeItem;
            _navigatorTree.bindItem = BindNavigatorTreeItem;
            _navigatorTree.selectionChanged += OnNavigatorSelectionChanged;
            _navigatorTree.SetRootItems(BuildNavigatorTreeItems());
            _navigatorTree.Rebuild();
            _navigatorTree.ExpandAll();
            _navigatorTree.style.flexGrow = 1f;
            _navigatorHost.Add(_navigatorTree);

            RefreshNavigatorSelection();
        }

        private void SelectStory(StoryDefinition story)
        {
            if (story == null)
            {
                return;
            }

            _selectedStory = story;
            RefreshNavigatorSelection();
            ShowStory(story);
        }

        private void RefreshNavigatorSelection()
        {
            if (_navigatorTree != null)
            {
                if (_selectedStory == null)
                {
                    _isSyncingNavigatorSelection = true;
                    try
                    {
                        _navigatorTree.ClearSelection();
                    }
                    finally
                    {
                        _isSyncingNavigatorSelection = false;
                    }

                    return;
                }

                int itemId;
                if (_navigatorStoryIds.TryGetValue(_selectedStory.Id, out itemId))
                {
                    _isSyncingNavigatorSelection = true;
                    try
                    {
                        _navigatorTree.SetSelectionById(new[] { itemId });
                    }
                    finally
                    {
                        _isSyncingNavigatorSelection = false;
                    }
                }
            }
        }

        private void ShowStory(StoryDefinition story)
        {
            if (_contentHost == null || story == null)
            {
                return;
            }

            _contentHost.Clear();

            var page = new UiWindowPage(new UiWindowPageState(
                story.Title,
                story.Description));

            var breadcrumb = new Label(story.Group);
            breadcrumb.AddToClassList(UiClassNames.StatusBadge);
            breadcrumb.AddToClassList(UiClassNames.StatusIdle);

            var implementationBadge = new Label(GetImplementationLabel(story.Implementation));
            implementationBadge.AddToClassList(UiClassNames.StatusBadge);
            implementationBadge.AddToClassList(story.Implementation == ComponentImplementationKind.UiToolkit
                ? UiClassNames.StatusPassed
                : UiClassNames.StatusRunning);

            var reloadButton = new Button(() =>
            {
                I18N.Reload();
                RebuildWindow();
            })
            {
                text = I18N.Get("catalog.window.reloadLanguage")
            };
            reloadButton.style.minWidth = UiTokens.ActionButtonWidth;

            page.ToolbarLeft.Add(breadcrumb);
            page.ToolbarLeft.Add(implementationBadge);
            page.ToolbarRight.Add(reloadButton);
            page.Body.Add(CreateImplementationSection(story));

            story.Build(page);

            _contentHost.Add(page);
        }

        private void BuildWindowPageStory(UiWindowPage page)
        {
            var title = "ストーリーページ";
            var description = "入れ子になった UiWindowPage のプレビューです。";
            var showToolbar = true;
            var bodyMessage = "本文領域には任意の VisualElement を配置できます。";
            Action refresh = null;

            var controls = CreateControlsSection(page, "入れ子ページの見出しや本文を編集します。");
            AddTextField(controls.Body, "タイトル", title, value =>
            {
                title = value;
                refresh();
            });
            AddTextField(controls.Body, "説明", description, value =>
            {
                description = value;
                refresh();
            });
            AddTextField(controls.Body, "本文メッセージ", bodyMessage, value =>
            {
                bodyMessage = value;
                refresh();
            });
            AddToggle(controls.Body, "ツールバーを表示", showToolbar, value =>
            {
                showToolbar = value;
                refresh();
            });

            var preview = CreatePreviewSection(page);
            var previewSurface = CreatePreviewSurface();
            var nestedPage = new UiWindowPage();
            previewSurface.Add(nestedPage);
            preview.Body.Add(previewSurface);

            refresh = () =>
            {
                nestedPage.SetState(new UiWindowPageState(title, description, showToolbar));
                nestedPage.ToolbarLeft.Clear();
                nestedPage.ToolbarRight.Clear();
                nestedPage.Body.Clear();

                nestedPage.ToolbarLeft.Add(new Label("左スロット"));
                nestedPage.ToolbarRight.Add(CreatePreviewButton("操作"));
                nestedPage.Body.Add(new UiCard(new UiCardState("本文", bodyMessage, "プレビュー")));
            };

            refresh();
        }

        private void BuildToolbarRowStory(UiWindowPage page)
        {
            var leftText = "プロジェクトツールバー";
            var rightText = "準備完了";
            var quiet = false;
            var showAction = true;
            Action refresh = null;

            var controls = CreateControlsSection(page, "左右スロットの文言とバリエーションを編集します。");
            AddTextField(controls.Body, "左テキスト", leftText, value =>
            {
                leftText = value;
                refresh();
            });
            AddTextField(controls.Body, "右テキスト", rightText, value =>
            {
                rightText = value;
                refresh();
            });
            AddToggle(controls.Body, "控えめな見た目", quiet, value =>
            {
                quiet = value;
                refresh();
            });
            AddToggle(controls.Body, "操作ボタンを表示", showAction, value =>
            {
                showAction = value;
                refresh();
            });

            var preview = CreatePreviewSection(page);
            var previewSurface = CreatePreviewSurface();
            var toolbar = new UiToolbarRow();
            previewSurface.Add(toolbar);
            preview.Body.Add(previewSurface);

            refresh = () =>
            {
                toolbar.SetState(new UiToolbarRowState(quiet));
                toolbar.LeftSlot.Clear();
                toolbar.RightSlot.Clear();

                toolbar.LeftSlot.Add(new Label(leftText));

                if (showAction)
                {
                    toolbar.RightSlot.Add(CreatePreviewButton("実行"));
                }

                toolbar.RightSlot.Add(new Label(rightText));
            };

            refresh();
        }

        private void BuildCardStory(UiWindowPage page)
        {
            var preset = UiCardStoryPreset.Default;
            var eyebrow = "Core";
            var title = "Feature Test Manager";
            var description = "密度の高い Editor パネル向けのカードレイアウトです。";
            var badgeText = string.Empty;
            var bodyText = "カードはセクション内に積んだり、単体の表示面として使えます。";
            Action refresh = null;
            Action<UiCardStoryPreset> applyPreset = selectedPreset =>
            {
                preset = selectedPreset;
                switch (selectedPreset)
                {
                    case UiCardStoryPreset.Section:
                        eyebrow = string.Empty;
                        title = "不足キー";
                        description = "解析結果のグループ表示を包むセクションです。";
                        badgeText = "12";
                        bodyText = "本文には結果リスト、カード、任意のコントロールを配置できます。";
                        break;
                    case UiCardStoryPreset.EmptyState:
                        eyebrow = string.Empty;
                        title = "結果なし";
                        description = "現在の条件で表示対象がないときに使う状態です。";
                        badgeText = string.Empty;
                        bodyText = string.Empty;
                        break;
                    default:
                        eyebrow = "Core";
                        title = "Feature Test Manager";
                        description = "密度の高い Editor パネル向けのカードレイアウトです。";
                        badgeText = string.Empty;
                        bodyText = "カードはセクション内に積んだり、単体の表示面として使えます。";
                        break;
                }

                if (refresh != null)
                {
                    refresh();
                }
            };

            var controls = CreateControlsSection(page, "カードのメタ情報と補助文言を編集します。");
            var presetRow = new VisualElement();
            presetRow.AddToClassList(UiClassNames.CatalogButtonRow);
            presetRow.Add(new Label("プリセット"));
            presetRow.Add(new Button(() => applyPreset(UiCardStoryPreset.Default)) { text = "標準" });
            presetRow.Add(new Button(() => applyPreset(UiCardStoryPreset.Section)) { text = "UiSection" });
            presetRow.Add(new Button(() => applyPreset(UiCardStoryPreset.EmptyState)) { text = "UiEmptyState" });
            controls.Body.Add(presetRow);

            var eyebrowField = AddTextField(controls.Body, "Eyebrow", eyebrow, value =>
            {
                eyebrow = value;
                refresh();
            });
            var titleField = AddTextField(controls.Body, "タイトル", title, value =>
            {
                title = value;
                refresh();
            });
            var descriptionField = AddTextField(controls.Body, "説明", description, value =>
            {
                description = value;
                refresh();
            });
            var badgeField = AddTextField(controls.Body, "バッジ", badgeText, value =>
            {
                badgeText = value;
                refresh();
            });
            var bodyTextField = AddTextField(controls.Body, "本文テキスト", bodyText, value =>
            {
                bodyText = value;
                refresh();
            }, true);

            var preview = CreatePreviewSection(page);
            var card = new UiCard();
            preview.Body.Add(card);

            refresh = () =>
            {
                eyebrowField.SetValueWithoutNotify(eyebrow);
                titleField.SetValueWithoutNotify(title);
                descriptionField.SetValueWithoutNotify(description);
                badgeField.SetValueWithoutNotify(badgeText);
                bodyTextField.SetValueWithoutNotify(bodyText);

                card.SetState(new UiCardState(title, description, eyebrow, badgeText));
                card.Body.Clear();

                if (!string.IsNullOrWhiteSpace(bodyText))
                {
                    var bodyLabel = new Label(bodyText);
                    bodyLabel.style.whiteSpace = WhiteSpace.Normal;
                    card.Body.Add(bodyLabel);
                }
            };

            applyPreset(preset);
        }

        private void BuildAlertsStory(UiWindowPage page)
        {
            var tone = UiBannerTone.Info;
            var title = "情報表示";
            var message = "非ブロッキングな案内やエラー通知に使います。";
            Action refresh = null;

            var controls = CreateControlsSection(page, "バナーの種類と文言を編集します。");
            AddEnumField(controls.Body, "種類", tone, value =>
            {
                tone = value;
                refresh();
            });
            AddTextField(controls.Body, "タイトル", title, value =>
            {
                title = value;
                refresh();
            });
            AddTextField(controls.Body, "メッセージ", message, value =>
            {
                message = value;
                refresh();
            });

            var preview = CreatePreviewSection(page);
            var alerts = new Alerts();
            preview.Body.Add(CreatePreviewSurface(alerts));

            refresh = () =>
            {
                alerts.SetState(new AlertsState(tone, title, message));
            };

            refresh();
        }

        private void BuildActionRowStory(UiWindowPage page)
        {
            var compact = false;
            var primaryText = "実行";
            var secondaryText = "設定を開く";
            var showLeftLabel = true;
            Action refresh = null;

            var controls = CreateControlsSection(page, "アクション名とレイアウトを編集します。");
            AddToggle(controls.Body, "コンパクト", compact, value =>
            {
                compact = value;
                refresh();
            });
            AddTextField(controls.Body, "主ボタン", primaryText, value =>
            {
                primaryText = value;
                refresh();
            });
            AddTextField(controls.Body, "副ボタン", secondaryText, value =>
            {
                secondaryText = value;
                refresh();
            });
            AddToggle(controls.Body, "左ラベルを表示", showLeftLabel, value =>
            {
                showLeftLabel = value;
                refresh();
            });

            var preview = CreatePreviewSection(page);
            var actionRow = new UiActionRow();
            preview.Body.Add(CreatePreviewSurface(actionRow));

            refresh = () =>
            {
                actionRow.SetState(new UiActionRowState(compact));
                actionRow.LeftSlot.Clear();
                actionRow.RightSlot.Clear();

                if (showLeftLabel)
                {
                    actionRow.LeftSlot.Add(new Label("フッター操作"));
                }

                actionRow.RightSlot.Add(CreatePreviewButton(primaryText));
                actionRow.RightSlot.Add(CreatePreviewButton(secondaryText));
            };

            refresh();
        }

        private void BuildStatusBadgeStory(UiWindowPage page)
        {
            var text = "実行中";
            var tone = UiStatusTone.Running;
            Action refresh = null;

            var controls = CreateControlsSection(page, "状態文言と種類を編集します。");
            AddTextField(controls.Body, "テキスト", text, value =>
            {
                text = value;
                refresh();
            });
            AddEnumField(controls.Body, "種類", tone, value =>
            {
                tone = value;
                refresh();
            });

            var preview = CreatePreviewSection(page);
            var badge = new StatusBadge();
            var surface = CreatePreviewSurface();
            surface.Add(badge);
            preview.Body.Add(surface);

            refresh = () =>
            {
                badge.SetState(new StatusBadgeState(text, tone));
            };

            refresh();
        }

        private void BuildMetaListStory(UiWindowPage page)
        {
            var rowCount = 3;
            var labelPrefix = "項目";
            var valuePrefix = "値";
            Action refresh = null;

            var controls = CreateControlsSection(page, "行数と接頭辞を編集します。");
            AddIntegerField(controls.Body, "行数", rowCount, value =>
            {
                rowCount = Mathf.Clamp(value, 1, 5);
                refresh();
            });
            AddTextField(controls.Body, "ラベル接頭辞", labelPrefix, value =>
            {
                labelPrefix = value;
                refresh();
            });
            AddTextField(controls.Body, "値接頭辞", valuePrefix, value =>
            {
                valuePrefix = value;
                refresh();
            });

            var preview = CreatePreviewSection(page);
            var metaList = new UiMetaList();
            preview.Body.Add(CreatePreviewSurface(metaList));

            refresh = () =>
            {
                var items = new List<UiMetaListItem>();
                for (var i = 0; i < rowCount; i++)
                {
                    items.Add(new UiMetaListItem(labelPrefix + " " + (i + 1), valuePrefix + " " + (i + 1)));
                }

                metaList.SetState(new UiMetaListState(items));
            };

            refresh();
        }

        private void BuildReferenceRowStory(UiWindowPage page)
        {
            var primary = "testing.window.runAll";
            var secondary = "FeatureTestManagerWindow.cs:81";
            var actionLabel = "Jump";
            var actionEnabled = true;
            Action refresh = null;

            var controls = CreateControlsSection(page, "結果行の内容と操作可否を編集します。");
            AddTextField(controls.Body, "主テキスト", primary, value =>
            {
                primary = value;
                refresh();
            });
            AddTextField(controls.Body, "補助テキスト", secondary, value =>
            {
                secondary = value;
                refresh();
            });
            AddTextField(controls.Body, "操作ラベル", actionLabel, value =>
            {
                actionLabel = value;
                refresh();
            });
            AddToggle(controls.Body, "操作を有効化", actionEnabled, value =>
            {
                actionEnabled = value;
                refresh();
            });

            var preview = CreatePreviewSection(page);
            var referenceRow = new ReferenceRow();
            preview.Body.Add(CreatePreviewSurface(referenceRow));

            refresh = () =>
            {
                referenceRow.SetState(new ReferenceRowState(
                    primary,
                    secondary,
                    actionLabel,
                    () => Debug.Log("[ee4v:ui] ReferenceRow preview action."),
                    actionEnabled));
            };

            refresh();
        }

        private void BuildGroupedResultListStory(UiWindowPage page)
        {
            var groupTitle = "[ja-JP][Core] 3 item(s)";
            var groupDescription = "各行は解析結果の参照情報を表します。";
            var rowCount = 3;
            var actionLabel = "Jump";
            Action refresh = null;

            var controls = CreateControlsSection(page, "グループ見出しと行密度を編集します。");
            AddTextField(controls.Body, "グループタイトル", groupTitle, value =>
            {
                groupTitle = value;
                refresh();
            });
            AddTextField(controls.Body, "グループ説明", groupDescription, value =>
            {
                groupDescription = value;
                refresh();
            });
            AddIntegerField(controls.Body, "行数", rowCount, value =>
            {
                rowCount = Mathf.Clamp(value, 1, 6);
                refresh();
            });
            AddTextField(controls.Body, "操作ラベル", actionLabel, value =>
            {
                actionLabel = value;
                refresh();
            });

            var preview = CreatePreviewSection(page);
            var groupedList = new GroupedResultList();
            preview.Body.Add(CreatePreviewSurface(groupedList));

            refresh = () =>
            {
                var rows = new List<ReferenceRowState>();
                for (var i = 0; i < rowCount; i++)
                {
                    rows.Add(new ReferenceRowState(
                        "settings.field." + (i + 1),
                        "Definitions.cs:" + (10 + i),
                        actionLabel,
                        () => Debug.Log("[ee4v:ui] GroupedResultList preview action.")));
                }

                groupedList.SetState(new GroupedResultListState(new[]
                {
                    new GroupedResultGroupState(groupTitle, rows, groupDescription)
                }));
            };

            refresh();
        }

        private void BuildAnalyzerResultSectionStory(UiWindowPage page)
        {
            var title = "不足キー";
            var description = "グループ結果と空状態を切り替えられる解析セクションです。";
            var rowCount = 2;
            var populated = true;
            var emptyTitle = "問題は見つかりませんでした";
            var emptyMessage = "現在の解析結果には表示する行がありません。";
            Action refresh = null;

            var controls = CreateControlsSection(page, "結果ありと空状態を切り替えて確認します。");
            AddTextField(controls.Body, "タイトル", title, value =>
            {
                title = value;
                refresh();
            });
            AddTextField(controls.Body, "説明", description, value =>
            {
                description = value;
                refresh();
            });
            AddToggle(controls.Body, "結果あり", populated, value =>
            {
                populated = value;
                refresh();
            });
            AddIntegerField(controls.Body, "行数", rowCount, value =>
            {
                rowCount = Mathf.Clamp(value, 1, 6);
                refresh();
            });
            AddTextField(controls.Body, "空状態タイトル", emptyTitle, value =>
            {
                emptyTitle = value;
                refresh();
            });
            AddTextField(controls.Body, "空状態メッセージ", emptyMessage, value =>
            {
                emptyMessage = value;
                refresh();
            });

            var preview = CreatePreviewSection(page);
            var analyzerSection = new AnalyzerResultSection();
            preview.Body.Add(CreatePreviewSurface(analyzerSection));

            refresh = () =>
            {
                var groups = new List<GroupedResultGroupState>();
                if (populated)
                {
                    var rows = new List<ReferenceRowState>();
                    for (var i = 0; i < rowCount; i++)
                    {
                        rows.Add(new ReferenceRowState(
                            "catalog.sample.key." + (i + 1),
                            "CatalogWindow.cs:" + (80 + i),
                            "Jump",
                            () => Debug.Log("[ee4v:ui] AnalyzerResultSection preview action.")));
                    }

                    groups.Add(new GroupedResultGroupState("[en-US][UI] " + rowCount + " item(s)", rows));
                }

                analyzerSection.SetState(new AnalyzerResultSectionState(
                    title,
                    description,
                    new GroupedResultListState(groups),
                    emptyTitle,
                    emptyMessage));
            };

            refresh();
        }

        private void BuildFeatureTestSuiteCardStory(UiWindowPage page)
        {
            var title = "Core suite";
            var scope = "Core";
            var assembly = "Ee4v.Core.Tests.Editor";
            var description = "metadata、登録済み case、直近結果をまとめて見せる suite カードです。";
            var runButton = "実行";
            var canRun = true;
            var caseCount = 2;
            var statusText = "成功";
            var statusTone = UiStatusTone.Passed;
            var resultTitle = "直近結果: 成功";
            var resultCounts = "Pass 4  Fail 0  Skip 0  Inc 0  0.48s";
            var resultMessage = "登録されたテストケースはすべて成功しました。";
            var resultTone = UiBannerTone.Info;
            Action refresh = null;

            var controls = CreateControlsSection(page, "suite 情報、結果文言、状態を編集します。");
            AddTextField(controls.Body, "タイトル", title, value =>
            {
                title = value;
                refresh();
            });
            AddTextField(controls.Body, "Scope", scope, value =>
            {
                scope = value;
                refresh();
            });
            AddTextField(controls.Body, "Assembly", assembly, value =>
            {
                assembly = value;
                refresh();
            });
            AddTextField(controls.Body, "説明", description, value =>
            {
                description = value;
                refresh();
            });
            AddTextField(controls.Body, "実行ラベル", runButton, value =>
            {
                runButton = value;
                refresh();
            });
            AddToggle(controls.Body, "実行可能", canRun, value =>
            {
                canRun = value;
                refresh();
            });
            AddIntegerField(controls.Body, "ケース数", caseCount, value =>
            {
                caseCount = Mathf.Clamp(value, 1, 5);
                refresh();
            });
            AddTextField(controls.Body, "状態テキスト", statusText, value =>
            {
                statusText = value;
                refresh();
            });
            AddEnumField(controls.Body, "状態種類", statusTone, value =>
            {
                statusTone = value;
                refresh();
            });
            AddTextField(controls.Body, "結果タイトル", resultTitle, value =>
            {
                resultTitle = value;
                refresh();
            });
            AddTextField(controls.Body, "結果集計", resultCounts, value =>
            {
                resultCounts = value;
                refresh();
            });
            AddTextField(controls.Body, "結果メッセージ", resultMessage, value =>
            {
                resultMessage = value;
                refresh();
            });
            AddEnumField(controls.Body, "結果種類", resultTone, value =>
            {
                resultTone = value;
                refresh();
            });

            var preview = CreatePreviewSection(page);
            var suiteCard = new FeatureTestSuiteCard();
            preview.Body.Add(CreatePreviewSurface(suiteCard));

            refresh = () =>
            {
                var metaItems = new List<UiMetaListItem>
                {
                    new UiMetaListItem("Scope", scope),
                    new UiMetaListItem("Assembly", assembly)
                };

                var cases = new List<FeatureTestSuiteCaseState>();
                for (var i = 0; i < caseCount; i++)
                {
                    cases.Add(new FeatureTestSuiteCaseState(
                        "Case " + (i + 1),
                        "テストケース " + (i + 1) + " のプレビュー説明です。"));
                }

                suiteCard.SetState(new FeatureTestSuiteCardState(
                    title,
                    scope,
                    assembly,
                    description,
                    new UiMetaListState(metaItems),
                    cases,
                    runButton,
                    () => Debug.Log("[ee4v:ui] FeatureTestSuiteCard preview action."),
                    canRun,
                    statusText,
                    statusTone,
                    resultTitle,
                    resultCounts,
                    resultMessage,
                    resultTone));
            };

            refresh();
        }

        private UiCard CreateControlsSection(UiWindowPage page, string description)
        {
            var card = new UiCard(new UiCardState("コントロール", description));
            card.userData = "catalog-controls-section";
            page.Body.Add(card);
            return card;
        }

        private UiCard CreatePreviewSection(UiWindowPage page)
        {
            var card = new UiCard(new UiCardState("プレビュー", "コントロールの変更はすぐにプレビューへ反映されます。"));
            var inserted = false;
            for (var i = 0; i < page.Body.childCount; i++)
            {
                var child = page.Body.ElementAt(i);
                if (!Equals(child.userData, "catalog-controls-section"))
                {
                    continue;
                }

                page.Body.Insert(i, card);
                inserted = true;
                break;
            }

            if (!inserted)
            {
                page.Body.Add(card);
            }

            return card;
        }

        private VisualElement CreatePreviewSurface()
        {
            var surface = new VisualElement();
            surface.AddToClassList("ee4v-ui-catalog-preview-surface");
            return surface;
        }

        private VisualElement CreatePreviewSurface(VisualElement content)
        {
            var surface = CreatePreviewSurface();
            surface.Add(content);
            return surface;
        }

        private Button CreatePreviewButton(string label)
        {
            return new Button(() => Debug.Log("[ee4v:ui] Catalog preview button."))
            {
                text = label
            };
        }

        private static TextField AddTextField(VisualElement parent, string label, string value, Action<string> onChanged, bool multiline = false)
        {
            var field = new TextField(label);
            field.multiline = multiline;
            if (multiline)
            {
                field.style.minHeight = 72f;
            }

            field.value = value;
            field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            parent.Add(field);
            return field;
        }

        private static Toggle AddToggle(VisualElement parent, string label, bool value, Action<bool> onChanged)
        {
            var field = new Toggle(label);
            field.value = value;
            field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            parent.Add(field);
            return field;
        }

        private static IntegerField AddIntegerField(VisualElement parent, string label, int value, Action<int> onChanged)
        {
            var field = new IntegerField(label);
            field.value = value;
            field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            parent.Add(field);
            return field;
        }

        private static EnumField AddEnumField<TEnum>(VisualElement parent, string label, TEnum value, Action<TEnum> onChanged)
            where TEnum : struct, Enum
        {
            var field = new EnumField(label, (Enum)(object)value);
            field.RegisterValueChangedCallback(evt => onChanged((TEnum)(object)evt.newValue));
            parent.Add(field);
            return field;
        }

        private VisualElement CreateNavigatorTreeItem()
        {
            var row = new VisualElement();
            row.AddToClassList("ee4v-ui-catalog-tree-item");

            var title = new Label();
            title.AddToClassList("ee4v-ui-catalog-tree-item__title");

            var implementation = new Label();
            implementation.AddToClassList("ee4v-ui-catalog-tree-item__implementation");

            row.Add(title);
            row.Add(implementation);
            return row;
        }

        private void BindNavigatorTreeItem(VisualElement element, int index)
        {
            var node = _navigatorTree.GetItemDataForIndex<NavigatorTreeNode>(index);
            var title = element.ElementAt(0) as Label;
            var implementation = element.ElementAt(1) as Label;

            if (title != null)
            {
                title.text = node.Title;
            }

            if (implementation != null)
            {
                implementation.text = node.ImplementationShortLabel;
                implementation.EnableInClassList("ee4v-ui-catalog-tree-item__implementation--hidden", string.IsNullOrEmpty(node.ImplementationShortLabel));
            }
        }

        private void OnNavigatorSelectionChanged(IEnumerable<object> items)
        {
            if (_isSyncingNavigatorSelection || items == null)
            {
                return;
            }

            foreach (var item in items)
            {
                var node = item as NavigatorTreeNode;
                if (node != null && node.Story != null)
                {
                    SelectStory(node.Story);
                    return;
                }
            }
        }

        private List<TreeViewItemData<NavigatorTreeNode>> BuildNavigatorTreeItems()
        {
            _navigatorStoryIds.Clear();

            var roots = new List<NavigatorTreeNodeBuilder>();
            var folders = new Dictionary<string, NavigatorTreeNodeBuilder>(StringComparer.Ordinal);
            var nextId = 1;

            for (var i = 0; i < _stories.Count; i++)
            {
                var story = _stories[i];
                var currentChildren = roots;
                var segments = story.Group.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                var path = string.Empty;

                for (var segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
                {
                    path = string.IsNullOrEmpty(path)
                        ? segments[segmentIndex]
                        : path + "/" + segments[segmentIndex];

                    NavigatorTreeNodeBuilder folder;
                    if (!folders.TryGetValue(path, out folder))
                    {
                        folder = new NavigatorTreeNodeBuilder(
                            nextId++,
                            new NavigatorTreeNode(segments[segmentIndex], string.Empty, null));
                        folders.Add(path, folder);
                        currentChildren.Add(folder);
                    }

                    currentChildren = folder.Children;
                }

                var storyNode = new NavigatorTreeNodeBuilder(
                    nextId++,
                    new NavigatorTreeNode(story.Title, GetImplementationShortLabel(story.Implementation), story));
                currentChildren.Add(storyNode);
                _navigatorStoryIds[story.Id] = storyNode.Id;
            }

            return ConvertNavigatorTreeItems(roots);
        }

        private static List<TreeViewItemData<NavigatorTreeNode>> ConvertNavigatorTreeItems(IReadOnlyList<NavigatorTreeNodeBuilder> builders)
        {
            var items = new List<TreeViewItemData<NavigatorTreeNode>>(builders.Count);
            for (var i = 0; i < builders.Count; i++)
            {
                items.Add(new TreeViewItemData<NavigatorTreeNode>(
                    builders[i].Id,
                    builders[i].Node,
                    ConvertNavigatorTreeItems(builders[i].Children)));
            }

            return items;
        }

        private UiCard CreateImplementationSection(StoryDefinition story)
        {
            var card = new UiCard(new UiCardState(
                I18N.Get("catalog.common.implementation"),
                I18N.Get("catalog.common.implementationDescription"),
                null,
                GetImplementationLabel(story.Implementation)));

            card.Body.Add(new UiMetaList(new UiMetaListState(new List<UiMetaListItem>
            {
                new UiMetaListItem(I18N.Get("catalog.common.implementation"), GetImplementationLabel(story.Implementation)),
                new UiMetaListItem(I18N.Get("catalog.common.policy"), GetImplementationPolicy(story.Implementation))
            })));

            return card;
        }

        private static string GetImplementationShortLabel(ComponentImplementationKind implementation)
        {
            switch (implementation)
            {
                case ComponentImplementationKind.Imgui:
                    return "IMGUI";
                default:
                    return string.Empty;
            }
        }

        private static string GetImplementationLabel(ComponentImplementationKind implementation)
        {
            switch (implementation)
            {
                case ComponentImplementationKind.Imgui:
                    return I18N.Get("catalog.common.imgui");
                default:
                    return I18N.Get("catalog.common.uiToolkit");
            }
        }

        private static string GetImplementationPolicy(ComponentImplementationKind implementation)
        {
            switch (implementation)
            {
                case ComponentImplementationKind.Imgui:
                    return I18N.Get("catalog.common.imguiPolicy");
                default:
                    return I18N.Get("catalog.common.uiToolkitPolicy");
            }
        }

        private sealed class StoryDefinition
        {
            public StoryDefinition(string id, string group, string title, string description, ComponentImplementationKind implementation, Action<UiWindowPage> build)
            {
                Id = id;
                Group = group;
                Title = title;
                Description = description;
                Implementation = implementation;
                Build = build;
            }

            public string Id { get; }

            public string Group { get; }

            public string Title { get; }

            public string Description { get; }

            public ComponentImplementationKind Implementation { get; }

            public Action<UiWindowPage> Build { get; }
        }

        private sealed class NavigatorTreeNode
        {
            public NavigatorTreeNode(string title, string implementationShortLabel, StoryDefinition story)
            {
                Title = title ?? string.Empty;
                ImplementationShortLabel = implementationShortLabel ?? string.Empty;
                Story = story;
            }

            public string Title { get; }

            public string ImplementationShortLabel { get; }

            public StoryDefinition Story { get; }
        }

        private sealed class NavigatorTreeNodeBuilder
        {
            public NavigatorTreeNodeBuilder(int id, NavigatorTreeNode node)
            {
                Id = id;
                Node = node;
                Children = new List<NavigatorTreeNodeBuilder>();
            }

            public int Id { get; }

            public NavigatorTreeNode Node { get; }

            public List<NavigatorTreeNodeBuilder> Children { get; }
        }
    }
}
