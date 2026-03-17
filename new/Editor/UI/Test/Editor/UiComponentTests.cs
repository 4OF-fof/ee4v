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
            "CollapsibleSection が開閉状態を反映する",
            "CollapsibleSection が expanded state に応じて content の表示状態と通知を切り替えることを確認します。",
            order: 205)]
        public void CollapsibleSection_SetExpanded_TogglesContentVisibility()
        {
            var section = new CollapsibleSection(new CollapsibleSectionState("Tests", "2 items", expanded: false));
            section.Content.Add(UiTextFactory.Create("Case 1"));

            var notifications = new List<bool>();
            section.ExpandedChanged += notifications.Add;

            Assert.That(section.Expanded, Is.False);
            Assert.That(section.Content.style.display.value, Is.EqualTo(DisplayStyle.None));

            section.SetExpanded(true);

            Assert.That(section.Expanded, Is.True);
            Assert.That(section.Content.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(notifications, Is.EqualTo(new[] { true }));

            section.SetExpanded(false);

            Assert.That(section.Expanded, Is.False);
            Assert.That(section.Content.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(notifications, Is.EqualTo(new[] { true, false }));
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
