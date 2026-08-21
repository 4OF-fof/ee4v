using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.WindowGroup
{
    internal sealed class WindowGroupSettingsWindow : EditorWindow
    {
        private const string WindowClassName =
            "ee4v-window-group-settings";
        private const float MinimumWidth = 640f;
        private const float MinimumHeight = 400f;

        private WindowGroupConfiguration _configuration;
        private string _selectedGroupId;
        private UiTextElement _feedback;
        private bool _rebuildScheduled;

        [MenuItem("ee4v/Window Groups")]
        private static void Open()
        {
            var window = GetWindow<WindowGroupSettingsWindow>();
            window.titleContent = UiTextFactory.CreateGuiContent(
                I18N.Get("window.title"));
            window.minSize = new Vector2(
                MinimumWidth,
                MinimumHeight);
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            _configuration = WindowGroupBootstrap.Settings;
            _configuration.Changed += OnConfigurationChanged;
            titleContent = UiTextFactory.CreateGuiContent(
                I18N.Get("window.title"));
            minSize = new Vector2(MinimumWidth, MinimumHeight);
        }

        private void OnDisable()
        {
            if (_configuration != null)
            {
                _configuration.Changed -= OnConfigurationChanged;
            }

            EditorApplication.delayCall -= RebuildIfOpen;
            _rebuildScheduled = false;
        }

        private void CreateGUI()
        {
            BuildContent();
        }

        private void OnFocus()
        {
            if (rootVisualElement.panel != null)
            {
                BuildContent();
            }
        }

        private void BuildContent()
        {
            EnsureSelectedGroup();
            titleContent = UiTextFactory.CreateGuiContent(
                I18N.Get("window.title"));

            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList(WindowClassName);
            UiComposition.Prepare(root);
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/Feature/WindowGroup/UI/window-group-settings.uss");

            root.Add(CreateBody());
        }

        private VisualElement CreateBody()
        {
            var body = new VisualElement();
            body.AddToClassList(
                "ee4v-window-group-settings__body");
            body.Add(CreateGroupSidebar());
            body.Add(CreateDetail());
            return body;
        }

        private VisualElement CreateGroupSidebar()
        {
            var sidebar = new VisualElement();
            sidebar.AddToClassList(
                "ee4v-window-group-settings__sidebar");
            sidebar.Add(UiTextFactory.Create(
                I18N.Get("window.group.heading"),
                UiClassNames.SectionTitle,
                "ee4v-window-group-settings__section-title"));

            var list = new ScrollView(ScrollViewMode.Vertical);
            list.AddToClassList(
                "ee4v-window-group-settings__group-list");
            for (var i = 0; i < _configuration.Groups.Count; i++)
            {
                var group = _configuration.Groups[i];
                var button = UiTextFactory.CreateButton(
                    group.Name,
                    () => SelectGroup(group.Id),
                    UiClassNames.NavigationItemLabel);
                button.AddToClassList(
                    "ee4v-window-group-settings__group");
                button.TextElement.SetTextAlign(
                    TextAnchor.MiddleLeft);
                button.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button != 1)
                    {
                        return;
                    }

                    ShowGroupContextMenu(group.Id);
                    evt.StopPropagation();
                });
                button.EnableInClassList(
                    "ee4v-window-group-settings__group--selected",
                    string.Equals(
                        group.Id,
                        _selectedGroupId,
                        StringComparison.Ordinal));
                list.Add(button);
            }

            if (_configuration.Groups.Count == 0)
            {
                list.Add(UiTextFactory.Create(
                    I18N.Get("window.group.empty"),
                    "ee4v-window-group-settings__empty",
                    UiClassNames.SecondaryText));
            }

            sidebar.Add(list);
            var createGroup = UiTextFactory.CreateButton(
                I18N.Get("window.action.createGroup"),
                CreateGroup);
            createGroup.AddToClassList(
                "ee4v-window-group-settings__create-group");
            createGroup.TextElement.SetTextAlign(
                TextAnchor.MiddleCenter);
            sidebar.Add(createGroup);
            return sidebar;
        }

        private VisualElement CreateDetail()
        {
            var detail = new VisualElement();
            detail.AddToClassList(
                "ee4v-window-group-settings__detail");
            var group = _configuration.GetGroup(_selectedGroupId);
            if (group == null)
            {
                detail.Add(UiTextFactory.Create(
                    I18N.Get("window.detail.emptyTitle"),
                    UiClassNames.WindowTitle,
                    "ee4v-window-group-settings__empty-title"));
                detail.Add(UiTextFactory.Create(
                    I18N.Get("window.detail.emptyMessage"),
                    "ee4v-window-group-settings__empty",
                    UiClassNames.SecondaryText));
                return detail;
            }

            detail.Add(CreateGroupEditor(group));
            detail.Add(UiTextFactory.Create(
                I18N.Get("window.windows.heading"),
                UiClassNames.SectionTitle,
                "ee4v-window-group-settings__section-title"));
            detail.Add(UiTextFactory.Create(
                I18N.Get("window.windows.description"),
                "ee4v-window-group-settings__section-description",
                UiClassNames.SecondaryText));
            detail.Add(CreateWindowList(group));

            return detail;
        }

        private VisualElement CreateGroupEditor(
            WindowGroupDefinition group)
        {
            var editor = new VisualElement();
            editor.AddToClassList(
                "ee4v-window-group-settings__group-editor");

            var nameRow = new VisualElement();
            nameRow.AddToClassList(
                "ee4v-window-group-settings__name-row");
            nameRow.Add(UiTextFactory.Create(
                I18N.Get("window.group.name"),
                UiClassNames.FormLabel,
                "ee4v-window-group-settings__name-label"));

            var field = UiTextFactory.CreateTextField();
            field.value = group.Name;
            field.AddToClassList(
                "ee4v-window-group-settings__name-field");
            field.RegisterCallback<FocusOutEvent>(_ =>
                RenameGroup(group.Id, field.value));

            _feedback = UiTextFactory.Create(
                string.Empty,
                UiClassNames.FormError,
                "ee4v-window-group-settings__feedback");

            nameRow.Add(field);
            editor.Add(nameRow);
            editor.Add(_feedback);
            return editor;
        }

        private VisualElement CreateWindowList(
            WindowGroupDefinition selectedGroup)
        {
            var list = new ScrollView(ScrollViewMode.Vertical);
            list.AddToClassList(
                "ee4v-window-group-settings__window-list");
            list.AddToClassList(
                UiClassNames.ThinVerticalScrollbar);
            var options = GetKnownWindowTypes(selectedGroup.Id);
            if (options.Count == 0)
            {
                list.Add(UiTextFactory.Create(
                    I18N.Get("window.windows.empty"),
                    "ee4v-window-group-settings__empty",
                    UiClassNames.SecondaryText));
                return list;
            }

            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                list.Add(CreateWindowRow(
                    option,
                    selectedGroup));
            }

            return list;
        }

        private VisualElement CreateWindowRow(
            WindowTypeOption option,
            WindowGroupDefinition selectedGroup)
        {
            var isSelected = _configuration.IsAssigned(
                option.TypeId,
                selectedGroup.Id);
            var followerRequired =
                _configuration.HasRegularMembershipInOtherGroup(
                    option.TypeId,
                    selectedGroup.Id);
            var row = new VisualElement();
            row.AddToClassList(
                "ee4v-window-group-settings__window-row");
            row.EnableInClassList(
                "ee4v-window-group-settings__window-row--assigned",
                isSelected);

            var toggle = UiTextFactory.CreateToggle(
                string.Empty,
                "ee4v-window-group-settings__window-toggle");
            toggle.SetValueWithoutNotify(isSelected);
            toggle.RegisterValueChangedCallback(evt =>
            {
                _configuration.SetWindowTypeAssigned(
                    option.TypeId,
                    selectedGroup.Id,
                    evt.newValue);
            });

            var follower = UiTextFactory.CreateToggle(
                I18N.Get("window.windows.follower"),
                "ee4v-window-group-settings__follower-toggle");
            follower.SetValueWithoutNotify(
                followerRequired ||
                (isSelected && _configuration.IsFollower(
                     option.TypeId,
                     selectedGroup.Id)));
            follower.SetEnabled(isSelected && !followerRequired);
            follower.RegisterValueChangedCallback(evt =>
                _configuration.SetFollower(
                    option.TypeId,
                    selectedGroup.Id,
                    evt.newValue));

            var name = UiTextFactory.Create(
                option.DisplayName,
                UiClassNames.NavigationItemLabel,
                "ee4v-window-group-settings__window-name");
            var nameColumn = new VisualElement();
            nameColumn.AddToClassList(
                "ee4v-window-group-settings__window-name-column");
            nameColumn.Add(name);
            var otherRegularMembership =
                CreateOtherRegularMembershipText(
                    option.TypeId,
                    selectedGroup.Id);
            if (!string.IsNullOrEmpty(otherRegularMembership))
            {
                nameColumn.Add(UiTextFactory.Create(
                    otherRegularMembership,
                    UiClassNames.SecondaryText,
                    "ee4v-window-group-settings__window-memberships"));
            }

            var focus = UiTextFactory.CreateButton(
                I18N.Get("window.action.focus"),
                option.Focus);
            focus.AddToClassList(
                "ee4v-window-group-settings__window-focus");
            focus.TextElement.SetTextAlign(
                TextAnchor.MiddleCenter);
            focus.SetEnabled(option.Window != null);

            row.Add(toggle);
            row.Add(nameColumn);
            row.Add(follower);
            row.Add(focus);
            return row;
        }

        private IReadOnlyList<WindowTypeOption> GetKnownWindowTypes(
            string selectedGroupId)
        {
            var options = UnityWindowCatalog.GetOpenWindowTypes()
                .ToDictionary(
                    option => option.TypeId,
                    StringComparer.Ordinal);
            foreach (var group in _configuration.Groups)
            {
                foreach (var typeId in group.WindowTypeIds)
                {
                    if (!options.ContainsKey(typeId))
                    {
                        var displayName =
                            WindowTypeIdentity.GetDisplayName(typeId);
                        options.Add(
                            typeId,
                            new WindowTypeOption(
                                typeId,
                                displayName,
                                null));
                    }
                }
            }

            return options.Values
                .OrderBy(option =>
                    _configuration.IsAssigned(
                        option.TypeId,
                        selectedGroupId)
                        ? 0
                        : 1)
                .ThenBy(
                    option => option.DisplayName,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }

        private string CreateOtherRegularMembershipText(
            string windowTypeId,
            string selectedGroupId)
        {
            var group = _configuration.Groups.FirstOrDefault(candidate =>
                !string.Equals(
                    candidate.Id,
                    selectedGroupId,
                    StringComparison.Ordinal) &&
                candidate.WindowTypeIds.Contains(windowTypeId) &&
                !_configuration.IsFollower(
                    windowTypeId,
                    candidate.Id));
            return group == null
                ? string.Empty
                : I18N.Get("window.windows.memberships") + " " +
                  group.Name;
        }

        private void CreateGroup()
        {
            _selectedGroupId = _configuration.CreateGroup(
                I18N.Get("window.group.newName"));
        }

        private void SelectGroup(string groupId)
        {
            _selectedGroupId = groupId;
            BuildContent();
        }

        private void ShowGroupContextMenu(string groupId)
        {
            var menu = new GenericMenu();
            menu.AddItem(
                UiTextFactory.CreateGuiContent(
                    I18N.Get("window.action.delete")),
                false,
                () => DeleteGroup(groupId));
            menu.ShowAsContext();
        }

        private void DeleteGroup(string groupId)
        {
            if (string.Equals(
                    _selectedGroupId,
                    groupId,
                    StringComparison.Ordinal))
            {
                _selectedGroupId = null;
            }

            _configuration.DeleteGroup(groupId);
        }

        private void RenameGroup(string groupId, string name)
        {
            if (_configuration.RenameGroup(groupId, name))
            {
                _feedback.SetText(string.Empty);
                return;
            }

            _feedback.SetText(
                I18N.Get("window.feedback.invalidName"));
        }

        private void EnsureSelectedGroup()
        {
            if (_configuration.GetGroup(_selectedGroupId) != null)
            {
                return;
            }

            _selectedGroupId = _configuration.Groups.Count > 0
                ? _configuration.Groups[0].Id
                : null;
        }

        private void OnConfigurationChanged()
        {
            if (_rebuildScheduled)
            {
                return;
            }

            _rebuildScheduled = true;
            EditorApplication.delayCall += RebuildIfOpen;
        }

        private void RebuildIfOpen()
        {
            _rebuildScheduled = false;
            if (this == null || rootVisualElement.panel == null)
            {
                return;
            }

            BuildContent();
        }
    }
}
