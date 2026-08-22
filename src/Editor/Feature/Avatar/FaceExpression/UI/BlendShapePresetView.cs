using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.Core.Settings;
using Ee4v.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.FaceExpression
{
    internal sealed class BlendShapePresetView : VisualElement, IDisposable
    {
        private const string DragDataKey = "ee4v.face-expression.preset-drag";
        private ISettingsService _settings;
        private IBlendShapePresetStore _presetStore;
        private BlendShapeNamePresetState _state;
        private BlendShapeFbxPreset _draft;
        private GameObject _selectedAvatar;
        private GameObject _sourceFbxAsset;
        private readonly List<BlendShapeNameMapping> _filtered =
            new List<BlendShapeNameMapping>();
        private readonly List<RoleGroup> _roleGroups =
            new List<RoleGroup>();
        private readonly Dictionary<BlendShapeNameMapping, string> _sectionKeys =
            new Dictionary<BlendShapeNameMapping, string>();
        private ObjectField _avatarField;
        private TextField _searchField;
        private ListView _list;
        private ScrollView _groupList;
        private UiTextElement _detailTitle;
        private TextField _roleEditor;
        private InlineMessage _status;
        private UiTextElement _sourceFbx;
        private UiTextButton _save;
        private UiTextButton _reclassify;
        private bool _dirty;
        private bool _settingAsset;
        private string _selectedGroupKey;

        internal BlendShapePresetView(
            ISettingsService settings,
            IBlendShapePresetStore presetStore = null)
        {
            _settings = settings ??
                throw new ArgumentNullException(nameof(settings));
            _presetStore = presetStore ?? BlendShapePresetStorage.Shared;
            _settings.Changed += OnSettingChanged;
            _presetStore.Changed += OnPresetStoreChanged;
            LoadState();
            RegisterCallback<DetachFromPanelEvent>(_ => Dispose());
            BuildContent(this);
            Refresh();
        }

        public void Dispose()
        {
            if (_settings != null)
            {
                _settings.Changed -= OnSettingChanged;
            }

            if (_presetStore != null)
            {
                _presetStore.Changed -= OnPresetStoreChanged;
            }
        }

        private void BuildContent(VisualElement root)
        {
            root.Clear();
            root.AddToClassList("ee4v-blend-shape-preset");

            var avatarRow = new VisualElement();
            avatarRow.AddToClassList("ee4v-blend-shape-preset__avatar-row");
            _avatarField = UiTextFactory.CreateObjectField(
                I18N.Get("presetWindow.avatar"));
            _avatarField.objectType = typeof(GameObject);
            _avatarField.allowSceneObjects = true;
            _avatarField.AddToClassList(
                "ee4v-blend-shape-preset__avatar-field");
            _avatarField.RegisterValueChangedCallback(evt =>
            {
                if (!_settingAsset)
                {
                    SelectAvatar(evt.newValue as GameObject);
                }
            });
            avatarRow.Add(_avatarField);
            root.Add(avatarRow);

            var toolbar = new ActionBar();
            toolbar.AddToClassList("ee4v-blend-shape-preset__toolbar");
            _sourceFbx = UiTextFactory.Create(
                string.Empty,
                UiClassNames.SecondaryText);
            _sourceFbx.AddToClassList(
                "ee4v-blend-shape-preset__source");
            toolbar.Leading.Add(_sourceFbx);

            _save = UiTextFactory.CreateButton(
                I18N.Get("presetWindow.save"),
                Save);
            _save.AddToClassList("ee4v-blend-shape-preset__action");
            toolbar.Actions.Add(_save);
            _reclassify = UiTextFactory.CreateButton(
                I18N.Get("presetWindow.reclassify"),
                Reclassify);
            _reclassify.AddToClassList("ee4v-blend-shape-preset__action");
            toolbar.Actions.Add(_reclassify);
            root.Add(toolbar);

            _status = new InlineMessage();
            _status.AddToClassList("ee4v-blend-shape-preset__status");
            _status.style.display = DisplayStyle.None;
            root.Add(_status);

            var workspace = new VisualElement();
            workspace.AddToClassList("ee4v-blend-shape-preset__workspace");

            var navigation = new VisualElement();
            navigation.AddToClassList("ee4v-blend-shape-preset__navigation");
            navigation.Add(new SectionHeader(I18N.Get("presetWindow.roles")));
            _searchField = UiTextFactory.CreateTextField(
                I18N.Get("presetWindow.search"));
            _searchField.AddToClassList(
                "ee4v-blend-shape-preset__search");
            _searchField.RegisterValueChangedCallback(_ => RefreshFilter());
            navigation.Add(_searchField);
            _groupList = new ScrollView(ScrollViewMode.Vertical);
            _groupList.AddToClassList(
                "ee4v-blend-shape-preset__role-list");
            navigation.Add(_groupList);
            workspace.Add(navigation);

            var detail = new VisualElement();
            detail.AddToClassList("ee4v-blend-shape-preset__detail");
            var roleRow = new LabeledContentRow();
            roleRow.AddToClassList("ee4v-blend-shape-preset__role-row");
            _detailTitle = roleRow.LabelText;
            _roleEditor = UiTextFactory.CreateTextField();
            _roleEditor.AddToClassList(
                "ee4v-blend-shape-preset__role-editor");
            _roleEditor.RegisterCallback<FocusOutEvent>(_ => RenameSelectedRole());
            roleRow.Content.Add(_roleEditor);
            detail.Add(roleRow);
            detail.Add(CreateDetailHeader());
            _list = new ListView
            {
                itemsSource = _filtered,
                fixedItemHeight = 30f,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.None,
                makeItem = () => new BlendShapePresetMappingRow(
                    MarkDirty,
                    CreateMappingPayload,
                    DropOnMapping),
                bindItem = (element, index) =>
                    ((BlendShapePresetMappingRow)element).Bind(
                        _filtered[index])
            };
            _list.AddToClassList("ee4v-blend-shape-preset__mapping-list");
            detail.Add(_list);
            workspace.Add(detail);
            root.Add(workspace);
        }

        private VisualElement CreateDetailHeader()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.paddingLeft = 8f;
            header.style.paddingRight = 8f;
            header.style.paddingBottom = 4f;
            header.Add(CreateHeaderText("presetWindow.source", 2.2f));
            header.Add(CreateHeaderText("presetWindow.variation", 1f));
            header.Add(CreateHeaderText("presetWindow.side", 0.55f));
            return header;
        }

        private static UiTextElement CreateHeaderText(string key, float grow)
        {
            var text = UiTextFactory.Create(I18N.Get(key), UiClassNames.FormLabel);
            text.style.flexBasis = 0f;
            text.style.flexGrow = grow;
            text.style.marginRight = 4f;
            return text;
        }

        private void LoadState()
        {
            _state = _presetStore.Load();
        }

        internal void SelectAvatar(GameObject avatar)
        {
            var sourceFbx = BlendShapeNamePresetSetting.ResolveSourceFbx(avatar);
            if (avatar != null && sourceFbx == null)
            {
                SetAvatarField(_selectedAvatar);
                SetStatus(I18N.Get("presetWindow.invalidAvatar"));
                return;
            }

            _selectedAvatar = avatar;
            _sourceFbxAsset = sourceFbx;
            SetAvatarField(avatar);
            if (avatar == null)
            {
                _draft = null;
                _dirty = false;
                Refresh();
                SetStatus(string.Empty);
                return;
            }

            var path = AssetDatabase.GetAssetPath(sourceFbx);
            var guid = AssetDatabase.AssetPathToGUID(path);
            var saved = BlendShapeNamePresetSetting.Find(_state, guid);
            _draft = saved == null
                ? BlendShapeNamePresetSetting.CreatePreset(
                    sourceFbx,
                    FaceExpressionSettings.GetSeparators(_settings))
                : Clone(saved);
            if (_draft != null)
            {
                _draft.assetPath = path;
                _draft.name = Path.GetFileNameWithoutExtension(path);
                BlendShapeNamePresetSetting.ApplyHeaders(
                    _draft,
                    FaceExpressionSettings.GetSeparators(_settings));
            }
            _dirty = saved == null;
            Refresh();
            SetStatus(saved == null
                ? I18N.Get("presetWindow.autoClassified")
                : string.Empty);
        }

        private void SetAvatarField(GameObject avatar)
        {
            if (_avatarField == null)
            {
                return;
            }

            _settingAsset = true;
            _avatarField.SetValueWithoutNotify(avatar);
            _settingAsset = false;
        }

        private void Refresh()
        {
            if (_avatarField == null)
            {
                return;
            }

            SetAvatarField(_selectedAvatar);
            _sourceFbx.SetText(_sourceFbxAsset == null
                ? string.Empty
                : string.Format(
                    I18N.Get("presetWindow.detectedFbx"),
                    _sourceFbxAsset.name));
            _save.SetEnabled(_draft != null && _dirty);
            _reclassify.SetEnabled(_sourceFbxAsset != null);
            RefreshFilter();
            if (_draft == null)
            {
                SetStatus(I18N.Get("presetWindow.selectAvatar"));
            }
        }

        private void RefreshFilter()
        {
            _filtered.Clear();
            _roleGroups.Clear();
            _sectionKeys.Clear();
            CreateRoleGroups();
            var search = (_searchField?.value ?? string.Empty).Trim();
            var selected = _roleGroups.FirstOrDefault(group =>
                               string.Equals(
                                   group.Key,
                                   _selectedGroupKey,
                                   StringComparison.Ordinal)) ??
                           _roleGroups.FirstOrDefault(group =>
                               group.Mappings.Count > 0) ??
                           _roleGroups.FirstOrDefault();
            _selectedGroupKey = selected?.Key;
            RebuildGroupList(selected, search);
            if (selected != null)
            {
                _filtered.AddRange(selected.Mappings.Where(mapping =>
                    MappingMatchesSearch(mapping, search)));
            }

            _detailTitle?.SetText(selected == null
                ? I18N.Get("presetWindow.noRoles")
                : selected.SectionName + " / ");
            _roleEditor?.SetEnabled(selected != null);
            if (_roleEditor != null)
            {
                _roleEditor.style.display = selected == null
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
                _roleEditor.SetValueWithoutNotify(
                    selected == null ? string.Empty : GetRoleLabel(selected));
            }

            _list?.Rebuild();
        }

        private void CreateRoleGroups()
        {
            long currentMeshId = long.MinValue;
            var sectionIndex = 0;
            var sectionName = string.Empty;
            var groups = new Dictionary<string, RoleGroup>(StringComparer.Ordinal);
            for (var index = 0; index < (_draft?.mappings?.Count ?? 0); index++)
            {
                var mapping = _draft.mappings[index];
                if (mapping.meshLocalId != currentMeshId)
                {
                    currentMeshId = mapping.meshLocalId;
                    sectionIndex = 0;
                    sectionName = mapping.meshName ?? string.Empty;
                }

                if (!string.IsNullOrEmpty(mapping.headerText))
                {
                    sectionIndex++;
                    sectionName = (mapping.meshName ?? string.Empty) + " / " +
                                  mapping.headerText;
                    continue;
                }

                var sectionKey = currentMeshId.ToString(
                                     CultureInfo.InvariantCulture) +
                                 ":" + sectionIndex;
                _sectionKeys[mapping] = sectionKey;
                var role = (mapping.role ?? string.Empty).Trim();
                var key = role.Length == 0
                    ? CreateStandaloneGroupKey(sectionKey, index)
                    : CreateGroupKey(sectionKey, role);
                if (!groups.TryGetValue(key, out var group))
                {
                    group = new RoleGroup(
                        key,
                        sectionKey,
                        sectionName,
                        role);
                    groups.Add(key, group);
                    _roleGroups.Add(group);
                }

                group.Mappings.Add(mapping);
            }
        }

        private void RebuildGroupList(RoleGroup selected, string search)
        {
            if (_groupList == null)
            {
                return;
            }

            _groupList.Clear();
            string currentSection = null;
            for (var index = 0; index < _roleGroups.Count; index++)
            {
                var group = _roleGroups[index];
                if (!RoleGroupMatchesSearch(group, search))
                {
                    continue;
                }

                if (!string.Equals(
                        currentSection,
                        group.SectionKey,
                        StringComparison.Ordinal))
                {
                    AddCreateRoleCard(currentSection);
                    currentSection = group.SectionKey;
                    var heading = new SectionHeader(group.SectionName);
                    heading.style.marginTop = 10f;
                    heading.style.marginBottom = 4f;
                    _groupList.Add(heading);
                }

                var card = new NavigationItem(
                    new NavigationItemState(
                        GetRoleLabel(group),
                        selected: ReferenceEquals(group, selected)),
                    () => SelectRoleGroup(group.Key));
                card.Trailing.Add(new Badge(new BadgeState(
                    group.Mappings.Count.ToString())));
                card.style.marginBottom = 3f;
                RegisterPresetDrag(
                    card,
                    () => new MappingDragPayload(
                        group.SectionKey,
                        group.Mappings,
                        GetRoleLabel(group)));
                RegisterPresetDrop(
                    card,
                    payload => string.Equals(
                        payload.SectionKey,
                        group.SectionKey,
                        StringComparison.Ordinal),
                    payload => DropOnMapping(
                        payload,
                        group.Mappings.FirstOrDefault()));
                _groupList.Add(card);
            }

            AddCreateRoleCard(currentSection);
        }

        private void AddCreateRoleCard(string sectionKey)
        {
            if (string.IsNullOrEmpty(sectionKey))
            {
                return;
            }

            var card = new NavigationItem(
                new NavigationItemState("+"));
            card.style.marginBottom = 3f;
            RegisterPresetDrop(
                card,
                payload => string.Equals(
                    payload.SectionKey,
                    sectionKey,
                    StringComparison.Ordinal),
                CreateRole);
            _groupList.Add(card);
        }

        private void CreateRole(MappingDragPayload payload)
        {
            if (payload == null || payload.Mappings.Count == 0)
            {
                return;
            }

            var baseRole = payload.Mappings[0].shapeName.Trim();
            var role = baseRole;
            var suffix = 2;
            while (_roleGroups.Any(group =>
                       string.Equals(
                           group.SectionKey,
                           payload.SectionKey,
                           StringComparison.Ordinal) &&
                       string.Equals(
                           group.Role,
                           role,
                           StringComparison.Ordinal)))
            {
                role = baseRole + " " + suffix;
                suffix++;
            }

            AssignRole(payload, role);
        }

        private void SelectRoleGroup(string key)
        {
            _selectedGroupKey = key;
            RefreshFilter();
        }

        private void RenameSelectedRole()
        {
            var selected = _roleGroups.FirstOrDefault(group => string.Equals(
                group.Key,
                _selectedGroupKey,
                StringComparison.Ordinal));
            if (selected == null)
            {
                return;
            }

            var role = (_roleEditor.value ?? string.Empty).Trim();
            if (role.Length == 0)
            {
                _roleEditor.SetValueWithoutNotify(GetRoleLabel(selected));
                return;
            }

            if (string.Equals(role, selected.Role, StringComparison.Ordinal))
            {
                return;
            }

            AssignRole(
                new MappingDragPayload(
                    selected.SectionKey,
                    selected.Mappings,
                    selected.Role),
                role);
        }

        private MappingDragPayload CreateMappingPayload(
            BlendShapeNameMapping mapping)
        {
            return mapping != null && _sectionKeys.TryGetValue(
                mapping,
                out var sectionKey)
                ? new MappingDragPayload(
                    sectionKey,
                    new[] { mapping },
                    mapping.shapeName)
                : null;
        }

        private void DropOnMapping(
            MappingDragPayload payload,
            BlendShapeNameMapping target)
        {
            if (payload == null ||
                target == null ||
                !_sectionKeys.TryGetValue(target, out var sectionKey) ||
                !string.Equals(
                    payload.SectionKey,
                    sectionKey,
                    StringComparison.Ordinal))
            {
                return;
            }

            var role = (target.role ?? string.Empty).Trim();
            var targetChanged = false;
            if (role.Length == 0)
            {
                role = BlendShapeNameClassifier.Classify(
                    target.meshLocalId,
                    target.meshName,
                    target.shapeName).role;
                if (string.IsNullOrWhiteSpace(role))
                {
                    role = target.shapeName;
                }

                target.role = role;
                targetChanged = true;
            }

            if (!AssignRole(payload, role) && targetChanged)
            {
                _selectedGroupKey = CreateGroupKey(sectionKey, role);
                MarkDirty();
                RefreshFilter();
            }
        }

        private bool AssignRole(MappingDragPayload payload, string role)
        {
            if (payload == null)
            {
                return false;
            }

            role = (role ?? string.Empty).Trim();
            var changed = false;
            for (var index = 0; index < payload.Mappings.Count; index++)
            {
                var mapping = payload.Mappings[index];
                if (!_sectionKeys.TryGetValue(mapping, out var sectionKey) ||
                    !string.Equals(
                        sectionKey,
                        payload.SectionKey,
                        StringComparison.Ordinal) ||
                    string.Equals(
                        mapping.role ?? string.Empty,
                        role,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                mapping.role = role;
                changed = true;
            }

            if (!changed)
            {
                return false;
            }

            _selectedGroupKey = CreateGroupKey(payload.SectionKey, role);
            MarkDirty();
            RefreshFilter();
            return true;
        }

        private static string CreateGroupKey(string sectionKey, string role)
        {
            return (sectionKey ?? string.Empty) + "\n" + (role ?? string.Empty);
        }

        private static string CreateStandaloneGroupKey(
            string sectionKey,
            int mappingIndex)
        {
            return (sectionKey ?? string.Empty) + "\n#" +
                   mappingIndex.ToString(CultureInfo.InvariantCulture);
        }

        private static bool RoleGroupMatchesSearch(RoleGroup group, string search)
        {
            return search.Length == 0 ||
                   Contains(group.SectionName, search) ||
                   Contains(GetRoleLabel(group), search) ||
                   group.Mappings.Any(mapping =>
                       MappingMatchesSearch(mapping, search));
        }

        private static bool MappingMatchesSearch(
            BlendShapeNameMapping mapping,
            string search)
        {
            return search.Length == 0 || Contains(mapping.meshName, search) ||
                   Contains(mapping.shapeName, search) ||
                   Contains(mapping.role, search) ||
                   Contains(mapping.variation, search) ||
                   Contains(mapping.side, search);
        }

        private static string GetRoleLabel(RoleGroup group)
        {
            return string.IsNullOrWhiteSpace(group?.Role)
                ? group?.Mappings.FirstOrDefault()?.shapeName ?? string.Empty
                : group.Role;
        }

        private static bool Contains(string value, string search)
        {
            return (value ?? string.Empty).IndexOf(
                search,
                StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void MarkDirty()
        {
            _dirty = true;
            _save?.SetEnabled(true);
            SetStatus(I18N.Get("presetWindow.unsaved"));
        }

        private void Save()
        {
            if (_draft == null)
            {
                return;
            }

            _presetStore.Save(_draft);
            LoadState();
            _dirty = false;
            Refresh();
            SetStatus(I18N.Get("presetWindow.saved"));
        }

        private void Reclassify()
        {
            if (_sourceFbxAsset == null)
            {
                return;
            }

            _draft = BlendShapeNamePresetSetting.CreatePreset(
                _sourceFbxAsset,
                FaceExpressionSettings.GetSeparators(_settings));
            _dirty = true;
            Refresh();
            SetStatus(I18N.Get("presetWindow.autoClassified"));
        }

        private void OnSettingChanged(object sender, SettingChangedEventArgs args)
        {
            if (ReferenceEquals(
                    args.Definition,
                    FaceExpressionSettings.BlendShapeSeparators))
            {
                BlendShapeNamePresetSetting.ApplyHeaders(
                    _draft,
                    FaceExpressionSettings.GetSeparators(_settings));
                Refresh();
                return;
            }
        }

        private void OnPresetStoreChanged()
        {
            LoadState();
            if (_selectedAvatar != null && !_dirty)
            {
                SelectAvatar(_selectedAvatar);
            }
        }

        internal void Rebuild()
        {
            BuildContent(this);
            Refresh();
        }

        internal void SetDraft(
            BlendShapeFbxPreset draft,
            bool dirty,
            string status)
        {
            _selectedAvatar = null;
            _sourceFbxAsset = null;
            _draft = draft;
            _dirty = dirty;
            Refresh();
            SetStatus(status);
        }

        internal void SetAvatarSelectionEnabled(bool enabled)
        {
            _avatarField.SetEnabled(enabled);
        }

        private void SetStatus(string value)
        {
            if (_status == null)
            {
                return;
            }

            var hasValue = !string.IsNullOrWhiteSpace(value);
            _status.style.display = hasValue
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            if (hasValue)
            {
                _status.SetState(new InlineMessageState(value));
            }
        }

        private static BlendShapeFbxPreset Clone(BlendShapeFbxPreset preset)
        {
            return JsonUtility.FromJson<BlendShapeFbxPreset>(
                JsonUtility.ToJson(preset));
        }

        internal static void RegisterPresetDrag(
            VisualElement element,
            Func<MappingDragPayload> createPayload)
        {
            var start = Vector2.zero;
            var ready = false;
            element.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != (int)MouseButton.LeftMouse)
                {
                    return;
                }

                start = evt.mousePosition;
                ready = true;
            });
            element.RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (!ready ||
                    (evt.pressedButtons & 1) == 0 ||
                    Vector2.Distance(start, evt.mousePosition) < 4f)
                {
                    return;
                }

                ready = false;
                var payload = createPayload?.Invoke();
                if (payload == null)
                {
                    return;
                }

                DragAndDrop.PrepareStartDrag();
                DragAndDrop.SetGenericData(DragDataKey, payload);
                DragAndDrop.StartDrag(payload.Label ?? string.Empty);
                evt.StopPropagation();
            });
            element.RegisterCallback<MouseUpEvent>(_ => ready = false);
        }

        internal static void RegisterPresetDrop(
            VisualElement element,
            Func<MappingDragPayload, bool> canDrop,
            Action<MappingDragPayload> onDrop)
        {
            element.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                var payload = DragAndDrop.GetGenericData(DragDataKey) as
                    MappingDragPayload;
                if (payload == null || !(canDrop?.Invoke(payload) ?? true))
                {
                    return;
                }

                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                element.style.opacity = 0.7f;
                evt.StopPropagation();
            });
            element.RegisterCallback<DragLeaveEvent>(_ =>
                element.style.opacity = 1f);
            element.RegisterCallback<DragExitedEvent>(_ =>
                element.style.opacity = 1f);
            element.RegisterCallback<DragPerformEvent>(evt =>
            {
                var payload = DragAndDrop.GetGenericData(DragDataKey) as
                    MappingDragPayload;
                if (payload == null || !(canDrop?.Invoke(payload) ?? true))
                {
                    return;
                }

                element.style.opacity = 1f;
                DragAndDrop.AcceptDrag();
                onDrop?.Invoke(payload);
                evt.StopPropagation();
            });
        }

        private sealed class RoleGroup
        {
            internal RoleGroup(
                string key,
                string sectionKey,
                string sectionName,
                string role)
            {
                Key = key;
                SectionKey = sectionKey;
                SectionName = sectionName;
                Role = role;
            }

            internal string Key { get; }
            internal string SectionKey { get; }
            internal string SectionName { get; }
            internal string Role { get; }
            internal List<BlendShapeNameMapping> Mappings { get; } =
                new List<BlendShapeNameMapping>();
        }

        internal sealed class MappingDragPayload
        {
            internal MappingDragPayload(
                string sectionKey,
                IEnumerable<BlendShapeNameMapping> mappings,
                string label)
            {
                SectionKey = sectionKey ?? string.Empty;
                Mappings = mappings?.ToArray() ??
                           Array.Empty<BlendShapeNameMapping>();
                Label = label ?? string.Empty;
            }

            internal string SectionKey { get; }
            internal IReadOnlyList<BlendShapeNameMapping> Mappings { get; }
            internal string Label { get; }
        }

    }

}
