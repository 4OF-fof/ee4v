using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal enum DiffConfirmationResult
    {
        Overwrite,
        Cancel
    }

    internal sealed class DiffConfirmationFieldState
    {
        internal DiffConfirmationFieldState(string currentValue, string incomingValue)
        {
            CurrentValue = currentValue ?? string.Empty;
            IncomingValue = incomingValue ?? string.Empty;
        }

        internal string CurrentValue { get; }

        internal string IncomingValue { get; }
    }

    internal sealed class DiffConfirmationItemState
    {
        internal DiffConfirmationItemState(
            string title,
            string metadata,
            IReadOnlyList<DiffConfirmationFieldState> fields,
            ItemImageState thumbnail = null)
        {
            Title = title ?? string.Empty;
            Metadata = metadata ?? string.Empty;
            Fields = fields ?? Array.Empty<DiffConfirmationFieldState>();
            Thumbnail = thumbnail ?? new ItemImageState();
        }

        internal string Title { get; }

        internal string Metadata { get; }

        internal IReadOnlyList<DiffConfirmationFieldState> Fields { get; }

        internal ItemImageState Thumbnail { get; }
    }

    internal sealed class DiffConfirmationState
    {
        internal DiffConfirmationState(
            string title,
            string message,
            string currentHeader,
            string incomingHeader,
            string overwriteLabel,
            string cancelLabel,
            IReadOnlyList<DiffConfirmationItemState> items)
        {
            Title = title ?? string.Empty;
            Message = message ?? string.Empty;
            CurrentHeader = currentHeader ?? string.Empty;
            IncomingHeader = incomingHeader ?? string.Empty;
            OverwriteLabel = overwriteLabel ?? string.Empty;
            CancelLabel = cancelLabel ?? string.Empty;
            Items = items ?? Array.Empty<DiffConfirmationItemState>();
        }

        internal string Title { get; }

        internal string Message { get; }

        internal string CurrentHeader { get; }

        internal string IncomingHeader { get; }

        internal string OverwriteLabel { get; }

        internal string CancelLabel { get; }

        internal IReadOnlyList<DiffConfirmationItemState> Items { get; }
    }

    internal sealed class DiffConfirmationOverlay : VisualElement
    {
        private const string RootClassName = "ee4v-ui-diff-confirmation";
        private readonly Action<DiffConfirmationResult> _onResolved;
        private readonly List<ItemImage> _thumbnails = new List<ItemImage>();
        private bool _resolved;

        internal DiffConfirmationOverlay(DiffConfirmationState state, Action<DiffConfirmationResult> onResolved)
        {
            _onResolved = onResolved;
            name = DiffConfirmationOverlayApi.HostElementName;
            AddToClassList(RootClassName);
            RegisterCallback<DetachFromPanelEvent>(_ => ResolveDetached());
            Build(state ?? new DiffConfirmationState(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, null));
        }

        internal void Cancel()
        {
            Resolve(DiffConfirmationResult.Cancel);
        }

        internal void SetThumbnail(int itemIndex, ItemImageState state)
        {
            if (itemIndex < 0 || itemIndex >= _thumbnails.Count)
            {
                return;
            }

            _thumbnails[itemIndex].SetState(state ?? new ItemImageState());
        }

        private void Build(DiffConfirmationState state)
        {
            var scrim = new VisualElement();
            scrim.AddToClassList(RootClassName + "__scrim");
            Add(scrim);

            var panel = new VisualElement();
            panel.AddToClassList(RootClassName + "__panel");

            var header = new VisualElement();
            header.AddToClassList(RootClassName + "__header");
            header.Add(UiTextFactory.Create(state.Title, RootClassName + "__title"));
            var message = UiTextFactory.Create(state.Message, RootClassName + "__message");
            message.SetWhiteSpace(WhiteSpace.Normal);
            header.Add(message);
            panel.Add(header);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList(RootClassName + "__scroll");
            scroll.Add(CreateColumnHeader(state));
            for (var i = 0; i < state.Items.Count; i++)
            {
                scroll.Add(CreateItem(state.Items[i]));
            }

            panel.Add(scroll);

            var actions = new VisualElement();
            actions.AddToClassList(RootClassName + "__actions");
            var cancel = new Button(() => Resolve(DiffConfirmationResult.Cancel)) { text = state.CancelLabel };
            cancel.AddToClassList(RootClassName + "__button");
            var overwrite = new Button(() => Resolve(DiffConfirmationResult.Overwrite)) { text = state.OverwriteLabel };
            overwrite.AddToClassList(RootClassName + "__button");
            overwrite.AddToClassList(RootClassName + "__button--primary");
            actions.Add(cancel);
            actions.Add(overwrite);
            panel.Add(actions);

            Add(panel);
        }

        private VisualElement CreateItem(DiffConfirmationItemState state)
        {
            state = state ?? new DiffConfirmationItemState(string.Empty, string.Empty, null);
            var item = new VisualElement();
            item.AddToClassList(RootClassName + "__item");

            var itemHeader = new VisualElement();
            itemHeader.AddToClassList(RootClassName + "__item-header");
            var thumbnail = new ItemImage(state.Thumbnail);
            thumbnail.SetSize(48f);
            thumbnail.AddToClassList(RootClassName + "__thumbnail");
            _thumbnails.Add(thumbnail);
            itemHeader.Add(thumbnail);

            var heading = new VisualElement();
            heading.AddToClassList(RootClassName + "__item-heading");
            heading.Add(UiTextFactory.Create(state.Title, RootClassName + "__item-title"));
            var metadata = UiTextFactory.Create(state.Metadata, RootClassName + "__item-meta");
            metadata.SetWhiteSpace(WhiteSpace.Normal);
            heading.Add(metadata);
            itemHeader.Add(heading);
            item.Add(itemHeader);

            for (var i = 0; i < state.Fields.Count; i++)
            {
                var field = state.Fields[i];
                if (field == null)
                {
                    continue;
                }

                var row = new VisualElement();
                row.AddToClassList(RootClassName + "__row");
                row.Add(CreateColumnText(field.CurrentValue, RootClassName + "__value"));
                row.Add(CreateColumnText(field.IncomingValue, RootClassName + "__value"));
                item.Add(row);
            }

            return item;
        }

        private static VisualElement CreateColumnHeader(DiffConfirmationState state)
        {
            var header = new VisualElement();
            header.AddToClassList(RootClassName + "__column-header");
            header.Add(CreateColumnText(state.CurrentHeader, RootClassName + "__value"));
            header.Add(CreateColumnText(state.IncomingHeader, RootClassName + "__value"));
            return header;
        }

        private static UiTextElement CreateColumnText(string text, string className)
        {
            var label = UiTextFactory.Create(text ?? string.Empty, className);
            label.SetWhiteSpace(WhiteSpace.Normal);
            return label;
        }

        private void Resolve(DiffConfirmationResult result)
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;
            RemoveFromHierarchy();
            _onResolved?.Invoke(result);
        }

        private void ResolveDetached()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;
            _onResolved?.Invoke(DiffConfirmationResult.Cancel);
        }
    }

    internal static class DiffConfirmationOverlayApi
    {
        internal const string HostElementName = "ee4v-diff-confirmation-overlay";

        internal static DiffConfirmationOverlay Show(EditorWindow owner, DiffConfirmationState state, Action<DiffConfirmationResult> onResolved)
        {
            if (owner == null || owner.rootVisualElement == null)
            {
                onResolved?.Invoke(DiffConfirmationResult.Cancel);
                return null;
            }

            var root = owner.rootVisualElement;
            root.AddToClassList("ee4v-ui");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/common.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/Content/ItemImage/item-image.uss");
            UiStyleUtility.AddPackageStyleSheet(root, "Editor/Core/UI/Components/Overlays/DiffConfirmationOverlay/diff-confirmation-overlay.uss");
            root.Q<DiffConfirmationOverlay>(HostElementName)?.Cancel();
            var overlay = new DiffConfirmationOverlay(state, onResolved);
            root.Add(overlay);
            return overlay;
        }
    }
}
