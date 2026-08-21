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
    internal sealed class AssetTagOption
    {
        public AssetTagOption(string path, int usageCount)
        {
            Path = (path ?? string.Empty).Trim();
            UsageCount = Math.Max(0, usageCount);
        }

        public string Path { get; }
        public int UsageCount { get; }
    }

    internal sealed class AssetTagSelection
    {
        private readonly List<AssetTagOption> _options;
        private readonly List<string> _selected;

        public AssetTagSelection(
            IReadOnlyList<AssetTagOption> options,
            IReadOnlyList<string> selected)
        {
            _selected = Normalize(selected);
            _options = NormalizeOptions(options, _selected);
        }

        public IReadOnlyList<string> Selected => _selected;

        public IReadOnlyList<AssetTagOption> Filter(string query)
        {
            var normalizedQuery = NormalizeValue(query);
            return _options
                .Where(option =>
                    normalizedQuery.Length == 0 ||
                    option.Path.IndexOf(
                        normalizedQuery,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();
        }

        public bool ContainsOption(string value)
        {
            var normalized = NormalizeValue(value);
            return _options.Any(option => string.Equals(
                option.Path,
                normalized,
                StringComparison.OrdinalIgnoreCase));
        }

        public void Add(string value)
        {
            var normalized = NormalizeValue(value);
            if (normalized.Length == 0)
            {
                return;
            }

            var option = _options.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.Path,
                        normalized,
                        StringComparison.OrdinalIgnoreCase));
            normalized = option?.Path ?? normalized;
            if (!Contains(_selected, normalized))
            {
                _selected.Add(normalized);
            }
            if (option == null)
            {
                _options.Insert(0, new AssetTagOption(normalized, 0));
            }
        }

        private static List<AssetTagOption> NormalizeOptions(
            IEnumerable<AssetTagOption> options,
            IEnumerable<string> selected)
        {
            var result = new List<AssetTagOption>();
            foreach (var option in options ??
                     Array.Empty<AssetTagOption>())
            {
                if (option == null || option.Path.Length == 0 ||
                    result.Any(candidate => string.Equals(
                        candidate.Path,
                        option.Path,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                result.Add(option);
            }

            foreach (var value in selected ?? Array.Empty<string>())
            {
                var normalized = NormalizeValue(value);
                if (normalized.Length > 0 &&
                    result.All(candidate => !string.Equals(
                        candidate.Path,
                        normalized,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    result.Add(new AssetTagOption(normalized, 0));
                }
            }

            return result;
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

        private static string NormalizeValue(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static bool Contains(
            IEnumerable<string> values,
            string value)
        {
            return values.Any(candidate =>
                string.Equals(
                    candidate,
                    value,
                    StringComparison.OrdinalIgnoreCase));
        }
    }

    internal sealed class AssetTagField : VisualElement
    {
        private readonly VisualElement _tags;
        private readonly UiButton _addButton;
        private IReadOnlyList<AssetTagOption> _available =
            Array.Empty<AssetTagOption>();
        private IReadOnlyList<string> _values = Array.Empty<string>();
        private Vector2? _pickerPanelPosition;

        public AssetTagField()
        {
            AddToClassList("ee4v-asset-manager-tag-field");
            _tags = new VisualElement();
            _tags.AddToClassList("ee4v-asset-manager-tag-field__tags");
            Add(_tags);

            _addButton = AssetManagerControls.CreateIconTextButton(
                I18N.Get("detail.item.tagsNew"),
                "add.png",
                OpenPicker,
                "ee4v-asset-manager-tag-field__add");
            Add(_addButton);
            RegisterCallback<ClickEvent>(
                OnClick,
                TrickleDown.TrickleDown);
        }

        public event Action ValuesCommitted;

        public IReadOnlyList<string> Values => _values;

        public void SetValues(
            IReadOnlyList<AssetTagOption> available,
            IReadOnlyList<string> values)
        {
            _available = available ?? Array.Empty<AssetTagOption>();
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
            for (var index = 0; index < _values.Count; index++)
            {
                var tag = _values[index];
                var chip = new TagPill(
                    new TagPillState(
                        tag,
                        string.Format(
                            I18N.Get("detail.item.tagsRemove"),
                            tag)),
                    () => Remove(tag));
                chip.AddToClassList(
                    "ee4v-asset-manager-tag-field__tag");
                _tags.Add(chip);
            }
        }

        private void Remove(string tag)
        {
            _values = _values
                .Where(value => !string.Equals(
                    value,
                    tag,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            RebuildTags();
            ValuesCommitted?.Invoke();
        }

        private void OpenPicker()
        {
            var panelPosition = _pickerPanelPosition;
            _pickerPanelPosition = null;
            AssetTagPickerWindow.Show(
                _addButton,
                panelPosition,
                _available,
                _values,
                SetValuesWithoutNotify,
                () => ValuesCommitted?.Invoke());
        }

        private void OnClick(ClickEvent evt)
        {
            if (evt.button == (int)MouseButton.LeftMouse &&
                _addButton.worldBound.Contains(evt.position))
            {
                _pickerPanelPosition = evt.position;
            }
        }
    }

    internal sealed class AssetTagPickerWindow : CustomPopupWindow
    {
        private const float PopupWidth = 300f;
        private const float PopupHeight = 400f;
        private AssetTagSelection _selection;
        private Action<IReadOnlyList<string>> _selectionChanged;
        private Action _closed;
        private SearchField _search;
        private VisualElement _tagContainer;
        private bool _didClose;

        public static AssetTagPickerWindow Show(
            VisualElement anchor,
            Vector2? panelPosition,
            IReadOnlyList<AssetTagOption> options,
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
            if (panelPosition.HasValue)
            {
                CustomPopup.ShowAtPanelPosition(
                    window,
                    anchor,
                    panelPosition.Value,
                    size);
            }
            else
            {
                CustomPopup.ShowAsDropDown(window, anchor, size);
            }
            return window;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            AssetManagerWindowSession.PrepareRoot(root);
            root.focusable = true;
            root.RegisterCallback<KeyDownEvent>(OnKeyDown);

            var popup = new CustomPopup(
                I18N.Get("detail.item.tagsPickerTitle"));
            var close = AssetManagerControls.CreateIconButton(
                I18N.Get("detail.item.tagsClose"),
                "dismiss.png",
                Close,
                "ee4v-asset-manager-tag-picker__close");
            popup.HeaderActions.Add(close);

            var body = new VisualElement();
            body.AddToClassList(
                "ee4v-asset-manager-tag-picker__body");
            _search = new SearchField(new SearchFieldState(
                placeholder: I18N.Get("detail.item.tagsSearch"),
                searchIconState:
                    AssetManagerControls.LoadFluentIconState(
                        "search.png",
                        UiSizeTokens.Size14),
                clearIconState:
                    AssetManagerControls.LoadFluentIconState(
                        "dismiss.png",
                        UiSizeTokens.Size10)));
            _search.AddToClassList(
                "ee4v-asset-manager-tag-picker__search");
            _search.ValueChanged += _ => RebuildOptions();
            body.Add(_search);

            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden
            };
            scroll.AddToClassList(
                "ee4v-asset-manager-tag-picker__content");
            _tagContainer = scroll.contentContainer;
            _tagContainer.AddToClassList(
                "ee4v-asset-manager-tag-picker__tags");
            body.Add(scroll);
            popup.Content.Add(body);
            SetPopup(popup);
            RebuildOptions();

            _search.schedule.Execute(() =>
            {
                _search.Q<TextField>()?.Focus();
            });
        }

        private void RebuildOptions()
        {
            if (_tagContainer == null || _selection == null)
            {
                return;
            }

            _tagContainer.Clear();
            var query = _search?.Value ?? string.Empty;
            var options = _selection.Filter(query);
            var normalizedQuery = query.Trim();
            if (normalizedQuery.Length > 0 &&
                !_selection.ContainsOption(normalizedQuery))
            {
                _tagContainer.Add(CreateOptionTag(
                    string.Format(
                        I18N.Get("detail.item.tagsCreate"),
                        normalizedQuery),
                    () => Add(normalizedQuery),
                    true));
            }

            for (var index = 0; index < options.Count; index++)
            {
                var option = options[index];
                _tagContainer.Add(CreateOptionTag(
                    string.Format(
                        I18N.Get("detail.item.tagsWithCount"),
                        option.Path,
                        option.UsageCount),
                    () => Add(option.Path),
                    false,
                    option.Path));
            }

            if (_tagContainer.childCount == 0)
            {
                var empty = new EmptyState(new EmptyStateState(
                    string.Empty,
                    string.IsNullOrWhiteSpace(query)
                        ? I18N.Get("detail.item.tagsEmpty")
                        : I18N.Get("detail.item.tagsNoMatch")));
                empty.AddToClassList(
                    "ee4v-asset-manager-tag-picker__empty");
                _tagContainer.Add(empty);
            }
        }

        private static TagPill CreateOptionTag(
            string text,
            Action clicked,
            bool create,
            string tooltip = null)
        {
            var tag = new TagPill(
                new TagPillState(
                    text,
                    icon: create
                        ? FluentUiIcons.CreateState(
                            "add.png",
                            UiSizeTokens.Size12)
                        : null),
                onClick: clicked);
            tag.AddToClassList(
                "ee4v-asset-manager-tag-picker__tag");
            if (create)
            {
                tag.AddToClassList(
                    "ee4v-asset-manager-tag-picker__tag--create");
            }
            if (!string.IsNullOrEmpty(tooltip))
            {
                tag.tooltip = tooltip;
            }

            return tag;
        }

        private void Add(string value)
        {
            _selection.Add(value);
            _selectionChanged?.Invoke(_selection.Selected.ToArray());
            _search?.SetValueWithoutNotify(string.Empty);
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
            Add(query);
        }

        private void OnLostFocus()
        {
            Close();
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
            for (var index = 0; index < windows.Length; index++)
            {
                windows[index]?.Close();
            }
        }

    }
}
