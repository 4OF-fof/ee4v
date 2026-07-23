using System;
using Ee4v.UI;

namespace Ee4v.AssetManager
{
    internal sealed class AssetManagerViewItemState
    {
        public AssetManagerViewItemState(
            string id,
            string label,
            string meta,
            string eyebrow,
            string title,
            string description,
            string[] rows,
            IconState iconState = null)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            Meta = meta ?? string.Empty;
            Eyebrow = eyebrow ?? string.Empty;
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Rows = rows ?? Array.Empty<string>();
            IconState = iconState;
        }

        public string Id { get; }

        public string Label { get; }

        public string Meta { get; }

        public string Eyebrow { get; }

        public string Title { get; }

        public string Description { get; }

        public string[] Rows { get; }

        public IconState IconState { get; }
    }

}
