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
        private readonly List<StoryDefinition> _stories = new List<StoryDefinition>();
        private readonly List<NavigatorEntry> _navigatorEntries = new List<NavigatorEntry>();

        private VisualElement _navigatorHost;
        private VisualElement _contentHost;
        private StoryDefinition _selectedStory;

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

            _stories.Add(new StoryDefinition("window-page", "Components/Layout", "UiWindowPage", "Page shell with title, toolbar, and scroll body.", BuildWindowPageStory));
            _stories.Add(new StoryDefinition("toolbar-row", "Components/Layout", "UiToolbarRow", "Toolbar row with left and right slots.", BuildToolbarRowStory));
            _stories.Add(new StoryDefinition("action-row", "Components/Layout", "UiActionRow", "Action layout for aligned button groups.", BuildActionRowStory));
            _stories.Add(new StoryDefinition("section", "Components/Surface", "UiSection", "Section surface with title, description, and badge.", BuildSectionStory));
            _stories.Add(new StoryDefinition("card", "Components/Surface", "UiCard", "Card surface with eyebrow and body content.", BuildCardStory));
            _stories.Add(new StoryDefinition("message-banner", "Components/Feedback", "UiMessageBanner", "Informational, warning, and error banner variants.", BuildMessageBannerStory));
            _stories.Add(new StoryDefinition("empty-state", "Components/Feedback", "UiEmptyState", "Dedicated presentation for empty results.", BuildEmptyStateStory));
            _stories.Add(new StoryDefinition("status-badge", "Components/Status", "UiStatusBadge", "Compact status label variants.", BuildStatusBadgeStory));
            _stories.Add(new StoryDefinition("meta-list", "Components/Data", "UiMetaList", "Label and value rows for compact metadata.", BuildMetaListStory));
            _stories.Add(new StoryDefinition("reference-row", "Components/Results", "ReferenceRow", "Jump action row for analyzer-like results.", BuildReferenceRowStory));
            _stories.Add(new StoryDefinition("grouped-result-list", "Components/Results", "GroupedResultList", "Locale and scope grouped result list.", BuildGroupedResultListStory));
            _stories.Add(new StoryDefinition("analyzer-result-section", "Components/Results", "AnalyzerResultSection", "Section wrapper for populated and empty analyzer states.", BuildAnalyzerResultSectionStory));
            _stories.Add(new StoryDefinition("feature-test-suite-card", "Components/Testing", "FeatureTestSuiteCard", "Feature test suite card with run action and result summary.", BuildFeatureTestSuiteCardStory));

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
            _navigatorEntries.Clear();

            var title = new Label(I18N.Get("catalog.window.title"));
            title.AddToClassList("ee4v-ui-catalog-shell__navigator-title");
            _navigatorHost.Add(title);

            var subtitle = new Label("Tree view component explorer");
            subtitle.AddToClassList("ee4v-ui-catalog-shell__navigator-subtitle");
            _navigatorHost.Add(subtitle);

            var navigatorScroll = new ScrollView();
            navigatorScroll.AddToClassList("ee4v-ui-catalog-shell__navigator-scroll");
            var folders = new Dictionary<string, VisualElement>(StringComparer.Ordinal);
            folders[string.Empty] = navigatorScroll;
            for (var i = 0; i < _stories.Count; i++)
            {
                var story = _stories[i];
                var parent = EnsureFolderTree(folders, navigatorScroll, story.Group);

                var button = new Button(() => SelectStory(story))
                {
                    text = story.Title
                };
                button.AddToClassList("ee4v-ui-catalog-shell__nav-button");
                parent.Add(button);

                _navigatorEntries.Add(new NavigatorEntry(story, button));
            }

            _navigatorHost.Add(navigatorScroll);
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
            for (var i = 0; i < _navigatorEntries.Count; i++)
            {
                var entry = _navigatorEntries[i];
                entry.Button.EnableInClassList(
                    "ee4v-ui-catalog-shell__nav-button--selected",
                    ReferenceEquals(entry.Story, _selectedStory));
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
            page.ToolbarRight.Add(reloadButton);

            story.Build(page);

            _contentHost.Add(page);
        }

        private static VisualElement EnsureFolderTree(
            IDictionary<string, VisualElement> folders,
            VisualElement root,
            string groupPath)
        {
            if (string.IsNullOrWhiteSpace(groupPath))
            {
                return root;
            }

            var segments = groupPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var currentPath = string.Empty;
            var parent = root;

            for (var i = 0; i < segments.Length; i++)
            {
                currentPath = string.IsNullOrEmpty(currentPath)
                    ? segments[i]
                    : currentPath + "/" + segments[i];

                VisualElement folder;
                if (!folders.TryGetValue(currentPath, out folder))
                {
                    var foldout = new Foldout
                    {
                        text = segments[i],
                        value = true
                    };
                    foldout.AddToClassList("ee4v-ui-catalog-shell__foldout");
                    parent.Add(foldout);
                    folders[currentPath] = foldout;
                    folder = foldout;
                }

                parent = folder;
            }

            return parent;
        }

        private void BuildWindowPageStory(UiWindowPage page)
        {
            var title = "Story Page";
            var description = "Nested UiWindowPage preview.";
            var showToolbar = true;
            var bodyMessage = "The body area hosts arbitrary VisualElements.";
            Action refresh = null;

            var controls = CreateControlsSection(page, "Edit the nested page shell.");
            AddTextField(controls.Body, "Title", title, value =>
            {
                title = value;
                refresh();
            });
            AddTextField(controls.Body, "Description", description, value =>
            {
                description = value;
                refresh();
            });
            AddTextField(controls.Body, "Body message", bodyMessage, value =>
            {
                bodyMessage = value;
                refresh();
            });
            AddToggle(controls.Body, "Show toolbar", showToolbar, value =>
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

                nestedPage.ToolbarLeft.Add(new Label("Left slot"));
                nestedPage.ToolbarRight.Add(CreatePreviewButton("Action"));
                nestedPage.Body.Add(new UiCard(new UiCardState("Body", bodyMessage, "Preview")));
            };

            refresh();
        }

        private void BuildToolbarRowStory(UiWindowPage page)
        {
            var leftText = "Project toolbar";
            var rightText = "Ready";
            var quiet = false;
            var showAction = true;
            Action refresh = null;

            var controls = CreateControlsSection(page, "Edit slot text and toolbar variant.");
            AddTextField(controls.Body, "Left text", leftText, value =>
            {
                leftText = value;
                refresh();
            });
            AddTextField(controls.Body, "Right text", rightText, value =>
            {
                rightText = value;
                refresh();
            });
            AddToggle(controls.Body, "Quiet variant", quiet, value =>
            {
                quiet = value;
                refresh();
            });
            AddToggle(controls.Body, "Show action button", showAction, value =>
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
                    toolbar.RightSlot.Add(CreatePreviewButton("Run"));
                }

                toolbar.RightSlot.Add(new Label(rightText));
            };

            refresh();
        }

        private void BuildSectionStory(UiWindowPage page)
        {
            var title = "Missing Keys";
            var description = "Section wrapper for grouped analyzer output.";
            var badge = "12";
            var bodyText = "The section body can host result lists, cards, and custom controls.";
            Action refresh = null;

            var controls = CreateControlsSection(page, "Edit header copy and badge text.");
            AddTextField(controls.Body, "Title", title, value =>
            {
                title = value;
                refresh();
            });
            AddTextField(controls.Body, "Description", description, value =>
            {
                description = value;
                refresh();
            });
            AddTextField(controls.Body, "Badge", badge, value =>
            {
                badge = value;
                refresh();
            });
            AddTextField(controls.Body, "Body text", bodyText, value =>
            {
                bodyText = value;
                refresh();
            });

            var preview = CreatePreviewSection(page);
            var section = new UiSection();
            preview.Body.Add(section);

            refresh = () =>
            {
                section.SetState(new UiSectionState(title, description, badge));
                section.Body.Clear();
                section.Body.Add(new Label(bodyText));
            };

            refresh();
        }

        private void BuildCardStory(UiWindowPage page)
        {
            var eyebrow = "Core";
            var title = "Feature Test Manager";
            var description = "Dense card layout for reusable editor panels.";
            var bodyText = "Cards can be stacked inside sections or used as standalone preview surfaces.";
            Action refresh = null;

            var controls = CreateControlsSection(page, "Adjust the card metadata and supporting text.");
            AddTextField(controls.Body, "Eyebrow", eyebrow, value =>
            {
                eyebrow = value;
                refresh();
            });
            AddTextField(controls.Body, "Title", title, value =>
            {
                title = value;
                refresh();
            });
            AddTextField(controls.Body, "Description", description, value =>
            {
                description = value;
                refresh();
            });
            AddTextField(controls.Body, "Body text", bodyText, value =>
            {
                bodyText = value;
                refresh();
            });

            var preview = CreatePreviewSection(page);
            var card = new UiCard();
            preview.Body.Add(card);

            refresh = () =>
            {
                card.SetState(new UiCardState(title, description, eyebrow));
                card.Body.Clear();
                card.Body.Add(new Label(bodyText));
            };

            refresh();
        }

        private void BuildMessageBannerStory(UiWindowPage page)
        {
            var tone = UiBannerTone.Info;
            var title = "Informational state";
            var message = "Use banners to communicate non-blocking guidance or errors.";
            Action refresh = null;

            var controls = CreateControlsSection(page, "Switch variants and edit banner copy.");
            AddEnumField(controls.Body, "Tone", tone, value =>
            {
                tone = value;
                refresh();
            });
            AddTextField(controls.Body, "Title", title, value =>
            {
                title = value;
                refresh();
            });
            AddTextField(controls.Body, "Message", message, value =>
            {
                message = value;
                refresh();
            });

            var preview = CreatePreviewSection(page);
            var banner = new UiMessageBanner();
            preview.Body.Add(CreatePreviewSurface(banner));

            refresh = () =>
            {
                banner.SetState(new UiMessageBannerState(tone, title, message));
            };

            refresh();
        }

        private void BuildEmptyStateStory(UiWindowPage page)
        {
            var title = "No results";
            var message = "This state appears when the current filter returns nothing.";
            Action refresh = null;

            var controls = CreateControlsSection(page, "Edit empty-state copy.");
            AddTextField(controls.Body, "Title", title, value =>
            {
                title = value;
                refresh();
            });
            AddTextField(controls.Body, "Message", message, value =>
            {
                message = value;
                refresh();
            });

            var preview = CreatePreviewSection(page);
            var emptyState = new UiEmptyState();
            preview.Body.Add(CreatePreviewSurface(emptyState));

            refresh = () =>
            {
                emptyState.SetState(new UiEmptyStateState(title, message));
            };

            refresh();
        }

        private void BuildActionRowStory(UiWindowPage page)
        {
            var compact = false;
            var primaryText = "Run";
            var secondaryText = "Open Settings";
            var showLeftLabel = true;
            Action refresh = null;

            var controls = CreateControlsSection(page, "Edit action labels and compact layout.");
            AddToggle(controls.Body, "Compact", compact, value =>
            {
                compact = value;
                refresh();
            });
            AddTextField(controls.Body, "Primary button", primaryText, value =>
            {
                primaryText = value;
                refresh();
            });
            AddTextField(controls.Body, "Secondary button", secondaryText, value =>
            {
                secondaryText = value;
                refresh();
            });
            AddToggle(controls.Body, "Show left label", showLeftLabel, value =>
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
                    actionRow.LeftSlot.Add(new Label("Footer actions"));
                }

                actionRow.RightSlot.Add(CreatePreviewButton(primaryText));
                actionRow.RightSlot.Add(CreatePreviewButton(secondaryText));
            };

            refresh();
        }

        private void BuildStatusBadgeStory(UiWindowPage page)
        {
            var text = "Running";
            var tone = UiStatusTone.Running;
            Action refresh = null;

            var controls = CreateControlsSection(page, "Edit status text and variant.");
            AddTextField(controls.Body, "Text", text, value =>
            {
                text = value;
                refresh();
            });
            AddEnumField(controls.Body, "Tone", tone, value =>
            {
                tone = value;
                refresh();
            });

            var preview = CreatePreviewSection(page);
            var badge = new UiStatusBadge();
            var surface = CreatePreviewSurface();
            surface.Add(badge);
            preview.Body.Add(surface);

            refresh = () =>
            {
                badge.SetState(new UiStatusBadgeState(text, tone));
            };

            refresh();
        }

        private void BuildMetaListStory(UiWindowPage page)
        {
            var rowCount = 3;
            var labelPrefix = "Field";
            var valuePrefix = "Value";
            Action refresh = null;

            var controls = CreateControlsSection(page, "Adjust how many rows are rendered and the text prefixes.");
            AddIntegerField(controls.Body, "Row count", rowCount, value =>
            {
                rowCount = Mathf.Clamp(value, 1, 5);
                refresh();
            });
            AddTextField(controls.Body, "Label prefix", labelPrefix, value =>
            {
                labelPrefix = value;
                refresh();
            });
            AddTextField(controls.Body, "Value prefix", valuePrefix, value =>
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

            var controls = CreateControlsSection(page, "Edit analyzer row content and action state.");
            AddTextField(controls.Body, "Primary text", primary, value =>
            {
                primary = value;
                refresh();
            });
            AddTextField(controls.Body, "Secondary text", secondary, value =>
            {
                secondary = value;
                refresh();
            });
            AddTextField(controls.Body, "Action label", actionLabel, value =>
            {
                actionLabel = value;
                refresh();
            });
            AddToggle(controls.Body, "Action enabled", actionEnabled, value =>
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
            var groupDescription = "Rows represent grouped analyzer references.";
            var rowCount = 3;
            var actionLabel = "Jump";
            Action refresh = null;

            var controls = CreateControlsSection(page, "Generate grouped rows and preview grouping density.");
            AddTextField(controls.Body, "Group title", groupTitle, value =>
            {
                groupTitle = value;
                refresh();
            });
            AddTextField(controls.Body, "Group description", groupDescription, value =>
            {
                groupDescription = value;
                refresh();
            });
            AddIntegerField(controls.Body, "Row count", rowCount, value =>
            {
                rowCount = Mathf.Clamp(value, 1, 6);
                refresh();
            });
            AddTextField(controls.Body, "Action label", actionLabel, value =>
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
            var title = "Missing Keys";
            var description = "Analyzer section with grouped rows and empty-state fallback.";
            var rowCount = 2;
            var populated = true;
            var emptyTitle = "No issues detected";
            var emptyMessage = "There are no rows to report for the current analysis.";
            Action refresh = null;

            var controls = CreateControlsSection(page, "Toggle between populated and empty variants.");
            AddTextField(controls.Body, "Title", title, value =>
            {
                title = value;
                refresh();
            });
            AddTextField(controls.Body, "Description", description, value =>
            {
                description = value;
                refresh();
            });
            AddToggle(controls.Body, "Populated", populated, value =>
            {
                populated = value;
                refresh();
            });
            AddIntegerField(controls.Body, "Row count", rowCount, value =>
            {
                rowCount = Mathf.Clamp(value, 1, 6);
                refresh();
            });
            AddTextField(controls.Body, "Empty title", emptyTitle, value =>
            {
                emptyTitle = value;
                refresh();
            });
            AddTextField(controls.Body, "Empty message", emptyMessage, value =>
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
            var description = "Preview of a suite card with metadata, registered cases, and the latest result.";
            var runButton = "Run";
            var canRun = true;
            var caseCount = 2;
            var statusText = "Passed";
            var statusTone = UiStatusTone.Passed;
            var resultTitle = "Last result: Passed";
            var resultCounts = "Pass 4  Fail 0  Skip 0  Inc 0  0.48s";
            var resultMessage = "All registered test cases completed successfully.";
            var resultTone = UiBannerTone.Info;
            Action refresh = null;

            var controls = CreateControlsSection(page, "Edit suite metadata, result copy, and variants.");
            AddTextField(controls.Body, "Title", title, value =>
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
            AddTextField(controls.Body, "Description", description, value =>
            {
                description = value;
                refresh();
            });
            AddTextField(controls.Body, "Run label", runButton, value =>
            {
                runButton = value;
                refresh();
            });
            AddToggle(controls.Body, "Can run", canRun, value =>
            {
                canRun = value;
                refresh();
            });
            AddIntegerField(controls.Body, "Case count", caseCount, value =>
            {
                caseCount = Mathf.Clamp(value, 1, 5);
                refresh();
            });
            AddTextField(controls.Body, "Status text", statusText, value =>
            {
                statusText = value;
                refresh();
            });
            AddEnumField(controls.Body, "Status tone", statusTone, value =>
            {
                statusTone = value;
                refresh();
            });
            AddTextField(controls.Body, "Result title", resultTitle, value =>
            {
                resultTitle = value;
                refresh();
            });
            AddTextField(controls.Body, "Result counts", resultCounts, value =>
            {
                resultCounts = value;
                refresh();
            });
            AddTextField(controls.Body, "Result message", resultMessage, value =>
            {
                resultMessage = value;
                refresh();
            });
            AddEnumField(controls.Body, "Result tone", resultTone, value =>
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
                        "Preview description for test case " + (i + 1) + "."));
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

        private UiSection CreateControlsSection(UiWindowPage page, string description)
        {
            var section = new UiSection(new UiSectionState("Controls", description));
            page.Body.Add(section);
            return section;
        }

        private UiSection CreatePreviewSection(UiWindowPage page)
        {
            var section = new UiSection(new UiSectionState("Preview", "Component output updates immediately when controls change."));
            page.Body.Add(section);
            return section;
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

        private static TextField AddTextField(VisualElement parent, string label, string value, Action<string> onChanged)
        {
            var field = new TextField(label);
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

        private sealed class StoryDefinition
        {
            public StoryDefinition(string id, string group, string title, string description, Action<UiWindowPage> build)
            {
                Id = id;
                Group = group;
                Title = title;
                Description = description;
                Build = build;
            }

            public string Id { get; }

            public string Group { get; }

            public string Title { get; }

            public string Description { get; }

            public Action<UiWindowPage> Build { get; }
        }

        private sealed class NavigatorEntry
        {
            public NavigatorEntry(StoryDefinition story, Button button)
            {
                Story = story;
                Button = button;
            }

            public StoryDefinition Story { get; }

            public Button Button { get; }
        }
    }
}
