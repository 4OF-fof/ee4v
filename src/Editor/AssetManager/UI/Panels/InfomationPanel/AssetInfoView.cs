using System;
using System.Collections.Generic;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetInfoState
    {
        internal AssetInfoState(
            string itemId,
            string name,
            string description,
            IReadOnlyList<string> tagNames,
            int fileCount,
            string totalFileSize,
            string fileTypes,
            string sources,
            string createdAt,
            string updatedAt,
            bool showTags = true,
            bool canAddFile = true,
            IReadOnlyList<string> availableTagNames = null)
        {
            ItemId = itemId ?? string.Empty;
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            TagNames = tagNames ?? Array.Empty<string>();
            FileCount = Math.Max(0, fileCount);
            TotalFileSize = totalFileSize ?? string.Empty;
            FileTypes = fileTypes ?? string.Empty;
            Sources = sources ?? string.Empty;
            CreatedAt = createdAt ?? string.Empty;
            UpdatedAt = updatedAt ?? string.Empty;
            ShowTags = showTags;
            CanAddFile = canAddFile;
            AvailableTagNames = availableTagNames ?? TagNames;
        }

        internal string ItemId { get; }
        internal string Name { get; }
        internal string Description { get; }
        internal IReadOnlyList<string> TagNames { get; }
        internal int FileCount { get; }
        internal string TotalFileSize { get; }
        internal string FileTypes { get; }
        internal string Sources { get; }
        internal string CreatedAt { get; }
        internal string UpdatedAt { get; }
        internal bool ShowTags { get; }
        internal bool CanAddFile { get; }
        internal IReadOnlyList<string> AvailableTagNames { get; }
    }

    internal sealed class AssetInfoEditRequest
    {
        internal AssetInfoEditRequest(
            string name,
            string description,
            IReadOnlyList<string> tagNames)
        {
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            TagNames = tagNames ?? Array.Empty<string>();
        }

        internal string Name { get; }
        internal string Description { get; }
        internal IReadOnlyList<string> TagNames { get; }
    }

    internal sealed class AssetInfoView : VisualElement
    {
        private const string RootClassName =
            "ee4v-asset-manager-asset-info";
        private const string FieldClassName =
            "ee4v-asset-manager-asset-info__field";
        private const string LabelClassName =
            "ee4v-asset-manager-asset-info__label";
        private const string SectionClassName =
            "ee4v-asset-manager-asset-info__section";
        private const string SectionTitleClassName =
            "ee4v-asset-manager-asset-info__section-title";
        private const string MetadataClassName =
            "ee4v-asset-manager-asset-info__metadata";
        private const string MetadataRowClassName =
            "ee4v-asset-manager-asset-info__metadata-row";
        private const string MetadataLabelClassName =
            "ee4v-asset-manager-asset-info__metadata-label";
        private const string MetadataValueClassName =
            "ee4v-asset-manager-asset-info__metadata-value";
        private const string FeedbackClassName =
            "ee4v-asset-manager-asset-info__feedback";
        private const string ErrorClassName =
            "ee4v-asset-manager-asset-info__feedback--error";
        private const string ActionsClassName =
            "ee4v-asset-manager-asset-info__actions";

        private readonly InputField _nameField;
        private readonly InputField _descriptionField;
        private readonly AssetTagField _tagsField;
        private readonly VisualElement _tagsFieldContainer;
        private readonly UiTextElement _fileCount;
        private readonly UiTextElement _totalFileSize;
        private readonly UiTextElement _fileTypes;
        private readonly UiTextElement _sources;
        private readonly UiTextElement _createdAt;
        private readonly UiTextElement _updatedAt;
        private readonly UiTextElement _feedback;
        private readonly UiButton _addFileButton;
        private readonly VisualElement _actions;
        private AssetInfoState _state;

        internal AssetInfoView()
        {
            AddToClassList(RootClassName);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.horizontalScrollerVisibility =
                ScrollerVisibility.Hidden;
            Add(scroll);

            _nameField = new InputField(new InputFieldState(
                placeholder: I18N.Get(
                    "assetManager.assetInfo.namePlaceholder")));
            scroll.Add(CreateField(
                I18N.Get("assetManager.assetInfo.name"),
                _nameField));

            _descriptionField = new InputField(new InputFieldState(
                multiline: true,
                maxHeight: 144f,
                placeholder: I18N.Get(
                    "assetManager.assetInfo.descriptionPlaceholder")));
            scroll.Add(CreateField(
                I18N.Get("assetManager.assetInfo.description"),
                _descriptionField));

            _tagsField = new AssetTagField();
            _tagsFieldContainer = CreateSection(
                I18N.Get("assetManager.assetInfo.tags"),
                _tagsField);
            scroll.Add(_tagsFieldContainer);

            _nameField.RegisterCallback<FocusOutEvent>(
                _ => SubmitIfChanged());
            _descriptionField.RegisterCallback<FocusOutEvent>(
                _ => SubmitIfChanged());
            _tagsField.ValuesCommitted += SubmitIfChanged;

            var metadata = new VisualElement();
            metadata.AddToClassList(MetadataClassName);
            _fileCount = AddMetadataRow(
                metadata,
                I18N.Get("assetManager.assetInfo.files"));
            _totalFileSize = AddMetadataRow(
                metadata,
                I18N.Get("assetManager.assetInfo.totalFileSize"));
            _fileTypes = AddMetadataRow(
                metadata,
                I18N.Get("assetManager.assetInfo.fileTypes"));
            _sources = AddMetadataRow(
                metadata,
                I18N.Get("assetManager.assetInfo.sources"));
            _createdAt = AddMetadataRow(
                metadata,
                I18N.Get("assetManager.assetInfo.createdAt"));
            _updatedAt = AddMetadataRow(
                metadata,
                I18N.Get("assetManager.assetInfo.updatedAt"));
            scroll.Add(CreateSection(
                I18N.Get("assetManager.assetInfo.information"),
                metadata));

            _feedback = UiTextFactory.Create(
                string.Empty,
                UiClassNames.FormError,
                FeedbackClassName);
            _feedback.SetWhiteSpace(WhiteSpace.Normal);
            scroll.Add(_feedback);

            _actions = new VisualElement();
            _actions.AddToClassList(ActionsClassName);
            _addFileButton = new UiButton(
                new UiButtonState(
                    I18N.Get("assetManager.assetInfo.addFile"),
                    iconState: IconState.FromFluentIcon(
                        UiFluentIcon.Attach,
                        UiSizeTokens.Size12),
                    variant: UiButtonVariant.Ghost),
                () => AddFileRequested?.Invoke());
            _actions.Add(_addFileButton);
            scroll.Add(_actions);

            SetState(null);
        }

        internal event Action<AssetInfoEditRequest> UpdateRequested;
        internal event Action AddFileRequested;

        internal void SetState(AssetInfoState state)
        {
            _state = state;
            style.display = state == null
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            if (state == null)
            {
                _nameField.SetValueWithoutNotify(string.Empty);
                _descriptionField.SetValueWithoutNotify(string.Empty);
                _tagsField.SetValues(
                    Array.Empty<string>(),
                    Array.Empty<string>());
                ClearFeedback();
                return;
            }

            _nameField.SetValueWithoutNotify(state.Name);
            _descriptionField.SetValueWithoutNotify(
                state.Description);
            _tagsField.SetValues(
                state.AvailableTagNames,
                state.TagNames);
            _tagsFieldContainer.style.display = state.ShowTags
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _actions.style.display = state.CanAddFile
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _fileCount.SetText(state.FileCount.ToString());
            _totalFileSize.SetText(state.TotalFileSize);
            _fileTypes.SetText(state.FileTypes);
            _sources.SetText(state.Sources);
            _createdAt.SetText(state.CreatedAt);
            _updatedAt.SetText(state.UpdatedAt);
            ClearFeedback();
        }

        internal void SetError(string message)
        {
            _feedback.EnableInClassList(ErrorClassName, true);
            _feedback.SetText(message ?? string.Empty);
        }

        internal void SetNotice(string message)
        {
            _feedback.EnableInClassList(ErrorClassName, false);
            _feedback.SetText(message ?? string.Empty);
        }

        private void SubmitIfChanged()
        {
            if (_state == null)
            {
                return;
            }

            var name = (_nameField.Value ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                SetError(I18N.Get(
                    "assetManager.assetInfo.error.nameRequired"));
                return;
            }

            var request = new AssetInfoEditRequest(
                name,
                _descriptionField.Value,
                _tagsField.Values);
            if (!HasChanges(request, _state))
            {
                return;
            }

            UpdateRequested?.Invoke(request);
        }

        internal static bool HasChanges(
            AssetInfoEditRequest request,
            AssetInfoState state)
        {
            if (!string.Equals(
                    request.Name,
                    state.Name,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    request.Description,
                    state.Description,
                    StringComparison.Ordinal) ||
                request.TagNames.Count != state.TagNames.Count)
            {
                return true;
            }

            for (var i = 0; i < request.TagNames.Count; i++)
            {
                if (!string.Equals(
                        request.TagNames[i],
                        state.TagNames[i],
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearFeedback()
        {
            _feedback.EnableInClassList(ErrorClassName, false);
            _feedback.SetText(string.Empty);
        }

        private static VisualElement CreateField(
            string label,
            VisualElement field)
        {
            var container = new VisualElement();
            container.AddToClassList(FieldClassName);
            container.Add(UiTextFactory.Create(
                label,
                UiClassNames.FormLabel,
                LabelClassName));
            container.Add(field);
            return container;
        }

        private static VisualElement CreateSection(
            string title,
            VisualElement content)
        {
            var section = new VisualElement();
            section.AddToClassList(SectionClassName);
            section.Add(UiTextFactory.Create(
                title,
                UiClassNames.SectionTitle,
                SectionTitleClassName));
            section.Add(content);
            return section;
        }

        private static UiTextElement AddMetadataRow(
            VisualElement parent,
            string label)
        {
            var row = new VisualElement();
            row.AddToClassList(MetadataRowClassName);
            row.Add(UiTextFactory.Create(
                label,
                UiClassNames.FormLabel,
                MetadataLabelClassName));
            var value = UiTextFactory.Create(
                string.Empty,
                MetadataValueClassName);
            value.SetWhiteSpace(WhiteSpace.Normal);
            row.Add(value);
            parent.Add(row);
            return value;
        }
    }
}
