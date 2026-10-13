using System;
using System.Collections.Generic;
using System.Linq;
using BlackStartX.GestureManager.Editor.Modules.Vrc3;
using BlackStartX.GestureManager.Library.VisualElements;
using Ee4v.AvatarEditing;
using Ee4v.Core.I18n;
using Ee4v.Core.AvatarEvaluation;
using Ee4v.UI;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;
using VRC.SDK3.Avatars.ScriptableObjects;
using MenuEntry = Ee4v.Core.AvatarEvaluation.AvatarMenuEntry;
using MenuPage = Ee4v.Core.AvatarEvaluation.AvatarMenuPage;

namespace Ee4v.ExpressionMenu
{
    public sealed class ExpressionMenuView : VisualElement
    {
        private const string MenuTreeDragDataKey = "ee4v.expression-menu.tree-drag";
        private readonly AvatarEditingContext _context;
        private readonly bool _showsEditSource;
        private readonly List<int> _path = new List<int>();
        private readonly HashSet<string> _expandedMenuPaths = new HashSet<string> { "" };
        private string _treeSelectionPath;
        private readonly List<MenuTreeNode> _treeNodes = new List<MenuTreeNode>();
        private readonly List<VisualElement> _treeAddRows = new List<VisualElement>();
        private ScrollView _treeScroll;
        private Action _renderTree;
        private MenuTreeDragPayload _treeDrag;
        private MenuTreeDragPreview _treeDragPreview;
        private IVisualElementScheduledItem _treeDragAutoScroll;
        private Vector2 _treeDragPosition;
        private MenuTreeNode _pendingTreeDestination;
        private bool _applyingTreeDrop;
        private bool _suppressTreeClick;
        private List<MenuPage> _sources;
        private MenuPage _root;
        private MenuPage _page;
        private Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> _replacedMenus;
        private readonly Dictionary<VRCExpressionsMenu, bool> _copyableMenus = new Dictionary<VRCExpressionsMenu, bool>();
        private Func<VRCExpressionsMenu, bool> _editableCopyCheck;
        private GameObject _resolvedAvatar;
        private bool _menuDirty = true;
        private bool _listening;
        private MenuEntry _submenuEntry;
        private int _source;
        private int _offset;
        private int _selected = -1;
        private bool _showsActionPreview;
        private ScrollView _listScroll;
        private ScrollView _inspectorScroll;
        private string _listScrollKey;
        private string _inspectorScrollKey;
        private Vector2 _listScrollOffset;
        private Vector2 _inspectorScrollOffset;
        private string _error;
        private ModularAvatarMenuItem _selectAddedSource;
        private bool _openAddedSubmenu;
        private VisualElement _menuHost;
        private ConfirmationOverlay _operationOverlay;
        private Action _closeCopyOverlay;
        private static string T(string key) => I18N.Get("expressionMenu." + key);

        public ExpressionMenuView(AvatarEditingContext context, bool showsEditSource = false)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _showsEditSource = showsEditSource;
            UiComposition.Prepare(this, "Editor/Feature/Avatar/ExpressionMenu/expression-menu.uss");
            AddToClassList("ee4v-expression-menu");
            RegisterCallback<GeometryChangedEvent>(evt =>
                EnableInClassList("ee4v-expression-menu--narrow", evt.newRect.width < 880f));
            RegisterCallback<AttachToPanelEvent>(OnAttach);
            RegisterCallback<DetachFromPanelEvent>(OnDetach);
            RegisterCallback<PointerDownEvent>(_ => _suppressTreeClick = false, TrickleDown.TrickleDown);
            RegisterCallback<KeyDownEvent>(_ => _suppressTreeClick = false, TrickleDown.TrickleDown);
            Refresh();
        }

        private void OnAttach(AttachToPanelEvent evt)
        {
            if (evt.target != this || _listening) return;
            _listening = true;
            EditorApplication.hierarchyChanged += InvalidateMenu;
            EditorApplication.projectChanged += InvalidateMenu;
            Undo.undoRedoPerformed += OnUndoRedo;
            Undo.postprocessModifications += OnModifications;
            ObjectChangeEvents.changesPublished += OnObjectChanges;
        }

        private void OnDetach(DetachFromPanelEvent evt)
        {
            if (evt.target != this) return;
            _operationOverlay?.Close();
            _closeCopyOverlay?.Invoke();
            InvalidateMenu();
            if (!_listening) return;
            _listening = false;
            EditorApplication.hierarchyChanged -= InvalidateMenu;
            EditorApplication.projectChanged -= InvalidateMenu;
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.postprocessModifications -= OnModifications;
            ObjectChangeEvents.changesPublished -= OnObjectChanges;
        }

        private void InvalidateMenu() => _menuDirty = true;

        private void OnUndoRedo()
        {
            InvalidateMenu();
            if (!_applyingTreeDrop && panel != null) RefreshNavigation();
        }

        private bool IsMenuSource(UnityEngine.Object target)
        {
            if (target == null || _context.Root == null) return false;
            if (target is VRCExpressionsMenu asset) return _sources != null && _sources.Any(page => page.Asset == asset);
            var transform = target is GameObject go ? go.transform : (target as Component)?.transform;
            return transform != null && transform.IsChildOf(_context.Root.transform);
        }

        private UndoPropertyModification[] OnModifications(UndoPropertyModification[] modifications)
        {
            if (!_menuDirty && modifications.Any(change => IsMenuSource(change.currentValue?.target))) InvalidateMenu();
            return modifications;
        }

        private void OnObjectChanges(ref ObjectChangeEventStream stream)
        {
            if (_menuDirty) return;
            for (var index = 0; index < stream.length; index++)
            {
                UnityEngine.Object target = null;
                if (stream.GetEventType(index) == ObjectChangeKind.ChangeGameObjectOrComponentProperties)
                {
                    stream.GetChangeGameObjectOrComponentPropertiesEvent(index, out var change);
                    target = EditorUtility.InstanceIDToObject(change.instanceId);
                }
                else if (stream.GetEventType(index) == ObjectChangeKind.ChangeAssetObjectProperties)
                {
                    stream.GetChangeAssetObjectPropertiesEvent(index, out var change);
                    target = EditorUtility.InstanceIDToObject(change.instanceId);
                }
                if (!IsMenuSource(target)) continue;
                InvalidateMenu();
                return;
            }
        }

        private void Refresh() => Refresh(true);

        private void RefreshNavigation() => Refresh(false);

        private void Refresh(bool reload)
        {
            _operationOverlay?.Close();
            CancelTreeDrag();
            if (_listScroll != null) { _listScrollOffset = _listScroll.scrollOffset; }
            if (_inspectorScroll != null) { _inspectorScrollOffset = _inspectorScroll.scrollOffset; }
            _listScroll = null;
            _inspectorScroll = null;
            Clear();
            _treeNodes.Clear();
            _treeAddRows.Clear();
            _treeScroll = null;
            _renderTree = null;
            try
            {
                if (_context.Root == null) return;
                if (reload || _menuDirty || _root == null || _resolvedAvatar != _context.Root)
                {
                    _menuDirty = true;
                    _copyableMenus.Clear();
                    _editableCopyCheck = null;
                    var sourcePage = _sources != null && _source > 0 && _source <= _sources.Count ? _sources[_source - 1] : null;
                    _root = ExpressionMenuModel.Read(_context.Root, out _sources);
                    _resolvedAvatar = _context.Root;
                    if (sourcePage != null)
                    {
                        var sourceAsset = sourcePage.Asset;
                        if (sourceAsset != null && _replacedMenus != null && _replacedMenus.TryGetValue(sourceAsset, out var replacement))
                            sourceAsset = replacement;
                        var index = _sources.FindIndex(page => sourceAsset != null ? page.Asset == sourceAsset :
                            page.Asset == null && page.Controls.FirstOrDefault()?.Owner == sourcePage.Controls.FirstOrDefault()?.Owner);
                        _source = index + 1;
                        if (index < 0) { _path.Clear(); _offset = 0; _selected = -1; }
                    }
                    _menuDirty = false;
                }
                _replacedMenus = null;
                if (_source > _sources.Count) _source = 0;
                if (_pendingTreeDestination != null)
                {
                    var destination = _pendingTreeDestination;
                    _pendingTreeDestination = null;
                    _path.Clear();
                    if (!SamePage(destination.Page, _source == 0 ? _root : _sources[_source - 1]))
                    {
                        var visited = new HashSet<MenuPage>();
                        bool Find(MenuPage page)
                        {
                            if (SamePage(page, destination.Page)) return true;
                            if (!visited.Add(page)) return false;
                            for (var index = 0; index < page.Controls.Count; index++)
                            {
                                var child = page.Controls[index].Submenu;
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
                    if (index >= _page.Controls.Count || _page.Controls[index].Submenu == null)
                    { _path.Clear(); _submenuEntry = null; _page = _source == 0 ? _root : _sources[_source - 1]; break; }
                    _submenuEntry = _page.Controls[index];
                    _page = _submenuEntry.Submenu;
                }
                _offset = _page.Controls.Count > 8 ? Mathf.Clamp(_offset, 0, (_page.Controls.Count - 1) / 7 * 7) : 0;
                if (_selected >= _page.Controls.Count) _selected = -1;
                if (_selected >= 0 && _page.Controls[_selected].Control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
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
            var selectedEntry = _selected >= 0 && _selected < _page.Controls.Count ? _page.Controls[_selected] : null;
            var showsPreview = _showsActionPreview && selectedEntry != null;
            if (_showsEditSource && !showsPreview)
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
                    _path.Clear(); _offset = 0; _selected = -1; RefreshNavigation();
                });
                Add(source);
            }

            var layout = new VisualElement();
            layout.AddToClassList("ee4v-expression-menu__browser-layout");
            Add(layout);
            var browser = new VisualElement();
            browser.AddToClassList("ee4v-expression-menu__browser");
            browser.EnableInClassList("ee4v-expression-menu__browser--preview", showsPreview);
            layout.Add(browser);
            var sourceKey = _context.Root.GetInstanceID() + ":" + _source;
            var pageKey = sourceKey + ":" + string.Join("/", _path);
            if (showsPreview)
            {
                browser.Add(new ExpressionMenuPreview(_context, selectedEntry, () =>
                { _showsActionPreview = false; RefreshNavigation(); }));
            }
            else
            {
                var toolbar = new ActionBar();
                toolbar.AddToClassList("ee4v-expression-menu__browser-toolbar");
                if (selectedEntry != null)
                    toolbar.Actions.Add(new UiButton(T("showPreview"), () =>
                    { _showsActionPreview = true; RefreshNavigation(); }));
                browser.Add(toolbar);
                BuildRadial(browser);
                var listScroll = new ScrollView(ScrollViewMode.Vertical) { name = "expressionMenuScroll" };
                var listOffset = sourceKey == _listScrollKey ? _listScrollOffset : Vector2.zero;
                _listScrollKey = sourceKey;
                _listScroll = listScroll;
                RestoreScroll(listScroll, listOffset);
                listScroll.AddToClassList("ee4v-expression-menu__browser-scroll");
                browser.Add(listScroll);
                BuildSubmenuTree(listScroll);
                BuildExcludedItems(listScroll);
            }

            var inspector = new VisualElement();
            inspector.AddToClassList("ee4v-expression-menu__inspector");
            layout.Add(inspector);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            var inspectorKey = pageKey + ":" + _selected;
            var inspectorOffset = inspectorKey == _inspectorScrollKey ? _inspectorScrollOffset : Vector2.zero;
            _inspectorScrollKey = inspectorKey;
            _inspectorScroll = scroll;
            RestoreScroll(scroll, inspectorOffset);
            scroll.AddToClassList("ee4v-expression-menu__scroll");
            inspector.Add(scroll);
            _menuHost = new VisualElement { name = "expressionMenuEditor" };
            _menuHost.AddToClassList("ee4v-expression-menu__editor");
            scroll.Add(_menuHost);
            if (selectedEntry == null)
            {
                BuildLocation(_menuHost);
                return;
            }
            _menuHost.Add(new SectionHeader(selectedEntry.Control.name));
            var item = selectedEntry.Owner as ModularAvatarMenuItem;
            if (item != null && (ExpressionMenuTemplateModel.HasActions(item) || item.PortableControl.Type == PortableControlType.SubMenu))
                _menuHost.Add(new ExpressionMenuTemplateEditor(_context, item, Refresh, RefreshNavigation));
            BuildSettings(selectedEntry);
            if (!CanInteract(selectedEntry)) AddEditableCopyAction(_menuHost, selectedEntry.Owner as VRCExpressionsMenu);
        }

        private static void RestoreScroll(ScrollView scroll, Vector2 offset)
        {
            scroll.schedule.Execute(() =>
            {
                if (scroll.panel != null) { scroll.scrollOffset = offset; }
            }).StartingIn(1);
        }

        private void BuildSubmenuTree(ScrollView scroll)
        {
            scroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            var tree = new VisualElement { name = "expressionMenuTree" };
            tree.AddToClassList("ee4v-expression-menu__tree");
            scroll.Add(tree);
            _treeScroll = scroll;
            RegisterTreeDropping(scroll);
            var selection = _selected >= 0 ? _path.Concat(new[] { _selected }).ToArray() : _path.ToArray();
            var selectedPath = string.Join("/", selection);
            var revealSelection = _treeSelectionPath != selectedPath;
            if (revealSelection)
            {
                for (var depth = 0; depth < selection.Length; depth++)
                    _expandedMenuPaths.Add(string.Join("/", selection.Take(depth)));
                _treeSelectionPath = selectedPath;
            }
            void Render(bool revealSelection)
            {
                var offset = scroll.scrollOffset;
                tree.Clear();
                _treeNodes.Clear();
                _treeAddRows.Clear();
                VisualElement selectedRow = null;
                var ancestors = new HashSet<MenuPage>();
                void AddNode(MenuPage page, MenuPage parentPage, MenuEntry entry, string label, Texture2D icon,
                    int[] path, string tooltip, VisualElement parent)
                {
                    var key = string.Join("/", path);
                    var current = selection.SequenceEqual(path);
                    var canExpand = page != null && !ancestors.Contains(page);
                    var expanded = canExpand && _expandedMenuPaths.Contains(key);
                    var row = new VisualElement { userData = path };
                    row.AddToClassList("ee4v-expression-menu__tree-row");
                    row.EnableInClassList("ee4v-expression-menu__tree-row--current", current);
                    parent.Add(row);
                    var node = new MenuTreeNode { Row = row, Page = page, ParentPage = parentPage,
                        Entry = entry, Path = path, Name = tooltip, CanExpand = canExpand };
                    var canEdit = entry != null ? CanInteract(entry) : CanEditMenuRoot(page);
                    _treeNodes.Add(node);
                    if (current) selectedRow = row;
                    if (canExpand)
                    {
                        var toggle = new UiButton("", () =>
                        {
                            if (_suppressTreeClick) return;
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
                        if (_suppressTreeClick) return;
                        if (page != null) NavigateMenu(path);
                        else
                        {
                            _path.Clear();
                            _path.AddRange(path.Take(path.Length - 1));
                            _selected = path[path.Length - 1];
                            _offset = parentPage.Controls.Count > 8 ? _selected / 7 * 7 : 0;
                            _error = null;
                            RefreshNavigation();
                        }
                    }
                    var button = new UiButton(label, Open, tooltip: tooltip,
                        variant: UiButtonVariant.Ghost);
                    button.AddToClassList("ee4v-expression-menu__tree-link");
                    button.EnableInClassList("ee4v-expression-menu__tree-link--current", current);
                    button.RegisterCallback<FocusInEvent>(_ => row.AddToClassList("ee4v-expression-menu__tree-row--focused"));
                    button.RegisterCallback<FocusOutEvent>(_ => row.RemoveFromClassList("ee4v-expression-menu__tree-row--focused"));
                    button.SetContentAlignment(Justify.FlexStart);
                    var image = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                    image.AddToClassList("ee4v-expression-menu__tree-icon");
                    image.EnableInClassList("ee4v-expression-menu__tree-icon--root", path.Length == 0);
                    button.Content.Insert(0, image);
                    row.Add(button);
                    if (entry != null)
                        button.Content.Add(UiTextFactory.Create(ControlTypeName(entry), UiClassNames.SecondaryText));
                    if (!canEdit)
                    {
                        var badge = new Badge(T("readOnlyBadge")) { pickingMode = PickingMode.Ignore };
                        badge.AddToClassList("ee4v-expression-menu__tree-read-only");
                        button.Content.Add(badge);
                    }
                    if (entry != null)
                    {
                        var actions = new VisualElement();
                        actions.AddToClassList("ee4v-expression-menu__quick-actions");
                        var generated = ExpressionMenuModel.IsGenerated(entry);
                        var remove = new UiButton(string.Empty, () =>
                            { if (!_suppressTreeClick) ConfirmRemove(entry, path.Take(path.Length - 1).ToArray()); },
                            tooltip: T(generated ? "deleteGimmick" : "excludeItem"),
                            icon: FluentUiIcons.CreateState("dismiss.png", 16), variant: UiButtonVariant.Ghost);
                        remove.AddToClassList("ee4v-expression-menu__tree-remove");
                        remove.SetEnabled(generated ? canEdit : ExpressionMenuModel.CanExclude(_context, entry));
                        actions.Add(remove);
                        row.Add(actions);
                        button.AddManipulator(new MenuTreeDragManipulator(this, node));
                        row.AddManipulator(new MenuTreeDragManipulator(this, node));
                    }
                    if (!expanded) return;
                    var children = new VisualElement();
                    node.Children = children;
                    children.AddToClassList("ee4v-expression-menu__tree-children");
                    parent.Add(children);
                    ancestors.Add(page);
                    for (var index = 0; index < page.Controls.Count; index++)
                    {
                        var child = page.Controls[index];
                        AddNode(child.Submenu, page, child, child.Control.name, ExpressionMenuModel.DisplayIcon(child.Control.icon),
                            path.Concat(new[] { index }).ToArray(), tooltip + " / " + child.Control.name, children);
                    }
                    var addRow = new VisualElement();
                    addRow.AddToClassList("ee4v-expression-menu__tree-row");
                    addRow.AddToClassList("ee4v-expression-menu__tree-add-row");
                    var addSpacer = new VisualElement();
                    addSpacer.AddToClassList("ee4v-expression-menu__tree-spacer");
                    addRow.Add(addSpacer);
                    var add = new UiButton(T("addItem"), () =>
                        { if (!_suppressTreeClick) ShowAddMenu(page, path); }, tooltip: tooltip + " / " + T("add"),
                        icon: FluentUiIcons.CreateState("add.png", 28), variant: UiButtonVariant.Ghost);
                    add.AddToClassList("ee4v-expression-menu__tree-link");
                    add.AddToClassList("ee4v-expression-menu__tree-add");
                    add.SetContentAlignment(Justify.FlexStart);
                    add.SetEnabled(CanAddToPage(page));
                    addRow.Add(add);
                    children.Add(addRow);
                    node.AddRow = addRow;
                    _treeAddRows.Add(addRow);
                    ancestors.Remove(page);
                }
                var root = _source == 0 ? _root : _sources[_source - 1];
                var rootName = _source == 0 ? T("root") : root.Name;
                AddNode(root, null, null, rootName, FluentUiIcons.LoadTexture("folder.png", 20), Array.Empty<int>(), rootName, tree);
                scroll.scrollOffset = offset;
                if (revealSelection && selectedRow != null)
                    scroll.schedule.Execute(() => { if (selectedRow.panel != null) scroll.ScrollTo(selectedRow); });
            }
            _renderTree = () => Render(false);
            Render(revealSelection);
        }

        private static string ControlTypeName(MenuEntry entry) =>
            entry.Control.type == VRCExpressionsMenu.Control.ControlType.RadialPuppet ? "0–100" :
            entry.Control.type == VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet ? "2 Axis" :
            entry.Control.type == VRCExpressionsMenu.Control.ControlType.FourAxisPuppet ? "4 Axis" : entry.Control.type.ToString();

        private void NavigateMenu(int[] path)
        {
            _path.Clear();
            _path.AddRange(path);
            _offset = 0;
            _selected = -1;
            _error = null;
            RefreshNavigation();
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
                if (!CanInteract(_submenuEntry))
                    AddEditableCopyAction(details, _submenuEntry.Owner as VRCExpressionsMenu);
                else if (!CanEditMenuRoot(_page)) AddEditableCopyAction(details, _page.Asset);
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
            if (!CanEditMenuRoot(_page)) AddEditableCopyAction(details, _page.Asset);
            BuildRegistrationActions(host);
        }

        private void BuildExcludedItems(VisualElement host)
        {
            var entries = AvatarMenuExclusionUtility.Entries(_context.Root);
            if (entries.Count == 0) return;
            var section = new VisualElement();
            section.AddToClassList("ee4v-expression-menu__exclusions");
            section.Add(new SectionHeader(string.Format(T("excludedItems"), entries.Count)));
            foreach (var entry in entries)
            {
                var row = new VisualElement();
                row.AddToClassList("ee4v-expression-menu__exclusion-row");
                var label = UiTextFactory.Create(entry.DisplayName, UiClassNames.SecondaryText);
                label.tooltip = entry.Source != null ? entry.Source.name : T("missingExclusionSource");
                label.AddToClassList("ee4v-expression-menu__exclusion-name");
                row.Add(label);
                if (entry.Source is ModularAvatarMenuInstaller)
                    row.Add(UiTextFactory.Create(T("registrationScope"), UiClassNames.SecondaryText));
                var restore = new UiButton(T("restoreItem"), () => Run(() =>
                {
                    ExpressionMenuModel.Restore(_context, entry.Id);
                    NotifyChanged();
                }), variant: UiButtonVariant.Ghost);
                restore.SetEnabled(CanManageExclusions());
                row.Add(restore);
                section.Add(row);
                if (entry.Source != null && !AvatarMenuExclusionUtility.IsCurrent(entry))
                    section.Add(UiTextFactory.CreateHelpBox(T("exclusionSourceChanged"), HelpBoxMessageType.Warning));
            }
            host.Add(section);
        }

        private bool CanManageExclusions() => _context.Edits.CanEditPrefab() &&
            ExpressionMenuModel.CanWrite(_context.Root) && ExpressionMenuModel.CanWrite(_context.PrefabAsset);

        private void BuildRegistrationActions(VisualElement host)
        {
            var entries = AvatarMenuExclusionUtility.Entries(_context.Root);
            var installers = _context.Root.GetComponentsInChildren<ModularAvatarMenuInstaller>(true)
                .Where(installer => !ExpressionMenuInstaller.IsGeneratedPrefab(installer.gameObject) &&
                    !entries.Any(entry => entry.Source == installer) &&
                    (_source == 0 || installer.menuToAppend == _page.Asset)).ToArray();
            if (installers.Length == 0) return;
            host.Add(new SectionHeader(T("menuRegistrations")));
            foreach (var installer in installers)
            {
                var row = new VisualElement();
                row.AddToClassList("ee4v-expression-menu__exclusion-row");
                var label = UiTextFactory.Create(installer.gameObject.name, UiClassNames.SecondaryText);
                label.AddToClassList("ee4v-expression-menu__exclusion-name");
                row.Add(label);
                var exclude = new UiButton(T("excludeRegistration"), () =>
                {
                    var overlay = OpenOperationOverlay(new MessagePanelState(T("excludeItemTitle"),
                        string.Format(T("excludeRegistrationMessage"), installer.gameObject.name), MessageSeverity.Warning));
                    if (overlay == null) return;
                    overlay.Notification.Actions.Add(new UiButton(T("excludeRegistration"), () =>
                    {
                        overlay.Close();
                        Run(() => { ExpressionMenuModel.ExcludeInstaller(_context, installer); NotifyChanged(); });
                    }));
                    overlay.Notification.Actions.Add(new UiButton(T("cancel"), overlay.Close));
                }, variant: UiButtonVariant.Ghost);
                exclude.SetEnabled(CanManageExclusions());
                row.Add(exclude);
                host.Add(row);
            }
        }


        private bool CanCreateEditableCopy(VRCExpressionsMenu menu)
        {
            if (menu == null || !_context.Edits.CanEditPrefab() ||
                EditorApplication.isPlayingOrWillChangePlaymode) return false;
            if (!_copyableMenus.TryGetValue(menu, out var canCopy))
            {
                if (_editableCopyCheck == null) _editableCopyCheck = ExpressionMenuModel.CreateEditableCopyCheck(_context);
                canCopy = _editableCopyCheck(menu);
                _copyableMenus.Add(menu, canCopy);
            }
            return canCopy;
        }

        private void AddEditableCopyAction(VisualElement host, VRCExpressionsMenu menu, Action copied = null)
        {
            if (!CanCreateEditableCopy(menu)) return;
            host.Add(new UiButton(T("makeEditable"), () => CreateEditableCopy(menu, copied),
                tooltip: T("makeEditableHint")) { name = "expressionMenuMakeEditable" });
        }

        private void CreateEditableCopy(VRCExpressionsMenu menu, Action copied)
        {
            if (_closeCopyOverlay != null || !CanCreateEditableCopy(menu) || panel == null) return;
            var avatar = _context.Root;
            var host = (VisualElement)this;
            while (host.parent != null && host.parent != panel.visualTree) host = host.parent;
            var previousFocus = panel.focusController.focusedElement as VisualElement;
            var background = host.Children().Select(element => (Element: element, Enabled: element.enabledSelf)).ToArray();
            var loading = new VisualElement { name = "expressionMenuCopyProcessing", focusable = true, tabIndex = -1 };
            UiComposition.Prepare(loading, "Editor/Feature/Avatar/ExpressionMenu/expression-menu.uss");
            loading.AddToClassList("ee4v-expression-menu__processing");
            loading.Add(new StatusOverlay(new StatusOverlayState(true, T("copyingMenu"))));
            loading.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            loading.RegisterCallback<WheelEvent>(evt => evt.StopPropagation());
            loading.RegisterCallback<KeyDownEvent>(evt => { evt.PreventDefault(); evt.StopPropagation(); });
            IVisualElementScheduledItem pending = null;
            var closed = false;
            void Close()
            {
                if (closed) return;
                closed = true;
                pending?.Pause();
                foreach (var item in background) item.Element.SetEnabled(item.Enabled);
                loading.RemoveFromHierarchy();
                _closeCopyOverlay = null;
                if (previousFocus?.panel != null && previousFocus.enabledInHierarchy) previousFocus.Focus();
            }
            _closeCopyOverlay = Close;
            foreach (var item in background) item.Element.SetEnabled(false);
            host.Add(loading);
            loading.Focus();
            pending = loading.schedule.Execute(() =>
            {
                try
                {
                    if (panel == null || _context.Root != avatar) return;
                    Run(() =>
                    {
                        var replacements = ExpressionMenuModel.CreateEditableCopy(_context, menu);
                        if (replacements == null) return;
                        _replacedMenus = replacements;
                        copied?.Invoke();
                        NotifyChanged();
                    });
                }
                finally { Close(); }
            }).StartingIn(100);
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

        private void ConfirmRemove(MenuEntry entry, int[] parentPath)
        {
            var generated = ExpressionMenuModel.IsGenerated(entry);
            if (generated ? !CanInteract(entry) : !ExpressionMenuModel.CanExclude(_context, entry)) return;
            void Remove()
            {
                if (generated)
                {
                    if (!CanInteract(entry)) throw new InvalidOperationException(T("readOnly"));
                    ExpressionMenuModel.RemoveGenerated(entry);
                }
                else ExpressionMenuModel.Exclude(_context, entry);
                _path.Clear();
                _path.AddRange(parentPath);
                _expandedMenuPaths.Add(string.Join("/", parentPath));
                _selected = -1;
                _offset = 0;
                NotifyChanged();
            }
            var hasContents = entry.Submenu != null && entry.Submenu.Controls.Count > 0;
            var overlay = OpenOperationOverlay(new MessagePanelState(T(generated ? "deleteGimmickTitle" : "excludeItemTitle"),
                string.Format(T(generated ? "deleteGimmickMessage" : "excludeItemMessage"), entry.Control.name), MessageSeverity.Warning));
            if (overlay == null) return;
            void Confirm()
            {
                overlay.Close();
                Run(Remove);
            }
            if (generated) overlay.AddDiscardAction(T(hasContents ? "deleteWithContents" : "remove"), Confirm);
            else overlay.Notification.Actions.Add(new UiButton(T("excludeItem"), Confirm));
            overlay.Notification.Actions.Add(new UiButton(T("cancel"), overlay.Close));
        }


        private void BuildIdentityFields(MenuEntry entry, VisualElement iconHost, VisualElement details, string previewClass,
            bool? editable = null)
        {
            var canEdit = editable ?? CanInteract(entry);
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
            icon.SetEnabled(canEdit);
            icon.RegisterValueChangedCallback(evt => Run(() =>
            {
                try { Change(() => source.Control.icon = evt.newValue as Texture2D, () => draft.icon = evt.newValue as Texture2D); }
                catch { icon.SetValueWithoutNotify(evt.previousValue); throw; }
            }));
            iconHost.Add(icon);
            var name = new InputField(new InputFieldState(entry.Control.name)) { IsDelayed = true };
            name.tooltip = T("name");
            name.SetEnabled(canEdit);
            name.ValueChanged += value => Run(() => Change(() => source.label = value, () => draft.name = value));
            details.Add(name);
        }

        private bool CanMoveControl(MenuPage page, int from, int to) => from >= 0 && to >= 0 &&
            from < page.Controls.Count && to < page.Controls.Count &&
            from != to && CanInteract(page.Controls[from]) &&
            Enumerable.Range(Math.Min(from, to), Math.Abs(to - from) + 1).All(index => index == from ||
                CanInteract(page.Controls[index]) && ExpressionMenuModel.CanMove(page.Controls[from], page.Controls[index]));


        private sealed class MenuTreeNode
        {
            internal VisualElement Row;
            internal VisualElement Children;
            internal VisualElement AddRow;
            internal bool CanExpand;
            internal MenuPage Page;
            internal MenuPage ParentPage;
            internal MenuEntry Entry;
            internal int[] Path;
            internal string Name;
        }

        private sealed class MenuTreeDrop
        {
            internal MenuTreeNode Node;
            internal MenuPage Destination;
            internal int Index = -1;
            internal bool After;
            internal bool AtEnd;
        }

        private static bool SamePage(MenuPage first, MenuPage second) => first != null && second != null &&
            (first == second || first.Asset != null && first.Asset == second.Asset && first.ChildRoot == second.ChildRoot ||
                first.ChildRoot != null && first.ChildRoot == second.ChildRoot);

        private bool CanDropInto(MenuEntry entry, MenuPage source, MenuTreeNode node, int index = -1) => node.Page != null &&
            !SamePage(source, node.Page) && CanInteract(entry) && (node.Entry == null || CanInteract(node.Entry)) &&
            ExpressionMenuModel.CanRelocate(_context, entry, node.Page, index);

        private void ApplyTreeDrop(MenuEntry entry, MenuPage source, MenuTreeDrop drop) => Run(() =>
        {
            var previousExpanded = _expandedMenuPaths.ToArray();
            var previousPath = _path.ToArray();
            var previousSelection = _selected;
            var previousOffset = _offset;
            var previousTreeSelection = _treeSelectionPath;
            _applyingTreeDrop = true;
            try
            {
                var from = source.Controls.IndexOf(entry);
                if (from < 0) throw new InvalidOperationException(T("cannotReorder"));
                var destination = drop.Index < 0 ? drop.Node.Page : drop.Destination ?? source;
                var destinationPath = drop.Index < 0 || drop.AtEnd ? drop.Node.Path :
                    drop.Node.Path.Take(drop.Node.Path.Length - 1).ToArray();
                var index = drop.Index < 0 ? destination.Controls.Count : drop.Index;
                var expandedPaths = TreeExpansionAfterMove(source, from, destination, index, destinationPath);
                if (!SamePage(source, destination))
                {
                    var parent = _treeNodes.FirstOrDefault(node => node.Path.SequenceEqual(destinationPath));
                    if (parent == null || !CanDropInto(entry, source, parent, index))
                        throw new InvalidOperationException(T("noMoveDestination"));
                    ExpressionMenuModel.Relocate(_context, entry, destination, index);
                }
                else
                {
                    if (!CanMoveControl(source, from, index)) throw new InvalidOperationException(T("cannotReorder"));
                    ExpressionMenuModel.Move(entry, source.Controls[index]);
                }
                _pendingTreeDestination = new MenuTreeNode { Page = destination, Path = destinationPath };
                _expandedMenuPaths.Clear();
                _expandedMenuPaths.UnionWith(expandedPaths);
                NotifyChanged();
            }
            catch
            {
                _pendingTreeDestination = null;
                _expandedMenuPaths.Clear();
                _expandedMenuPaths.UnionWith(previousExpanded);
                _path.Clear();
                _path.AddRange(previousPath);
                _selected = previousSelection;
                _offset = previousOffset;
                _treeSelectionPath = previousTreeSelection;
                throw;
            }
            finally { _applyingTreeDrop = false; }
        });

        private HashSet<string> TreeExpansionAfterMove(MenuPage source, int from, MenuPage destination, int to,
            int[] destinationPath)
        {
            var root = _source == 0 ? _root : _sources[_source - 1];
            var reorder = SamePage(source, destination);
            int[] Remap(int[] path)
            {
                var page = root;
                var result = new int[path.Length];
                for (var depth = 0; depth < path.Length; depth++)
                {
                    var index = path[depth];
                    if (page == null || index < 0 || index >= page.Controls.Count) return null;
                    var mapped = index;
                    if (SamePage(page, source))
                    {
                        if (index == from)
                        {
                            if (!reorder)
                            {
                                var parent = Remap(destinationPath);
                                return parent == null ? null : parent.Concat(new[] { to }).Concat(path.Skip(depth + 1)).ToArray();
                            }
                            mapped = to;
                        }
                        else if (index > from && (!reorder || index <= to)) mapped--;
                        else if (reorder && index < from && index >= to) mapped++;
                    }
                    if (!reorder && SamePage(page, destination) && index >= to) mapped++;
                    result[depth] = mapped;
                    page = page.Controls[index].Submenu;
                }
                return result;
            }
            var expanded = new HashSet<string>();
            foreach (var key in _expandedMenuPaths)
            {
                var path = key.Length == 0 ? Array.Empty<int>() : key.Split('/').Select(int.Parse).ToArray();
                var mapped = Remap(path);
                if (mapped != null) expanded.Add(string.Join("/", mapped));
            }
            return expanded;
        }

        private sealed class MenuTreeDragPayload
        {
            internal ExpressionMenuView View;
            internal MenuTreeNode Node;
        }

        private void ClearTreeDragTarget()
        {
            _treeDragAutoScroll?.Pause();
            _treeDragAutoScroll = null;
            _treeDragPreview?.Dispose();
            _treeDragPreview = null;
        }

        private void CancelTreeDrag()
        {
            ClearTreeDragTarget();
            if (_treeDrag != null && ReferenceEquals(DragAndDrop.GetGenericData(MenuTreeDragDataKey), _treeDrag))
                DragAndDrop.SetGenericData(MenuTreeDragDataKey, null);
            _treeDrag = null;
        }

        private void RegisterTreeDropping(ScrollView scroll)
        {
            bool TryGetPayload(out MenuTreeDragPayload payload)
            {
                payload = DragAndDrop.GetGenericData(MenuTreeDragDataKey) as MenuTreeDragPayload;
                return payload != null && ReferenceEquals(payload.View, this) &&
                    ReferenceEquals(payload, _treeDrag) && CanInteract(payload.Node.Entry);
            }

            void UpdateTarget(MenuTreeDragPayload payload, Vector2 position)
            {
                _treeDragPosition = position;
                if (_treeDragPreview == null)
                {
                    _treeDragPreview = new MenuTreeDragPreview(this, payload.Node.Entry, payload.Node.ParentPage);
                    _treeDragAutoScroll = scroll.schedule.Execute(() =>
                    {
                        if (_treeDragPreview != null && _treeDragPreview.AutoScroll(_treeDragPosition))
                            _treeDragPreview.Update(_treeDragPosition);
                    }).Every(16);
                }
                _treeDragPreview.Update(position);
            }

            scroll.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (!TryGetPayload(out var payload)) { ClearTreeDragTarget(); return; }
                UpdateTarget(payload, evt.mousePosition);
                evt.StopPropagation();
            }, TrickleDown.TrickleDown);
            scroll.RegisterCallback<DragLeaveEvent>(evt =>
            {
                if (ReferenceEquals(evt.target, scroll)) ClearTreeDragTarget();
            });
            scroll.RegisterCallback<DragExitedEvent>(_ =>
            {
                var payload = _treeDrag;
                ClearTreeDragTarget();
                if (payload == null) return;
                scroll.schedule.Execute(() =>
                {
                    if (ReferenceEquals(_treeDrag, payload) && DragAndDrop.visualMode == DragAndDropVisualMode.None)
                        CancelTreeDrag();
                }).StartingIn(1);
            });
            scroll.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                if (ReferenceEquals(evt.target, scroll)) CancelTreeDrag();
            });
            scroll.RegisterCallback<DragPerformEvent>(evt =>
            {
                if (!TryGetPayload(out var payload)) { ClearTreeDragTarget(); return; }
                UpdateTarget(payload, evt.mousePosition);
                var drop = _treeDragPreview.Drop;
                if (drop != null)
                {
                    DragAndDrop.AcceptDrag();
                    CancelTreeDrag();
                    ApplyTreeDrop(payload.Node.Entry, payload.Node.ParentPage, drop);
                }
                else CancelTreeDrag();
                evt.StopImmediatePropagation();
                evt.PreventDefault();
            }, TrickleDown.TrickleDown);
        }

        private sealed class MenuTreeDragPreview
        {
            private readonly ExpressionMenuView _view;
            private readonly MenuEntry _entry;
            private readonly MenuPage _source;
            private readonly VisualElement _indicator;
            private MenuTreeNode _highlight;
            private string _highlightClass;
            private MenuTreeNode _expandTarget;
            private IVisualElementScheduledItem _hoverExpand;
            private readonly Dictionary<MenuTreeNode, bool> _dropChecks = new Dictionary<MenuTreeNode, bool>();
            private readonly Dictionary<int, bool> _moveChecks = new Dictionary<int, bool>();
            private readonly Dictionary<(MenuTreeNode, int), bool> _insertChecks = new Dictionary<(MenuTreeNode, int), bool>();
            internal MenuTreeDrop Drop { get; private set; }

            internal MenuTreeDragPreview(ExpressionMenuView view, MenuEntry entry, MenuPage source)
            {
                _view = view;
                _entry = entry;
                _source = source;
                _indicator = new VisualElement { pickingMode = PickingMode.Ignore };
                _indicator.AddToClassList("ee4v-expression-menu__tree-insertion-line");
                view._treeScroll.contentViewport.Add(_indicator);
            }

            internal bool Update(Vector2 position)
            {
                if (_view._menuDirty) { _dropChecks.Clear(); _moveChecks.Clear(); _insertChecks.Clear(); }
                Drop = null;
                _indicator.style.display = DisplayStyle.None;
                var scroll = _view._treeScroll;
                var outer = scroll?.GetFirstAncestorOfType<ScrollView>();
                var within = scroll != null && scroll.contentViewport.worldBound.Contains(position) &&
                    (outer == null || outer.contentViewport.worldBound.Contains(position));
                var tail = within ? _view._treeNodes.FirstOrDefault(node =>
                    node.AddRow != null && position.x >= node.AddRow.worldBound.xMin &&
                    position.x <= node.AddRow.worldBound.xMax && position.y >= TailTop(node) &&
                    position.y <= node.AddRow.worldBound.center.y) : null;
                MenuTreeNode hit = null;
                if (tail != null)
                {
                    if (SamePage(_source, tail.Page))
                    {
                        var index = _source.Controls.Count - 1;
                        if (!_moveChecks.TryGetValue(index, out var canMove))
                        {
                            canMove = _view.CanMoveControl(_source, _source.Controls.IndexOf(_entry), index);
                            _moveChecks.Add(index, canMove);
                        }
                        if (canMove) Drop = new MenuTreeDrop { Node = tail, Index = index, AtEnd = true };
                    }
                    else
                    {
                        if (!_dropChecks.TryGetValue(tail, out var canDrop))
                        {
                            canDrop = _view.CanDropInto(_entry, _source, tail);
                            _dropChecks.Add(tail, canDrop);
                        }
                        if (canDrop) Drop = new MenuTreeDrop { Node = tail, AtEnd = true };
                    }
                    if (Drop != null) ShowTailInsertion(tail);
                }
                else if (within && _view._treeNodes.Count > 0 &&
                    !_view._treeAddRows.Any(row => row.worldBound.Contains(position)))
                {
                    var root = _view._treeNodes[0];
                    var bottom = root.Children != null ? root.Children.worldBound.yMax : root.Row.worldBound.yMax;
                    if (position.y >= root.Row.worldBound.yMin && position.y <= bottom)
                    {
                        hit = _view._treeNodes.FirstOrDefault(node => node.Row.worldBound.Contains(position)) ??
                            _view._treeNodes.Where(node => position.x >= node.Row.worldBound.xMin && position.x <= node.Row.worldBound.xMax)
                                .OrderBy(node => Mathf.Abs(position.y - Mathf.Clamp(position.y, node.Row.worldBound.yMin, node.Row.worldBound.yMax)))
                                .FirstOrDefault();
                    }
                }
                if (hit != null)
                {
                    var bounds = hit.Row.worldBound;
                    var fraction = (position.y - bounds.yMin) / bounds.height;
                    var sameParent = SamePage(_source, hit.ParentPage);
                    var inside = hit.Page != null && bounds.Contains(position);
                    if (inside)
                    {
                        if (!_dropChecks.TryGetValue(hit, out var canDrop))
                        {
                            canDrop = _view.CanDropInto(_entry, _source, hit);
                            _dropChecks.Add(hit, canDrop);
                        }
                        if (canDrop) Drop = new MenuTreeDrop { Node = hit };
                    }
                    else if (hit.Entry != null)
                    {
                        var sourceIndex = _source.Controls.IndexOf(_entry);
                        var after = fraction >= 0.5f;
                        var boundary = hit.Path[hit.Path.Length - 1] + (after ? 1 : 0);
                        var index = sameParent && boundary > sourceIndex ? boundary - 1 : boundary;
                        bool canMove;
                        if (sameParent)
                        {
                            if (!_moveChecks.TryGetValue(index, out canMove))
                            {
                                canMove = _view.CanMoveControl(_source, sourceIndex, index);
                                _moveChecks.Add(index, canMove);
                            }
                        }
                        else
                        {
                            var key = (hit, boundary);
                            if (!_insertChecks.TryGetValue(key, out canMove))
                            {
                                var parentPath = hit.Path.Take(hit.Path.Length - 1).ToArray();
                                var parent = _view._treeNodes.FirstOrDefault(node => node.Path.SequenceEqual(parentPath));
                                canMove = parent != null && _view.CanDropInto(_entry, _source, parent, boundary);
                                _insertChecks.Add(key, canMove);
                            }
                        }
                        if (canMove)
                        {
                            Drop = new MenuTreeDrop { Node = hit, Destination = hit.ParentPage, Index = index, After = after };
                            ShowInsertion(hit, boundary);
                        }
                    }
                }
                var highlightClass = hit == null || Drop != null && Drop.Index >= 0 ? null :
                    Drop == null ? "ee4v-expression-menu__tree-drop--invalid" : "ee4v-expression-menu__tree-drop--inside";
                if (_highlight != hit || _highlightClass != highlightClass)
                {
                    ClearHighlight();
                    _highlight = hit;
                    _highlightClass = highlightClass;
                    if (hit != null && highlightClass != null) hit.Row.AddToClassList(highlightClass);
                }
                UpdateHoverExpansion(Drop != null && Drop.Index < 0 && !Drop.AtEnd ? hit : null);
                DragAndDrop.visualMode = Drop != null ? DragAndDropVisualMode.Move : DragAndDropVisualMode.Rejected;
                return within;
            }

            private void UpdateHoverExpansion(MenuTreeNode node)
            {
                if (node?.Entry == null || !node.CanExpand || node.Children != null) node = null;
                if (ReferenceEquals(_expandTarget, node)) return;
                _hoverExpand?.Pause();
                _hoverExpand = null;
                _expandTarget = node;
                if (node == null) return;
                _hoverExpand = _view._treeScroll.schedule.Execute(() =>
                {
                    if (_view._treeDrag == null || !ReferenceEquals(Drop?.Node, node) || Drop.Index >= 0) return;
                    _expandTarget = null;
                    _hoverExpand = null;
                    _view._expandedMenuPaths.Add(string.Join("/", node.Path));
                    _view._renderTree?.Invoke();
                    _dropChecks.Clear();
                    _moveChecks.Clear();
                    _insertChecks.Clear();
                    Update(_view._treeDragPosition);
                }).StartingIn(600);
            }

            private void ShowInsertion(MenuTreeNode hit, int boundary)
            {
                var parentPath = hit.Path.Take(hit.Path.Length - 1).ToArray();
                var destination = hit.ParentPage;
                if (boundary == destination.Controls.Count)
                {
                    var parent = _view._treeNodes.FirstOrDefault(node => node.AddRow != null &&
                        SamePage(node.Page, destination) && node.Path.SequenceEqual(parentPath));
                    if (parent != null) { ShowTailInsertion(parent); return; }
                }
                var siblings = _view._treeNodes.Where(node => node.Entry != null &&
                    SamePage(node.ParentPage, destination) && node.Path.Take(node.Path.Length - 1).SequenceEqual(parentPath)).ToArray();
                var previous = siblings.FirstOrDefault(node => node.Path[node.Path.Length - 1] == boundary - 1);
                var next = siblings.FirstOrDefault(node => node.Path[node.Path.Length - 1] == boundary);
                var previousBottom = previous?.Children != null ? previous.Children.worldBound.yMax : previous?.Row.worldBound.yMax;
                var y = previous == null ? next.Row.worldBound.yMin : next == null ? previousBottom.Value :
                    (previousBottom.Value + next.Row.worldBound.yMin) * 0.5f;
                ShowInsertionLine(hit.Row.worldBound, y);
            }

            private static float TailTop(MenuTreeNode node) => node.Children.childCount > 1
                ? node.Children.ElementAt(node.Children.childCount - 2).worldBound.yMax : node.Row.worldBound.yMax;

            private void ShowTailInsertion(MenuTreeNode node) =>
                ShowInsertionLine(node.AddRow.worldBound, (TailTop(node) + node.AddRow.worldBound.yMin) * 0.5f);

            private void ShowInsertionLine(Rect bounds, float y)
            {
                var point = _view._treeScroll.contentViewport.WorldToLocal(new Vector2(bounds.xMin, y));
                _indicator.style.left = point.x;
                _indicator.style.top = Mathf.Round(point.y);
                _indicator.style.width = bounds.width;
                _indicator.style.display = DisplayStyle.Flex;
            }

            internal bool AutoScroll(Vector2 position)
            {
                var scroll = _view._treeScroll;
                if (scroll == null || !scroll.contentViewport.worldBound.Contains(position)) return false;
                var bounds = scroll.contentViewport.worldBound;
                var delta = position.y < bounds.yMin + 24f ? -7f : position.y > bounds.yMax - 24f ? 7f : 0f;
                var offset = scroll.scrollOffset;
                var maxY = Mathf.Max(0f, scroll.contentContainer.layout.height - scroll.contentViewport.layout.height);
                var nextY = Mathf.Clamp(offset.y + delta, 0f, maxY);
                if (!Mathf.Approximately(offset.y, nextY)) scroll.scrollOffset = new Vector2(offset.x, nextY);
                return true;
            }

            private void ClearHighlight()
            {
                if (_highlight != null && _highlightClass != null) _highlight.Row.RemoveFromClassList(_highlightClass);
                _highlight = null;
                _highlightClass = null;
            }

            internal void Dispose()
            {
                _hoverExpand?.Pause();
                _hoverExpand = null;
                _expandTarget = null;
                ClearHighlight();
                _indicator.RemoveFromHierarchy();
                Drop = null;
            }
        }

        private sealed class MenuTreeDragManipulator : PointerManipulator
        {
            private readonly ExpressionMenuView _view;
            private readonly MenuTreeNode _node;
            private Vector2 _start;
            private int _pointerId = -1;
            private bool _ready;
            private bool _dragging;

            internal MenuTreeDragManipulator(ExpressionMenuView view, MenuTreeNode node)
            { _view = view; _node = node; }

            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
                target.RegisterCallback<PointerMoveEvent>(OnMove, TrickleDown.TrickleDown);
                target.RegisterCallback<PointerUpEvent>(OnUp, TrickleDown.TrickleDown);
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
                target.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
                target.UnregisterCallback<KeyDownEvent>(OnKey);
                target.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
            }

            private void OnDown(PointerDownEvent evt)
            {
                var pressedElement = evt.target as VisualElement;
                while (pressedElement != null && pressedElement != target && !(pressedElement is UiButton))
                    pressedElement = pressedElement.parent;
                _ready = evt.button == 0 && ReferenceEquals(pressedElement, target) &&
                    target.enabledInHierarchy && _view.CanInteract(_node.Entry);
                _dragging = false;
                if (!_ready) return;
                if (_view._treeDrag != null) _view.CancelTreeDrag();
                _start = evt.position;
                if (!(target is UiButton))
                {
                    _pointerId = evt.pointerId;
                    target.CapturePointer(evt.pointerId);
                }
            }

            private void OnMove(PointerMoveEvent evt)
            {
                if (!_ready || (evt.pressedButtons & 1) == 0 || ((Vector2)evt.position - _start).sqrMagnitude < 36f) return;
                _ready = false;
                _dragging = true;
                _view.CancelTreeDrag();
                if (target.HasPointerCapture(evt.pointerId)) target.ReleasePointer(evt.pointerId);
                var payload = new MenuTreeDragPayload { View = _view, Node = _node };
                _view._treeDrag = payload;
                _view._suppressTreeClick = true;
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.SetGenericData(MenuTreeDragDataKey, payload);
                DragAndDrop.StartDrag(_node.Entry.Control.name);
                evt.StopImmediatePropagation();
            }

            private void OnUp(PointerUpEvent evt)
            {
                _ready = false;
                if (_pointerId == evt.pointerId && target.HasPointerCapture(evt.pointerId)) target.ReleasePointer(evt.pointerId);
                _pointerId = -1;
                if (!_dragging) return;
                _dragging = false;
                evt.StopImmediatePropagation();
            }

            private void OnCaptureOut(PointerCaptureOutEvent evt)
            {
                if (!ReferenceEquals(evt.target, target)) return;
                _ready = false;
                _pointerId = -1;
            }
            private void OnDetach(DetachFromPanelEvent evt) { if (ReferenceEquals(evt.target, target)) Cancel(); }
            private void Cancel()
            {
                _ready = false;
                _dragging = false;
                if (_pointerId >= 0 && target.HasPointerCapture(_pointerId)) target.ReleasePointer(_pointerId);
                _pointerId = -1;
            }

            private void OnKey(KeyDownEvent evt)
            {
                if (evt.keyCode == KeyCode.Escape && (_ready || _view._treeDrag != null))
                { Cancel(); _view.CancelTreeDrag(); evt.StopPropagation(); return; }
            }
        }


        private void BuildRadial(VisualElement host)
        {
            var ring = new VisualElement();
            ring.AddToClassList("ee4v-expression-menu__ring");
            var labels = new VisualElement { pickingMode = PickingMode.Ignore };
            labels.AddToClassList("ee4v-expression-menu__labels");
            host.Add(ring);
            var paged = _page.Controls.Count > 8;
            var count = Math.Min(paged ? 7 : 8, _page.Controls.Count - _offset);
            var hasNext = paged && _offset + count < _page.Controls.Count;
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
                    RefreshNavigation();
                }, variant: UiButtonVariant.Ghost, labelTypographyClassName: UiClassNames.SecondaryText);
                PlaceSlot(ring, labels, back, 0, slices, Resources.Load<Texture2D>("Vrc3/BSX_GM_Back"));
            }
            for (var slot = 0; slot < count; slot++)
            {
                var index = _offset + slot;
                var entry = _page.Controls[index];
                var button = new UiButton(entry.Control.name, () =>
                {
                    if (entry.Submenu != null) OpenSubmenu(index);
                    else SelectControl(index);
                }, variant: UiButtonVariant.Ghost, labelTypographyClassName: UiClassNames.SecondaryText);
                button.tooltip = entry.Control.name + "\n" + entry.Control.type + " / " +
                    (entry.Owner == null ? T("ambiguous") : entry.Owner.name);
                var icon = ExpressionMenuModel.DisplayIcon(entry.Control.icon);
                var initialOn = entry.Control.type == VRCExpressionsMenu.Control.ControlType.Toggle &&
                    entry.Owner is ModularAvatarMenuItem item && item.isDefault;
                var slice = PlaceSlot(ring, labels, button, slot + (hasBack ? 1 : 0), slices, icon, running: initialOn);
                if (initialOn) button.tooltip += "\n" + T("initialStateOn");
                var subIcon = RadialMenuUtility.GetSubIcon(entry.Control.type);
                if (subIcon != null)
                {
                    var indicator = new VisualElement { pickingMode = PickingMode.Ignore };
                    indicator.AddToClassList("ee4v-expression-menu__sub-icon");
                    indicator.Add(new Image { image = subIcon,
                        scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore });
                    button.Content.Add(indicator);
                }
                slice.SetAvailable(entry.Submenu != null || CanInteract(entry), button);
            }
            if (hasNext)
            {
                var next = new UiButton(T("next"), () => { _offset += 7; _selected = -1; RefreshNavigation(); },
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
            if (index >= 0 && index < _page.Controls.Count &&
                _page.Controls[index].Submenu != null)
            { OpenSubmenu(index); return; }
            if (index < 0 || index >= _page.Controls.Count) return;
            _selected = index;
            _offset = _page.Controls.Count > 8 ? index / 7 * 7 : 0;
            _error = null;
            RefreshNavigation();
        }

        private bool LocateSource(UnityEngine.Object source)
        {
            if (_root == null || source == null) return false;
            var visited = new HashSet<MenuPage>();
            var path = new List<int>(_path);
            int selected = -1;
            bool Find(MenuPage page)
            {
                if (!visited.Add(page)) return false;
                for (var i = 0; i < page.Controls.Count; i++)
                {
                    var entry = page.Controls[i];
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
            var sourceRoot = _source == 0 ? _root : _sources[_source - 1];
            var preferredPage = sourceRoot;
            foreach (var index in path)
            {
                if (index < 0 || index >= preferredPage.Controls.Count || preferredPage.Controls[index].Submenu == null)
                { preferredPage = null; break; }
                preferredPage = preferredPage.Controls[index].Submenu;
            }
            if (preferredPage == null || !Find(preferredPage))
            {
                path.Clear();
                visited.Clear();
                if (!Find(sourceRoot))
                {
                    path.Clear();
                    visited.Clear();
                    if (_source == 0 || !Find(_root)) return false;
                    _source = 0;
                    sourceRoot = _root;
                }
            }
            _path.Clear();
            _path.AddRange(path);
            _selected = selected;
            var page = sourceRoot;
            foreach (var index in path) page = page.Controls[index].Submenu;
            _offset = page.Controls.Count > 8 ? selected / 7 * 7 : 0;
            return true;
        }

        private void OpenSubmenu(int index)
        {
            if (index < 0 || index >= _page.Controls.Count || _page.Controls[index].Submenu == null) return;
            _path.Add(index);
            _selected = -1;
            _offset = 0;
            _error = null;
            RefreshNavigation();
        }

        private bool CanInteract(MenuEntry entry) => ExpressionMenuModel.CanEdit(entry) && _context.Edits.CanEditPrefab() &&
            !EditorApplication.isPlayingOrWillChangePlaymode &&
            (!(entry.Owner is Component component) || ExpressionMenuTemplateModel.CanEdit(_context, component));

        private bool CanEditMenuRoot(MenuPage page)
        {
            if (page == null || !ExpressionMenuTemplateModel.CanEdit(_context)) return false;
            var target = (UnityEngine.Object)page.Asset ?? page.ChildRoot ??
                (_source > 0 ? page.Controls.FirstOrDefault()?.Owner : _context.Root);
            return ExpressionMenuModel.CanWrite(target) &&
                (!(target is Component) && !(target is GameObject) || ExpressionMenuTemplateModel.CanEdit(_context, target));
        }

        // Install target assets are references; new controls are stored in a separate prefab.
        private bool CanAddToPage() => CanAddToPage(_page);

        private bool CanAddToPage(MenuPage page) => page != null && _context.Edits.CanEditPrefab() &&
            !EditorApplication.isPlayingOrWillChangePlaymode &&
            (page.ChildRoot == null || ExpressionMenuTemplateModel.CanEdit(_context, page.ChildRoot));

        private static RadialSlice PlaceSlot(VisualElement ring, VisualElement labels, UiButton button, int index, int count,
            Texture2D icon, bool selected = false, bool running = false)
        {
            button.AddToClassList("ee4v-expression-menu__slot");
            button.Content.AddToClassList("ee4v-expression-menu__slot-content");
            button.LabelText.SetWhiteSpace(WhiteSpace.Normal);
            button.SetLabelTextAlign(TextAnchor.MiddleCenter);
            button.SetLabelColor(new Color(0.824f, 0.824f, 0.824f));
            button.SetLabelFontSize(12);
            var image = new Image
            {
                image = icon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore
            };
            if (running) image.Add(new RadialRunningIndicator());
            button.Content.Insert(0, image);
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

        private sealed class RadialRunningIndicator : VisualElement
        {
            private ValueAnimation<float> _rotationAnimation;

            internal RadialRunningIndicator()
            {
                pickingMode = PickingMode.Ignore;
                AddToClassList("ee4v-expression-menu__running-indicator");
                style.backgroundImage = Resources.Load<Texture2D>("Vrc3/BSX_GM_Running_Param");
                RegisterCallback<AttachToPanelEvent>(evt =>
                {
                    if (evt.target != this || _rotationAnimation != null) return;
                    _rotationAnimation = experimental.animation.Start(25f, 200f, int.MaxValue, (element, value) =>
                    {
                        var rotation = element.transform.rotation.eulerAngles;
                        rotation.z += 2f;
                        element.transform.rotation = Quaternion.Euler(rotation);
                    });
                    _rotationAnimation.KeepAlive();
                });
                RegisterCallback<DetachFromPanelEvent>(evt =>
                {
                    if (evt.target != this || _rotationAnimation == null) return;
                    _rotationAnimation.Stop();
                    _rotationAnimation.Recycle();
                    _rotationAnimation = null;
                });
            }
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
            var sectionStart = host.childCount;
            var heading = UiTextFactory.Create(T("settings"), UiClassNames.SectionTitle);
            host.Add(heading);
            var maSource = entry.Owner as ModularAvatarMenuItem;
            var simple = ExpressionMenuTemplateModel.HasActions(maSource);
            var submenuControl = entry.Control.type == VRCExpressionsMenu.Control.ControlType.SubMenu;
            var compact = simple || submenuControl && maSource != null &&
                ExpressionMenuTemplateModel.IsOwned(maSource.gameObject) && maSource.MenuSource == SubmenuSource.Children;
            var canEdit = CanInteract(entry);
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
            if (host.childCount == sectionStart + 1) { heading.RemoveFromHierarchy(); }
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

        private void AddControl(bool submenu, MenuPage page = null, int[] path = null)
        {
            page = page ?? _page;
            if (!CanAddToPage(page)) throw new InvalidOperationException(T("readOnly"));
            _selectAddedSource = ExpressionMenuTemplateModel.Create(_context,
                T(submenu ? "newSubmenu" : "newControl"), page.Asset, page.ChildRoot, submenu);
            if (path != null) { _path.Clear(); _path.AddRange(path); }
            _openAddedSubmenu = submenu;
            NotifyChanged();
        }

        private void ShowAddMenu()
        {
            ShowAddMenu(_page, _path.ToArray());
        }

        private void ShowAddMenu(MenuPage page, int[] path)
        {
            var choices = new GenericMenu();
            choices.AddItem(UiTextFactory.CreateGuiContent(T("addControl")), false, () => Run(() => AddControl(false, page, path)));
            choices.AddItem(UiTextFactory.CreateGuiContent(T("addSubmenu")), false, () => Run(() => AddControl(true, page, path)));
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
