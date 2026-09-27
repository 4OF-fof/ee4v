using System;
using System.Linq;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class AssetVariantSaveOverlay : VisualElement
    {
        private readonly Action<string> _save;
        private readonly InputField _message;
        private readonly VisualElement _previousFocus;
        private readonly (VisualElement Element, bool Enabled)[] _background;
        private bool _closed;

        public static void Show(VisualElement host, Action<string> save)
        {
            if (host == null || save == null)
            {
                return;
            }
            var existing = host.Children().OfType<AssetVariantSaveOverlay>().FirstOrDefault();
            if (existing != null)
            {
                return;
            }

            var overlay = new AssetVariantSaveOverlay(host, save);
            host.Add(overlay);
            overlay.schedule.Execute(overlay.Focus);
        }

        private AssetVariantSaveOverlay(VisualElement host, Action<string> save)
        {
            _save = save;
            _previousFocus = host.panel?.focusController?.focusedElement as VisualElement;
            _background = host.Children().Select(element => (element, element.enabledSelf)).ToArray();
            focusable = true;
            tabIndex = -1;
            AddToClassList("ee4v-asset-manager__variant-save-overlay");

            var card = new VisualElement();
            card.AddToClassList("ee4v-asset-manager__variant-save-card");
            var header = new VisualElement();
            header.AddToClassList("ee4v-asset-manager__variant-save-header");
            header.Add(UiTextFactory.Create(
                I18N.Get("variant.save"), UiClassNames.SectionTitle,
                "ee4v-asset-manager__variant-save-title"));
            header.Add(AssetManagerControls.CreateIconButton(
                I18N.Get("action.cancel"), "dismiss.png",
                UiSizeTokens.Size16, UiButtonVariant.Ghost, Close,
                "ee4v-asset-manager__variant-save-close"));
            card.Add(header);

            var form = new VisualElement();
            form.AddToClassList("ee4v-asset-manager__variant-save-form");
            _message = new InputField(new InputFieldState(
                placeholder: I18N.Get("variant.saveMessage")));
            form.Add(_message);
            var submit = AssetManagerControls.CreateButton(
                I18N.Get("variant.confirmSave"), Submit,
                "ee4v-asset-manager__variant-save-submit");
            submit.SetLabelColor(UiColorTokens.TextOnState);
            form.Add(submit);
            card.Add(form);
            Add(card);

            RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0 && evt.target == this) { Close(); }
                evt.StopPropagation();
            });
            RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());
            RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            RegisterCallback<WheelEvent>(evt => evt.StopPropagation());
            RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape)
                {
                    Close();
                    evt.PreventDefault();
                }
                else if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    if (!string.IsNullOrEmpty(Input.compositionString)) { return; }
                    Submit();
                    evt.PreventDefault();
                }
                else { return; }
                evt.StopPropagation();
            }, TrickleDown.TrickleDown);
            RegisterCallback<KeyDownEvent>(evt => evt.StopPropagation());
            RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                if (evt.target == this) { RestoreBackground(); }
            });
            foreach (var background in _background)
            {
                background.Element.SetEnabled(false);
            }
        }

        private void Submit()
        {
            if (_closed) { return; }
            var message = _message.Value.Trim();
            Close();
            _save(message);
        }

        public void Close()
        {
            if (_closed) { return; }
            RestoreBackground();
            RemoveFromHierarchy();
            if (_previousFocus?.panel != null && _previousFocus.enabledInHierarchy)
            {
                _previousFocus.Focus();
            }
        }

        private void RestoreBackground()
        {
            if (_closed) { return; }
            _closed = true;
            foreach (var background in _background)
            {
                background.Element.SetEnabled(background.Enabled);
            }
        }
    }
}
