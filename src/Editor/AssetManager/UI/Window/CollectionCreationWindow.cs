using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class CollectionCreationWindow : EditorWindow
    {
        private const float RegularWidth = 400f;
        private const float SmartWidth = 620f;
        private const float RegularInitialHeight = 280f;
        private const float SmartInitialHeight = 520f;
        private const float MinimumPopupHeight = 160f;
        private const float MaximumPopupHeight = 676f;
        private const string RootClassName =
            "ee4v-collection-creation-window";
        private const string FormClassName =
            "ee4v-collection-creation-window__form";
        private const string TitleClassName =
            "ee4v-collection-creation-window__title";
        private const string FieldClassName =
            "ee4v-collection-creation-window__field";
        private const string LabelClassName =
            "ee4v-collection-creation-window__label";
        private const string ConditionsClassName =
            "ee4v-collection-creation-window__conditions";
        private const string AddConditionClassName =
            "ee4v-collection-creation-window__add-condition";
        private const string ConditionClassName =
            "ee4v-collection-creation-window__condition";
        private const string ConditionControlsClassName =
            "ee4v-collection-creation-window__condition-controls";
        private const string ConditionQueryClassName =
            "ee4v-collection-creation-window__condition-query";
        private const string ErrorClassName =
            "ee4v-collection-creation-window__error";
        private const string ActionsClassName =
            "ee4v-collection-creation-window__actions";

        private CollectionWindowMode _mode;
        private bool _smart;
        private AssetCollection _initialCollection;
        private Action<CreateCollectionRequest> _createCollection;
        private Action<CreateSmartCollectionRequest> _createSmartCollection;
        private Action<string, UpdateCollectionRequest> _updateCollection;
        private Action<string, UpdateSmartCollectionRequest>
            _updateSmartCollection;
        private InputField _nameField;
        private AssetCollectionIconSelector _iconField;
        private PopupField<SmartCollectionMatchMode> _matchModeField;
        private ScrollView _form;
        private VisualElement _conditions;
        private UiTextElement _error;
        private bool _popupSizeRefreshQueued;
        private readonly List<ConditionRow> _conditionRows =
            new List<ConditionRow>();

        public static CollectionCreationWindow Show(
            VisualElement anchor,
            bool smart,
            Action<CreateCollectionRequest> createCollection,
            Action<CreateSmartCollectionRequest> createSmartCollection)
        {
            var window = CreateInstance<CollectionCreationWindow>();
            window._mode = CollectionWindowMode.Create;
            window._smart = smart;
            window._createCollection = createCollection;
            window._createSmartCollection = createSmartCollection;
            return ShowConfigured(anchor, null, window);
        }

        public static CollectionCreationWindow ShowRename(
            VisualElement anchor,
            Vector2 panelPosition,
            AssetCollection collection,
            Action<string, UpdateCollectionRequest> updateCollection)
        {
            if (collection == null)
            {
                return null;
            }

            var window = CreateInstance<CollectionCreationWindow>();
            window._mode = CollectionWindowMode.Rename;
            window._smart = collection.IsSmartCollection;
            window._initialCollection = collection;
            window._updateCollection = updateCollection;
            return ShowConfigured(anchor, panelPosition, window);
        }

        public static CollectionCreationWindow ShowSmartConditions(
            VisualElement anchor,
            Vector2 panelPosition,
            AssetCollection collection,
            Action<string, UpdateSmartCollectionRequest>
                updateSmartCollection)
        {
            if (collection == null ||
                !collection.IsSmartCollection)
            {
                return null;
            }

            var window = CreateInstance<CollectionCreationWindow>();
            window._mode = CollectionWindowMode.SmartConditions;
            window._smart = true;
            window._initialCollection = collection;
            window._updateSmartCollection =
                updateSmartCollection;
            return ShowConfigured(anchor, panelPosition, window);
        }

        private static CollectionCreationWindow ShowConfigured(
            VisualElement anchor,
            Vector2? panelPosition,
            CollectionCreationWindow window)
        {
            CloseExistingWindows(window);
            anchor?.Blur();
            window.titleContent = new GUIContent(
                window.GetTitleText());
            var size = CalculateInitialSize(
                window._smart,
                window._mode);
            window.minSize = size;
            window.maxSize = size;
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
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/Inputs/Button/ui-button.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/Inputs/InputField/input-field.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/UI/Components/Content/Icon/icon.uss");
            UiStyleUtility.AddPackageStyleSheet(
                root,
                "Editor/AssetManager/UI/Window/collection-creation-window.uss");

            _form = new ScrollView(ScrollViewMode.Vertical)
            {
                verticalScrollerVisibility = ScrollerVisibility.Auto,
                horizontalScrollerVisibility = ScrollerVisibility.Hidden
            };
            _form.AddToClassList(FormClassName);
            _form.contentContainer.RegisterCallback<
                GeometryChangedEvent>(OnFormContentGeometryChanged);
            root.Add(_form);

            _form.Add(UiTextFactory.Create(
                GetTitleText(),
                UiClassNames.SectionTitle,
                TitleClassName));

            if (_mode != CollectionWindowMode.SmartConditions)
            {
                _nameField = new InputField(new InputFieldState(
                    value: _initialCollection != null
                        ? _initialCollection.Name
                        : string.Empty,
                    placeholder: I18N.Get(
                        "assetManager.collectionCreation.namePlaceholder")));
                _form.Add(CreateField(
                    I18N.Get("assetManager.collectionCreation.name"),
                    _nameField));
            }

            if (_smart &&
                _mode != CollectionWindowMode.SmartConditions)
            {
                _iconField = new AssetCollectionIconSelector(
                    _initialCollection != null
                        ? _initialCollection.Icon
                        : AssetCollectionIcon.Search,
                    _initialCollection != null
                        ? _initialCollection.IconAssetGuid
                        : null);
                _form.Add(CreateField(
                    I18N.Get(
                        "assetManager.collectionCreation.iconLabel"),
                    _iconField));
            }

            if (_smart &&
                _mode != CollectionWindowMode.Rename)
            {
                BuildSmartCollectionFields(_form);
            }

            _error = UiTextFactory.Create(
                string.Empty,
                UiClassNames.FormError,
                ErrorClassName);
            _error.SetWhiteSpace(WhiteSpace.Normal);
            _form.Add(_error);

            var actions = new VisualElement();
            actions.AddToClassList(ActionsClassName);
            actions.Add(new UiButton(
                new UiButtonState(
                    I18N.Get(
                        "assetManager.collectionCreation.cancel"),
                    variant: UiButtonVariant.Ghost),
                Close));
            actions.Add(new UiButton(
                new UiButtonState(
                    GetSubmitLabel()),
                Submit));
            _form.Add(actions);
            QueuePopupSizeRefresh();
        }

        private void BuildSmartCollectionFields(VisualElement form)
        {
            var matchModes = Enum.GetValues(
                    typeof(SmartCollectionMatchMode))
                .Cast<SmartCollectionMatchMode>()
                .ToList();
            _matchModeField =
                new PopupField<SmartCollectionMatchMode>(
                    matchModes,
                    0,
                    FormatMatchMode,
                    FormatMatchMode);
            if (_initialCollection != null &&
                _initialCollection.SmartRule != null)
            {
                _matchModeField.SetValueWithoutNotify(
                    _initialCollection.SmartRule.MatchMode);
            }
            form.Add(CreateField(
                I18N.Get("assetManager.collectionCreation.matchMode"),
                _matchModeField));

            var conditionField = new VisualElement();
            conditionField.AddToClassList(FieldClassName);
            conditionField.Add(UiTextFactory.Create(
                I18N.Get("assetManager.collectionCreation.conditions"),
                UiClassNames.FormLabel,
                LabelClassName));

            _conditions = new VisualElement();
            _conditions.AddToClassList(ConditionsClassName);
            conditionField.Add(_conditions);
            var addConditionButton = new UiButton(
                new UiButtonState(
                    I18N.Get(
                        "assetManager.collectionCreation.addCondition"),
                    iconState: IconState.FromBuiltinIcon(
                        UiBuiltinIcon.Add,
                        UiSizeTokens.Size12),
                    variant: UiButtonVariant.Ghost));
            addConditionButton.clicked += () =>
                AddConditionFromButton(addConditionButton);
            addConditionButton.AddToClassList(
                AddConditionClassName);
            conditionField.Add(addConditionButton);
            form.Add(conditionField);
            var initialConditions =
                _initialCollection != null &&
                _initialCollection.SmartRule != null
                    ? _initialCollection.SmartRule.Conditions
                    : null;
            if (initialConditions != null &&
                initialConditions.Count > 0)
            {
                for (var i = 0; i < initialConditions.Count; i++)
                {
                    AddCondition(
                        initialConditions[i],
                        refreshPopupSize: false);
                }
            }
            else
            {
                AddCondition(
                    null,
                    refreshPopupSize: false);
            }
        }

        private void AddConditionFromButton(
            UiButton addConditionButton)
        {
            if (addConditionButton != null)
            {
                addConditionButton.Blur();
            }

            AddCondition(null);
            if (addConditionButton != null)
            {
                addConditionButton.schedule.Execute(
                    addConditionButton.Blur);
            }
        }

        private static VisualElement CreateField(
            string labelText,
            VisualElement field)
        {
            var container = new VisualElement();
            container.AddToClassList(FieldClassName);
            container.Add(UiTextFactory.Create(
                labelText,
                UiClassNames.FormLabel,
                LabelClassName));
            container.Add(field);
            return container;
        }

        private void AddCondition(
            SmartCollectionCondition initialCondition,
            bool refreshPopupSize = true)
        {
            var row = new ConditionRow(
                RemoveCondition,
                initialCondition);
            _conditionRows.Add(row);
            _conditions.Add(row.Root);
            if (refreshPopupSize)
            {
                QueuePopupSizeRefresh();
            }
        }

        private void RemoveCondition(ConditionRow row)
        {
            if (row == null)
            {
                return;
            }

            _conditionRows.Remove(row);
            row.Root.RemoveFromHierarchy();
            QueuePopupSizeRefresh();
        }

        private void OnFormContentGeometryChanged(
            GeometryChangedEvent evt)
        {
            if (Mathf.Abs(
                    evt.newRect.height -
                    evt.oldRect.height) > 0.5f)
            {
                QueuePopupSizeRefresh();
            }
        }

        private void QueuePopupSizeRefresh()
        {
            if (_popupSizeRefreshQueued ||
                _form == null ||
                rootVisualElement.panel == null)
            {
                return;
            }

            _popupSizeRefreshQueued = true;
            rootVisualElement.schedule.Execute(() =>
            {
                _popupSizeRefreshQueued = false;
                RefreshPopupSizeFromLayout();
            });
        }

        private void RefreshPopupSizeFromLayout()
        {
            if (_form == null ||
                _form.contentContainer == null)
            {
                return;
            }

            var contentHeight =
                _form.contentContainer.resolvedStyle.height;
            if (float.IsNaN(contentHeight) ||
                contentHeight <= 0f)
            {
                return;
            }

            var rootStyle = rootVisualElement.resolvedStyle;
            var formStyle = _form.resolvedStyle;
            var chromeHeight =
                formStyle.paddingTop +
                formStyle.paddingBottom +
                rootStyle.borderTopWidth +
                rootStyle.borderBottomWidth;
            var size = new Vector2(
                _smart ? SmartWidth : RegularWidth,
                CalculatePopupHeight(
                    contentHeight,
                    chromeHeight));
            var currentPosition = position;

            if (Mathf.Abs(currentPosition.width - size.x) <= 0.5f &&
                Mathf.Abs(currentPosition.height - size.y) <= 0.5f)
            {
                return;
            }

            var nextPosition = CalculateResizedPopupRect(
                currentPosition,
                size);
            minSize = size;
            maxSize = size;
            position = nextPosition;
        }

        internal static Rect CalculateResizedPopupRect(
            Rect currentPosition,
            Vector2 size)
        {
            return new Rect(
                currentPosition.position,
                size);
        }

        internal static float CalculatePopupHeight(
            float contentHeight,
            float chromeHeight)
        {
            var naturalHeight = Mathf.Ceil(
                Mathf.Max(0f, contentHeight) +
                Mathf.Max(0f, chromeHeight) +
                UiSpacingTokens.Xs);
            return Mathf.Clamp(
                naturalHeight,
                MinimumPopupHeight,
                MaximumPopupHeight);
        }

        private static Vector2 CalculateInitialSize(
            bool smart,
            CollectionWindowMode mode)
        {
            return new Vector2(
                smart ? SmartWidth : RegularWidth,
                smart &&
                mode != CollectionWindowMode.Rename
                    ? SmartInitialHeight
                    : RegularInitialHeight);
        }

        private void Submit()
        {
            var name = _nameField != null
                ? (_nameField.Value ?? string.Empty).Trim()
                : string.Empty;
            if (_mode != CollectionWindowMode.SmartConditions &&
                string.IsNullOrWhiteSpace(name))
            {
                SetError(I18N.Get(
                    "assetManager.collectionCreation.error.nameRequired"));
                return;
            }

            if (_mode == CollectionWindowMode.Rename)
            {
                _updateCollection?.Invoke(
                    _initialCollection.Id,
                    new UpdateCollectionRequest
                    {
                        Name = name,
                        Icon = _smart
                            ? _iconField.Value
                            : AssetCollectionIcon.Folder,
                        IconAssetGuid = _smart
                            ? _iconField.AssetGuid
                            : null
                    });
                Close();
                return;
            }

            if (_mode == CollectionWindowMode.Create && !_smart)
            {
                _createCollection?.Invoke(new CreateCollectionRequest
                {
                    Name = name
                });
                Close();
                return;
            }

            if (_conditionRows.Count == 0)
            {
                SetError(I18N.Get(
                    "assetManager.collectionCreation.error.conditionRequired"));
                return;
            }

            var conditions = new List<SmartCollectionCondition>();
            for (var i = 0; i < _conditionRows.Count; i++)
            {
                SmartCollectionCondition condition;
                if (!_conditionRows[i].TryCreate(out condition))
                {
                    SetError(I18N.Get(
                        "assetManager.collectionCreation.error.queryRequired"));
                    return;
                }

                conditions.Add(condition);
            }

            if (_mode == CollectionWindowMode.SmartConditions)
            {
                _updateSmartCollection?.Invoke(
                    _initialCollection.Id,
                    new UpdateSmartCollectionRequest
                    {
                        MatchMode = _matchModeField.value,
                        Conditions = conditions
                    });
            }
            else
            {
                _createSmartCollection?.Invoke(
                    new CreateSmartCollectionRequest
                    {
                        Name = name,
                        Icon = _iconField.Value,
                        IconAssetGuid = _iconField.AssetGuid,
                        MatchMode = _matchModeField.value,
                        Conditions = conditions
                    });
            }

            Close();
        }

        private string GetTitleText()
        {
            switch (_mode)
            {
                case CollectionWindowMode.Rename:
                    return I18N.Get(
                        "assetManager.collectionCreation.renameTitle");
                case CollectionWindowMode.SmartConditions:
                    return I18N.Get(
                        "assetManager.collectionCreation.conditionsTitle");
                default:
                    return _smart
                        ? I18N.Get(
                            "assetManager.collectionCreation.smartTitle")
                        : I18N.Get(
                            "assetManager.collectionCreation.collectionTitle");
            }
        }

        private string GetSubmitLabel()
        {
            return _mode == CollectionWindowMode.Create
                ? I18N.Get(
                    "assetManager.collectionCreation.create")
                : I18N.Get(
                    "assetManager.collectionCreation.save");
        }

        private enum CollectionWindowMode
        {
            Create,
            Rename,
            SmartConditions
        }

        private void SetError(string message)
        {
            _error.SetText(message ?? string.Empty);
            QueuePopupSizeRefresh();
        }

        private static void CloseExistingWindows(
            CollectionCreationWindow except = null)
        {
            var windows =
                Resources.FindObjectsOfTypeAll<
                    CollectionCreationWindow>();
            for (var i = 0; i < windows.Length; i++)
            {
                if (windows[i] != null &&
                    !ReferenceEquals(windows[i], except) &&
                    windows[i].rootVisualElement != null &&
                    windows[i].rootVisualElement.panel != null)
                {
                    windows[i].Close();
                }
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
            var rootOffset =
                root != null
                    ? root.worldBound.position
                    : Vector2.zero;
            var localPosition =
                anchor.worldBound.position - rootOffset;
            var owner = FindOwnerWindow(anchor);
            var screenPosition =
                owner != null
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
                var point =
                    GUIUtility.GUIToScreenPoint(panelPosition);
                return new Rect(point, Vector2.zero);
            }

            var root = anchor.panel.visualTree;
            var rootOffset =
                root != null
                    ? root.worldBound.position
                    : Vector2.zero;
            var owner = FindOwnerWindow(anchor);
            var screenPosition =
                owner != null
                    ? CalculatePointerScreenPosition(
                        owner.position.position,
                        rootOffset,
                        panelPosition)
                    : GUIUtility.GUIToScreenPoint(
                        panelPosition - rootOffset);
            return new Rect(screenPosition, Vector2.zero);
        }

        internal static Vector2 CalculatePointerScreenPosition(
            Vector2 ownerPosition,
            Vector2 rootOffset,
            Vector2 panelPosition)
        {
            return ownerPosition + panelPosition - rootOffset;
        }

        private static EditorWindow FindOwnerWindow(
            VisualElement target)
        {
            var windows =
                Resources.FindObjectsOfTypeAll<EditorWindow>();
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

        private static string FormatMatchMode(
            SmartCollectionMatchMode matchMode)
        {
            return I18N.Get(
                matchMode == SmartCollectionMatchMode.Any
                    ? "assetManager.collectionCreation.matchModeAny"
                    : "assetManager.collectionCreation.matchModeAll");
        }

        private sealed class ConditionRow
        {
            private readonly PopupField<SmartCollectionConditionField>
                _field;
            private readonly PopupField<SmartCollectionConditionOperator>
                _operator;
            private readonly InputField _query;

            public ConditionRow(
                Action<ConditionRow> remove,
                SmartCollectionCondition initialCondition = null)
            {
                Root = new VisualElement();
                Root.AddToClassList(ConditionClassName);

                var controls = new VisualElement();
                controls.AddToClassList(ConditionControlsClassName);

                var fields = Enum.GetValues(
                        typeof(SmartCollectionConditionField))
                    .Cast<SmartCollectionConditionField>()
                    .ToList();
                _field =
                    new PopupField<SmartCollectionConditionField>(
                        fields,
                        0,
                        FormatField,
                        FormatField);

                var operators = Enum.GetValues(
                        typeof(SmartCollectionConditionOperator))
                    .Cast<SmartCollectionConditionOperator>()
                    .ToList();
                _operator =
                    new PopupField<SmartCollectionConditionOperator>(
                        operators,
                        0,
                        FormatOperator,
                        FormatOperator);
                _operator.RegisterValueChangedCallback(_ =>
                    RefreshQueryVisibility());
                if (initialCondition != null)
                {
                    _field.SetValueWithoutNotify(
                        initialCondition.Field);
                    _operator.SetValueWithoutNotify(
                        initialCondition.Operator);
                }

                var removeButton = new UiButton(
                    new UiButtonState(
                        tooltip: I18N.Get(
                            "assetManager.collectionCreation.removeCondition"),
                        iconState: IconState.FromBuiltinIcon(
                            UiBuiltinIcon.Close,
                            UiSizeTokens.Size12),
                        variant: UiButtonVariant.Ghost,
                        size: UiButtonSize.Compact),
                    () => remove?.Invoke(this));

                controls.Add(_field);
                controls.Add(_operator);
                controls.Add(removeButton);
                Root.Add(controls);

                _query = new InputField(new InputFieldState(
                    value: initialCondition != null
                        ? initialCondition.QueryText
                        : string.Empty,
                    placeholder: I18N.Get(
                        "assetManager.collectionCreation.queryPlaceholder")));
                _query.AddToClassList(ConditionQueryClassName);
                Root.Add(_query);
                RefreshQueryVisibility();
            }

            public VisualElement Root { get; }

            public bool TryCreate(
                out SmartCollectionCondition condition)
            {
                var query = (_query.Value ?? string.Empty).Trim();
                if (_operator.value !=
                        SmartCollectionConditionOperator.Exists &&
                    string.IsNullOrWhiteSpace(query))
                {
                    condition = null;
                    return false;
                }

                condition = new SmartCollectionCondition
                {
                    Field = _field.value,
                    Operator = _operator.value,
                    QueryText = _operator.value ==
                                SmartCollectionConditionOperator.Exists
                        ? null
                        : query
                };
                return true;
            }

            private void RefreshQueryVisibility()
            {
                _query.style.display =
                    _operator.value ==
                    SmartCollectionConditionOperator.Exists
                        ? DisplayStyle.None
                        : DisplayStyle.Flex;
            }

            private static string FormatField(
                SmartCollectionConditionField field)
            {
                return I18N.Get(
                    "assetManager.collectionCreation.field." +
                    ToCamelCase(field.ToString()));
            }

            private static string FormatOperator(
                SmartCollectionConditionOperator op)
            {
                return I18N.Get(
                    "assetManager.collectionCreation.operator." +
                    op.ToString().ToLowerInvariant());
            }

            private static string ToCamelCase(string value)
            {
                return string.IsNullOrEmpty(value)
                    ? string.Empty
                    : char.ToLowerInvariant(value[0]) +
                      value.Substring(1);
            }
        }
    }
}
