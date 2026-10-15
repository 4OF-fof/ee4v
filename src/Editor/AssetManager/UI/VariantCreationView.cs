using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class VariantCreationView : VisualElement
    {
        private readonly UiTextElement _destination;
        private readonly bool _original;

        internal VariantCreationView(
            AssetItem item,
            IReadOnlyList<GameObject> prefabs,
            string name,
            string description,
            GameObject selectedPrefab,
            string submitLabel,
            Action goBack,
            bool original = false,
            Action<VisualElement, GameObject, Action<GameObject>> showPrefabPicker = null)
        {
            _original = original;
            AddToClassList("ee4v-variant-creation");
            var header = new VisualElement();
            header.AddToClassList("ee4v-variant-creation__header");
            header.Add(AssetManagerControls.CreateIconButton(
                I18N.Get("toolbar.back"), "arrow_left.png", UiSizeTokens.Size18,
                UiButtonVariant.Ghost, goBack, "ee4v-variant-creation__back"));
            var title = UiTextFactory.Create(I18N.Get(original
                    ? "workflow.original.title" : "variant.creation.title"),
                UiClassNames.SectionTitle, "ee4v-variant-creation__title");
            title.SetFontSize(UiTypographyTokens.TitleFontSize);
            header.Add(title);
            var divider = new VisualElement { pickingMode = PickingMode.Ignore };
            divider.AddToClassList("ee4v-variant-creation__header-divider");
            header.Add(divider);
            var context = new VisualElement();
            context.AddToClassList("ee4v-variant-creation__item-context");
            context.Add(new Icon(FluentUiIcons.CreateState("library.png", UiSizeTokens.Size16,
                tintColor: UiColorTokens.TextMuted)));
            context.Add(UiTextFactory.Create(I18N.Get("workflow.selection.sourceItem"),
                UiClassNames.SecondaryText, "ee4v-variant-creation__item-label"));
            var itemName = item?.Name ?? string.Empty;
            var itemNameText = UiTextFactory.Create(itemName,
                "ee4v-variant-creation__item-name");
            itemNameText.SetWhiteSpace(WhiteSpace.NoWrap);
            itemNameText.tooltip = itemName;
            context.Add(itemNameText);
            header.Add(context);
            divider.style.display = context.style.display = original ? DisplayStyle.None : DisplayStyle.Flex;
            Add(header);

            var body = new VisualElement();
            body.AddToClassList("ee4v-variant-creation__body");
            var source = new VisualElement();
            source.AddToClassList("ee4v-variant-creation__source");
            prefabs = prefabs ?? Array.Empty<GameObject>();
            var pickerSource = new AssetModificationWorkflowView.VariantSourceOption
            {
                Item = item,
                Prefabs = prefabs
            };
            Prefab = new PrefabSelector(prefabs, showPrefabPicker ?? ((anchor, selected, select) =>
                AssetPrefabPickerWindow.ShowForSource(anchor, pickerSource, selected, select)), allowEmpty: original);
            Prefab.AddToClassList("ee4v-variant-creation__prefab");
            UiButton clearBase = null;
            if (original)
            {
                clearBase = AssetManagerControls.CreateIconButton(I18N.Get("workflow.original.clearBase"),
                    "dismiss.png", UiSizeTokens.Size16, UiButtonVariant.Ghost, Prefab.ClearValue,
                    "ee4v-variant-creation__clear-base");
            }
            var sourceToolbar = new FormInput(I18N.Get(original
                ? "workflow.original.optionalBase" : "workflow.selection.sourcePrefab"), Prefab, clearBase);
            sourceToolbar.AddToClassList("ee4v-variant-creation__source-toolbar");
            source.Add(sourceToolbar);
            if (!original && prefabs.Count == 0)
            {
                Prefab.SetEnabled(false);
                source.Add(UiTextFactory.CreateMessagePanel(
                    I18N.Get("notice.derivedAssetPrefabNotFound"), MessageSeverity.Warning));
            }
            var preview = new PrefabScenePreview();
            preview.SetFlexibleLayout(true);
            preview.AddToClassList("ee4v-variant-creation__preview");
            source.Add(preview);
            body.Add(source);

            var controls = new VisualElement();
            controls.AddToClassList("ee4v-variant-creation__controls");
            var fieldsScroll = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible
            };
            fieldsScroll.AddToClassList("ee4v-variant-creation__fields-scroll");
            var fields = new VisualElement();
            fields.AddToClassList("ee4v-variant-creation__fields");
            fields.Add(new SectionHeader(
                I18N.Get("variant.creation.information"),
                I18N.Get("variant.creation.informationDescription")));
            Name = new InputField(new InputFieldState(name));
            fields.Add(CreateField(I18N.Get("field.name"), Name));
            Description = new InputField(new InputFieldState(description, multiline: true, maxHeight: 192f));
            var descriptionField = CreateField(I18N.Get("field.description"), Description);
            descriptionField.AddToClassList("ee4v-variant-creation__description");
            fields.Add(descriptionField);
            var destination = new VisualElement();
            destination.AddToClassList("ee4v-variant-creation__destination");
            destination.Add(UiTextFactory.Create(
                I18N.Get("variant.creation.destination"), UiClassNames.FormLabel));
            _destination = UiTextFactory.Create(string.Empty, UiClassNames.SecondaryText);
            _destination.SetWhiteSpace(WhiteSpace.Normal);
            destination.Add(_destination);
            fields.Add(destination);
            fieldsScroll.Add(fields);
            controls.Add(fieldsScroll);
            body.Add(controls);
            Add(body);

            var footer = new VisualElement();
            footer.AddToClassList("ee4v-variant-creation__footer");
            Feedback = UiTextFactory.Create(string.Empty, UiClassNames.SecondaryText,
                "ee4v-variant-creation__feedback");
            Feedback.SetColor(UiColorTokens.StatusFailedText);
            Feedback.SetWhiteSpace(WhiteSpace.Normal);
            footer.Add(Feedback);
            Create = new UiButton(submitLabel, icon: FluentUiIcons.CreateState("add.png", UiSizeTokens.Size12));
            Create.AddToClassList("ee4v-variant-creation__submit");
            footer.Add(Create);
            controls.Add(footer);

            Prefab.ValueChanged += prefab =>
            {
                Prefab.Row.DescriptionText.style.display = DisplayStyle.None;
                preview.SetPrefab(prefab);
                if (clearBase != null)
                {
                    clearBase.style.display = prefab != null ? DisplayStyle.Flex : DisplayStyle.None;
                }
                SetFeedback(string.Empty);
                RefreshAction();
            };
            Prefab.SelectionRejected += () => SetFeedback(I18N.Get("notice.derivedAssetPrefabInvalid"));
            Name.ValueChanged += value =>
            {
                SetFeedback(!string.IsNullOrWhiteSpace(value) && !DerivedAssetCreator.IsValidName(value)
                    ? I18N.Get("notice.derivedAssetNameInvalid") : string.Empty);
                RefreshAction();
            };
            Prefab.SetValueWithoutNotify(prefabs.Contains(selectedPrefab) ? selectedPrefab :
                original ? null : prefabs.FirstOrDefault());
            if (clearBase != null)
            {
                clearBase.style.display = Prefab.Value != null ? DisplayStyle.Flex : DisplayStyle.None;
            }
            Prefab.Row.DescriptionText.style.display = DisplayStyle.None;
            preview.SetPrefab(Prefab.Value);
            SetFeedback(string.Empty);
            RefreshAction();
            RegisterCallback<GeometryChangedEvent>(evt =>
                EnableInClassList("ee4v-variant-creation--compact", evt.newRect.width < 940f));
        }

        internal InputField Name { get; }
        internal InputField Description { get; }
        internal PrefabSelector Prefab { get; }
        internal UiTextElement Feedback { get; }
        internal UiButton Create { get; }

        internal void SetFeedback(string message)
        {
            Feedback.SetText(message);
            Feedback.style.display = string.IsNullOrWhiteSpace(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void RefreshAction()
        {
            var validName = DerivedAssetCreator.IsValidName(Name.Value);
            Create.SetPrimaryActionEnabled(validName && (_original || Prefab.Value != null));
            var normalizedName = Name.Value.Trim();
            var path = validName
                ? DerivedAssetCreator.GetVariantFolder(normalizedName) + "/" + normalizedName + ".prefab"
                : I18N.Get("variant.creation.destinationPlaceholder");
            _destination.SetText(path);
            _destination.tooltip = validName ? path : string.Empty;
        }

        private static FormInput CreateField(string label, VisualElement input)
        {
            var field = new FormInput(label, input);
            field.AddToClassList("ee4v-variant-creation__field");
            return field;
        }
    }
}
