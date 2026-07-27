using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.SceneSwitcher
{
    internal sealed class SceneSwitcherViewText
    {
        public string SearchPlaceholder { get; set; }
        public string SearchTooltip { get; set; }
        public string ClearSearchTooltip { get; set; }
        public string Empty { get; set; }
        public string NoMatches { get; set; }
        public string Open { get; set; }
        public string OpenTooltip { get; set; }
        public string FavoriteTooltip { get; set; }
        public string UnfavoriteTooltip { get; set; }
        public string CreateFormat { get; set; }
    }

    internal sealed class SceneSwitcherView : VisualElement
    {
        private const string RootClassName =
            "ee4v-scene-switcher";
        private const string SearchClassName =
            "ee4v-scene-switcher__search";
        private const string ListClassName =
            "ee4v-scene-switcher__list";
        private const string EmptyClassName =
            "ee4v-scene-switcher__empty";
        private const string FooterClassName =
            "ee4v-scene-switcher__footer";
        private const string CreateClassName =
            "ee4v-scene-switcher__create";

        private readonly SceneSwitcherViewText _text;
        private readonly Texture _sceneIcon;
        private readonly Texture _favoriteIcon;
        private readonly SearchField _search;
        private readonly ListView _list;
        private readonly UiTextElement _empty;
        private readonly VisualElement _footer;
        private readonly Button _create;
        private List<SceneSwitcherItem> _items =
            new List<SceneSwitcherItem>();
        private SceneSwitcherViewState _state;
        private bool _rendering;

        public SceneSwitcherView(
            SceneSwitcherViewText text,
            Texture sceneIcon,
            Texture favoriteIcon)
        {
            _text = text ?? new SceneSwitcherViewText();
            _sceneIcon = sceneIcon;
            _favoriteIcon = favoriteIcon;
            AddToClassList(RootClassName);

            _search = new SearchField(
                new SearchFieldState(
                    placeholder: _text.SearchPlaceholder,
                    searchTooltip: _text.SearchTooltip,
                    clearTooltip: _text.ClearSearchTooltip));
            _search.AddToClassList(SearchClassName);
            _search.ValueChanged += value =>
            {
                if (!_rendering)
                {
                    QueryChanged?.Invoke(value);
                }
            };
            Add(_search);

            _list = new ListView
            {
                fixedItemHeight = 30f,
                virtualizationMethod =
                    CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.Single,
                reorderable = true,
                showAlternatingRowBackgrounds =
                    AlternatingRowBackground.None,
                makeItem = CreateRow,
                bindItem = BindRow
            };
            _list.AddToClassList(ListClassName);
            _list.itemIndexChanged += OnItemIndexChanged;
            Add(_list);

            _empty = UiTextFactory.Create(
                _text.Empty,
                UiClassNames.SecondaryText);
            _empty.AddToClassList(EmptyClassName);
            Add(_empty);

            _footer = new VisualElement();
            _footer.AddToClassList(FooterClassName);
            _create = new Button(() =>
                CreateRequested?.Invoke(_state?.Query ?? string.Empty));
            _create.AddToClassList(CreateClassName);
            _footer.Add(_create);
            Add(_footer);
        }

        public event Action<string> QueryChanged;

        public event Action<string> ActivateRequested;

        public event Action<string> AddRequested;

        public event Action<string> FavoriteRequested;

        public event Action<IReadOnlyList<string>> OrderChanged;

        public event Action<string> CreateRequested;

        public void FocusSearch()
        {
            _search.Q<TextField>()?.Focus();
        }

        public void SetState(SceneSwitcherViewState state)
        {
            _state = state ?? new SceneSwitcherViewState(
                string.Empty,
                Array.Empty<SceneSwitcherItem>(),
                false);
            _rendering = true;
            _search.SetValueWithoutNotify(_state.Query);
            _items = _state.Items.ToList();
            _list.itemsSource = (IList)_items;
            _list.reorderable = !_state.IsFiltered;
            _list.RefreshItems();

            var hasItems = _items.Count > 0;
            _list.style.display = hasItems
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _empty.SetText(_state.IsFiltered
                ? _text.NoMatches
                : _text.Empty);
            _empty.style.display = hasItems
                ? DisplayStyle.None
                : DisplayStyle.Flex;

            _footer.style.display = _state.CanCreate
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _create.style.display = _state.CanCreate
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _create.text = _state.CanCreate
                ? string.Format(
                    _text.CreateFormat ?? "{0}",
                    _state.Query)
                : string.Empty;
            _rendering = false;
        }

        private VisualElement CreateRow()
        {
            var row = new SceneSwitcherRow(
                _text,
                _sceneIcon,
                _favoriteIcon);
            row.ActivateRequested += path =>
                ActivateRequested?.Invoke(path);
            row.AddRequested += path =>
                AddRequested?.Invoke(path);
            row.FavoriteRequested += path =>
                FavoriteRequested?.Invoke(path);
            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            if (element is SceneSwitcherRow row &&
                index >= 0 &&
                index < _items.Count)
            {
                row.SetState(_items[index]);
            }
        }

        private void OnItemIndexChanged(
            int fromIndex,
            int toIndex)
        {
            if (_rendering || _state == null || _state.IsFiltered)
            {
                return;
            }

            OrderChanged?.Invoke(
                _items.Select(item => item.Path).ToArray());
        }
    }

    internal sealed class SceneSwitcherRow : VisualElement
    {
        private const string RootClassName =
            "ee4v-scene-switcher-row";
        private const string OpenClassName =
            "ee4v-scene-switcher-row--open";
        private const string IconClassName =
            "ee4v-scene-switcher-row__icon";
        private const string NameClassName =
            "ee4v-scene-switcher-row__name";
        private const string BadgeClassName =
            "ee4v-scene-switcher-row__badge";
        private const string FavoriteClassName =
            "ee4v-scene-switcher-row__favorite";
        private const string FavoriteActiveClassName =
            "ee4v-scene-switcher-row__favorite--active";

        private readonly SceneSwitcherViewText _text;
        private readonly UiTextElement _name;
        private readonly UiTextElement _badge;
        private readonly Button _favorite;
        private readonly Image _favoriteImage;
        private Vector2 _pointerStart;
        private int _pointerButton = -1;
        private bool _pointerDown;
        private bool _dragging;
        private SceneSwitcherItem _item;

        public SceneSwitcherRow(
            SceneSwitcherViewText text,
            Texture sceneIcon,
            Texture favoriteIcon)
        {
            _text = text ?? new SceneSwitcherViewText();
            AddToClassList(RootClassName);

            var icon = new Image
            {
                image = sceneIcon,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            icon.AddToClassList(IconClassName);
            Add(icon);

            _name = UiTextFactory.Create(string.Empty);
            _name.AddToClassList(NameClassName);
            _name.SetWhiteSpace(WhiteSpace.NoWrap);
            Add(_name);

            _badge = UiTextFactory.Create(
                _text.Open,
                UiClassNames.SecondaryText);
            _badge.AddToClassList(BadgeClassName);
            _badge.tooltip = _text.OpenTooltip;
            Add(_badge);

            _favoriteImage = new Image
            {
                image = favoriteIcon,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            _favorite = new Button(() =>
            {
                if (_item != null)
                {
                    FavoriteRequested?.Invoke(_item.Path);
                }
            });
            _favorite.AddToClassList(FavoriteClassName);
            _favorite.Add(_favoriteImage);
            _favorite.RegisterCallback<PointerDownEvent>(
                evt => evt.StopPropagation());
            _favorite.RegisterCallback<PointerUpEvent>(
                evt => evt.StopPropagation());
            Add(_favorite);

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        public event Action<string> ActivateRequested;

        public event Action<string> AddRequested;

        public event Action<string> FavoriteRequested;

        public void SetState(SceneSwitcherItem item)
        {
            _item = item;
            _name.SetText(item?.Name ?? string.Empty);
            var isOpen = item?.IsOpen == true;
            var isFavorite = item?.IsFavorite == true;
            EnableInClassList(OpenClassName, isOpen);
            _badge.style.display = isOpen
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _favorite.EnableInClassList(
                FavoriteActiveClassName,
                isFavorite);
            _favoriteImage.tintColor = isFavorite
                ? UiColorTokens.StatusRunningText
                : UiColorTokens.TextDisabled;
            _favorite.tooltip = isFavorite
                ? _text.UnfavoriteTooltip
                : _text.FavoriteTooltip;
            tooltip = item?.Path ?? string.Empty;
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 && evt.button != 1)
            {
                return;
            }

            _pointerButton = evt.button;
            _pointerDown = true;
            _dragging = false;
            _pointerStart = evt.position;
            if (evt.button == 1)
            {
                evt.StopPropagation();
            }
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_pointerDown &&
                !_dragging &&
                Vector2.Distance(
                    _pointerStart,
                    evt.position) > 4f)
            {
                _dragging = true;
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_pointerDown || evt.button != _pointerButton)
            {
                return;
            }

            var pressedButton = _pointerButton;
            var shouldInvoke =
                !_dragging &&
                Vector2.Distance(
                    _pointerStart,
                    evt.position) <= 4f &&
                _item != null;
            _pointerButton = -1;
            _pointerDown = false;
            _dragging = false;
            if (pressedButton == 1)
            {
                evt.StopPropagation();
            }

            if (!shouldInvoke)
            {
                return;
            }

            if (pressedButton == 0)
            {
                ActivateRequested?.Invoke(_item.Path);
            }
            else
            {
                AddRequested?.Invoke(_item.Path);
            }
        }
    }
}
