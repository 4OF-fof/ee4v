using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.PlayModeComponentSuppression
{
    internal sealed class ComponentTypePickerWindow : CustomPopupWindow
    {
        private static readonly Vector2 PopupSize =
            new Vector2(520f, 460f);

        private IReadOnlyList<string> _existing;
        private int _replacingIndex;
        private Action<Type> _select;
        private PickerItem _selected;
        private UiTextButton _selectButton;

        internal static void Show(
            VisualElement anchor,
            IReadOnlyList<string> existing,
            int replacingIndex,
            Action<Type> select)
        {
            if (anchor == null || select == null)
            {
                return;
            }

            foreach (var existingWindow in Resources
                         .FindObjectsOfTypeAll<ComponentTypePickerWindow>())
            {
                existingWindow.Close();
            }

            var window = CreateInstance<ComponentTypePickerWindow>();
            window._existing = existing ?? Array.Empty<string>();
            window._replacingIndex = replacingIndex;
            window._select = select;
            window.ShowAsPopup(anchor, PopupSize);
        }

        internal static string GetDisplayName(Type type)
        {
            var menuSegments = GetMenuSegments(type);
            if (menuSegments.Length > 0)
            {
                return menuSegments[menuSegments.Length - 1];
            }

            return type != null
                ? ObjectNames.NicifyVariableName(type.Name)
                : string.Empty;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            UiComposition.Prepare(root);
            ConfigureCloseAndSubmitKeys(root, SelectCurrent);

            var popup = new CustomPopup(
                I18N.Get("settings.suppressedTypes.picker.title"),
                showFooter: true,
                closeTooltip: I18N.Get(
                    "settings.suppressedTypes.picker.cancel"));
            var tree = new SearchableTreeView<PickerItem>(
                () => UiTextFactory.Create(),
                BindItem,
                OnSelectionChanged,
                I18N.Get("settings.suppressedTypes.picker.empty"),
                I18N.Get("settings.suppressedTypes.picker.search"),
                canInteractWithItem: item => item.CanSelect,
                onItemDoubleClicked: Select,
                searchTooltip: I18N.Get(
                    "settings.suppressedTypes.picker.searchTooltip"),
                clearTooltip: I18N.Get(
                    "settings.suppressedTypes.picker.clearTooltip"));
            tree.SetItems(BuildItems());
            popup.Content.Add(tree);

            popup.Footer.Add(UiTextFactory.CreateButton(
                I18N.Get("settings.suppressedTypes.picker.cancel"),
                Close));
            _selectButton = UiTextFactory.CreateButton(
                I18N.Get("settings.suppressedTypes.picker.select"),
                SelectCurrent);
            _selectButton.SetEnabled(false);
            popup.Footer.Add(_selectButton);
            SetPopup(popup);
        }

        private IReadOnlyList<SearchableTreeItemData<PickerItem>> BuildItems()
        {
            var unavailable = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < _existing.Count; index++)
            {
                if (index != _replacingIndex)
                {
                    unavailable.Add(_existing[index]);
                }
            }

            var nextId = 1;
            var root = new MenuGroup(string.Empty, string.Empty);
            var types = TypeCache.GetTypesDerivedFrom<MonoBehaviour>()
                .Where(ComponentTypeIdentity.IsSelectable)
                .OrderBy(type => type.FullName,
                    StringComparer.OrdinalIgnoreCase);

            foreach (var type in types)
            {
                var menuSegments = GetMenuSegments(type);
                var group = root;
                if (menuSegments.Length == 0)
                {
                    group = group.GetOrAdd("Scripts");
                }
                else
                {
                    for (var index = 0;
                         index < menuSegments.Length - 1;
                         index++)
                    {
                        group = group.GetOrAdd(menuSegments[index]);
                    }
                }

                group.Components.Add(new ComponentMenuItem(
                    type,
                    GetDisplayName(type),
                    menuSegments.Length > 0
                        ? string.Join("/", menuSegments)
                        : "Scripts/" + GetDisplayName(type)));
            }

            return BuildTreeItems(root, unavailable, ref nextId);
        }

        private static IReadOnlyList<SearchableTreeItemData<PickerItem>>
            BuildTreeItems(
                MenuGroup group,
                ISet<string> unavailable,
                ref int nextId)
        {
            var items = new List<SearchableTreeItemData<PickerItem>>();
            foreach (var child in group.Children.Values.OrderBy(
                         item => item.Name,
                         StringComparer.OrdinalIgnoreCase))
            {
                var children = BuildTreeItems(
                    child,
                    unavailable,
                    ref nextId);
                items.Add(new SearchableTreeItemData<PickerItem>(
                    nextId++,
                    new PickerItem(child.Name, null, false),
                    child.Path,
                    child.Path,
                    children));
            }

            foreach (var component in group.Components
                         .OrderBy(
                             item => item.DisplayName,
                             StringComparer.OrdinalIgnoreCase)
                         .ThenBy(
                             item => item.Type.FullName,
                             StringComparer.OrdinalIgnoreCase))
            {
                var identity = ComponentTypeIdentity.Create(component.Type);
                items.Add(new SearchableTreeItemData<PickerItem>(
                    nextId++,
                    new PickerItem(
                        component.DisplayName,
                        component.Type,
                        !unavailable.Contains(identity)),
                    string.Join("\n", new[]
                    {
                        component.DisplayName,
                        component.MenuPath,
                        component.Type.Name,
                        component.Type.FullName,
                        component.Type.Assembly.GetName().Name
                    }),
                    identity));
            }

            return items;
        }

        private static string[] GetMenuSegments(Type type)
        {
            var menuName = type?
                .GetCustomAttribute<AddComponentMenu>(false)?
                .componentMenu;
            return string.IsNullOrWhiteSpace(menuName)
                ? Array.Empty<string>()
                : menuName
                    .Split(new[] { '/' },
                        StringSplitOptions.RemoveEmptyEntries)
                    .Select(segment => segment.Trim())
                    .Where(segment => segment.Length > 0)
                    .ToArray();
        }

        private static void BindItem(
            VisualElement element,
            PickerItem item)
        {
            var text = element as UiTextElement;
            text?.SetText(item.DisplayName);
            element.SetEnabled(item.Type == null || item.CanSelect);
        }

        private void OnSelectionChanged(
            IReadOnlyList<PickerItem> selection)
        {
            _selected = selection != null && selection.Count > 0
                ? selection[0]
                : null;
            _selectButton?.SetEnabled(_selected?.CanSelect == true);
        }

        private void Select(PickerItem item)
        {
            if (item?.CanSelect != true)
            {
                return;
            }

            _select?.Invoke(item.Type);
            Close();
        }

        private void SelectCurrent()
        {
            Select(_selected);
        }

        private sealed class PickerItem
        {
            internal PickerItem(
                string displayName,
                Type type,
                bool canSelect)
            {
                DisplayName = displayName ?? string.Empty;
                Type = type;
                CanSelect = canSelect;
            }

            internal string DisplayName { get; }

            internal Type Type { get; }

            internal bool CanSelect { get; }
        }

        private sealed class ComponentMenuItem
        {
            internal ComponentMenuItem(
                Type type,
                string displayName,
                string menuPath)
            {
                Type = type;
                DisplayName = displayName;
                MenuPath = menuPath;
            }

            internal Type Type { get; }

            internal string DisplayName { get; }

            internal string MenuPath { get; }
        }

        private sealed class MenuGroup
        {
            internal MenuGroup(string name, string path)
            {
                Name = name;
                Path = path;
                Children = new Dictionary<string, MenuGroup>(
                    StringComparer.OrdinalIgnoreCase);
                Components = new List<ComponentMenuItem>();
            }

            internal string Name { get; }

            internal string Path { get; }

            internal IDictionary<string, MenuGroup> Children { get; }

            internal ICollection<ComponentMenuItem> Components { get; }

            internal MenuGroup GetOrAdd(string name)
            {
                if (!Children.TryGetValue(name, out var child))
                {
                    child = new MenuGroup(
                        name,
                        string.IsNullOrEmpty(Path)
                            ? name
                            : Path + "/" + name);
                    Children.Add(name, child);
                }

                return child;
            }
        }
    }
}
