using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ee4v.AvatarModify.Application;
using Ee4v.AvatarModify.Domain;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AvatarModify.UI
{
    internal sealed class AvatarVariantCreationPopup : EditorWindow
    {
        private const float PopupWidth = 520f;
        private const float PopupBaseHeight = 203f;
        internal const float PrefabRowHeight =
            UiSizeTokens.Size31 +
            UiSpacingTokens.Xxs * 2f;
        internal const int MaximumVisiblePrefabRows = 4;
        private AvatarModifyService _service;
        private string _itemId;
        private string _destinationRoot;
        private AvatarVariantCreationView _view;
        private AvatarVariantCreation _creation;

        internal static void Open(
            AvatarModifyService service,
            string itemId,
            string destinationRoot,
            float screenX,
            float screenY)
        {
            var popup = CreateInstance<AvatarVariantCreationPopup>();
            popup._service = service ??
                throw new ArgumentNullException(nameof(service));
            popup._itemId = itemId ?? string.Empty;
            popup._destinationRoot = destinationRoot ?? string.Empty;
            popup._creation = service.GetCreation(
                popup._itemId);
            popup.ShowAsDropDown(
                new Rect(screenX, screenY, 1f, 1f),
                CalculateWindowSize(
                    popup._creation.Candidates.Count));
        }

        internal static Vector2 CalculateWindowSize(
            int prefabCount)
        {
            var visibleRows = Mathf.Clamp(
                prefabCount,
                1,
                MaximumVisiblePrefabRows);
            return new Vector2(
                PopupWidth,
                PopupBaseHeight +
                visibleRows * PrefabRowHeight);
        }

        private void CreateGUI()
        {
            rootVisualElement.AddToClassList("ee4v-ui");
            rootVisualElement.AddToClassList(
                UiClassNames.PopupSurface);
            rootVisualElement.AddToClassList(
                "ee4v-avatar-variant-popup");
            AddStyle("Editor/UI/Components/common.uss");
            AddStyle(
                "Editor/UI/Components/Inputs/Button/ui-button.uss");
            AddStyle(
                "Editor/UI/Components/Inputs/InputField/input-field.uss");
            AddStyle(
                "Editor/AvatarModify/UI/avatar-variant-popup.uss");

            _creation = _creation ??
                _service.GetCreation(_itemId);
            var text = CreateText();
            _view = new AvatarVariantCreationView(text);
            var popup = new PopupLayout(
                _view,
                new PopupActionState(
                    text.Cancel,
                    Close),
                new PopupActionState(
                    text.Create,
                    CreateVariant,
                    enabled: false));
            _view.CreateAvailabilityChanged +=
                popup.SetPrimaryActionEnabled;
            rootVisualElement.Add(popup);
            Render(string.Empty);
            _view.schedule.Execute(
                _view.FocusVariantName);
        }

        private void CreateVariant()
        {
            var result = _service.CreateVariant(
                new CreateAvatarVariantRequest
                {
                    ItemId = _itemId,
                    SourcePrefabGuid = _view.PrefabGuid,
                    VariantName = _view.VariantName,
                    DestinationRoot = _destinationRoot
                });
            if (!result.Succeeded)
            {
                _view.SetStatus(I18N.Get(
                    "status.failed",
                    result.Error));
                return;
            }

            var path = AssetDatabase.GUIDToAssetPath(
                result.VariantPrefabGuid);
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }

            Close();
        }

        private void Render(string status)
        {
            var candidates = _creation?.Candidates ??
                             Array.Empty<PrefabCandidate>();
            _view.SetState(
                new AvatarVariantCreationViewState
                {
                    Prefabs = candidates
                        .Select(candidate =>
                            new AvatarVariantOption(
                                candidate.AssetGuid,
                                Path.GetFileNameWithoutExtension(
                                    candidate.AssetPath),
                                candidate.HasAvatarDescriptor
                                    ? I18N.Get(
                                        "option.avatarDescriptor",
                                        GetDirectory(candidate.AssetPath))
                                    : GetDirectory(
                                        candidate.AssetPath),
                                candidate.AssetPath))
                        .ToArray(),
                    PrefabGuid =
                        _creation?.SelectedPrefabGuid,
                    VariantName = string.Empty,
                    Status = string.IsNullOrWhiteSpace(status) &&
                             candidates.Count == 0
                        ? I18N.Get("status.prefabMissing")
                        : status,
                    CanCreate = candidates.Count > 0
                });
        }

        private static string GetDirectory(string assetPath)
        {
            return (Path.GetDirectoryName(assetPath) ??
                    string.Empty).Replace('\\', '/');
        }

        private void AddStyle(string path)
        {
            UiStyleUtility.AddPackageStyleSheet(
                rootVisualElement,
                path);
        }

        internal static AvatarVariantCreationText CreateText()
        {
            return new AvatarVariantCreationText
            {
                Title = I18N.Get("popup.title"),
                Description = I18N.Get("popup.description"),
                Prefab = I18N.Get("field.prefab"),
                VariantName = I18N.Get("field.variantName"),
                Cancel = I18N.Get("action.cancel"),
                Create = I18N.Get("action.createVariant")
            };
        }
    }

    internal sealed class AvatarVariantOption
    {
        internal AvatarVariantOption(
            string id,
            string label,
            string detail,
            string tooltip)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            Detail = detail ?? string.Empty;
            Tooltip = tooltip ?? string.Empty;
        }

        internal string Id { get; }
        internal string Label { get; }
        internal string Detail { get; }
        internal string Tooltip { get; }
    }

    internal sealed class AvatarVariantCreationViewState
    {
        internal IReadOnlyList<AvatarVariantOption> Prefabs { get; set; } =
            Array.Empty<AvatarVariantOption>();
        internal string PrefabGuid { get; set; }
        internal string VariantName { get; set; }
        internal string Status { get; set; }
        internal bool CanCreate { get; set; }
    }

    internal sealed class AvatarVariantCreationText
    {
        internal string Title { get; set; }
        internal string Description { get; set; }
        internal string Prefab { get; set; }
        internal string VariantName { get; set; }
        internal string Cancel { get; set; }
        internal string Create { get; set; }
    }

    internal sealed class AvatarVariantCreationView : VisualElement
    {
        private readonly AvatarVariantCreationText _text;
        private readonly ScrollView _prefabs =
            new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility =
                    ScrollerVisibility.Hidden,
                verticalScrollerVisibility =
                    ScrollerVisibility.Hidden
            };
        private readonly InputField _name =
            new InputField(new InputFieldState());
        private readonly UiTextElement _status;
        private bool _canCreate;
        private string _selectedPrefabGuid = string.Empty;
        private readonly List<UiButton> _prefabButtons =
            new List<UiButton>();

        internal AvatarVariantCreationView(
            AvatarVariantCreationText text)
        {
            _text = text ??
                throw new ArgumentNullException(nameof(text));
            AddToClassList("ee4v-avatar-variant-popup__content");
            Add(UiTextFactory.Create(
                _text.Title,
                UiClassNames.SectionTitle));
            Add(UiTextFactory.Create(
                _text.Description,
                UiClassNames.SecondaryText,
                "ee4v-avatar-variant-popup__description"));
            AddPrefabSelector();
            AddField(_text.VariantName, _name);

            _status = UiTextFactory.Create(
                string.Empty,
                UiClassNames.SecondaryText,
                "ee4v-avatar-variant-popup__status");
            Add(_status);

            _name.ValueChanged +=
                _ => RefreshCreateInteraction();
        }

        internal event Action<bool> CreateAvailabilityChanged;

        internal string PrefabGuid =>
            _selectedPrefabGuid;

        internal string VariantName =>
            _name.Value?.Trim() ?? string.Empty;

        internal void SetState(
            AvatarVariantCreationViewState state)
        {
            state = state ??
                    new AvatarVariantCreationViewState();
            RebuildPrefabOptions(
                state.Prefabs ??
                Array.Empty<AvatarVariantOption>(),
                state.PrefabGuid);
            _name.SetValueWithoutNotify(
                state.VariantName ?? string.Empty);
            _status.SetText(state.Status ?? string.Empty);
            _canCreate = state.CanCreate;
            RefreshCreateInteraction();
        }

        internal void SetStatus(string status)
        {
            _status.SetText(status ?? string.Empty);
        }

        internal void FocusVariantName()
        {
            _name.FocusInput();
        }

        private void RefreshCreateInteraction()
        {
            CreateAvailabilityChanged?.Invoke(
                _canCreate &&
                !string.IsNullOrWhiteSpace(PrefabGuid) &&
                !string.IsNullOrWhiteSpace(VariantName));
        }

        private void AddPrefabSelector()
        {
            var field = new VisualElement();
            field.AddToClassList(
                "ee4v-avatar-variant-popup__selector-field");
            field.Add(UiTextFactory.Create(
                _text.Prefab,
                UiClassNames.FormLabel,
                "ee4v-avatar-variant-popup__selector-label"));
            _prefabs.AddToClassList(
                "ee4v-avatar-variant-popup__prefabs");
            field.Add(_prefabs);
            Add(field);
        }

        private void RebuildPrefabOptions(
            IReadOnlyList<AvatarVariantOption> options,
            string selectedPrefabGuid)
        {
            _prefabs.Clear();
            _prefabButtons.Clear();
            _prefabs.style.height =
                AvatarVariantCreationPopup.PrefabRowHeight *
                Mathf.Clamp(
                    options.Count,
                    1,
                    AvatarVariantCreationPopup
                        .MaximumVisiblePrefabRows);
            _selectedPrefabGuid = options.Any(option =>
                    option.Id == selectedPrefabGuid)
                ? selectedPrefabGuid
                : options.FirstOrDefault()?.Id ??
                  string.Empty;
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var button = new UiButton(
                    new UiButtonState(
                        option.Label,
                        option.Detail,
                        option.Tooltip,
                        selected: option.Id ==
                                  _selectedPrefabGuid,
                        variant: UiButtonVariant.Ghost),
                    () => SelectPrefab(option.Id));
                button.userData = option.Id;
                button.AddToClassList(
                    "ee4v-avatar-variant-popup__prefab");
                _prefabButtons.Add(button);
                _prefabs.Add(button);
            }
        }

        private void SelectPrefab(string prefabGuid)
        {
            _selectedPrefabGuid = prefabGuid ??
                                  string.Empty;
            for (var i = 0;
                 i < _prefabButtons.Count;
                 i++)
            {
                var optionId =
                    _prefabButtons[i].userData as string;
                _prefabButtons[i].SetSelected(
                    optionId == _selectedPrefabGuid);
            }

            RefreshCreateInteraction();
        }

        private void AddField(
            string label,
            VisualElement field)
        {
            var row = new VisualElement();
            row.AddToClassList(
                "ee4v-avatar-variant-popup__field");
            row.Add(UiTextFactory.Create(
                label,
                UiClassNames.FormLabel,
                "ee4v-avatar-variant-popup__label"));
            field.AddToClassList(
                "ee4v-avatar-variant-popup__control");
            row.Add(field);
            Add(row);
        }
    }
}
