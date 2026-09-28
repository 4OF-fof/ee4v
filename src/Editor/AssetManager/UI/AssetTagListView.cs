using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetTagListView : VisualElement
    {
        private sealed class TagEntry
        {
            internal string Path { get; set; }
            internal int ItemCount { get; set; }
        }

        private readonly Action<string> _selectTag;
        private readonly SearchField _search;
        private readonly UiButton _usageSortButton;
        private readonly UiButton _nameSortButton;
        private readonly UiTextElement _empty;
        private readonly ScrollView _scroll;
        private readonly VisualElement _pills;
        private IReadOnlyList<TagEntry> _entries = Array.Empty<TagEntry>();
        private bool _sortByUsage;

        internal AssetTagListView(Action<string> selectTag)
        {
            _selectTag = selectTag;
            AddToClassList("ee4v-asset-manager__tag-list");
            var toolbar = new VisualElement();
            toolbar.AddToClassList("ee4v-asset-manager__tag-list-toolbar");
            _search = AssetManagerControls.CreateSearchField(
                I18N.Get("tagList.searchPlaceholder"),
                false,
                "ee4v-asset-manager__tag-list-search");
            _search.ValueChanged += _ => RefreshPills();
            toolbar.Add(_search);
            Add(toolbar);

            var metadata = new VisualElement();
            metadata.AddToClassList("ee4v-asset-manager__tag-list-metadata");
            var sort = new VisualElement();
            sort.AddToClassList("ee4v-asset-manager__tag-list-sort");
            _usageSortButton = AssetManagerControls.CreateButton(
                I18N.Get("tagList.orderUsage"),
                () => SetSortByUsage(true),
                "ee4v-asset-manager__tag-sort-choice");
            _usageSortButton.tooltip = I18N.Get("tagList.sortUsage");
            _usageSortButton.SetLabelFontSize(UiTypographyTokens.SmallFontSize);
            _nameSortButton = AssetManagerControls.CreateButton(
                I18N.Get("tagList.orderName"),
                () => SetSortByUsage(false),
                "ee4v-asset-manager__tag-sort-choice");
            _nameSortButton.tooltip = I18N.Get("tagList.sortName");
            _nameSortButton.SetLabelFontSize(UiTypographyTokens.SmallFontSize);
            sort.Add(_nameSortButton);
            sort.Add(_usageSortButton);
            metadata.Add(sort);
            Add(metadata);

            _scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden
            };
            _scroll.AddToClassList("ee4v-asset-manager__tag-list-scroll");
            _pills = new VisualElement();
            _pills.AddToClassList("ee4v-asset-manager__tag-pills");
            _scroll.Add(_pills);
            Add(_scroll);
            _empty = UiTextFactory.Create(
                string.Empty,
                UiClassNames.SecondaryText,
                "ee4v-asset-manager__tag-list-empty");
            _empty.SetWhiteSpace(WhiteSpace.Normal);
            Add(_empty);
            RefreshPills();
        }

        internal void SetData(
            IReadOnlyList<AssetTag> tags,
            IReadOnlyList<AssetItem> items)
        {
            var entries = new Dictionary<string, TagEntry>(StringComparer.Ordinal);
            foreach (var tag in tags ?? Array.Empty<AssetTag>())
            {
                var current = tag?.Path;
                if (string.IsNullOrWhiteSpace(current))
                {
                    continue;
                }
                while (!string.IsNullOrEmpty(current))
                {
                    if (!entries.ContainsKey(current))
                    {
                        entries.Add(current, new TagEntry { Path = current });
                    }
                    current = GetParentPath(current);
                }
            }

            foreach (var item in items ?? Array.Empty<AssetItem>())
            {
                if (item == null || item.IsArchived)
                {
                    continue;
                }
                var countedPaths = new HashSet<string>(StringComparer.Ordinal);
                foreach (var tag in item.Tags ?? Array.Empty<AssetTag>())
                {
                    var current = tag?.Path;
                    while (!string.IsNullOrEmpty(current))
                    {
                        if (countedPaths.Add(current) &&
                            entries.TryGetValue(current, out var entry))
                        {
                            entry.ItemCount++;
                        }
                        current = GetParentPath(current);
                    }
                }
            }
            _entries = entries.Values.ToArray();
            RefreshPills();
        }

        private static string GetParentPath(string path)
        {
            var separator = path.LastIndexOf('/');
            return separator < 0 ? string.Empty : path.Substring(0, separator);
        }

        private void SetSortByUsage(bool byUsage)
        {
            if (_sortByUsage == byUsage)
            {
                return;
            }
            _sortByUsage = byUsage;
            RefreshPills();
        }

        private void RefreshPills()
        {
            var query = (_search.Value ?? string.Empty).Trim();
            var matching = _entries.Where(entry =>
                query.Length == 0 || entry.Path.IndexOf(
                    query, StringComparison.OrdinalIgnoreCase) >= 0);
            var ordered = _sortByUsage
                ? matching.OrderByDescending(entry => entry.ItemCount)
                    .ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
                : matching.OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase);
            var entries = ordered.ThenBy(entry => entry.Path, StringComparer.Ordinal)
                .ToArray();
            _usageSortButton.EnableInClassList(
                "ee4v-asset-manager__tag-sort-choice--selected", _sortByUsage);
            _nameSortButton.EnableInClassList(
                "ee4v-asset-manager__tag-sort-choice--selected", !_sortByUsage);
            _usageSortButton.SetLabelColor(_sortByUsage
                ? UiColorTokens.TextPrimary : UiColorTokens.TextMuted);
            _nameSortButton.SetLabelColor(_sortByUsage
                ? UiColorTokens.TextMuted : UiColorTokens.TextPrimary);
            _pills.Clear();
            _pills.EnableInClassList(
                "ee4v-asset-manager__tag-pills--grouped", !_sortByUsage);
            if (_sortByUsage)
            {
                foreach (var entry in entries)
                {
                    _pills.Add(CreatePill(entry));
                }
            }
            else
            {
                foreach (var group in entries.GroupBy(entry =>
                    GetNameHeading(entry.Path))
                    .OrderBy(group => IsKanjiStart(group.First().Path)))
                {
                    var section = new VisualElement();
                    section.AddToClassList("ee4v-asset-manager__tag-group");
                    var heading = UiTextFactory.Create(
                        group.Key,
                        UiClassNames.SectionTitle,
                        "ee4v-asset-manager__tag-group-heading");
                    heading.SetFontSize(UiTypographyTokens.SmallFontSize);
                    heading.SetColor(UiColorTokens.TextMuted);
                    heading.pickingMode = PickingMode.Ignore;
                    section.Add(heading);
                    var pills = new VisualElement();
                    pills.AddToClassList("ee4v-asset-manager__tag-group-pills");
                    foreach (var entry in group)
                    {
                        pills.Add(CreatePill(entry));
                    }
                    section.Add(pills);
                    _pills.Add(section);
                }
            }
            _scroll.scrollOffset = Vector2.zero;
            _scroll.style.display = entries.Length > 0
                ? DisplayStyle.Flex : DisplayStyle.None;
            _empty.SetText(I18N.Get(
                _entries.Count == 0 ? "notice.noTags" : "tagList.noMatches"));
            _empty.style.display = entries.Length == 0
                ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static string GetNameHeading(string path)
        {
            if (IsKanjiStart(path))
            {
                return I18N.Get("tagList.otherHeading");
            }
            if (char.IsDigit(path, 0))
            {
                return "0–9";
            }
            var category = CharUnicodeInfo.GetUnicodeCategory(path, 0);
            switch (category)
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                    return StringInfo.GetNextTextElement(path).ToUpperInvariant();
                default:
                    return "#";
            }
        }

        private static bool IsKanjiStart(string path)
        {
            var codePoint = char.IsSurrogatePair(path, 0)
                ? char.ConvertToUtf32(path, 0) : path[0];
            return codePoint == 0x3005 || codePoint == 0x3007 ||
                codePoint == 0x303B ||
                (codePoint >= 0x3400 && codePoint <= 0x4DBF) ||
                (codePoint >= 0x4E00 && codePoint <= 0x9FFF) ||
                (codePoint >= 0xF900 && codePoint <= 0xFAFF) ||
                (codePoint >= 0x20000 && codePoint <= 0x2A6DF) ||
                (codePoint >= 0x2A700 && codePoint <= 0x2EE5F) ||
                (codePoint >= 0x2F800 && codePoint <= 0x2FA1F) ||
                (codePoint >= 0x30000 && codePoint <= 0x3347F);
        }

        private TagPill CreatePill(TagEntry entry)
        {
            var path = entry.Path;
            var separator = path.LastIndexOf('/');
            var pill = new TagPill(
                new TagPillState(path.Substring(separator + 1)),
                onClick: () => _selectTag?.Invoke(path));
            pill.AddToClassList("ee4v-asset-manager__tag-pill");
            pill.tooltip = I18N.Get(
                "tagList.itemCountTooltip", path, entry.ItemCount);
            if (separator >= 0)
            {
                var context = UiTextFactory.Create(
                    path.Substring(0, separator + 1),
                    UiClassNames.SecondaryText,
                    "ee4v-asset-manager__tag-pill-context");
                context.SetWhiteSpace(WhiteSpace.NoWrap);
                context.pickingMode = PickingMode.Ignore;
                pill.Insert(1, context);
            }
            var count = UiTextFactory.Create(
                entry.ItemCount.ToString(),
                UiClassNames.SecondaryText,
                "ee4v-asset-manager__tag-pill-count");
            count.pickingMode = PickingMode.Ignore;
            count.SetFontSize(UiTypographyTokens.CaptionFontSize);
            count.SetWhiteSpace(WhiteSpace.NoWrap);
            count.SetTextAlign(TextAnchor.MiddleRight);
            pill.Add(count);
            return pill;
        }
    }
}
