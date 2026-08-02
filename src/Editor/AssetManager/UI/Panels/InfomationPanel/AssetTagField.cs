using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetTagSelection
    {
        private readonly List<string> _options;
        private readonly List<string> _selected;

        internal AssetTagSelection(
            IReadOnlyList<string> options,
            IReadOnlyList<string> selected)
        {
            _selected = Normalize(selected);
            _options = Normalize(
                (options ?? Array.Empty<string>())
                    .Concat(_selected)
                    .ToArray());
        }

        internal IReadOnlyList<string> Selected => _selected;

        internal IReadOnlyList<string> SelectedOptions(string query = null) =>
            Filter(_selected, query);

        internal IReadOnlyList<string> AvailableOptions(string query = null) =>
            Filter(
                _options.Where(option => !Contains(_selected, option)),
                query);

        internal bool ContainsOption(string value) =>
            Contains(_options, NormalizeValue(value));

        internal void Toggle(string value)
        {
            var normalized = NormalizeValue(value);
            if (normalized.Length == 0)
            {
                return;
            }

            normalized = _options.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate,
                        normalized,
                        StringComparison.OrdinalIgnoreCase)) ??
                normalized;

            var selectedIndex = _selected.FindIndex(candidate =>
                string.Equals(
                    candidate,
                    normalized,
                    StringComparison.OrdinalIgnoreCase));
            if (selectedIndex >= 0)
            {
                _selected.RemoveAt(selectedIndex);
                return;
            }

            _selected.Add(normalized);
            if (!Contains(_options, normalized))
            {
                _options.Add(normalized);
            }
        }

        private static List<string> Normalize(
            IEnumerable<string> values)
        {
            var result = new List<string>();
            foreach (var value in values ?? Array.Empty<string>())
            {
                var normalized = NormalizeValue(value);
                if (normalized.Length > 0 &&
                    !Contains(result, normalized))
                {
                    result.Add(normalized);
                }
            }

            return result;
        }

        private static string NormalizeValue(string value) =>
            (value ?? string.Empty).Trim();

        private static IReadOnlyList<string> Filter(
            IEnumerable<string> values,
            string query)
        {
            var normalizedQuery = NormalizeValue(query);
            return values
                .Where(value =>
                    normalizedQuery.Length == 0 ||
                    value.IndexOf(
                        normalizedQuery,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();
        }

        private static bool Contains(
            IEnumerable<string> values,
            string value) =>
            values.Any(candidate =>
                string.Equals(
                    candidate,
                    value,
                    StringComparison.OrdinalIgnoreCase));
    }

    internal sealed class AssetTagField : VisualElement
    {
        private const string RootClassName =
            "ee4v-asset-manager-tag-field";
        private const string TagsClassName =
            "ee4v-asset-manager-tag-field__tags";
        private const string ChipClassName =
            "ee4v-asset-manager-tag-field__chip";
        private const string ChipLabelClassName =
            "ee4v-asset-manager-tag-field__chip-label";
        private const string RemoveClassName =
            "ee4v-asset-manager-tag-field__remove";
        private const string AddClassName =
            "ee4v-asset-manager-tag-field__add";

        private readonly VisualElement _tags;
        private readonly UiButton _addButton;
        private IReadOnlyList<string> _available = Array.Empty<string>();
        private IReadOnlyList<string> _values = Array.Empty<string>();

        internal AssetTagField()
        {
            AddToClassList(RootClassName);
            _tags = new VisualElement();
            _tags.AddToClassList(TagsClassName);
            Add(_tags);

            _addButton = new UiButton(
                new UiButtonState(
                    I18N.Get("assetManager.assetInfo.tagsNew"),
                    iconState: IconState.FromFluentIcon(
                        UiFluentIcon.Add,
                        UiSizeTokens.Size12),
                    variant: UiButtonVariant.Ghost));
            _addButton.AddToClassList(AddClassName);
            _addButton.RegisterCallback<ClickEvent>(evt =>
                OpenPicker(ToVector2(evt.position)));
            Add(_addButton);
        }

        internal event Action ValuesCommitted;

        internal IReadOnlyList<string> Values => _values;

        internal void SetValues(
            IReadOnlyList<string> available,
            IReadOnlyList<string> values)
        {
            _available = available ?? Array.Empty<string>();
            SetValuesWithoutNotify(values);
        }

        private void SetValuesWithoutNotify(
            IReadOnlyList<string> values)
        {
            _values = values == null
                ? Array.Empty<string>()
                : values.ToArray();
            RebuildTags();
        }

        private void RebuildTags()
        {
            _tags.Clear();
            _tags.style.display = _values.Count == 0
                ? DisplayStyle.None
                : DisplayStyle.Flex;

            for (var i = 0; i < _values.Count; i++)
            {
                var tagName = _values[i];
                var chip = new VisualElement();
                chip.AddToClassList(ChipClassName);
                chip.Add(UiTextFactory.Create(
                    tagName,
                    UiClassNames.ButtonLabel,
                    ChipLabelClassName));

                var removeButton = new UiButton(
                    new UiButtonState(
                        tooltip: string.Format(
                            I18N.Get(
                                "assetManager.assetInfo.tagsRemove"),
                            tagName),
                        iconState: IconState.FromFluentIcon(
                            UiFluentIcon.Dismiss,
                            UiSizeTokens.Size10),
                        variant: UiButtonVariant.Ghost,
                        size: UiButtonSize.Compact),
                    () => Remove(tagName));
                removeButton.AddToClassList(RemoveClassName);
                chip.Add(removeButton);
                _tags.Add(chip);
            }
        }

        private void Remove(string tagName)
        {
            _values = _values
                .Where(value => !string.Equals(
                    value,
                    tagName,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            RebuildTags();
            ValuesCommitted?.Invoke();
        }

        private void OpenPicker(Vector2 panelPosition)
        {
            AssetTagPickerWindow.Show(
                _addButton,
                _addButton.worldBound.Contains(panelPosition)
                    ? panelPosition
                    : (Vector2?)null,
                _available,
                _values,
                SetValuesWithoutNotify,
                () => ValuesCommitted?.Invoke());
        }

        private static Vector2 ToVector2(Vector3 position) =>
            new Vector2(position.x, position.y);
    }

    internal sealed class AssetTagPickerWindow : EditorWindow
    {
        private const float PopupWidth = 312f;
        private const float PopupHeight = 320f;
        private const string RootClassName =
            "ee4v-asset-manager-tag-picker";
        private const string SearchClassName =
            "ee4v-asset-manager-tag-picker__search";
        private const string ContentClassName =
            "ee4v-asset-manager-tag-picker__content";
        private const string SectionClassName =
            "ee4v-asset-manager-tag-picker__section";
        private const string SectionTitleClassName =
            "ee4v-asset-manager-tag-picker__section-title";
        private const string RowClassName =
            "ee4v-asset-manager-tag-picker__row";
        private const string EmptyClassName =
            "ee4v-asset-manager-tag-picker__empty";

        private AssetTagSelection _selection;
        private Action<IReadOnlyList<string>> _selectionChanged;
        private Action _closed;
        private SearchField _search;
        private VisualElement _content;
        private bool _didClose;

        internal static AssetTagPickerWindow Show(
            VisualElement anchor,
            Vector2? panelPosition,
            IReadOnlyList<string> options,
            IReadOnlyList<string> selected,
            Action<IReadOnlyList<string>> selectionChanged,
            Action closed)
        {
            CloseExistingWindows();
            var window = CreateInstance<AssetTagPickerWindow>();
            window._selection = new AssetTagSelection(options, selected);
            window._selectionChanged = selectionChanged;
            window._closed = closed;
            var size = new Vector2(PopupWidth, PopupHeight);
            window.minSize = size;
            window.maxSize = size;
            anchor?.Blur();
            window.ShowAsDropDown(
                panelPosition.HasValue
                    ? ResolvePointerAnchor(
                        anchor,
                        panelPosition.Value)
                    : ResolveElementAnchor(anchor),
                size);
            window.Focus();
            return window;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList("ee4v-ui");
            root.AddToClassList(RootClassName);
            root.AddToClassList(UiClassNames.PopupSurface);
            root.focusable = true;
            root.RegisterCallback<KeyDownEvent>(OnKeyDown);

            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/Inputs/Button/ui-button.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/Inputs/SearchField/search-field.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/Content/Icon/icon.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/AssetManager/UI/Panels/InfomationPanel/infomation-panel.uss");

            _search = new SearchField(new SearchFieldState(
                placeholder: I18N.Get(
                    "assetManager.assetInfo.tagsSearch")));
            _search.AddToClassList(SearchClassName);
            _search.ValueChanged += _ => RebuildOptions();
            root.Add(_search);

            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden
            };
            scroll.AddToClassList(ContentClassName);
            _content = scroll.contentContainer;
            root.Add(scroll);
            RebuildOptions();

            _search.schedule.Execute(() =>
            {
                var textField = _search.Q<TextField>();
                textField?.Focus();
            });
        }

        private void RebuildOptions()
        {
            if (_content == null || _selection == null)
            {
                return;
            }

            _content.Clear();
            var query = _search?.Value ?? string.Empty;
            var selected = _selection.SelectedOptions(query);
            var available = _selection.AvailableOptions(query);

            if (selected.Count > 0)
            {
                AddSection(
                    I18N.Get(
                        "assetManager.assetInfo.tagsSelected"),
                    selected,
                    selected: true);
            }

            if (available.Count > 0)
            {
                AddSection(
                    string.IsNullOrWhiteSpace(query)
                        ? I18N.Get(
                            "assetManager.assetInfo.tagsSuggested")
                        : I18N.Get(
                            "assetManager.assetInfo.tagsResults"),
                    available,
                    selected: false);
            }

            var normalizedQuery = (query ?? string.Empty).Trim();
            if (normalizedQuery.Length > 0 &&
                !_selection.ContainsOption(normalizedQuery))
            {
                var create = new UiButton(
                    new UiButtonState(
                        string.Format(
                            I18N.Get(
                                "assetManager.assetInfo.tagsCreate"),
                            normalizedQuery),
                        iconState: IconState.FromFluentIcon(
                            UiFluentIcon.Add,
                            UiSizeTokens.Size12),
                        variant: UiButtonVariant.Ghost),
                    () => Toggle(normalizedQuery));
                create.AddToClassList(RowClassName);
                _content.Add(create);
            }

            if (_content.childCount == 0)
            {
                _content.Add(UiTextFactory.Create(
                    I18N.Get("assetManager.assetInfo.tagsEmpty"),
                    UiClassNames.SecondaryText,
                    EmptyClassName));
            }
        }

        private void AddSection(
            string title,
            IReadOnlyList<string> values,
            bool selected)
        {
            var section = new VisualElement();
            section.AddToClassList(SectionClassName);
            section.Add(UiTextFactory.Create(
                string.Format(title, values.Count),
                UiClassNames.FormLabel,
                SectionTitleClassName));

            for (var i = 0; i < values.Count; i++)
            {
                var value = values[i];
                var row = new UiButton(
                    new UiButtonState(
                        value,
                        selected: selected,
                        variant: UiButtonVariant.Ghost),
                    () => Toggle(value));
                row.AddToClassList(RowClassName);
                section.Add(row);
            }

            _content.Add(section);
        }

        private void Toggle(string value)
        {
            _selection.Toggle(value);
            _selectionChanged?.Invoke(
                _selection.Selected.ToArray());
            RebuildOptions();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                evt.StopPropagation();
                Close();
                return;
            }

            if (evt.keyCode != KeyCode.Return &&
                evt.keyCode != KeyCode.KeypadEnter)
            {
                return;
            }

            var query = (_search?.Value ?? string.Empty).Trim();
            if (query.Length == 0)
            {
                return;
            }

            evt.StopPropagation();
            Toggle(query);
            _search.SetValueWithoutNotify(string.Empty);
            RebuildOptions();
        }

        private void OnDisable()
        {
            if (_didClose)
            {
                return;
            }

            _didClose = true;
            _closed?.Invoke();
        }

        private static void CloseExistingWindows()
        {
            var windows =
                Resources.FindObjectsOfTypeAll<AssetTagPickerWindow>();
            for (var i = 0; i < windows.Length; i++)
            {
                windows[i]?.Close();
            }
        }

        private static Rect ResolveElementAnchor(
            VisualElement anchor)
        {
            if (anchor == null || anchor.panel == null)
            {
                var point = GUIUtility.GUIToScreenPoint(Vector2.zero);
                return new Rect(point, Vector2.zero);
            }

            var root = anchor.panel.visualTree;
            var rootOffset = root != null
                ? root.worldBound.position
                : Vector2.zero;
            var localPosition =
                anchor.worldBound.position - rootOffset;
            var owner = FindOwnerWindow(anchor);
            var screenPosition = owner != null
                ? owner.position.position + localPosition
                : GUIUtility.GUIToScreenPoint(localPosition);
            return new Rect(
                screenPosition,
                anchor.worldBound.size);
        }

        private static Rect ResolvePointerAnchor(
            VisualElement anchor,
            Vector2 panelPosition)
        {
            if (anchor == null || anchor.panel == null)
            {
                return new Rect(
                    GUIUtility.GUIToScreenPoint(panelPosition),
                    Vector2.zero);
            }

            var root = anchor.panel.visualTree;
            var rootOffset = root != null
                ? root.worldBound.position
                : Vector2.zero;
            var owner = FindOwnerWindow(anchor);
            var screenPosition = owner != null
                ? owner.position.position + panelPosition - rootOffset
                : GUIUtility.GUIToScreenPoint(
                    panelPosition - rootOffset);
            return new Rect(screenPosition, Vector2.zero);
        }

        private static EditorWindow FindOwnerWindow(
            VisualElement target)
        {
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (var i = 0; i < windows.Length; i++)
            {
                var window = windows[i];
                if (window != null &&
                    window.rootVisualElement != null &&
                    window.rootVisualElement.panel == target.panel)
                {
                    return window;
                }
            }

            return EditorWindow.mouseOverWindow ??
                   EditorWindow.focusedWindow;
        }
    }
}
