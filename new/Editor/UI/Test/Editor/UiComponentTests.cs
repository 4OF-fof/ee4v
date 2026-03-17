using System.Collections.Generic;
using Ee4v.Core.Testing;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Ee4v.UI.Tests
{
    public sealed class UiComponentTests
    {
        [Test]
        [FeatureTestCase(
            "SearchField が clear と placeholder を切り替える",
            "SearchField が入力値の有無に応じて clear button と placeholder 表示を切り替えることを確認します。",
            order: 204)]
        public void SearchField_SetValueAndClear_UpdatesVisualState()
        {
            var field = new SearchField(new SearchFieldState(string.Empty, "Search"));

            var clearButton = field.Q<Button>(className: UiClassNames.SearchFieldClear);
            var placeholder = field.Q<UiTextElement>(className: UiClassNames.SearchFieldPlaceholder);

            Assert.That(field.Value, Is.Empty);
            Assert.That(clearButton.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(placeholder.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            field.Value = "Core";

            Assert.That(field.Value, Is.EqualTo("Core"));
            Assert.That(clearButton.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(placeholder.style.display.value, Is.EqualTo(DisplayStyle.None));

            field.ClearValue();

            Assert.That(field.Value, Is.Empty);
            Assert.That(clearButton.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(placeholder.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        [FeatureTestCase(
            "TestResultGroup が開閉状態を反映する",
            "TestResultGroup が alert と registered test 一覧の表示状態を切り替えることを確認します。",
            order: 205)]
        public void TestResultGroup_SetExpanded_TogglesCaseVisibility()
        {
            var result = new TestResultGroup(new TestResultGroupState(
                new InfoCardState("Hoge", "Hogeのテスト", "Ee4v.Hoge.Test.Editor"),
                runText: "Run",
                runEnabled: true,
                summaryMessage: "Pass 1  Fail 0  Skip 0  Inc 0  0.01s",
                summaryTone: UiBannerTone.Info,
                casesTitle: "Tests",
                casesMeta: "1 item",
                expanded: false,
                cases: new[]
                {
                    new TestResultGroupCaseState("Case 1", "Description", new StatusBadgeState("Passed", UiStatusTone.Passed))
                }));

            var notifications = new List<bool>();
            result.ExpandedChanged += notifications.Add;
            var summaryAlert = result.Q<Alerts>(className: UiClassNames.TestResultGroupSummaryAlert);
            var casesBody = result.Q<VisualElement>(className: UiClassNames.TestResultGroupCasesBody);
            var caseCard = casesBody.Q<InfoCard>(className: UiClassNames.TestResultGroupCaseCard);

            Assert.That(result.Expanded, Is.False);
            Assert.That(result.Badge.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(summaryAlert.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(casesBody.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(caseCard, Is.Not.Null);
            Assert.That(caseCard.Badge.Q<UiTextElement>(className: UiClassNames.StatusBadge).Text, Is.EqualTo("Passed"));

            result.SetExpanded(true);

            Assert.That(result.Expanded, Is.True);
            Assert.That(casesBody.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(notifications, Is.EqualTo(new[] { true }));

            result.SetExpanded(false);

            Assert.That(result.Expanded, Is.False);
            Assert.That(casesBody.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(notifications, Is.EqualTo(new[] { true, false }));

            result.SetState(new TestResultGroupState(
                new InfoCardState("Hoge", "Hogeのテスト", "Ee4v.Hoge.Test.Editor"),
                runText: "Run",
                runEnabled: true,
                summaryMessage: string.Empty,
                summaryTone: UiBannerTone.Info,
                casesTitle: "Tests",
                casesMeta: "1 item",
                expanded: false,
                cases: new[]
                {
                    new TestResultGroupCaseState("Case 1", "Description", new StatusBadgeState("Not Run", UiStatusTone.Idle))
                }));

            Assert.That(summaryAlert.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        [FeatureTestCase(
            "StatusBadge が skipped と inconclusive を表現する",
            "StatusBadge が追加した skipped / inconclusive tone を class と表示テキストへ反映することを確認します。",
            order: 206)]
        public void StatusBadge_SetState_SupportsSkippedAndInconclusive()
        {
            var badge = new StatusBadge();
            var textElement = badge.Q<UiTextElement>(className: UiClassNames.StatusBadge);

            badge.SetState(new StatusBadgeState("Skipped", UiStatusTone.Skipped));
            Assert.That(textElement.Text, Is.EqualTo("Skipped"));
            Assert.That(textElement.ClassListContains(UiClassNames.StatusSkipped), Is.True);

            badge.SetState(new StatusBadgeState("Inconclusive", UiStatusTone.Inconclusive));
            Assert.That(textElement.Text, Is.EqualTo("Inconclusive"));
            Assert.That(textElement.ClassListContains(UiClassNames.StatusInconclusive), Is.True);
        }

        [Test]
        [FeatureTestCase(
            "SearchableTreeView が SearchField 経由で絞り込む",
            "SearchableTreeView が内部の SearchField 変更を受けて tree と empty state を更新することを確認します。",
            order: 207)]
        public void SearchableTreeView_SearchFieldFiltersTree()
        {
            var treeView = new SearchableTreeView<string>(
                () => UiTextFactory.Create(),
                (element, data) => ((UiTextElement)element).SetText(data),
                null,
                "Empty");
            treeView.SetItems(new[]
            {
                new SearchableTreeItemData<string>(1, "Core", "Core suite"),
                new SearchableTreeItemData<string>(2, "Phase1", "Phase1 suite")
            });

            var searchField = treeView.Q<SearchField>();
            var emptyLabel = treeView.Q<UiTextElement>(className: UiClassNames.SearchableTreeViewEmpty);
            var tree = treeView.Q<TreeView>();

            Assert.That(searchField, Is.Not.Null);
            Assert.That(tree.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            searchField.Value = "missing";

            Assert.That(tree.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(emptyLabel.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            searchField.Value = "Core";

            Assert.That(tree.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(emptyLabel.style.display.value, Is.EqualTo(DisplayStyle.None));
        }
    }
}
