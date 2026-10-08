using System;
using System.Collections.Generic;
using System.Linq;
using BlackStartX.GestureManager.Editor.Modules.Vrc3;
using BlackStartX.GestureManager.Library.VisualElements;
using Ee4v.AvatarEditing;
using Ee4v.Core.I18n;
using Ee4v.UI;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Ee4v.ExpressionMenu
{
    public sealed class ExpressionMenuView : VisualElement
    {
        private readonly AvatarEditingContext _context;
        private readonly bool _showsEditSource;
        private readonly List<int> _path = new List<int>();
        private readonly HashSet<string> _expandedMenuPaths = new HashSet<string> { "" };
        private string _treeSelectionPath;
        private readonly List<MenuTreeNode> _treeNodes = new List<MenuTreeNode>();
        private ScrollView _treeScroll;
        private MenuTreeNode _pendingTreeDestination;
        private List<MenuPage> _sources;
        private MenuPage _root;
        private MenuPage _page;
        private MenuEntry _submenuEntry;
        private int _source;
        private int _offset;
        private int _selected = -1;
        private string _error;
        private ModularAvatarMenuItem _selectAddedSource;
        private bool _openAddedSubmenu;
        private VisualElement _menuHost;
        private ConfirmationOverlay _operationOverlay;
        private static string T(string key) => I18N.Get("expressionMenu." + key);

        public ExpressionMenuView(AvatarEditingContext context, bool showsEditSource = false)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _showsEditSource = showsEditSource;
            UiComposition.Prepare(this, "Editor/Feature/Avatar/ExpressionMenu/expression-menu.uss");
            AddToClassList("ee4v-expression-menu");
            RegisterCallback<DetachFromPanelEvent>(evt => { if (evt.target == this) _operationOverlay?.Close(); });
            Refresh();
        }

        private void Refresh()
        {
            _operationOverlay?.Close();
            Clear();
            _treeNodes.Clear();
            _treeScroll = null;
            try
            {
                if (_context.Root == null) return;
                var sourcePage = _sources != null && _source > 0 && _source <= _sources.Count ? _sources[_source - 1] : null;
                _root = ExpressionMenuModel.Read(_context.Root, out _sources);
                if (sourcePage != null)
                {
                    var index = _sources.FindIndex(page => sourcePage.Asset != null ? page.Asset == sourcePage.Asset :
                        page.Asset == null && page.Entries.FirstOrDefault()?.Owner == sourcePage.Entries.FirstOrDefault()?.Owner);
                    _source = index + 1;
                    if (index < 0) { _path.Clear(); _offset = 0; _selected = -1; }
                }
                if (_source > _sources.Count) _source = 0;
                if (_pendingTreeDestination != null)
                {
                    var destination = _pendingTreeDestination;
                    _pendingTreeDestination = null;
                    _path.Clear();
                    if (destination.Path.Length > 0)
                    {
                        var visited = new HashSet<MenuPage>();
                        bool Find(MenuPage page)
                        {
                            if (SamePage(page, destination.Page)) return true;
                            if (!visited.Add(page)) return false;
                            for (var index = 0; index < page.Entries.Count; index++)
                            {
                                var child = page.Entries[index].Submenu;
                                if (child == null) continue;
                                _path.Add(index);
                                if (Find(child)) return true;
                                _path.RemoveAt(_path.Count - 1);
                            }
                            return false;
                        }
                        Find(_source == 0 ? _root : _sources[_source - 1]);
                    }
                    _expandedMenuPaths.Add(string.Join("/", _path));
                    _selected = -1;
                    _offset = 0;
                }
                if (_selectAddedSource != null)
                {
                    if (LocateSource(_selectAddedSource) && _openAddedSubmenu)
                    {
                        _path.Add(_selected);
                        _selected = -1;
                        _offset = 0;
                    }
                    _selectAddedSource = null;
                    _openAddedSubmenu = false;
                }
                _page = _source == 0 ? _root : _sources[_source - 1];
                _submenuEntry = null;
                foreach (var index in _path.ToArray())
                {
                    if (index >= _page.Entries.Count || _page.Entries[index].Submenu == null)
                    { _path.Clear(); _submenuEntry = null; _page = _source == 0 ? _root : _sources[_source - 1]; break; }
                    _submenuEntry = _page.Entries[index];
                    _page = _submenuEntry.Submenu;
                }
                _offset = _page.Entries.Count > 8 ? Mathf.Clamp(_offset, 0, (_page.Entries.Count - 1) / 7 * 7) : 0;
                if (_selected >= _page.Entries.Count) _selected = -1;
                if (_selected >= 0 && _page.Entries[_selected].Control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                    _selected = -1;
                Build();
            }
            catch (Exception exception)
            {
                Add(UiTextFactory.CreateHelpBox(exception.GetBaseException().Message, HelpBoxMessageType.Error));
            }
        }

        private void Build()
        {
            if (!string.IsNullOrEmpty(_error))
                Add(UiTextFactory.CreateHelpBox(_error, HelpBoxMessageType.Error));
            var selectedEntry = _selected >= 0 && _selected < _page.Entries.Count ? _page.Entries[_selected] : null;
            VisualElement editorColumn = this;
            if (selectedEntry != null)
            {
                var detail = new VisualElement { name = "expressionMenuDetail" };
                detail.AddToClassList("ee4v-expression-menu__detail");
                Add(detail);
                detail.Add(new ExpressionMenuPreview(_context, selectedEntry, () =>
                {
                    _selected = -1;
                    _error = null;
                    Refresh();
                }));
                editorColumn = new VisualElement();
                editorColumn.AddToClassList("ee4v-expression-menu__detail-editor");
                detail.Add(editorColumn);
                var navigation = new VisualElement();
                navigation.AddToClassList("ee4v-expression-menu__navigation");
                navigation.Add(UiTextFactory.Create(selectedEntry.Control.name, UiClassNames.SectionTitle));
                editorColumn.Add(navigation);
            }
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "expressionMenuScroll" };
            scroll.AddToClassList("ee4v-expression-menu__scroll");
            editorColumn.Add(scroll);
            _menuHost = new VisualElement { name = "expressionMenuEditor" };
            _menuHost.AddToClassList("ee4v-expression-menu__editor");
            _menuHost.EnableInClassList("ee4v-expression-menu__editor--detail", selectedEntry != null);
            scroll.Add(_menuHost);
            if (selectedEntry != null)
            {
                var item = selectedEntry.Owner as ModularAvatarMenuItem;
                if (item != null && (ExpressionMenuTemplateModel.HasActions(item) ||
                    item.PortableControl.Type == PortableControlType.SubMenu))
                    _menuHost.Add(new ExpressionMenuTemplateEditor(_context, item, Refresh));
                BuildSettings(selectedEntry);
                return;
            }
            if (_showsEditSource)
            {
                var choices = new List<string> { T("effective") };
                choices.AddRange(_sources.Select((page, i) => (i + 1) + ": " + page.Name));
                var source = UiTextFactory.CreatePopupField(T("view"), choices, _source);
                source.RegisterValueChangedCallback(evt =>
                {
                    _source = choices.IndexOf(evt.newValue);
                    _expandedMenuPaths.Clear();
                    _expandedMenuPaths.Add("");
                    _treeSelectionPath = null;
                    _path.Clear(); _offset = 0; _selected = -1; Refresh();
                });
                _menuHost.Add(source);
                _menuHost.Add(UiTextFactory.Create(_page.Name, UiClassNames.SecondaryText));
            }
            var overview = new VisualElement();
            overview.AddToClassList("ee4v-expression-menu__overview");
            _menuHost.Add(overview);
            var menu = new VisualElement();
            menu.AddToClassList("ee4v-expression-menu__menu-column");
            overview.Add(menu);
            BuildLocation(menu);
            BuildRadial(menu);
            BuildSubmenuTree(menu);
            var items = new VisualElement();
            items.AddToClassList("ee4v-expression-menu__items");
            items.RegisterCallback<GeometryChangedEvent>(evt =>
                items.EnableInClassList("ee4v-expression-menu__items--two-columns", evt.newRect.width >= 900f));
            overview.Add(items);
            var count = Math.Min(_page.Entries.Count > 8 ? 7 : 8, _page.Entries.Count - _offset);
            for (var slot = 0; slot < count; slot++)
            {
                var index = _offset + slot;
                BuildQuickSettings(_page.Entries[index], index, () => SelectControl(index),
                    _page.Entries[index].Submenu == null ? null : (Action)(() => OpenSubmenu(index)), items);
            }
            if (_offset + count >= _page.Entries.Count)
            {
                var cell = new VisualElement();
                cell.AddToClassList("ee4v-expression-menu__item-cell");
                var add = new UiButton(T("addItem"), ShowAddMenu, tooltip: T("add"),
                    icon: FluentUiIcons.CreateState("add.png", 32), variant: UiButtonVariant.Ghost)
                    { name = "expressionMenuAdd" };
                add.AddToClassList("ee4v-expression-menu__add-card");
                add.SetEnabled(CanAddToPage());
                cell.Add(add);
                items.Add(cell);
            }
        }

        private void BuildSubmenuTree(VisualElement host)
        {
            var tree = new VisualElement { name = "expressionMenuTree" };
            tree.AddToClassList("ee4v-expression-menu__tree");
            host.Add(tree);
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "expressionMenuTreeScroll" };
            _treeScroll = scroll;
            scroll.AddToClassList("ee4v-expression-menu__tree-scroll");
            tree.Add(scroll);
            var selectedPath = string.Join("/", _path);
            if (_treeSelectionPath != selectedPath)
            {
                for (var depth = 0; depth < _path.Count; depth++)
                    _expandedMenuPaths.Add(string.Join("/", _path.Take(depth)));
                _treeSelectionPath = selectedPath;
            }
            void Render(bool revealSelection)
            {
                var offset = scroll.scrollOffset;
                scroll.Clear();
                _treeNodes.Clear();
                var rows = 0;
                VisualElement selectedRow = null;
                var ancestors = new HashSet<MenuPage>();
                void AddNode(MenuPage page, MenuPage parentPage, MenuEntry entry, string label, Texture2D icon,
                    int[] path, string tooltip, VisualElement parent)
                {
                    var key = string.Join("/", path);
                    var current = page != null && _path.SequenceEqual(path);
                    var canExpand = page != null && !ancestors.Contains(page) && page.Entries.Count > 0;
                    var expanded = canExpand && _expandedMenuPaths.Contains(key);
                    var row = new VisualElement { userData = path };
                    row.AddToClassList("ee4v-expression-menu__tree-row");
                    parent.Add(row);
                    var node = new MenuTreeNode { Row = row, Page = page, ParentPage = parentPage,
                        Entry = entry, Path = path, Name = tooltip };
                    _treeNodes.Add(node);
                    rows++;
                    if (current) selectedRow = row;
                    if (canExpand)
                    {
                        var toggle = new UiButton("", () =>
                        {
                            if (!_expandedMenuPaths.Remove(key)) _expandedMenuPaths.Add(key);
                            Render(false);
                        }, tooltip: T(expanded ? "collapseSubmenus" : "expandSubmenus"),
                            icon: FluentUiIcons.CreateState(expanded ? "chevron_down.png" : "chevron_right.png"),
                            variant: UiButtonVariant.Ghost);
                        toggle.AddToClassList("ee4v-expression-menu__tree-toggle");
                        row.Add(toggle);
                    }
                    else
                    {
                        var spacer = new VisualElement();
                        spacer.AddToClassList("ee4v-expression-menu__tree-spacer");
                        row.Add(spacer);
                    }
                    void Open()
                    {
                        if (page != null) NavigateMenu(path);
                        else
                        {
                            _path.Clear();
                            _path.AddRange(path.Take(path.Length - 1));
                            _selected = path[path.Length - 1];
                            _offset = parentPage.Entries.Count > 8 ? _selected / 7 * 7 : 0;
                            _error = null;
                            Refresh();
                        }
                    }
                    var button = new UiButton(label, Open, tooltip: tooltip,
                        variant: UiButtonVariant.Ghost);
                    button.AddToClassList("ee4v-expression-menu__tree-link");
                    button.EnableInClassList("ee4v-expression-menu__tree-link--current", current);
                    button.SetContentAlignment(Justify.FlexStart);
                    var image = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                    image.AddToClassList("ee4v-expression-menu__tree-icon");
                    image.EnableInClassList("ee4v-expression-menu__tree-icon--root", path.Length == 0);
                    button.Content.Insert(0, image);
                    button.SetEnabled(page != null || CanInteract(entry));
                    if (entry != null) button.AddManipulator(new MenuTreeDragManipulator(this, node, Open));
                    row.Add(button);
                    if (entry != null ? !CanInteract(entry) : !CanEditMenuRoot(page))
                    {
                        var badge = new Badge(T("readOnlyBadge")) { pickingMode = PickingMode.Ignore };
                        badge.AddToClassList("ee4v-expression-menu__tree-read-only");
                        button.Content.Add(badge);
                    }
                    if (!expanded) return;
                    var children = new VisualElement();
                    children.AddToClassList("ee4v-expression-menu__tree-children");
                    parent.Add(children);
                    ancestors.Add(page);
                    for (var index = 0; index < page.Entries.Count; index++)
                    {
                        var child = page.Entries[index];
                        AddNode(child.Submenu, page, child, child.Control.name, ExpressionMenuModel.DisplayIcon(child.Control.icon),
                            path.Concat(new[] { index }).ToArray(), tooltip + " / " + child.Control.name, children);
                    }
                    ancestors.Remove(page);
                }
                var root = _source == 0 ? _root : _sources[_source - 1];
                var rootName = _source == 0 ? T("root") : root.Name;
                AddNode(root, null, null, rootName, FluentUiIcons.LoadTexture("folder.png", 16), Array.Empty<int>(), rootName, scroll);
                scroll.style.height = Mathf.Min(280f, rows * 28f);
                scroll.scrollOffset = offset;
                if (revealSelection && selectedRow != null)
                    scroll.schedule.Execute(() => { if (selectedRow.panel != null) scroll.ScrollTo(selectedRow); });
            }
            Render(true);
        }

        private void NavigateMenu(int[] path)
        {
            _path.Clear();
            _path.AddRange(path);
            _offset = 0;
            _selected = -1;
            _error = null;
            Refresh();
        }

        private void BuildLocation(VisualElement host)
        {
            var location = new VisualElement();
            location.AddToClassList("ee4v-expression-menu__location");
            host.Add(location);
            var iconHost = new VisualElement();
            iconHost.AddToClassList("ee4v-expression-menu__location-icon-host");
            location.Add(iconHost);
            var details = new VisualElement();
            details.AddToClassList("ee4v-expression-menu__quick-details");
            location.Add(details);
            if (_submenuEntry != null)
            {
                BuildIdentityFields(_submenuEntry, iconHost, details, "ee4v-expression-menu__location-icon-preview");
                return;
            }
            if (!CanEditMenuRoot(_page))
            {
                var badge = new Badge(T("readOnlyBadge"));
                badge.AddToClassList("ee4v-expression-menu__card-read-only");
                details.Add(badge);
            }
            var rootIcon = FluentUiIcons.LoadTexture("folder.png");
            var preview = new Image { image = rootIcon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            preview.AddToClassList("ee4v-expression-menu__location-icon-preview");
            iconHost.Add(preview);
            var icon = UiTextFactory.CreateObjectField("");
            icon.objectType = typeof(Texture2D);
            icon.allowSceneObjects = false;
            icon.tooltip = T("rootFixed");
            icon.AddToClassList("ee4v-expression-menu__quick-icon");
            icon.SetValueWithoutNotify(rootIcon);
            icon.SetEnabled(false);
            iconHost.Add(icon);
            var name = new InputField(new InputFieldState(T("root"))) { tooltip = T("rootFixed") };
            name.SetEnabled(false);
            details.Add(name);
        }

        private void BuildQuickSettings(MenuEntry entry, int index, Action edit, Action open, VisualElement host)
        {
            var cell = new VisualElement();
            cell.AddToClassList("ee4v-expression-menu__item-cell");
            host.Add(cell);
            var row = new VisualElement();
            row.AddToClassList("ee4v-expression-menu__quick-item");
            cell.Add(row);
            cell.userData = index;
            var handle = new VisualElement { focusable = true, tooltip = T("dragToReorder") };
            handle.AddToClassList("ee4v-expression-menu__drag-handle");
            handle.Add(new Icon(FluentUiIcons.CreateState("re_order_dots_vertical.png")));
            handle.SetEnabled(CanMoveControl(index, index - 1) || CanMoveControl(index, index + 1) ||
                _treeNodes.Any(node => CanDropInto(_page.Entries[index], _page, node)));
            handle.AddManipulator(new MenuReorderManipulator(this, index, row, host));
            row.Add(handle);
            var iconHost = new VisualElement();
            iconHost.AddToClassList("ee4v-expression-menu__quick-icon-host");
            row.Add(iconHost);
            var details = new VisualElement();
            details.AddToClassList("ee4v-expression-menu__quick-details");
            row.Add(details);
            if (!CanInteract(entry))
            {
                var badge = new Badge(T("readOnlyBadge"));
                badge.AddToClassList("ee4v-expression-menu__card-read-only");
                details.Add(badge);
            }
            BuildIdentityFields(entry, iconHost, details, "ee4v-expression-menu__quick-icon-preview");
            var actions = new VisualElement();
            actions.AddToClassList("ee4v-expression-menu__quick-actions");
            details.Add(actions);
            if (open != null) actions.Add(new UiButton(T("openSubmenu"), open));
            if (entry.Control.type != VRCExpressionsMenu.Control.ControlType.SubMenu)
            {
                var editButton = new UiButton(T("editDetails"), edit);
                editButton.SetEnabled(CanInteract(entry));
                actions.Add(editButton);
            }
            var move = new UiButton(T("move"), () => ShowMoveMenu(entry));
            move.SetEnabled(CanInteract(entry));
            actions.Add(move);
            var remove = new UiButton(T("remove"), () => ConfirmRemove(entry));
            remove.SetEnabled(CanInteract(entry));
            actions.Add(remove);
        }

        private ConfirmationOverlay OpenOperationOverlay(MessagePanelState state)
        {
            if (_operationOverlay != null) return null;
            var host = (VisualElement)this;
            while (host.parent != null && host.parent != panel?.visualTree) host = host.parent;
            var overlay = new ConfirmationOverlay(host, state);
            _operationOverlay = overlay;
            overlay.Closed += () => _operationOverlay = null;
            return overlay;
        }

        private void ConfirmRemove(MenuEntry entry)
        {
            if (!CanInteract(entry)) return;
            void Remove()
            {
                if (!CanInteract(entry)) throw new InvalidOperationException(T("readOnly"));
                ExpressionMenuModel.Remove(entry);
                _selected = -1;
                NotifyChanged();
            }
            if (entry.Submenu == null || entry.Submenu.Entries.Count == 0) { Run(Remove); return; }
            var overlay = OpenOperationOverlay(new MessagePanelState(T("deleteSubmenuTitle"),
                string.Format(T("deleteSubmenuMessage"), entry.Control.name), MessageSeverity.Warning));
            if (overlay == null) return;
            overlay.AddDiscardAction(T("deleteWithContents"), () =>
            {
                overlay.Close();
                Run(Remove);
            });
            overlay.Notification.Actions.Add(new UiButton(T("cancel"), overlay.Close));
        }

        private List<(string Name, MenuPage Page)> MoveDestinations(MenuEntry entry)
        {
            var destinations = new List<(string, MenuPage)>();
            var visited = new HashSet<MenuPage>();
            void Visit(MenuPage page, string path, bool editable)
            {
                if (page == null || !visited.Add(page)) return;
                var same = page == _page || page.Asset != null && page.Asset == _page.Asset &&
                    page.ChildRoot == _page.ChildRoot || page.ChildRoot != null && page.ChildRoot == _page.ChildRoot;
                if (!same && editable && ExpressionMenuModel.CanRelocate(_context, entry, page))
                    destinations.Add((path, page));
                foreach (var child in page.Entries)
                    if (child.Submenu != null) Visit(child.Submenu, path + " / " + child.Control.name, CanInteract(child));
            }
            Visit(_root, T("root"), true);
            return destinations;
        }

        private void ShowMoveMenu(MenuEntry entry)
        {
            if (!CanInteract(entry)) return;
            var destinations = MoveDestinations(entry);
            var overlay = OpenOperationOverlay(new MessagePanelState(T("moveTitle"),
                destinations.Count == 0 ? T("noMoveDestination") : "", MessageSeverity.Info));
            if (overlay == null) return;
            if (destinations.Count > 0)
            {
                var choice = UiTextFactory.CreatePopupField(T("moveDestination"), destinations.Select(value => value.Name).ToList(), 0);
                overlay.Notification.MessageText.parent.Add(choice);
                overlay.Notification.Actions.Add(new UiButton(T("move"), () =>
                {
                    var destination = destinations[choice.index].Page;
                    overlay.Close();
                    Run(() =>
                    {
                        if (!CanInteract(entry)) throw new InvalidOperationException(T("readOnly"));
                        ExpressionMenuModel.Relocate(_context, entry, destination);
                        _selected = -1;
                        NotifyChanged();
                    });
                }));
            }
            overlay.Notification.Actions.Add(new UiButton(T("cancel"), overlay.Close));
        }

        private void BuildIdentityFields(MenuEntry entry, VisualElement iconHost, VisualElement details, string previewClass)
        {
            var preview = new Image { image = ExpressionMenuModel.DisplayIcon(entry.Control.icon),
                scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            preview.AddToClassList(previewClass);
            iconHost.Add(preview);
            var draft = ExpressionMenuModel.Copy(entry.SourceControl ?? entry.Control);
            var source = entry.Owner as ModularAvatarMenuItem;
            void Change(Action maChange, Action controlChange)
            {
                if (!CanInteract(entry)) throw new InvalidOperationException(T("readOnly"));
                if (source != null) ExpressionMenuTemplateModel.Edit(_context, source, maChange);
                else
                {
                    controlChange();
                    ExpressionMenuModel.Update(entry, draft);
                    NotifyChanged();
                }
            }
            var icon = UiTextFactory.CreateObjectField("");
            icon.objectType = typeof(Texture2D);
            icon.allowSceneObjects = false;
            icon.tooltip = T("icon");
            icon.AddToClassList("ee4v-expression-menu__quick-icon");
            icon.SetValueWithoutNotify(entry.Control.icon);
            icon.SetEnabled(CanInteract(entry));
            icon.RegisterValueChangedCallback(evt => Run(() =>
            {
                try { Change(() => source.Control.icon = evt.newValue as Texture2D, () => draft.icon = evt.newValue as Texture2D); }
                catch { icon.SetValueWithoutNotify(evt.previousValue); throw; }
            }));
            iconHost.Add(icon);
            var name = new InputField(new InputFieldState(entry.Control.name)) { IsDelayed = true };
            name.tooltip = T("name");
            name.SetEnabled(CanInteract(entry));
            name.ValueChanged += value => Run(() => Change(() => source.label = value, () => draft.name = value));
            details.Add(name);
        }

        private bool CanMoveControl(int from, int to) => CanMoveControl(_page, from, to);

        private bool CanMoveControl(MenuPage page, int from, int to) => from >= 0 && to >= 0 &&
            from < page.Entries.Count && to < page.Entries.Count &&
            from != to && CanInteract(page.Entries[from]) &&
            Enumerable.Range(Math.Min(from, to), Math.Abs(to - from) + 1).All(index => index == from ||
                CanInteract(page.Entries[index]) && ExpressionMenuModel.CanMove(page.Entries[from], page.Entries[index]));

        private void MoveControl(int from, int to) => Run(() =>
        {
            if (!CanMoveControl(from, to)) throw new InvalidOperationException(T("cannotReorder"));
            ExpressionMenuModel.Move(_page.Entries[from], _page.Entries[to]);
            _offset = _page.Entries.Count > 8 ? to / 7 * 7 : 0;
            NotifyChanged();
        });

        private sealed class MenuTreeNode
        {
            internal VisualElement Row;
            internal MenuPage Page;
            internal MenuPage ParentPage;
            internal MenuEntry Entry;
            internal int[] Path;
            internal string Name;
        }

        private sealed class MenuTreeDrop
        {
            internal MenuTreeNode Node;
            internal int Index = -1;
            internal bool After;
        }

        private static bool SamePage(MenuPage first, MenuPage second) => first != null && second != null &&
            (first == second || first.Asset != null && first.Asset == second.Asset && first.ChildRoot == second.ChildRoot ||
                first.ChildRoot != null && first.ChildRoot == second.ChildRoot);

        private bool CanDropInto(MenuEntry entry, MenuPage source, MenuTreeNode node) => node.Page != null &&
            !SamePage(source, node.Page) && CanInteract(entry) && (node.Entry == null || CanInteract(node.Entry)) &&
            ExpressionMenuModel.CanRelocate(_context, entry, node.Page);

        private void ApplyTreeDrop(MenuEntry entry, MenuPage source, MenuTreeDrop drop) => Run(() =>
        {
            if (drop.Index < 0)
            {
                if (!CanDropInto(entry, source, drop.Node)) throw new InvalidOperationException(T("noMoveDestination"));
                ExpressionMenuModel.Relocate(_context, entry, drop.Node.Page);
                _pendingTreeDestination = drop.Node;
            }
            else
            {
                if (!CanMoveControl(source, source.Entries.IndexOf(entry), drop.Index))
                    throw new InvalidOperationException(T("cannotReorder"));
                ExpressionMenuModel.Move(entry, source.Entries[drop.Index]);
                _pendingTreeDestination = new MenuTreeNode { Page = source,
                    Path = drop.Node.Path.Take(drop.Node.Path.Length - 1).ToArray() };
            }
            NotifyChanged();
        });

        private sealed class MenuTreeDragPreview
        {
            private readonly ExpressionMenuView _view;
            private readonly MenuEntry _entry;
            private readonly MenuPage _source;
            private readonly VisualElement _ghost;
            private readonly UiTextElement _status;
            private MenuTreeNode _highlight;
            internal MenuTreeDrop Drop { get; private set; }

            internal MenuTreeDragPreview(ExpressionMenuView view, MenuEntry entry, MenuPage source)
            {
                _view = view;
                _entry = entry;
                _source = source;
                _ghost = new VisualElement { pickingMode = PickingMode.Ignore };
                _ghost.AddToClassList("ee4v-expression-menu__tree-drag-preview");
                var name = UiTextFactory.Create(entry.Control.name);
                name.pickingMode = PickingMode.Ignore;
                _ghost.Add(name);
                _status = UiTextFactory.Create(T("treeDragHint"), UiClassNames.SecondaryText);
                _status.pickingMode = PickingMode.Ignore;
                _ghost.Add(_status);
                view.Add(_ghost);
            }

            internal bool Update(Vector2 position)
            {
                ClearHighlight();
                Drop = null;
                var local = _view.WorldToLocal(position);
                _ghost.style.translate = new Translate(local.x + 16f, local.y + 16f, 0);
                var scroll = _view._treeScroll;
                var outer = scroll?.GetFirstAncestorOfType<ScrollView>();
                var within = scroll != null && scroll.contentViewport.worldBound.Contains(position) &&
                    (outer == null || outer.contentViewport.worldBound.Contains(position));
                var hit = within ? _view._treeNodes.FirstOrDefault(node => node.Row.worldBound.Contains(position)) : null;
                if (hit != null)
                {
                    var fraction = (position.y - hit.Row.worldBound.yMin) / hit.Row.worldBound.height;
                    var inside = hit.Page != null && (hit.Entry == null || fraction >= 0.25f && fraction <= 0.75f);
                    if (inside && _view.CanDropInto(_entry, _source, hit)) Drop = new MenuTreeDrop { Node = hit };
                    else if (!inside && hit.Entry != null && SamePage(_source, hit.ParentPage))
                    {
                        var sourceIndex = _source.Entries.IndexOf(_entry);
                        var after = fraction >= 0.5f;
                        var boundary = hit.Path[hit.Path.Length - 1] + (after ? 1 : 0);
                        var index = boundary > sourceIndex ? boundary - 1 : boundary;
                        if (_view.CanMoveControl(_source, sourceIndex, index))
                            Drop = new MenuTreeDrop { Node = hit, Index = index, After = after };
                    }
                    _highlight = hit;
                    hit.Row.AddToClassList(Drop == null ? "ee4v-expression-menu__tree-drop--invalid" :
                        Drop.Index < 0 ? "ee4v-expression-menu__tree-drop--inside" :
                        Drop.After ? "ee4v-expression-menu__tree-drop--after" : "ee4v-expression-menu__tree-drop--before");
                }
                _status.SetText(Drop == null ? hit == null ? T("treeDragHint") : T("treeDropUnavailable") :
                    string.Format(T(Drop.Index < 0 ? "treeDropInto" : Drop.After ? "treeDropAfter" : "treeDropBefore"), hit.Name));
                return within;
            }

            internal bool AutoScroll(Vector2 position)
            {
                var scroll = _view._treeScroll;
                if (scroll == null || !scroll.contentViewport.worldBound.Contains(position)) return false;
                var bounds = scroll.contentViewport.worldBound;
                var delta = position.y < bounds.yMin + 24f ? -7f : position.y > bounds.yMax - 24f ? 7f : 0f;
                if (delta != 0f) scroll.scrollOffset += new Vector2(0, delta);
                return true;
            }

            private void ClearHighlight()
            {
                if (_highlight == null) return;
                foreach (var suffix in new[] { "invalid", "inside", "before", "after" })
                    _highlight.Row.RemoveFromClassList("ee4v-expression-menu__tree-drop--" + suffix);
                _highlight = null;
            }

            internal void Dispose()
            {
                ClearHighlight();
                _ghost.RemoveFromHierarchy();
                Drop = null;
            }
        }

        private sealed class MenuTreeDragManipulator : PointerManipulator
        {
            private readonly ExpressionMenuView _view;
            private readonly MenuTreeNode _node;
            private readonly Action _open;
            private int _pointer = -1;
            private Vector2 _start;
            private Vector2 _position;
            private MenuTreeDragPreview _preview;
            private IVisualElementScheduledItem _autoScroll;

            internal MenuTreeDragManipulator(ExpressionMenuView view, MenuTreeNode node, Action open)
            { _view = view; _node = node; _open = open; }

            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
                target.RegisterCallback<PointerMoveEvent>(OnMove, TrickleDown.TrickleDown);
                target.RegisterCallback<PointerUpEvent>(OnUp, TrickleDown.TrickleDown);
                target.RegisterCallback<PointerCancelEvent>(OnCancel);
                target.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
                target.RegisterCallback<KeyDownEvent>(OnKey);
                target.RegisterCallback<DetachFromPanelEvent>(OnDetach);
            }

            protected override void UnregisterCallbacksFromTarget()
            {
                Cancel();
                target.UnregisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
                target.UnregisterCallback<PointerMoveEvent>(OnMove, TrickleDown.TrickleDown);
                target.UnregisterCallback<PointerUpEvent>(OnUp, TrickleDown.TrickleDown);
                target.UnregisterCallback<PointerCancelEvent>(OnCancel);
                target.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
                target.UnregisterCallback<KeyDownEvent>(OnKey);
                target.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
            }

            private void OnDown(PointerDownEvent evt)
            {
                if (evt.button != 0 || _pointer >= 0 || !_view.CanInteract(_node.Entry)) return;
                _pointer = evt.pointerId;
                _start = _position = evt.position;
                target.Focus();
                target.CapturePointer(_pointer);
                evt.StopImmediatePropagation();
            }

            private void OnMove(PointerMoveEvent evt)
            {
                if (evt.pointerId != _pointer) return;
                _position = evt.position;
                if (_preview == null && (_position - _start).sqrMagnitude >= 36f)
                {
                    _preview = new MenuTreeDragPreview(_view, _node.Entry, _node.ParentPage);
                    _node.Row.AddToClassList("ee4v-expression-menu__tree-row--dragging");
                    _autoScroll = target.schedule.Execute(() =>
                    {
                        if (_preview != null && _preview.AutoScroll(_position)) _preview.Update(_position);
                    }).Every(16);
                }
                _preview?.Update(_position);
                evt.StopImmediatePropagation();
            }

            private void OnUp(PointerUpEvent evt)
            {
                if (evt.pointerId != _pointer || evt.button != 0) return;
                _preview?.Update(evt.position);
                var drop = _preview?.Drop;
                var click = _preview == null && target.worldBound.Contains(evt.position);
                Cancel();
                evt.StopImmediatePropagation();
                if (drop != null) _view.ApplyTreeDrop(_node.Entry, _node.ParentPage, drop);
                else if (click) _open();
            }

            private void Cancel()
            {
                var pointer = _pointer;
                _pointer = -1;
                _autoScroll?.Pause();
                _autoScroll = null;
                _preview?.Dispose();
                _preview = null;
                _node.Row.RemoveFromClassList("ee4v-expression-menu__tree-row--dragging");
                if (pointer >= 0 && target.HasPointerCapture(pointer)) target.ReleasePointer(pointer);
            }

            private void OnCancel(PointerCancelEvent evt) { if (evt.pointerId == _pointer) Cancel(); }
            private void OnCaptureOut(PointerCaptureOutEvent evt) { if (evt.pointerId == _pointer) Cancel(); }
            private void OnDetach(DetachFromPanelEvent evt) => Cancel();
            private void OnKey(KeyDownEvent evt)
            { if (evt.keyCode == KeyCode.Escape && _pointer >= 0) { Cancel(); evt.StopPropagation(); } }
        }

        private sealed class MenuReorderManipulator : PointerManipulator
        {
            private readonly ExpressionMenuView _view;
            private readonly int _index;
            private readonly VisualElement _card;
            private readonly VisualElement _items;
            private int _pointerId = -1;
            private int _dropIndex = -1;
            private bool _dragging;
            private Vector2 _start;
            private Vector2 _position;
            private VisualElement _placeholder;
            private readonly List<(VisualElement Cell, int Index, Rect Bounds)> _slots =
                new List<(VisualElement, int, Rect)>();
            private bool _twoColumns;
            private ScrollView _scroll;
            private IVisualElementScheduledItem _autoScroll;
            private MenuTreeDragPreview _treePreview;

            internal MenuReorderManipulator(ExpressionMenuView view, int index, VisualElement card, VisualElement items)
            {
                _view = view;
                _index = index;
                _card = card;
                _items = items;
            }

            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<PointerDownEvent>(OnDown);
                target.RegisterCallback<PointerMoveEvent>(OnMove);
                target.RegisterCallback<PointerUpEvent>(OnUp);
                target.RegisterCallback<PointerCancelEvent>(OnCancel);
                target.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
                target.RegisterCallback<KeyDownEvent>(OnKey);
                target.RegisterCallback<DetachFromPanelEvent>(OnDetach);
            }

            protected override void UnregisterCallbacksFromTarget()
            {
                Cancel();
                target.UnregisterCallback<PointerDownEvent>(OnDown);
                target.UnregisterCallback<PointerMoveEvent>(OnMove);
                target.UnregisterCallback<PointerUpEvent>(OnUp);
                target.UnregisterCallback<PointerCancelEvent>(OnCancel);
                target.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
                target.UnregisterCallback<KeyDownEvent>(OnKey);
                target.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
            }

            private void OnDown(PointerDownEvent evt)
            {
                if (evt.button != 0 || !target.enabledInHierarchy || _pointerId >= 0) return;
                _pointerId = evt.pointerId;
                _start = _position = evt.position;
                _scroll = target.GetFirstAncestorOfType<ScrollView>();
                target.Focus();
                target.CapturePointer(_pointerId);
                evt.StopPropagation();
            }

            private void OnMove(PointerMoveEvent evt)
            {
                if (evt.pointerId != _pointerId) return;
                _position = evt.position;
                if (!_dragging && (_position - _start).sqrMagnitude >= 36f)
                {
                    _dragging = true;
                    BeginPreview();
                    _autoScroll = target.schedule.Execute(AutoScroll).Every(16);
                }
                if (_dragging) UpdateDrop();
                evt.StopPropagation();
            }

            private void BeginPreview()
            {
                foreach (var cell in _items.Children())
                    if (cell.userData is int index) _slots.Add((cell, index, cell.layout));
                _twoColumns = _items.ClassListContains("ee4v-expression-menu__items--two-columns");
                var source = _slots.First(slot => slot.Index == _index);
                // Keep the captured handle attached while reserving its space with the preview.
                source.Cell.style.position = Position.Absolute;
                source.Cell.style.left = source.Bounds.x;
                source.Cell.style.top = source.Bounds.y;
                source.Cell.style.width = source.Bounds.width;
                source.Cell.style.height = source.Bounds.height;
                source.Cell.style.opacity = 0;
                _placeholder = new VisualElement { pickingMode = PickingMode.Ignore };
                _placeholder.AddToClassList("ee4v-expression-menu__item-cell");
                _placeholder.style.height = source.Bounds.height;
                var preview = new VisualElement { pickingMode = PickingMode.Ignore };
                preview.AddToClassList("ee4v-expression-menu__drop-placeholder");
                var entry = _view._page.Entries[_index];
                _treePreview = new MenuTreeDragPreview(_view, entry, _view._page);
                var icon = new Image { image = ExpressionMenuModel.DisplayIcon(entry.Control.icon),
                    scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                icon.AddToClassList("ee4v-expression-menu__drop-icon");
                preview.Add(icon);
                var name = UiTextFactory.Create(entry.Control.name);
                name.pickingMode = PickingMode.Ignore;
                preview.Add(name);
                var line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.AddToClassList("ee4v-expression-menu__drop-line");
                preview.Add(line);
                _placeholder.Add(preview);
                PlacePreview(source.Cell, false, false);
            }

            private void PlacePreview(VisualElement cell, bool after, bool active)
            {
                _placeholder.RemoveFromHierarchy();
                _items.Insert(_items.IndexOf(cell) + (after ? 1 : 0), _placeholder);
                _placeholder.EnableInClassList("ee4v-expression-menu__drop--active", active);
            }

            private void UpdateDrop()
            {
                if (_treePreview != null && _treePreview.Update(_position))
                {
                    _dropIndex = -1;
                    PlacePreview(_slots.First(slot => slot.Index == _index).Cell, false, false);
                    return;
                }
                var destination = -1;
                VisualElement hit = null;
                var after = false;
                if ((_scroll == null || _scroll.contentViewport.worldBound.Contains(_position)) &&
                    _items.worldBound.Contains(_position))
                {
                    var local = _items.WorldToLocal(_position);
                    // Stable slots avoid flicker as the surrounding cards make room for the preview.
                    var slot = _slots.OrderBy(value => (local - new Vector2(
                        Mathf.Clamp(local.x, value.Bounds.xMin, value.Bounds.xMax),
                        Mathf.Clamp(local.y, value.Bounds.yMin, value.Bounds.yMax))).sqrMagnitude).First();
                    after = _twoColumns ? local.x >= slot.Bounds.center.x : local.y >= slot.Bounds.center.y;
                    var boundary = slot.Index + (after ? 1 : 0);
                    var index = boundary > _index ? boundary - 1 : boundary;
                    if (_view.CanMoveControl(_index, index)) { destination = index; hit = slot.Cell; }
                }
                if (_dropIndex == destination) return;
                _dropIndex = destination;
                PlacePreview(hit ?? _slots.First(slot => slot.Index == _index).Cell, hit != null && after, hit != null);
            }

            private void AutoScroll()
            {
                if (!_dragging || _scroll == null) return;
                if (_treePreview != null && _treePreview.AutoScroll(_position)) { UpdateDrop(); return; }
                var bounds = _scroll.contentViewport.worldBound;
                if (_position.x < bounds.xMin || _position.x > bounds.xMax) return;
                var delta = _position.y < bounds.yMin + 32f ? -10f : _position.y > bounds.yMax - 32f ? 10f : 0f;
                if (delta == 0f) return;
                _scroll.scrollOffset += new Vector2(0f, delta);
                UpdateDrop();
            }

            private void OnUp(PointerUpEvent evt)
            {
                if (evt.pointerId != _pointerId || evt.button != 0) return;
                _position = evt.position;
                if (_dragging) UpdateDrop();
                var destination = _dropIndex;
                var treeDrop = _treePreview?.Drop;
                Cancel();
                evt.StopPropagation();
                if (treeDrop != null) _view.ApplyTreeDrop(_view._page.Entries[_index], _view._page, treeDrop);
                else if (destination >= 0 && _view.CanMoveControl(_index, destination))
                    _view.MoveControl(_index, destination);
            }

            private void ClearDrop()
            {
                if (_placeholder != null)
                {
                    _placeholder.RemoveFromHierarchy();
                    _placeholder = null;
                    var cell = _card.parent;
                    cell.style.position = StyleKeyword.Null;
                    cell.style.left = StyleKeyword.Null;
                    cell.style.top = StyleKeyword.Null;
                    cell.style.width = StyleKeyword.Null;
                    cell.style.height = StyleKeyword.Null;
                    cell.style.opacity = StyleKeyword.Null;
                }
                _slots.Clear();
                _dropIndex = -1;
            }

            private void Cancel()
            {
                var pointer = _pointerId;
                _pointerId = -1;
                _dragging = false;
                _autoScroll?.Pause();
                _autoScroll = null;
                _scroll = null;
                _treePreview?.Dispose();
                _treePreview = null;
                ClearDrop();
                if (pointer >= 0 && target.HasPointerCapture(pointer)) target.ReleasePointer(pointer);
            }

            private void OnCancel(PointerCancelEvent evt) { if (evt.pointerId == _pointerId) Cancel(); }
            private void OnCaptureOut(PointerCaptureOutEvent evt) { if (evt.pointerId == _pointerId) Cancel(); }
            private void OnDetach(DetachFromPanelEvent evt) => Cancel();

            private void OnKey(KeyDownEvent evt)
            {
                if (evt.keyCode == KeyCode.Escape && _pointerId >= 0)
                { Cancel(); evt.StopPropagation(); return; }
                if (_pointerId >= 0 || !target.enabledInHierarchy) return;
                var direction = evt.keyCode == KeyCode.LeftArrow || evt.keyCode == KeyCode.UpArrow ? -1 :
                    evt.keyCode == KeyCode.RightArrow || evt.keyCode == KeyCode.DownArrow ? 1 : 0;
                if (direction == 0 || !_view.CanMoveControl(_index, _index + direction)) return;
                evt.StopPropagation();
                _view.MoveControl(_index, _index + direction);
            }
        }

        private void BuildRadial(VisualElement host)
        {
            var ring = new VisualElement();
            ring.AddToClassList("ee4v-expression-menu__ring");
            var labels = new VisualElement { pickingMode = PickingMode.Ignore };
            labels.AddToClassList("ee4v-expression-menu__labels");
            host.Add(ring);
            var paged = _page.Entries.Count > 8;
            var count = Math.Min(paged ? 7 : 8, _page.Entries.Count - _offset);
            var hasNext = paged && _offset + count < _page.Entries.Count;
            var hasBack = _offset > 0 || _path.Count > 0;
            var slices = count + (hasBack ? 2 : 1);
            var addSlot = slices - 1;
            if (hasBack)
            {
                var back = new UiButton(T("back"), () =>
                {
                    if (_offset > 0) _offset = Math.Max(0, _offset - 7);
                    else _path.RemoveAt(_path.Count - 1);
                    _selected = -1;
                    _error = null;
                    Refresh();
                }, variant: UiButtonVariant.Ghost, labelTypographyClassName: UiClassNames.SecondaryText);
                PlaceSlot(ring, labels, back, 0, slices, Resources.Load<Texture2D>("Vrc3/BSX_GM_Back"));
            }
            for (var slot = 0; slot < count; slot++)
            {
                var index = _offset + slot;
                var entry = _page.Entries[index];
                var button = new UiButton(entry.Control.name, () =>
                {
                    if (entry.Submenu != null) OpenSubmenu(index);
                    else SelectControl(index);
                }, variant: UiButtonVariant.Ghost, labelTypographyClassName: UiClassNames.SecondaryText);
                button.tooltip = entry.Control.name + "\n" + entry.Control.type + " / " +
                    (entry.Owner == null ? T("ambiguous") : entry.Owner.name);
                var icon = ExpressionMenuModel.DisplayIcon(entry.Control.icon);
                var slice = PlaceSlot(ring, labels, button, slot + (hasBack ? 1 : 0), slices, icon);
                var subIcon = RadialMenuUtility.GetSubIcon(entry.Control.type);
                if (subIcon != null)
                {
                    var indicator = new VisualElement { pickingMode = PickingMode.Ignore };
                    indicator.AddToClassList("ee4v-expression-menu__sub-icon");
                    indicator.Add(new Image { image = subIcon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore });
                    button.Content.Add(indicator);
                }
                slice.SetAvailable(entry.Submenu != null || CanInteract(entry), button);
            }
            if (hasNext)
            {
                var next = new UiButton(T("next"), () => { _offset += 7; _selected = -1; Refresh(); },
                    variant: UiButtonVariant.Ghost, labelTypographyClassName: UiClassNames.SecondaryText);
                PlaceSlot(ring, labels, next, addSlot, slices, FluentUiIcons.LoadTexture("arrow_right.png"));
            }
            else
            {
                var add = new UiButton(T("addItem"), ShowAddMenu, tooltip: T("add"), variant: UiButtonVariant.Ghost,
                    labelTypographyClassName: UiClassNames.SecondaryText);
                var addSlice = PlaceSlot(ring, labels, add, addSlot, slices, FluentUiIcons.LoadTexture("add.png"));
                addSlice.SetAvailable(CanAddToPage(), add);
            }
            var center = RadialMenuUtility.Prefabs.NewCircle(RadialMenu.Size / 3f,
                RadialMenuUtility.Colors.RadialInner, RadialMenuUtility.Colors.CustomBorder, Position.Absolute);
            center.AddToClassList("ee4v-expression-menu__center");
            ring.Add(center);
            ring.Add(labels);
            var radialSlices = ring.Children().OfType<RadialSlice>().ToArray();
            var cursor = new RadialCursor { pickingMode = PickingMode.Ignore,
                style = { position = Position.Absolute, left = 125f, top = 125f } };
            ring.Add(cursor);
            void MoveCursor(Vector2 position)
            {
                var delta = position - ring.contentRect.center;
                delta = delta.magnitude < RadialMenu.Size / 2f ? Vector2.ClampMagnitude(delta, RadialMenu.Size / 3f) : Vector2.zero;
                cursor.style.left = ring.contentRect.center.x + delta.x - 25f;
                cursor.style.top = ring.contentRect.center.y + delta.y - 25f;
            }
            void UpdatePointer(Vector2 position)
            {
                MoveCursor(position);
                foreach (var slice in radialSlices) slice.SetHovered(slice.ContainsPoint(position));
            }
            ring.RegisterCallback<PointerEnterEvent>(evt => UpdatePointer(evt.localPosition));
            ring.RegisterCallback<PointerMoveEvent>(evt => UpdatePointer(evt.localPosition));
            ring.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                MoveCursor(ring.contentRect.center);
                foreach (var slice in radialSlices) slice.SetHovered(false);
            });
        }

        private void SelectControl(int index)
        {
            if (index >= 0 && index < _page.Entries.Count &&
                _page.Entries[index].Control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
            { OpenSubmenu(index); return; }
            if (index < 0 || index >= _page.Entries.Count || !CanInteract(_page.Entries[index])) return;
            _selected = index;
            _error = null;
            Refresh();
        }

        private bool LocateSource(UnityEngine.Object source)
        {
            if (_root == null || source == null) return false;
            var visited = new HashSet<MenuPage>();
            var path = new List<int>();
            int selected = -1;
            bool Find(MenuPage page)
            {
                if (!visited.Add(page)) return false;
                for (var i = 0; i < page.Entries.Count; i++)
                {
                    var entry = page.Entries[i];
                    if (entry.Owner == source)
                    {
                        selected = i;
                        return true;
                    }
                    if (entry.Submenu == null) continue;
                    path.Add(i);
                    if (Find(entry.Submenu)) return true;
                    path.RemoveAt(path.Count - 1);
                }
                return false;
            }
            if (!Find(_root)) return false;
            _source = 0;
            _path.Clear();
            _path.AddRange(path);
            _selected = selected;
            var page = _root;
            foreach (var index in path) page = page.Entries[index].Submenu;
            _offset = page.Entries.Count > 8 ? selected / 7 * 7 : 0;
            return true;
        }

        private void OpenSubmenu(int index)
        {
            if (index < 0 || index >= _page.Entries.Count || _page.Entries[index].Submenu == null) return;
            _path.Add(index);
            _selected = -1;
            _offset = 0;
            _error = null;
            Refresh();
        }

        private bool CanInteract(MenuEntry entry) => entry.CanEdit && _context.Edits.CanEditPrefab() &&
            !EditorApplication.isPlayingOrWillChangePlaymode &&
            (!(entry.Owner is Component component) || ExpressionMenuTemplateModel.CanEdit(_context, component));

        private bool CanEditMenuRoot(MenuPage page)
        {
            if (page == null || !ExpressionMenuTemplateModel.CanEdit(_context)) return false;
            var target = (UnityEngine.Object)page.Asset ?? page.ChildRoot ??
                (_source > 0 ? page.Entries.FirstOrDefault()?.Owner : _context.Root);
            return ExpressionMenuModel.CanWrite(target) &&
                (!(target is Component) && !(target is GameObject) || ExpressionMenuTemplateModel.CanEdit(_context, target));
        }

        // Install target assets are references; new controls are stored in a separate prefab.
        private bool CanAddToPage() => _context.Edits.CanEditPrefab() && !EditorApplication.isPlayingOrWillChangePlaymode &&
            (_page.ChildRoot == null || ExpressionMenuTemplateModel.CanEdit(_context, _page.ChildRoot));

        private static RadialSlice PlaceSlot(VisualElement ring, VisualElement labels, UiButton button, int index, int count,
            Texture2D icon, bool selected = false)
        {
            button.AddToClassList("ee4v-expression-menu__slot");
            button.Content.AddToClassList("ee4v-expression-menu__slot-content");
            button.LabelText.SetWhiteSpace(WhiteSpace.Normal);
            button.SetLabelTextAlign(TextAnchor.MiddleCenter);
            button.SetLabelColor(new Color(0.824f, 0.824f, 0.824f));
            button.SetLabelFontSize(12);
            button.Content.Insert(0, new Image
            {
                image = icon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore
            });
            var degrees = -90f + index * 360f / count;
            var angle = degrees * Mathf.Deg2Rad;
            button.Content.style.left = Length.Percent(50f + Mathf.Cos(angle) * 100f / 3f);
            button.Content.style.top = Length.Percent(50f + Mathf.Sin(angle) * 100f / 3f);
            var slice = new RadialSlice(degrees, 360f / count, selected, button);
            ring.Add(slice);
            button.Content.RemoveFromHierarchy();
            labels.Add(button.Content);
            return slice;
        }

        private sealed class RadialSlice : VisualElement
        {
            private readonly float _angle;
            private readonly float _span;
            private readonly bool _selected;
            private readonly GmgCircleElement _visual;
            private readonly VisualElement _content;
            private bool _hovered;
            private bool _focused;
            private bool _highlighted;
            private bool _highlightInitialized;
            private ValueAnimation<float> _scaleAnimation;

            public RadialSlice(float angle, float span, bool selected, UiButton button)
            {
                _angle = angle;
                _span = span;
                _selected = selected;
                _content = button.Content;
                AddToClassList("ee4v-expression-menu__slice");
                _visual = new GmgCircleElement { Progress = span / 360f, BorderWidth = 2f,
                    BorderColor = RadialMenuUtility.Colors.CustomBorder, pickingMode = PickingMode.Ignore };
                _visual.AddToClassList("ee4v-expression-menu__slice-visual");
                _visual.transform.rotation = Quaternion.Euler(0, 0, angle + 90f - span * 0.5f);
                Add(_visual);
                var lineHolder = new VisualElement { pickingMode = PickingMode.Ignore };
                lineHolder.AddToClassList("ee4v-expression-menu__line-holder");
                var line = RadialMenuUtility.Prefabs.NewBorder(RadialMenu.Size / 2f);
                line.transform.rotation = Quaternion.Euler(0, 0, angle - span * 0.5f);
                lineHolder.Add(line);
                Add(lineHolder);
                Add(button);
                RegisterCallback<FocusInEvent>(_ => { _focused = true; UpdateColors(); });
                RegisterCallback<FocusOutEvent>(_ => { _focused = false; UpdateColors(); });
                RegisterCallback<DetachFromPanelEvent>(_ => StopScaleAnimation());
                UpdateColors();
            }

            public void SetHovered(bool hovered)
            {
                if (_hovered == hovered) return;
                _hovered = hovered;
                UpdateColors();
            }

            public void SetAvailable(bool available, UiButton button)
            {
                SetEnabled(available);
                _content.SetEnabled(available);
                button.SetLabelColor(available ? new Color(0.824f, 0.824f, 0.824f) : Color.gray);
                _content.Query<Image>().ForEach(icon => icon.tintColor = available ? Color.white : Color.gray);
                _highlightInitialized = false;
                UpdateColors();
            }

            public override bool ContainsPoint(Vector2 localPoint)
            {
                var radius = Mathf.Min(contentRect.width, contentRect.height) * 0.5f;
                var delta = localPoint - contentRect.center;
                return radius > 0f && delta.sqrMagnitude <= radius * radius &&
                    delta.sqrMagnitude >= radius * radius / 9f &&
                    Mathf.Abs(Mathf.DeltaAngle(_angle, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg)) <= _span * 0.5f;
            }

            private void UpdateColors()
            {
                var highlighted = enabledInHierarchy && (_selected || _hovered || _focused);
                if (_highlightInitialized && _highlighted == highlighted) return;
                _highlightInitialized = true;
                _highlighted = highlighted;
                _visual.VertexColor = !enabledSelf ? new Color(0.18f, 0.18f, 0.18f) :
                    highlighted ? RadialMenuUtility.Colors.CustomSelected : RadialMenuUtility.Colors.CustomMain;
                _visual.CenterColor = highlighted ? RadialMenuUtility.Colors.CenterSelected : RadialMenuUtility.Colors.CenterIdle;
                StopScaleAnimation();
                _scaleAnimation = _content.experimental.animation.Scale(highlighted ? 1.1f : 1f, 100);
                _scaleAnimation.KeepAlive();
            }

            private void StopScaleAnimation()
            {
                if (_scaleAnimation == null) return;
                _scaleAnimation.Stop();
                _scaleAnimation.Recycle();
                _scaleAnimation = null;
            }
        }

        private void BuildSettings(MenuEntry entry)
        {
            var host = _menuHost;
            host.Add(UiTextFactory.Create(T("settings"), UiClassNames.SectionTitle));
            var maSource = entry.Owner as ModularAvatarMenuItem;
            var simple = ExpressionMenuTemplateModel.HasActions(maSource);
            var submenuControl = entry.Control.type == VRCExpressionsMenu.Control.ControlType.SubMenu;
            var compact = simple || submenuControl && maSource != null &&
                ExpressionMenuTemplateModel.IsOwned(maSource.gameObject) && maSource.MenuSource == SubmenuSource.Children;
            var canEdit = entry.CanEdit && _context.Edits.CanEditPrefab() && !EditorApplication.isPlayingOrWillChangePlaymode &&
                (!(entry.Owner is Component sourceComponent) || ExpressionMenuTemplateModel.CanEdit(_context, sourceComponent));
            if (!canEdit) host.Add(UiTextFactory.CreateHelpBox(T("readOnly"), HelpBoxMessageType.Info));
            if (!compact)
            {
                var owner = UiTextFactory.CreateObjectField(T("source"));
                owner.SetValueWithoutNotify(entry.Owner); owner.SetEnabled(false); host.Add(owner);
            }
            if (entry.Submenu != null)
            {
                var index = _selected;
                var open = new UiButton(T("openSubmenu"), () => OpenSubmenu(index));
                open.SetEnabled(canEdit);
                host.Add(open);
            }
            var settingsStart = host.childCount;
            var draft = ExpressionMenuModel.Copy(entry.Control);
            if (entry.Owner is VRCExpressionsMenu menu) draft.subMenu = menu.controls[entry.Index].subMenu;
            else if (maSource != null) draft.subMenu = maSource.Control.subMenu;
            var synced = maSource != null && maSource.isSynced;
            var saved = maSource != null && maSource.isSaved;
            var defaultValue = maSource != null && maSource.isDefault;
            var automaticValue = maSource != null && maSource.automaticValue;
            void ApplyDraft()
            {
                ExpressionMenuModel.Update(entry, draft);
                if (maSource != null)
                {
                    if (!simple)
                    {
                        maSource.isSynced = synced; maSource.isSaved = saved;
                        maSource.isDefault = defaultValue; maSource.automaticValue = automaticValue;
                    }
                    PrefabUtility.RecordPrefabInstancePropertyModifications(maSource);
                    EditorUtility.SetDirty(maSource);
                    ExpressionMenuInstaller.SaveGeneratedPrefab(maSource.gameObject);
                }
                NotifyChanged();
            }
            void Change(Action change) => Run(() =>
            {
                if (!canEdit) throw new InvalidOperationException(T("readOnly"));
                change();
                ApplyDraft();
            });
            if (maSource == null || !simple && !submenuControl)
            {
                AddText(T("name"), draft.name, value => Change(() => draft.name = value), host);
                var icon = UiTextFactory.CreateObjectField(T("icon"));
                icon.objectType = typeof(Texture2D); icon.allowSceneObjects = false;
                icon.SetValueWithoutNotify(draft.icon);
                icon.RegisterValueChangedCallback(evt => Change(() => draft.icon = evt.newValue as Texture2D)); host.Add(icon);
            }
            if (maSource != null && !submenuControl && !simple)
            {
                AddToggle(T("synced"), synced, value => Change(() => synced = value), host);
                if (!compact)
                {
                    AddToggle(T("saved"), saved, value => Change(() => saved = value), host);
                    AddToggle(T("defaultValue"), defaultValue, value => Change(() => defaultValue = value), host);
                    AddToggle(T("automaticValue"), automaticValue, value => Change(() => automaticValue = value), host);
                }
            }
            if (!compact)
            {
                var type = UiTextFactory.CreateEnumField(T("type"), draft.type);
                host.Add(type);
                var details = new VisualElement(); host.Add(details);
                void BuildDetails()
                {
                    details.Clear();
                    AddText(T("parameter"), draft.parameter?.name, value => Change(() =>
                        draft.parameter = new VRCExpressionsMenu.Control.Parameter { name = value }), details);
                    var valueField = UiTextFactory.CreateFloatField(T("value"));
                    valueField.isDelayed = true;
                    valueField.SetValueWithoutNotify(draft.value);
                    valueField.RegisterValueChangedCallback(evt => Change(() => draft.value = evt.newValue)); details.Add(valueField);
                    if (draft.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                    {
                        var supportsAsset = !(maSource != null && maSource.MenuSource == SubmenuSource.Children);
                        var submenu = UiTextFactory.CreateObjectField(T("submenu"));
                        submenu.objectType = typeof(VRCExpressionsMenu); submenu.allowSceneObjects = false;
                        submenu.SetValueWithoutNotify(draft.subMenu);
                        submenu.RegisterValueChangedCallback(evt => Change(() => draft.subMenu = evt.newValue as VRCExpressionsMenu));
                        submenu.SetEnabled(supportsAsset);
                        details.Add(submenu);
                        var createSubmenu = new UiButton(T("createSubmenu"), () => Run(() =>
                        {
                            if (!canEdit) throw new InvalidOperationException(T("readOnly"));
                            var path = AssetDatabase.GetAssetPath(entry.Owner is VRCExpressionsMenu asset ? asset : _context.PrefabAsset);
                            var newMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                            newMenu.name = string.IsNullOrWhiteSpace(draft.name) ? "Submenu" : draft.name;
                            var newPath = AssetDatabase.GenerateUniqueAssetPath(
                                System.IO.Path.GetDirectoryName(path).Replace('\\', '/') + "/Submenu.asset");
                            AssetDatabase.CreateAsset(newMenu, newPath);
                            Undo.RegisterCreatedObjectUndo(newMenu, "Create Expression Submenu");
                            try { draft.subMenu = newMenu; ApplyDraft(); }
                            catch { AssetDatabase.DeleteAsset(newPath); throw; }
                        }));
                        createSubmenu.SetEnabled(supportsAsset);
                        details.Add(createSubmenu);
                    }
                    var axes = draft.type == VRCExpressionsMenu.Control.ControlType.RadialPuppet ? 1 :
                        draft.type == VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet ? 2 :
                        draft.type == VRCExpressionsMenu.Control.ControlType.FourAxisPuppet ? 4 : 0;
                    var old = draft.subParameters ?? Array.Empty<VRCExpressionsMenu.Control.Parameter>();
                    draft.subParameters = Enumerable.Range(0, axes).Select(i => new VRCExpressionsMenu.Control.Parameter
                        { name = i < old.Length ? old[i]?.name ?? "" : "" }).ToArray();
                    for (var axis = 0; axis < axes; axis++)
                    {
                        var index = axis;
                        AddText(T("axis") + " " + (axis + 1), draft.subParameters[axis].name,
                            value => Change(() => draft.subParameters[index].name = value), details);
                    }
                    if (axes > 1)
                    {
                        var oldLabels = draft.labels ?? Array.Empty<VRCExpressionsMenu.Control.Label>();
                        draft.labels = Enumerable.Range(0, 4).Select(i => i < oldLabels.Length ? oldLabels[i] :
                            new VRCExpressionsMenu.Control.Label()).ToArray();
                        for (var axis = 0; axis < 4; axis++)
                        {
                            var index = axis;
                            AddText(T("axisLabel") + " " + (axis + 1), draft.labels[axis].name,
                                value => Change(() => draft.labels[index].name = value), details);
                            var labelIcon = UiTextFactory.CreateObjectField(T("icon") + " " + (axis + 1));
                            labelIcon.objectType = typeof(Texture2D); labelIcon.allowSceneObjects = false;
                            labelIcon.SetValueWithoutNotify(draft.labels[axis].icon);
                            labelIcon.RegisterValueChangedCallback(evt => Change(() => draft.labels[index].icon = evt.newValue as Texture2D));
                            details.Add(labelIcon);
                        }
                    }
                }
                type.RegisterValueChangedCallback(evt => Change(() =>
                {
                    draft.type = (VRCExpressionsMenu.Control.ControlType)evt.newValue;
                    BuildDetails();
                }));
                BuildDetails();
            }
            if (!canEdit)
                foreach (var field in host.Children().Skip(settingsStart)) field.SetEnabled(false);
        }

        private void AddText(string label, string value, Action<string> changed, VisualElement host = null)
        {
            var input = new InputField(new InputFieldState(value)) { IsDelayed = true };
            input.ValueChanged += changed;
            (host ?? _menuHost).Add(new FormInput(label, input));
        }

        private void AddToggle(string label, bool value, Action<bool> changed, VisualElement host = null)
        {
            var toggle = UiTextFactory.CreateToggle(label);
            toggle.SetValueWithoutNotify(value);
            toggle.RegisterValueChangedCallback(evt => changed(evt.newValue));
            (host ?? _menuHost).Add(toggle);
        }

        private void AddControl(bool submenu)
        {
            if (!CanAddToPage()) throw new InvalidOperationException(T("readOnly"));
            _selectAddedSource = ExpressionMenuTemplateModel.Create(_context,
                T(submenu ? "newSubmenu" : "newControl"), _page.Asset, _page.ChildRoot, submenu);
            _openAddedSubmenu = submenu;
            NotifyChanged();
        }

        private void ShowAddMenu()
        {
            var choices = new GenericMenu();
            choices.AddItem(UiTextFactory.CreateGuiContent(T("addControl")), false, () => Run(() => AddControl(false)));
            choices.AddItem(UiTextFactory.CreateGuiContent(T("addSubmenu")), false, () => Run(() => AddControl(true)));
            choices.ShowAsContext();
        }

        private void NotifyChanged()
        {
            _context.Edits.WorkingSceneDirty = true;
            _context.Edits.Changed();
        }

        private void Run(Action action)
        {
            _error = null;
            try { action(); }
            catch (Exception exception) { _error = exception.GetBaseException().Message; }
            Refresh();
        }
    }
}
