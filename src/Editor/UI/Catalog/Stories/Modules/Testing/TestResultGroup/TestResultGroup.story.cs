using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.Testing.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal static class TestingCatalogRegistrarStory
    {
        private sealed class TestingCatalogRegistrar : CatalogWindow.ICatalogRegistrar
        {
            public int Order
            {
                get { return 100; }
            }

            public void Register(CatalogWindow.CatalogRegistry registry)
            {
                registry.RegisterStyleSheet("Editor/Testing/UI/TestResultGroup/test-result-group.uss");
                registry.RegisterStory(new CatalogWindow.StoryRegistration(
                    "test-result-group",
                    "Domain/Testing",
                    "TestResultGroup",
                    "feature test の状態、件数 alert、実行導線、登録テスト一覧をまとめて表示する testing 向けコンポーネントです。",
                    "Test List の結果表示用に作った domain-specific component です。InfoCard を土台にしつつ、header 右側に status badge と run button、body に件数 alert、copy 可能な詳細結果、登録済みテスト一覧の開閉を持たせています。",
                    new[]
                    {
                        "InfoCard",
                        "StatusBadge",
                        "Alerts",
                        "CopyableTextArea"
                    },
                    CatalogWindow.ComponentImplementationKind.UiToolkit,
                    (window, parent) => BuildTestResultGroupStory(window, parent)));
            }
        }

        private static void BuildTestResultGroupStory(CatalogWindow window, VisualElement parent)
        {
            var statusText = "成功";
            var message = "Pass 3  Fail 0  Skip 0  Inc 0  0.08s";
            var details = "Test\n依存関係の初期化確認\n\nDescription\n実行前の static 状態が正しく復元されることを確認します。\n\nFailure Details\nja-JP/Core: testing.window.copy (Editor/Core/Localization/ja-JP/core.jsonc)";
            var expanded = false;
            var runEnabled = true;
            Action refresh = null;

            var controls = window.CreatePlainControlsSection(parent, "status、alert、run button と一覧開閉を変えながら testing 向け result panel を確認します。");
            var statusField = CatalogWindow.AddTextField(controls.Content, "Status", statusText, nextValue =>
            {
                statusText = nextValue;
                refresh();
            });
            var messageField = CatalogWindow.AddTextField(controls.Content, "Alert", message, nextValue =>
            {
                message = nextValue;
                refresh();
            }, true, 120f);
            var detailsField = CatalogWindow.AddTextField(controls.Content, "Details", details, nextValue =>
            {
                details = nextValue;
                refresh();
            }, true, 160f);
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

            var preview = window.CreatePreviewSection(parent);
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
            CatalogWindow.FinalizeControlsSection(parent, controls);
        }
    }
}
