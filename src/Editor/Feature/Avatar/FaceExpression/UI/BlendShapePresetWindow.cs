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
    internal sealed class BlendShapePresetWindow : EditorWindow
    {
        private const string DragDataKey = "ee4v.face-expression.preset-drag";
        private static readonly List<string> SideChoices =
            new List<string> { string.Empty, "L", "R" };

        private ISettingsService _settings;
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
        private UiTextElement _status;
        private UiTextElement _sourceFbx;
        private UiTextButton _save;
        private UiTextButton _reclassify;
        private bool _dirty;
        private bool _settingAsset;
        private string _selectedGroupKey;

        [MenuItem("ee4v/Avatar/BlendShape Presets")]
        private static void Open()
        {
            ShowWindow();
        }

        internal static void ShowWindow()
        {
            var window = GetWindow<BlendShapePresetWindow>();
            window.titleContent = UiTextFactory.CreateGuiContent(
                I18N.Get("presetWindow.title"));
            window.minSize = new Vector2(840f, 480f);
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            _settings = CoreSettings.Current;
            _settings.Register(FaceExpressionSettings.BlendShapePresets);
            _settings.Changed += OnSettingChanged;
            I18N.Reloaded += Rebuild;
            LoadState();
        }

        private void OnDisable()
        {
            if (_settings != null)
            {
                _settings.Changed -= OnSettingChanged;
            }

            I18N.Reloaded -= Rebuild;
        }

        private void CreateGUI()
        {
            BuildContent();
            var selection = Selection.activeGameObject;
            if (_selectedAvatar == null &&
                BlendShapeNamePresetSetting.ResolveSourceFbx(selection) != null)
            {
                SelectAvatar(selection);
            }
            else
            {
                Refresh();
            }
        }

        private void BuildContent()
        {
            titleContent = UiTextFactory.CreateGuiContent(
                I18N.Get("presetWindow.title"));
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList("ee4v-ui");
            UiComposition.Prepare(root);

            var avatarRow = new VisualElement();
            avatarRow.style.paddingLeft = 8f;
            avatarRow.style.paddingRight = 8f;
            avatarRow.style.paddingTop = 8f;
            _avatarField = UiTextFactory.CreateObjectField(
                I18N.Get("presetWindow.avatar"));
            _avatarField.objectType = typeof(GameObject);
            _avatarField.allowSceneObjects = true;
            _avatarField.style.flexGrow = 1f;
            _avatarField.style.minWidth = 0f;
            _avatarField.RegisterValueChangedCallback(evt =>
            {
                if (!_settingAsset)
                {
                    SelectAvatar(evt.newValue as GameObject);
                }
            });
            avatarRow.Add(_avatarField);
            root.Add(avatarRow);

            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.paddingLeft = 8f;
            toolbar.style.paddingRight = 8f;
            toolbar.style.paddingTop = 6f;
            toolbar.style.paddingBottom = 8f;
            _sourceFbx = UiTextFactory.Create(
                string.Empty,
                UiClassNames.SecondaryText);
            _sourceFbx.style.flexGrow = 1f;
            _sourceFbx.style.minWidth = 0f;
            toolbar.Add(_sourceFbx);

            _save = UiTextFactory.CreateButton(
                I18N.Get("presetWindow.save"),
                Save);
            _save.style.marginLeft = 8f;
            _save.style.flexShrink = 0f;
            toolbar.Add(_save);
            _reclassify = UiTextFactory.CreateButton(
                I18N.Get("presetWindow.reclassify"),
                Reclassify);
            _reclassify.style.marginLeft = 4f;
            _reclassify.style.flexShrink = 0f;
            toolbar.Add(_reclassify);
            root.Add(toolbar);

            var searchRow = new VisualElement();
            searchRow.style.flexDirection = FlexDirection.Row;
            searchRow.style.paddingLeft = 8f;
            searchRow.style.paddingRight = 8f;
            searchRow.style.paddingBottom = 6f;
            _searchField = UiTextFactory.CreateTextField(
                I18N.Get("presetWindow.search"));
            _searchField.style.flexGrow = 1f;
            _searchField.RegisterValueChangedCallback(_ => RefreshFilter());
            searchRow.Add(_searchField);
            _status = UiTextFactory.Create(string.Empty, UiClassNames.SecondaryText);
            _status.style.marginLeft = 12f;
            searchRow.Add(_status);
            root.Add(searchRow);

            var workspace = new VisualElement();
            workspace.style.flexDirection = FlexDirection.Row;
            workspace.style.flexGrow = 1f;
            workspace.style.minHeight = 0f;
            _groupList = new ScrollView(ScrollViewMode.Vertical);
            _groupList.style.width = 280f;
            _groupList.style.flexShrink = 0f;
            _groupList.style.paddingLeft = 8f;
            _groupList.style.paddingRight = 8f;
            workspace.Add(_groupList);

            var detail = new VisualElement();
            detail.style.flexGrow = 1f;
            detail.style.minWidth = 0f;
            var roleRow = new VisualElement();
            roleRow.style.flexDirection = FlexDirection.Row;
            roleRow.style.alignItems = Align.Center;
            roleRow.style.paddingLeft = 8f;
            roleRow.style.paddingRight = 8f;
            roleRow.style.paddingBottom = 6f;
            _detailTitle = UiTextFactory.Create(
                string.Empty,
                UiClassNames.SectionTitle);
            _detailTitle.style.flexShrink = 0f;
            roleRow.Add(_detailTitle);
            _roleEditor = UiTextFactory.CreateTextField();
            _roleEditor.style.width = 260f;
            _roleEditor.style.marginLeft = 4f;
            _roleEditor.RegisterCallback<FocusOutEvent>(_ => RenameSelectedRole());
            roleRow.Add(_roleEditor);
            detail.Add(roleRow);
            detail.Add(CreateDetailHeader());
            _list = new ListView
            {
                itemsSource = _filtered,
                fixedItemHeight = 30f,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.None,
                makeItem = () => new MappingRow(
                    MarkDirty,
                    CreateMappingPayload,
                    DropOnMapping),
                bindItem = (element, index) =>
                    ((MappingRow)element).Bind(_filtered[index])
            };
            _list.style.flexGrow = 1f;
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
            _state = BlendShapeNamePresetSetting.Parse(
                _settings.Get(FaceExpressionSettings.BlendShapePresets));
        }

        private void SelectAvatar(GameObject avatar)
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
                : I18N.Get("presetWindow.loaded"));
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
                    var heading = UiTextFactory.Create(
                        group.SectionName,
                        UiClassNames.SectionTitle);
                    heading.style.marginTop = 10f;
                    heading.style.marginBottom = 4f;
                    _groupList.Add(heading);
                }

                var card = UiTextFactory.CreateButton(
                    string.Format(
                        I18N.Get("presetWindow.roleCount"),
                        GetRoleLabel(group),
                        group.Mappings.Count),
                    () => SelectRoleGroup(group.Key));
                card.TextElement.SetTextAlign(TextAnchor.MiddleLeft);
                card.style.marginBottom = 3f;
                card.style.backgroundColor = ReferenceEquals(group, selected)
                    ? (Color)UiColorTokens.Selection
                    : (Color)UiColorTokens.SurfaceRaised;
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

            var card = UiTextFactory.CreateButton("+");
            card.style.marginBottom = 8f;
            card.style.backgroundColor = (Color)UiColorTokens.SurfaceRaised;
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

            BlendShapeNamePresetSetting.Upsert(_state, _draft);
            _settings.Set(
                FaceExpressionSettings.BlendShapePresets,
                BlendShapeNamePresetSetting.Serialize(_state));
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

            if (!ReferenceEquals(args.Definition, FaceExpressionSettings.BlendShapePresets))
            {
                return;
            }

            LoadState();
            if (_selectedAvatar != null && !_dirty)
            {
                SelectAvatar(_selectedAvatar);
            }
        }

        private void Rebuild()
        {
            if (rootVisualElement.panel != null)
            {
                BuildContent();
                Refresh();
            }
        }

        private void SetStatus(string value)
        {
            _status?.SetText(value ?? string.Empty);
        }

        private static BlendShapeFbxPreset Clone(BlendShapeFbxPreset preset)
        {
            return JsonUtility.FromJson<BlendShapeFbxPreset>(
                JsonUtility.ToJson(preset));
        }

        private static void RegisterPresetDrag(
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

        private static void RegisterPresetDrop(
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

        private sealed class MappingDragPayload
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

        private sealed class MappingRow : VisualElement
        {
            private readonly Action _changed;
            private readonly Func<BlendShapeNameMapping, MappingDragPayload>
                _createPayload;
            private readonly Action<MappingDragPayload, BlendShapeNameMapping>
                _drop;
            private readonly UiTextElement _source;
            private readonly TextField _variation;
            private readonly PopupField<string> _side;
            private BlendShapeNameMapping _mapping;
            private bool _binding;

            internal MappingRow(
                Action changed,
                Func<BlendShapeNameMapping, MappingDragPayload> createPayload,
                Action<MappingDragPayload, BlendShapeNameMapping> drop)
            {
                _changed = changed;
                _createPayload = createPayload;
                _drop = drop;
                style.flexDirection = FlexDirection.Row;
                style.alignItems = Align.Center;
                style.paddingLeft = 8f;
                style.paddingRight = 8f;

                _source = UiTextFactory.Create();
                Configure(_source, 2.2f);
                Add(_source);
                RegisterPresetDrag(
                    _source,
                    () => _createPayload?.Invoke(_mapping));
                RegisterPresetDrop(
                    this,
                    payload =>
                    {
                        var target = _createPayload?.Invoke(_mapping);
                        return target != null && string.Equals(
                            target.SectionKey,
                            payload.SectionKey,
                            StringComparison.Ordinal);
                    },
                    payload => _drop?.Invoke(payload, _mapping));
                _variation = UiTextFactory.CreateTextField();
                Configure(_variation, 1f);
                _variation.RegisterValueChangedCallback(evt => Change(
                    mapping => mapping.variation = evt.newValue));
                Add(_variation);
                _side = UiTextFactory.CreatePopupField(
                    string.Empty,
                    SideChoices,
                    0,
                    value => string.IsNullOrEmpty(value) ? "-" : value,
                    value => string.IsNullOrEmpty(value) ? "-" : value);
                Configure(_side, 0.55f);
                _side.RegisterValueChangedCallback(evt => Change(
                    mapping => mapping.side = evt.newValue));
                Add(_side);
            }

            internal void Bind(BlendShapeNameMapping mapping)
            {
                _binding = true;
                _mapping = mapping;
                _source.SetText(mapping.meshName + " / " + mapping.shapeName);
                _variation.SetValueWithoutNotify(mapping.variation ?? string.Empty);
                var side = SideChoices.Contains(mapping.side) ? mapping.side : string.Empty;
                _side.SetValueWithoutNotify(side);
                _binding = false;
            }

            private void Change(Action<BlendShapeNameMapping> change)
            {
                if (_binding || _mapping == null)
                {
                    return;
                }

                change(_mapping);
                _changed?.Invoke();
            }

            private static void Configure(VisualElement element, float grow)
            {
                element.style.flexBasis = 0f;
                element.style.flexGrow = grow;
                element.style.marginRight = 4f;
                element.style.minWidth = 0f;
            }
        }
    }
}
