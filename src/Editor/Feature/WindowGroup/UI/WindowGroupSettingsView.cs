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
    internal sealed class WindowGroupSettingsView : VisualElement, IDisposable
    {
        private readonly WindowGroupConfiguration _configuration;
        private readonly Func<IReadOnlyList<WindowTypeOption>>
            _getWindowTypes;
        private string _selectedGroupId;
        private UiTextElement _feedback;
        private bool _rebuildScheduled;
        private bool _disposed;

        internal WindowGroupSettingsView(
            WindowGroupConfiguration configuration,
            Func<IReadOnlyList<WindowTypeOption>> getWindowTypes = null)
        {
            _configuration = configuration ??
                throw new ArgumentNullException(nameof(configuration));
            _getWindowTypes = getWindowTypes ??
                UnityWindowCatalog.GetOpenWindowTypes;
            _configuration.Changed += OnConfigurationChanged;
            AddToClassList("ee4v-window-group-settings");
            RegisterCallback<DetachFromPanelEvent>(_ => Dispose());
            BuildContent();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _configuration.Changed -= OnConfigurationChanged;
            _rebuildScheduled = false;
        }

        internal void Refresh()
        {
            BuildContent();
        }

        private void BuildContent()
        {
            EnsureSelectedGroup();
            Clear();
            Add(CreateBody());
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
            var sidebarHeader = new SectionHeader(
                I18N.Get("window.group.heading"));
            sidebarHeader.TitleText.AddToClassList(
                "ee4v-window-group-settings__section-title");
            sidebar.Add(sidebarHeader);

            var list = new ScrollView(ScrollViewMode.Vertical);
            list.AddToClassList(
                "ee4v-window-group-settings__group-list");
            for (var i = 0; i < _configuration.Groups.Count; i++)
            {
                var group = _configuration.Groups[i];
                var button = new NavigationItem(
                    new NavigationItemState(
                        group.Name,
                        selected: string.Equals(
                            group.Id,
                            _selectedGroupId,
                            StringComparison.Ordinal)),
                    () => SelectGroup(group.Id));
                button.AddToClassList(
                    "ee4v-window-group-settings__group");
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
                    button.Selected);
                list.Add(button);
            }

            if (_configuration.Groups.Count == 0)
            {
                var empty = new EmptyState(new EmptyStateState(
                    string.Empty,
                    I18N.Get("window.group.empty")));
                empty.AddToClassList(
                    "ee4v-window-group-settings__empty");
                list.Add(empty);
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
                var empty = new EmptyState(new EmptyStateState(
                    I18N.Get("window.detail.emptyTitle"),
                    I18N.Get("window.detail.emptyMessage")));
                empty.AddToClassList(
                    "ee4v-window-group-settings__empty");
                empty.TitleText.AddToClassList(
                    "ee4v-window-group-settings__empty-title");
                detail.Add(empty);
                return detail;
            }

            detail.Add(CreateGroupEditor(group));
            var windowsHeader = new SectionHeader(
                I18N.Get("window.windows.heading"),
                I18N.Get("window.windows.description"));
            windowsHeader.TitleText.AddToClassList(
                "ee4v-window-group-settings__section-title");
            windowsHeader.DescriptionText.AddToClassList(
                "ee4v-window-group-settings__section-description");
            detail.Add(windowsHeader);
            detail.Add(CreateWindowList(group));

            return detail;
        }

        private VisualElement CreateGroupEditor(
            WindowGroupDefinition group)
        {
            var editor = new VisualElement();
            editor.AddToClassList(
                "ee4v-window-group-settings__group-editor");

            var field = UiTextFactory.CreateTextField();
            field.value = group.Name;
            field.AddToClassList(
                "ee4v-window-group-settings__name-field");
            field.RegisterCallback<FocusOutEvent>(_ =>
                RenameGroup(group.Id, field.value));

            var nameRow = new FormInput(
                I18N.Get("window.group.name"),
                field);
            nameRow.AddToClassList(
                "ee4v-window-group-settings__name-row");
            nameRow.LabelText.AddToClassList(
                "ee4v-window-group-settings__name-label");

            _feedback = UiTextFactory.Create(
                string.Empty,
                UiClassNames.SecondaryText,
                "ee4v-window-group-settings__feedback");
            _feedback.SetColor(UiColorTokens.StatusFailedText);
            _feedback.SetWhiteSpace(WhiteSpace.Normal);
            _feedback.style.display = DisplayStyle.None;

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
            var options = GetKnownWindowTypes(selectedGroup.Id);
            if (options.Count == 0)
            {
                var empty = new EmptyState(new EmptyStateState(
                    string.Empty,
                    I18N.Get("window.windows.empty")));
                empty.AddToClassList(
                    "ee4v-window-group-settings__empty");
                list.Add(empty);
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
            var otherRegularMembership =
                CreateOtherRegularMembershipText(
                    option.TypeId,
                    selectedGroup.Id);
            var row = new ItemRow(new ItemRowState(
                option.DisplayName,
                otherRegularMembership,
                layout: ItemRowLayout.Stacked));
            row.AddToClassList(
                "ee4v-window-group-settings__window-row");
            row.TitleText.AddToClassList(
                "ee4v-window-group-settings__window-name");
            row.DescriptionText.AddToClassList(
                "ee4v-window-group-settings__window-memberships");
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

            var focus = UiTextFactory.CreateButton(
                I18N.Get("window.action.focus"),
                option.Focus);
            focus.AddToClassList(
                "ee4v-window-group-settings__window-focus");
            focus.TextElement.SetTextAlign(
                TextAnchor.MiddleCenter);
            focus.SetEnabled(option.Window != null);

            row.Leading.Add(toggle);
            row.Trailing.Add(follower);
            row.Trailing.Add(focus);
            return row;
        }

        private IReadOnlyList<WindowTypeOption> GetKnownWindowTypes(
            string selectedGroupId)
        {
            var options = _getWindowTypes()
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
                SetFeedback(string.Empty);
                return;
            }

            SetFeedback(I18N.Get("window.feedback.invalidName"));
        }

        private void SetFeedback(string text)
        {
            _feedback.SetText(text);
            _feedback.style.display = string.IsNullOrWhiteSpace(text)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
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
            if (_rebuildScheduled || _disposed)
            {
                return;
            }

            _rebuildScheduled = true;
            schedule.Execute(RebuildIfAttached);
        }

        private void RebuildIfAttached()
        {
            _rebuildScheduled = false;
            if (_disposed || panel == null)
            {
                return;
            }

            BuildContent();
        }
    }

}
