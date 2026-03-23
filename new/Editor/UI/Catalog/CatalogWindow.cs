using System;
using System.Collections.Generic;
using Ee4v.Core.I18n;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed class CatalogWindow : EditorWindow
    {
        private const string RootClassName = "ee4v-ui";
        private enum ComponentImplementationKind
        {
            UiToolkit,
            Imgui
        }

        private enum InfoCardStoryPreset
        {
            Simple,
            Result
        }

        private enum WindowToastStoryPreset
        {
            Info,
            Success,
            Warning,
            Error
        }

        private readonly List<StoryDefinition> _stories = new List<StoryDefinition>();

        private VisualElement _navigatorHost;
        private VisualElement _contentHost;
        private StoryDefinition _selectedStory;
        private SearchableTreeView<NavigatorTreeNode> _navigatorTreeView;
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

            _stories.Add(new StoryDefinition(
                "search-field",
                "DataView",
                "SearchField",
                "検索入力と clear 操作をまとめた単体利用向けの検索コンポーネントです。",
                "一覧やカード列の絞り込みに使う軽量な検索入力です。placeholder と clear button を持ち、SearchableTreeView の検索 UI と同じ見た目・挙動を単体でも使えます。",
                new[]
                {
                    "Icon"
                },
                ComponentImplementationKind.UiToolkit,
                BuildSearchFieldStory));

            _stories.Add(new StoryDefinition(
                "searchable-tree-view",
                "DataView",
                "SearchableTreeView",
                "検索窓と tree view をまとめて提供する、絞り込み可能なツリーコンポーネントです。",
                "呼び出し側は階層データと row 描画だけを渡し、検索文字列の状態管理や tree の絞り込みは component 側に任せます。検索欄は SearchField を内部利用し、tree 本体と同じ面の中で扱います。",
                new[]
                {
                    "SearchField"
                },
                ComponentImplementationKind.UiToolkit,
                BuildSearchableTreeViewStory));

            _stories.Add(new StoryDefinition(
                "copyable-text-area",
                "Display",
                "CopyableTextArea",
                "長文の確認結果を選択・コピーできる、読み取り専用のテキスト領域コンポーネントです。",
                "右上に copy button を持つ readonly multiline text field です。テスト詳細や監査ログのような長文を表示し、そのまま clipboard へ渡す用途を想定しています。",
                new string[0],
                ComponentImplementationKind.UiToolkit,
                BuildCopyableTextAreaStory));

            _stories.Add(new StoryDefinition(
                "window-toast",
                "Overlay",
                "WindowToast",
                "ee4v 自前 EditorWindow に後付けできる、右上スタック型の toast 通知基盤です。",
                "window root に absolute overlay host を追加し、info/success/warning/error の通知を縦に積みます。action button を持つ toast も同じ面の中で扱えます。",
                new string[0],
                ComponentImplementationKind.UiToolkit,
                BuildWindowToastStory));

            _stories.Add(new StoryDefinition(
                "test-result-group",
                "Domain/Testing",
                "TestResultGroup",
                "feature test の状態、件数 alert、実行導線、登録テスト一覧をまとめて表示する testing 向けコンポーネントです。",
                "ee4v Test Manager の結果表示用に作った domain-specific component です。InfoCard を土台にしつつ、header 右側に status badge と run button、body に件数 alert、copy 可能な詳細結果、登録済みテスト一覧の開閉を持たせています。",
                new[]
                {
                    "InfoCard",
                    "StatusBadge",
                    "Alerts",
                    "CopyableTextArea"
                },
                ComponentImplementationKind.UiToolkit,
                BuildTestResultGroupStory));

            _stories.Add(new StoryDefinition(
                "tab-card",
                "Interactive",
                "TabCard",
                "左上のタブ列で内容を切り替える box コンポーネントです。",
                "ブラウザのタブのように、上部タブを切り替えながら下部 panel の内容を差し替える用途を想定しています。content slot には任意の UI 要素を配置できます。",
                new string[0],
                ComponentImplementationKind.UiToolkit,
                BuildTabCardStory));

            _stories.Add(new StoryDefinition(
                "info-card",
                "Display",
                "InfoCard",
                "タイトル、説明、eyebrow、badge、body を組み合わせて情報面を構成する基本コンポーネントです。",
                "シンプルな情報表示から、結果一覧の見出し付きカードまで幅広く使う土台です。header の各値が欠けても自然に見えるように余白を調整し、内蔵の badge と本文を組み合わせて情報密度を調整できます。",
                new string[0],
                ComponentImplementationKind.UiToolkit,
                BuildInfoCardStory));

            _stories.Add(new StoryDefinition(
                "alerts",
                "Display",
                "Alerts",
                "情報、警告、エラーの tone を切り替えてメッセージを表示する通知コンポーネントです。",
                "非ブロッキングな案内からエラー通知までを同じ構造で扱います。タイトルとメッセージの両方を持てるので、短い要約と補足説明を分けて表示できます。",
                new string[0],
                ComponentImplementationKind.UiToolkit,
                BuildAlertsStory));

            _stories.Add(new StoryDefinition(
                "status-badge",
                "Display",
                "StatusBadge",
                "短い状態テキストを pill 形で表示するステータス表示コンポーネントです。",
                "カード header や一覧の補助情報に載せる小さな状態表示です。長めのテキストでも楕円に潰れず、pill 形を維持する前提で調整しています。",
                new string[0],
                ComponentImplementationKind.UiToolkit,
                BuildStatusBadgeStory));

            _stories.Add(new StoryDefinition(
                "icon",
                "Display",
                "Icon",
                "任意の texture または enum 管理された Unity 内蔵アイコンを表示するアイコンコンポーネントです。",
                "Unity 内蔵アイコンは version 差分の影響を抑えるため enum で許可したものだけを解決します。初期状態では検索アイコンをサポートし、custom texture に切り替えれば任意 texture を表示できます。",
                new string[0],
                ComponentImplementationKind.UiToolkit,
                BuildIconStory));

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
            root.AddToClassList(RootClassName);
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/DataView/search-field.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/DataView/searchable-tree-view.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Domain/test-result-group.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Display/info-card.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Interactive/tab-card.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Display/alerts.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Display/status-badge.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Display/copyable-text-area.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Display/window-toast.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Components/Display/icon.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/UI/Catalog/catalog-window.uss");

            var shell = new VisualElement();
            shell.AddToClassList("ee4v-ui-catalog-shell");

            _navigatorHost = new VisualElement();
            _navigatorHost.AddToClassList("ee4v-ui-catalog-shell__navigator");

            _contentHost = new VisualElement();
            _contentHost.AddToClassList("ee4v-ui-catalog-shell__content");

            shell.Add(_navigatorHost);
            shell.Add(_contentHost);
            root.Add(shell);
            WindowToastApi.EnsureHost(this);

            BuildNavigator();
            ShowStory(_selectedStory);
        }

        private void BuildNavigator()
        {
            _navigatorHost.Clear();

            var title = UiTextFactory.Create(I18N.Get("catalog.window.title"), UiClassNames.CatalogNavigatorTitle);
            _navigatorHost.Add(title);

            _navigatorTreeView = new SearchableTreeView<NavigatorTreeNode>(
                CreateNavigatorTreeItem,
                BindNavigatorTreeItem,
                OnNavigatorSelectionChanged,
                I18N.Get("catalog.window.navigatorEmpty"));
            _navigatorTreeView.SetViewDataKey("ee4v-ui-catalog-navigator-tree");
            _navigatorTreeView.SetItems(BuildNavigatorTreeItems());
            _navigatorHost.Add(_navigatorTreeView);

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
            if (_navigatorTreeView == null)
            {
                return;
            }

            if (_selectedStory == null)
            {
                _isSyncingNavigatorSelection = true;
                try
                {
                    _navigatorTreeView.ClearSelection();
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
                    _navigatorTreeView.SetSelectionById(new[] { itemId });
                }
                finally
                {
                    _isSyncingNavigatorSelection = false;
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

            var page = new VisualElement();
            page.AddToClassList("ee4v-ui-catalog-page");

            var header = new VisualElement();
            header.AddToClassList("ee4v-ui-catalog-page__header");
            header.Add(UiTextFactory.Create(story.Title, UiClassNames.CatalogPageTitle));
            header.Add(UiTextFactory.Create(story.Description, UiClassNames.CatalogPageDescription));

            var body = new ScrollView();
            body.AddToClassList("ee4v-ui-catalog-page__body");

            page.Add(header);
            page.Add(body);

            body.contentContainer.Add(CreateDetailsSection(story));
            story.Build(body.contentContainer);

            _contentHost.Add(page);
        }

        private void BuildInfoCardStory(VisualElement parent)
        {
            var preset = InfoCardStoryPreset.Simple;
            var eyebrow = string.Empty;
            var title = "Feature Test Manager";
            var description = string.Empty;
            var badgeText = string.Empty;
            var bodyText = "カードは単体の情報表示面や、設定グループの土台として使えます。";
            Action refresh = null;

            Action<InfoCardStoryPreset> applyPreset = selectedPreset =>
            {
                preset = selectedPreset;
                switch (selectedPreset)
                {
                    case InfoCardStoryPreset.Result:
                        eyebrow = "I18N";
                        title = "解析結果";
                        description = "件数付きの結果カードとして使う用途を想定した preset です。";
                        badgeText = "12";
                        bodyText = "不足キー 8 件\n未参照エントリ 4 件";
                        break;
                    default:
                        eyebrow = string.Empty;
                        title = "Feature Test Manager";
                        description = string.Empty;
                        badgeText = string.Empty;
                        bodyText = "カードは単体の情報表示面や、設定グループの土台として使えます。";
                        break;
                }

                if (refresh != null)
                {
                    refresh();
                }
            };

            var controls = CreateTabbedControlsSection(parent, "InfoCard の各プロパティを編集し、値の有無ごとの見た目を確認します。");

            var eyebrowField = AddTextField(controls.Content, "Eyebrow", eyebrow, value =>
            {
                eyebrow = value;
                refresh();
            });
            var titleField = AddTextField(controls.Content, "タイトル（必須）", title, value =>
            {
                title = value;
                refresh();
            });
            var descriptionField = AddTextField(controls.Content, "説明", description, value =>
            {
                description = value;
                refresh();
            });
            var badgeField = AddTextField(controls.Content, "バッジ", badgeText, value =>
            {
                badgeText = value;
                refresh();
            });
            var bodyTextField = AddTextField(controls.Content, "本文テキスト", bodyText, value =>
            {
                bodyText = value;
                refresh();
            }, true);

            var preview = CreatePreviewSection(parent);
            var card = new InfoCard();
            preview.Body.Add(card);

            refresh = () =>
            {
                controls.TabCard.SetState(
                    new TabCardState(
                        new[]
                        {
                            new TabCardTabState(InfoCardStoryPreset.Simple.ToString(), "Simple"),
                            new TabCardTabState(InfoCardStoryPreset.Result.ToString(), "Result")
                        },
                        preset.ToString()),
                    id => applyPreset((InfoCardStoryPreset)Enum.Parse(typeof(InfoCardStoryPreset), id)));

                eyebrowField.SetValueWithoutNotify(eyebrow);
                titleField.SetValueWithoutNotify(title);
                descriptionField.SetValueWithoutNotify(description);
                badgeField.SetValueWithoutNotify(badgeText);
                bodyTextField.SetValueWithoutNotify(bodyText);

                card.SetState(new InfoCardState(title, description, eyebrow, badgeText));
                card.Body.Clear();

                if (!string.IsNullOrWhiteSpace(bodyText))
                {
                    var bodyLabel = UiTextFactory.Create(bodyText);
                    bodyLabel.SetWhiteSpace(WhiteSpace.Normal);
                    card.Body.Add(bodyLabel);
                }
            };

            applyPreset(preset);
            FinalizeControlsSection(parent, controls);
        }

        private void BuildSearchableTreeViewStory(VisualElement parent)
        {
            var preview = CreatePreviewSection(parent);
            var treeView = new SearchableTreeView<SampleTreeNode>(
                CreateSampleTreeItem,
                BindSampleTreeItem,
                null,
                "一致する項目がありません。");
            treeView.SetViewDataKey("ee4v-ui-catalog-searchable-tree-view-story");
            treeView.SetItems(BuildSampleTreeItems());
            preview.Body.Add(treeView);
        }

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

        private void BuildCopyableTextAreaStory(VisualElement parent)
        {
            var text = "ja-JP/Core: testing.window.failureDetailsTitle (Editor/Core/Localization/ja-JP/core.jsonc)\n" +
                       "en-US/Core: testing.window.copy (Editor/Core/Localization/en-US/core.jsonc)";
            var buttonText = "Copy";
            Action refresh = null;

            var controls = CreatePlainControlsSection(parent, "表示する長文と button text を変えながら、詳細結果表示用 text area を確認します。");
            var textField = AddTextField(controls.Content, "Text", text, nextValue =>
            {
                text = nextValue;
                refresh();
            }, true);
            var buttonField = AddTextField(controls.Content, "Button", buttonText, nextValue =>
            {
                buttonText = nextValue;
                refresh();
            });

            var preview = CreatePreviewSection(parent);
            var textArea = new CopyableTextArea();
            preview.Body.Add(textArea);

            refresh = () =>
            {
                textField.SetValueWithoutNotify(text);
                buttonField.SetValueWithoutNotify(buttonText);
                textArea.SetState(new CopyableTextAreaState(text, buttonText));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }

        private void BuildWindowToastStory(VisualElement parent)
        {
            var preset = WindowToastStoryPreset.Info;
            var tone = WindowToastTone.Info;
            var title = string.Empty;
            var message = string.Empty;
            var durationSeconds = 4d;
            var dismissible = true;
            var hasAction = false;
            Action refresh = null;

            Action<WindowToastStoryPreset> applyPreset = selectedPreset =>
            {
                preset = selectedPreset;
                ApplyCatalogToastPreset(selectedPreset, out tone, out title, out message, out durationSeconds, out dismissible, out hasAction);
                if (refresh != null)
                {
                    refresh();
                }
            };

            var controls = CreateTabbedControlsSection(parent, "preset で tone と文面を切り替えながら、Catalog window 右上に積まれる toast を確認します。");
            var toneField = AddEnumField(controls.Content, "Tone", tone, value =>
            {
                applyPreset((WindowToastStoryPreset)(int)value);
            });
            var titleField = AddTextField(controls.Content, "Title", title, value =>
            {
                title = value;
                refresh();
            });
            var messageField = AddTextField(controls.Content, "Message", message, value =>
            {
                message = value;
                refresh();
            }, true);
            var durationField = new FloatField("Duration")
            {
                value = (float)durationSeconds
            };
            durationField.RegisterValueChangedCallback(evt =>
            {
                durationSeconds = Math.Max(0d, evt.newValue);
                refresh();
            });
            controls.Content.Add(durationField);

            var dismissibleToggle = new Toggle("Dismissible")
            {
                value = dismissible
            };
            dismissibleToggle.RegisterValueChangedCallback(evt =>
            {
                dismissible = evt.newValue;
                refresh();
            });
            controls.Content.Add(dismissibleToggle);

            var actionToggle = new Toggle("Action")
            {
                value = hasAction
            };
            actionToggle.RegisterValueChangedCallback(evt =>
            {
                hasAction = evt.newValue;
                refresh();
            });
            controls.Content.Add(actionToggle);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.FlexEnd;
            controls.Content.Add(buttonRow);

            var pushButton = new Button(() =>
            {
                WindowToastApi.Show(this, CreateCatalogToastRequest(tone, title, message, durationSeconds, dismissible, hasAction));
            })
            {
                text = "Push"
            };
            buttonRow.Add(pushButton);

            var clearButton = new Button(() => WindowToastApi.Clear(this))
            {
                text = "Clear"
            };
            clearButton.style.marginLeft = 6f;
            buttonRow.Add(clearButton);

            refresh = () =>
            {
                controls.TabCard.SetState(
                    new TabCardState(
                        new[]
                        {
                            new TabCardTabState(WindowToastStoryPreset.Info.ToString(), "Info"),
                            new TabCardTabState(WindowToastStoryPreset.Success.ToString(), "Success"),
                            new TabCardTabState(WindowToastStoryPreset.Warning.ToString(), "Warning"),
                            new TabCardTabState(WindowToastStoryPreset.Error.ToString(), "Error")
                        },
                        preset.ToString()),
                    id => applyPreset((WindowToastStoryPreset)Enum.Parse(typeof(WindowToastStoryPreset), id)));
                toneField.SetValueWithoutNotify((Enum)(object)tone);
                titleField.SetValueWithoutNotify(title);
                messageField.SetValueWithoutNotify(message);
                durationField.SetValueWithoutNotify((float)durationSeconds);
                dismissibleToggle.SetValueWithoutNotify(dismissible);
                actionToggle.SetValueWithoutNotify(hasAction);
            };

            applyPreset(preset);
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

        private void BuildAlertsStory(VisualElement parent)
        {
            var tone = UiBannerTone.Info;
            var title = "情報表示";
            var message = "非ブロッキングな案内やエラー通知に使います。";
            Action refresh = null;
            Action<UiBannerTone> applyPreset = selectedTone =>
            {
                tone = selectedTone;
                switch (selectedTone)
                {
                    case UiBannerTone.Warning:
                        title = "警告表示";
                        message = "確認が必要な状態や注意喚起に使います。";
                        break;
                    case UiBannerTone.Error:
                        title = "エラー表示";
                        message = "処理失敗や設定不備など、強く伝える必要がある状態に使います。";
                        break;
                    default:
                        title = "情報表示";
                        message = "非ブロッキングな案内やエラー通知に使います。";
                        break;
                }

                if (refresh != null)
                {
                    refresh();
                }
            };

            var controls = CreateTabbedControlsSection(parent, "タイトル、メッセージ、tone を切り替えて通知の見た目を確認します。");

            var toneField = AddEnumField(controls.Content, "種類", tone, value =>
            {
                tone = value;
                refresh();
            });
            var titleField = AddTextField(controls.Content, "タイトル", title, value =>
            {
                title = value;
                refresh();
            });
            var messageField = AddTextField(controls.Content, "メッセージ", message, value =>
            {
                message = value;
                refresh();
            }, true);

            var preview = CreatePreviewSection(parent);
            var alerts = new Alerts();
            preview.Body.Add(CreatePreviewSurface(alerts, true));

            refresh = () =>
            {
                controls.TabCard.SetState(
                    new TabCardState(
                        new[]
                        {
                            new TabCardTabState(UiBannerTone.Info.ToString(), "Info"),
                            new TabCardTabState(UiBannerTone.Warning.ToString(), "Warning"),
                            new TabCardTabState(UiBannerTone.Error.ToString(), "Error")
                        },
                        tone.ToString()),
                    id => applyPreset((UiBannerTone)Enum.Parse(typeof(UiBannerTone), id)));

                toneField.SetValueWithoutNotify((Enum)(object)tone);
                titleField.SetValueWithoutNotify(title);
                messageField.SetValueWithoutNotify(message);
                alerts.SetState(new AlertsState(tone, title, message));
            };

            applyPreset(tone);
            FinalizeControlsSection(parent, controls);
        }

        private void BuildStatusBadgeStory(VisualElement parent)
        {
            var text = I18N.Get("catalog.status.running");
            var tone = UiStatusTone.Running;
            Action refresh = null;
            Action<UiStatusTone> applyPreset = selectedTone =>
            {
                tone = selectedTone;
                switch (selectedTone)
                {
                    case UiStatusTone.Passed:
                        text = I18N.Get("catalog.status.passed");
                        break;
                    case UiStatusTone.Failed:
                        text = I18N.Get("catalog.status.failed");
                        break;
                    case UiStatusTone.Skipped:
                        text = I18N.Get("catalog.status.skipped");
                        break;
                    case UiStatusTone.Inconclusive:
                        text = I18N.Get("catalog.status.inconclusive");
                        break;
                    case UiStatusTone.Idle:
                        text = I18N.Get("catalog.status.idle");
                        break;
                    default:
                        text = I18N.Get("catalog.status.running");
                        break;
                }

                if (refresh != null)
                {
                    refresh();
                }
            };

            var controls = CreateTabbedControlsSection(parent, "状態テキストと tone を切り替えて badge の見た目を確認します。");

            var textField = AddTextField(controls.Content, "テキスト", text, value =>
            {
                text = value;
                refresh();
            });
            var toneField = AddEnumField(controls.Content, "種類", tone, value =>
            {
                tone = value;
                refresh();
            });

            var preview = CreatePreviewSection(parent);
            var badge = new StatusBadge();
            var surface = CreatePreviewSurface(true);
            surface.Add(badge);
            preview.Body.Add(surface);

            refresh = () =>
            {
                controls.TabCard.SetState(
                    new TabCardState(
                        new[]
                        {
                            new TabCardTabState(UiStatusTone.Idle.ToString(), "Idle"),
                            new TabCardTabState(UiStatusTone.Running.ToString(), "Running"),
                            new TabCardTabState(UiStatusTone.Passed.ToString(), "Passed"),
                            new TabCardTabState(UiStatusTone.Failed.ToString(), "Failed"),
                            new TabCardTabState(UiStatusTone.Skipped.ToString(), "Skipped"),
                            new TabCardTabState(UiStatusTone.Inconclusive.ToString(), "Inconclusive")
                        },
                        tone.ToString()),
                    id => applyPreset((UiStatusTone)Enum.Parse(typeof(UiStatusTone), id)));

                textField.SetValueWithoutNotify(text);
                toneField.SetValueWithoutNotify((Enum)(object)tone);
                badge.SetState(new StatusBadgeState(text, tone));
            };

            applyPreset(tone);
            FinalizeControlsSection(parent, controls);
        }

        private void BuildIconStory(VisualElement parent)
        {
            var sourceKind = UiIconSourceKind.Builtin;
            var builtinIcon = UiBuiltinIcon.Search;
            Texture texture = null;
            Action refresh = null;

            var controls = CreatePlainControlsSection(parent, "source を切り替え、texture 指定と enum 管理の Unity 内蔵アイコン指定を確認します。");

            var sourceField = AddEnumField(controls.Content, "ソース", sourceKind, value =>
            {
                sourceKind = value;
                refresh();
            });
            var builtinField = AddEnumField(controls.Content, "内蔵アイコン", builtinIcon, value =>
            {
                builtinIcon = value;
                refresh();
            });
            var textureField = AddObjectField<Texture>(controls.Content, "Texture", texture, value =>
            {
                texture = value;
                refresh();
            });

            var preview = CreatePreviewSection(parent);
            var surface = CreatePreviewSurface(true);
            var icon = new Icon();
            surface.Add(icon);
            preview.Body.Add(surface);

            refresh = () =>
            {
                sourceField.SetValueWithoutNotify((Enum)(object)sourceKind);
                builtinField.SetValueWithoutNotify((Enum)(object)builtinIcon);
                textureField.SetValueWithoutNotify(texture);

                builtinField.style.display = sourceKind == UiIconSourceKind.Builtin ? DisplayStyle.Flex : DisplayStyle.None;
                textureField.style.display = sourceKind == UiIconSourceKind.Texture ? DisplayStyle.Flex : DisplayStyle.None;

                switch (sourceKind)
                {
                    case UiIconSourceKind.Texture:
                        icon.SetState(texture != null
                            ? IconState.FromTexture(texture, tooltip: texture.name)
                            : IconState.FromBuiltinIcon(builtinIcon, tooltip: "Assign a texture"));
                        break;
                    case UiIconSourceKind.Builtin:
                        icon.SetState(IconState.FromBuiltinIcon(builtinIcon, tooltip: UiBuiltinIconResolver.GetIconName(builtinIcon)));
                        break;
                }
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }

        private void BuildTestResultGroupStory(VisualElement parent)
        {
            var statusText = "成功";
            var message = "Pass 3  Fail 0  Skip 0  Inc 0  0.08s";
            var details = "Test\n依存関係の初期化確認\n\nDescription\n実行前の static 状態が正しく復元されることを確認します。\n\nFailure Details\nja-JP/Core: testing.window.copy (Editor/Core/Localization/ja-JP/core.jsonc)";
            var expanded = false;
            var runEnabled = true;
            Action refresh = null;

            var controls = CreatePlainControlsSection(parent, "status、alert、run button と一覧開閉を変えながら testing 向け result panel を確認します。");
            var statusField = AddTextField(controls.Content, "Status", statusText, nextValue =>
            {
                statusText = nextValue;
                refresh();
            });
            var messageField = AddTextField(controls.Content, "Alert", message, nextValue =>
            {
                message = nextValue;
                refresh();
            }, true);
            var detailsField = AddTextField(controls.Content, "Details", details, nextValue =>
            {
                details = nextValue;
                refresh();
            }, true);
            var runEnabledToggle = new Toggle("Run enabled")
            {
                value = runEnabled
            };
            runEnabledToggle.RegisterValueChangedCallback(evt =>
            {
                runEnabled = evt.newValue;
                refresh();
            });
            controls.Content.Add(runEnabledToggle);

            var expandedToggle = new Toggle("展開")
            {
                value = expanded
            };
            expandedToggle.RegisterValueChangedCallback(evt =>
            {
                expanded = evt.newValue;
                refresh();
            });
            controls.Content.Add(expandedToggle);

            var preview = CreatePreviewSection(parent);
            var result = new TestResultGroup();
            preview.Body.Add(result);

            result.ExpandedChanged += nextExpanded =>
            {
                expanded = nextExpanded;
                expandedToggle.SetValueWithoutNotify(nextExpanded);
            };

            refresh = () =>
            {
                statusField.SetValueWithoutNotify(statusText);
                messageField.SetValueWithoutNotify(message);
                detailsField.SetValueWithoutNotify(details);
                runEnabledToggle.SetValueWithoutNotify(runEnabled);
                expandedToggle.SetValueWithoutNotify(expanded);
                result.SetState(new TestResultGroupState(
                    new InfoCardState(
                        "Hoge",
                        "Hogeのテスト",
                        "Ee4v.Hoge.Test.Editor"),
                    runText: "Run",
                    runEnabled: runEnabled,
                    summaryMessage: message,
                    summaryTone: UiBannerTone.Info,
                    casesTitle: "Tests",
                    casesMeta: "3 items",
                    expanded: expanded,
                    cases: new[]
                    {
                        new TestResultGroupCaseState("設定定義の登録確認", "必要な定義が不足なく登録されることを確認します。", new StatusBadgeState(statusText, UiStatusTone.Passed)),
                        new TestResultGroupCaseState("依存関係の初期化確認", "実行前の static 状態が正しく復元されることを確認します。", new StatusBadgeState(statusText, UiStatusTone.Failed), detailsToggleText: "Failure Details", detailsText: details, detailsCopyButtonText: "Copy"),
                        new TestResultGroupCaseState("Unity Test Runner 連携確認", "suite 単位の実行要求が適切な assembly filter で送られることを確認します。", new StatusBadgeState(statusText, UiStatusTone.Passed))
                    }));
            };

            refresh();
            FinalizeControlsSection(parent, controls);
        }

        private static WindowToastRequest CreateCatalogToastRequest(
            WindowToastTone tone,
            string title,
            string message,
            double durationSeconds,
            bool dismissible,
            bool hasAction)
        {
            return new WindowToastRequest(
                tone,
                FormatCatalogToastTitle(title),
                message,
                durationSeconds: durationSeconds,
                dismissible: dismissible,
                actions: hasAction
                    ? new[]
                    {
                        new WindowToastAction("Open", closesToast: true)
                    }
                    : Array.Empty<WindowToastAction>());
        }

        private static void ApplyCatalogToastPreset(
            WindowToastStoryPreset preset,
            out WindowToastTone tone,
            out string title,
            out string message,
            out double durationSeconds,
            out bool dismissible,
            out bool hasAction)
        {
            dismissible = true;
            hasAction = false;

            switch (preset)
            {
                case WindowToastStoryPreset.Success:
                    tone = WindowToastTone.Success;
                    title = "Catalog Sync Completed";
                    message = "UI Catalog の story metadata 更新が反映されました。";
                    durationSeconds = 3d;
                    return;
                case WindowToastStoryPreset.Warning:
                    tone = WindowToastTone.Warning;
                    title = "Preview Requires Refresh";
                    message = "現在の変更を反映するには Catalog window の再描画が必要です。";
                    durationSeconds = 0d;
                    hasAction = true;
                    return;
                case WindowToastStoryPreset.Error:
                    tone = WindowToastTone.Error;
                    title = "Feature Test Launch Failed";
                    message = "Core の test run を開始できませんでした。詳細ログを確認してください。";
                    durationSeconds = 0d;
                    return;
                default:
                    tone = WindowToastTone.Info;
                    title = "Overlay Preview Active";
                    message = "Catalog window 自体に toast overlay を表示して挙動を確認します。";
                    durationSeconds = 4d;
                    return;
            }
        }

        private static string FormatCatalogToastTitle(string title)
        {
            var normalized = (title ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(normalized))
            {
                return "[TEST]";
            }

            return normalized.StartsWith("[TEST]", StringComparison.Ordinal)
                ? normalized
                : "[TEST] " + normalized;
        }

        private ControlsSectionContext CreateTabbedControlsSection(VisualElement parent, string description)
        {
            var card = new InfoCard(new InfoCardState("コントロール", description));
            card.userData = "catalog-controls-section";
            var tabCard = new TabCard();
            tabCard.Content.AddToClassList("ee4v-ui-catalog-controls");
            card.Body.Add(tabCard);
            parent.Add(card);
            return new ControlsSectionContext(card, tabCard.Content, tabCard);
        }

        private ControlsSectionContext CreatePlainControlsSection(VisualElement parent, string description)
        {
            var card = new InfoCard(new InfoCardState("コントロール", description));
            card.userData = "catalog-controls-section";
            var content = new VisualElement();
            content.AddToClassList("ee4v-ui-catalog-controls");
            content.style.flexDirection = FlexDirection.Column;
            card.Body.Add(content);
            parent.Add(card);
            return new ControlsSectionContext(card, content, null);
        }

        private InfoCard CreatePreviewSection(VisualElement parent)
        {
            var card = new InfoCard(new InfoCardState("プレビュー", "コントロールの変更はすぐにプレビューへ反映されます。"));
            var inserted = false;
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.ElementAt(i);
                if (!Equals(child.userData, "catalog-controls-section"))
                {
                    continue;
                }

                parent.Insert(i, card);
                inserted = true;
                break;
            }

            if (!inserted)
            {
                parent.Add(card);
            }

            return card;
        }

        private VisualElement CreatePreviewSurface(bool compact = false)
        {
            var surface = new VisualElement();
            surface.AddToClassList("ee4v-ui-catalog-preview-surface");
            if (compact)
            {
                surface.AddToClassList("ee4v-ui-catalog-preview-surface--compact");
            }

            return surface;
        }

        private VisualElement CreatePreviewSurface(VisualElement content, bool compact = false)
        {
            var surface = CreatePreviewSurface(compact);
            surface.Add(content);
            return surface;
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

        private static EnumField AddEnumField<TEnum>(VisualElement parent, string label, TEnum value, Action<TEnum> onChanged)
            where TEnum : struct, Enum
        {
            var field = new EnumField(label, (Enum)(object)value);
            field.RegisterValueChangedCallback(evt => onChanged((TEnum)(object)evt.newValue));
            parent.Add(field);
            return field;
        }

        private static ObjectField AddObjectField<TObject>(VisualElement parent, string label, TObject value, Action<TObject> onChanged)
            where TObject : UnityEngine.Object
        {
            var field = new ObjectField(label)
            {
                objectType = typeof(TObject),
                allowSceneObjects = false,
                value = value
            };
            field.RegisterValueChangedCallback(evt => onChanged((TObject)evt.newValue));
            parent.Add(field);
            return field;
        }

        private VisualElement CreateNavigatorTreeItem()
        {
            var row = new VisualElement();
            row.AddToClassList("ee4v-ui-catalog-tree-item");
            row.Add(UiTextFactory.Create(string.Empty, UiClassNames.CatalogTreeTitle));
            row.Add(UiTextFactory.Create(string.Empty, UiClassNames.CatalogTreeImplementation));
            return row;
        }

        private VisualElement CreateSampleTreeItem()
        {
            var row = new VisualElement();
            row.AddToClassList("ee4v-ui-catalog-tree-item");
            row.Add(UiTextFactory.Create(string.Empty, UiClassNames.CatalogTreeTitle));
            row.Add(UiTextFactory.Create(string.Empty, UiClassNames.CatalogTreeImplementation));
            return row;
        }

        private void BindNavigatorTreeItem(VisualElement element, NavigatorTreeNode node)
        {
            var title = element.ElementAt(0) as UiTextElement;
            var implementation = element.ElementAt(1) as UiTextElement;

            if (title != null)
            {
                title.SetText(node.Title);
            }

            if (implementation != null)
            {
                implementation.SetText(node.ImplementationShortLabel);
                implementation.EnableInClassList("ee4v-ui-catalog-tree-item__implementation--hidden", string.IsNullOrEmpty(node.ImplementationShortLabel));
            }
        }

        private void OnNavigatorSelectionChanged(IReadOnlyList<NavigatorTreeNode> items)
        {
            if (_isSyncingNavigatorSelection || items == null)
            {
                return;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var node = items[i];
                if (node != null && node.Story != null)
                {
                    SelectStory(node.Story);
                    return;
                }
            }
        }

        private void BindSampleTreeItem(VisualElement element, SampleTreeNode node)
        {
            var title = element.ElementAt(0) as UiTextElement;
            var meta = element.ElementAt(1) as UiTextElement;

            if (title != null)
            {
                title.SetText(node.Title);
            }

            if (meta != null)
            {
                meta.SetText(node.Meta);
                meta.EnableInClassList("ee4v-ui-catalog-tree-item__implementation--hidden", string.IsNullOrWhiteSpace(node.Meta));
            }
        }

        private List<SearchableTreeItemData<NavigatorTreeNode>> BuildNavigatorTreeItems()
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

        private static IReadOnlyList<SearchableTreeItemData<SampleTreeNode>> BuildSampleTreeItems()
        {
            return new[]
            {
                new SearchableTreeItemData<SampleTreeNode>(
                    1,
                    new SampleTreeNode("Display", string.Empty),
                    "Display",
                    new[]
                    {
                        new SearchableTreeItemData<SampleTreeNode>(
                            2,
                            new SampleTreeNode("InfoCard", "Card"),
                            "InfoCard Card information"),
                        new SearchableTreeItemData<SampleTreeNode>(
                            3,
                            new SampleTreeNode("Alerts", "Banner"),
                            "Alerts Banner feedback"),
                        new SearchableTreeItemData<SampleTreeNode>(
                            4,
                            new SampleTreeNode("StatusBadge", "Pill"),
                            "StatusBadge pill status"),
                        new SearchableTreeItemData<SampleTreeNode>(
                            5,
                            new SampleTreeNode("Icon", "Image"),
                            "Icon image texture builtin")
                    }),
                new SearchableTreeItemData<SampleTreeNode>(
                    6,
                    new SampleTreeNode("Overlay", string.Empty),
                    "Overlay",
                    new[]
                    {
                        new SearchableTreeItemData<SampleTreeNode>(
                            7,
                            new SampleTreeNode("WindowToast", "Toast"),
                            "WindowToast editor window overlay toast")
                    }),
                new SearchableTreeItemData<SampleTreeNode>(
                    8,
                    new SampleTreeNode("Interactive", string.Empty),
                    "Interactive",
                    new[]
                    {
                        new SearchableTreeItemData<SampleTreeNode>(
                            9,
                            new SampleTreeNode("TabCard", "Tabs"),
                            "TabCard Tabs switcher")
                    }),
                new SearchableTreeItemData<SampleTreeNode>(
                    10,
                    new SampleTreeNode("DataView", string.Empty),
                    "DataView",
                    new[]
                    {
                        new SearchableTreeItemData<SampleTreeNode>(
                            11,
                            new SampleTreeNode("SearchField", "Input"),
                            "SearchField input search"),
                        new SearchableTreeItemData<SampleTreeNode>(
                            12,
                            new SampleTreeNode("SearchableTreeView", "Tree"),
                            "SearchableTreeView searchable tree")
                    }),
                new SearchableTreeItemData<SampleTreeNode>(
                    13,
                    new SampleTreeNode("Domain", string.Empty),
                    "Domain",
                    new[]
                    {
                        new SearchableTreeItemData<SampleTreeNode>(
                            14,
                            new SampleTreeNode("Testing", string.Empty),
                            "Testing",
                            new[]
                            {
                                new SearchableTreeItemData<SampleTreeNode>(
                                    15,
                                    new SampleTreeNode("TestResultGroup", "Testing"),
                                    "TestResultGroup testing domain result")
                            })
                    })
            };
        }

        private static List<SearchableTreeItemData<NavigatorTreeNode>> ConvertNavigatorTreeItems(IReadOnlyList<NavigatorTreeNodeBuilder> builders)
        {
            var items = new List<SearchableTreeItemData<NavigatorTreeNode>>(builders.Count);
            for (var i = 0; i < builders.Count; i++)
            {
                items.Add(new SearchableTreeItemData<NavigatorTreeNode>(
                    builders[i].Id,
                    builders[i].Node,
                    builders[i].Node.SearchText,
                    ConvertNavigatorTreeItems(builders[i].Children)));
            }

            return items;
        }

        private InfoCard CreateDetailsSection(StoryDefinition story)
        {
            var card = new InfoCard(new InfoCardState(
                I18N.Get("catalog.common.details"),
                story.Details));

            card.Body.Add(CreateDetailItem(I18N.Get("catalog.common.implementation"), GetImplementationLabel(story.Implementation)));
            card.Body.Add(CreateDetailItem(
                I18N.Get("catalog.common.dependencies"),
                story.Dependencies.Count == 0 ? I18N.Get("catalog.common.none") : string.Join("\n", story.Dependencies)));
            return card;
        }

        private VisualElement CreateDetailItem(string label, string value)
        {
            var item = new VisualElement();
            item.AddToClassList("ee4v-ui-catalog-detail-item");

            var labelElement = UiTextFactory.Create(label, UiClassNames.CatalogDetailLabel);
            var valueElement = UiTextFactory.Create(value, UiClassNames.CatalogDetailValue);
            valueElement.SetWhiteSpace(WhiteSpace.Normal);

            item.Add(labelElement);
            item.Add(valueElement);
            return item;
        }

        private static void FinalizeControlsSection(VisualElement parent, ControlsSectionContext controls)
        {
            if (controls == null || controls.Content.childCount > 0)
            {
                return;
            }

            parent.Remove(controls.Card);
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
                    return I18N.Get("catalog.common.imguiVisual");
                default:
                    return I18N.Get("catalog.common.uiToolkitVisual");
            }
        }

        private sealed class StoryDefinition
        {
            public StoryDefinition(
                string id,
                string group,
                string title,
                string description,
                string details,
                IReadOnlyList<string> dependencies,
                ComponentImplementationKind implementation,
                Action<VisualElement> build)
            {
                Id = id;
                Group = group;
                Title = title;
                Description = description;
                Details = details;
                Dependencies = dependencies ?? new string[0];
                Implementation = implementation;
                Build = build;
            }

            public string Id { get; }

            public string Group { get; }

            public string Title { get; }

            public string Description { get; }

            public string Details { get; }

            public IReadOnlyList<string> Dependencies { get; }

            public ComponentImplementationKind Implementation { get; }

            public Action<VisualElement> Build { get; }
        }

        private sealed class NavigatorTreeNode
        {
            public NavigatorTreeNode(string title, string implementationShortLabel, StoryDefinition story)
            {
                Title = title ?? string.Empty;
                ImplementationShortLabel = implementationShortLabel ?? string.Empty;
                Story = story;
                SearchText = BuildSearchText(story, Title);
            }

            public string Title { get; }

            public string ImplementationShortLabel { get; }

            public StoryDefinition Story { get; }

            public string SearchText { get; }

            private static string BuildSearchText(StoryDefinition story, string title)
            {
                if (story == null)
                {
                    return title ?? string.Empty;
                }

                return string.Join("\n", new[]
                {
                    story.Title ?? string.Empty,
                    story.Group ?? string.Empty,
                    story.Description ?? string.Empty,
                    story.Details ?? string.Empty,
                });
            }
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

        private sealed class ControlsSectionContext
        {
            public ControlsSectionContext(InfoCard card, VisualElement content, TabCard tabCard)
            {
                Card = card;
                Content = content;
                TabCard = tabCard;
            }

            public InfoCard Card { get; }

            public VisualElement Content { get; }

            public TabCard TabCard { get; }
        }

        private sealed class SampleTreeNode
        {
            public SampleTreeNode(string title, string meta)
            {
                Title = title ?? string.Empty;
                Meta = meta ?? string.Empty;
            }

            public string Title { get; }

            public string Meta { get; }
        }
    }
}
