using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.AvatarEditing
{
    /// <summary>View navigation and optional integration with the surrounding editor.</summary>
    public sealed class AvatarEditingHost
    {
        public AvatarEditingHost(Action refresh, Action rebuild, Action clearCaches,
            Action repaint, Action syncPreviewSelection)
        {
            Refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
            Rebuild = rebuild ?? throw new ArgumentNullException(nameof(rebuild));
            ClearCaches = clearCaches ?? throw new ArgumentNullException(nameof(clearCaches));
            Repaint = repaint ?? throw new ArgumentNullException(nameof(repaint));
            SyncPreviewSelection = syncPreviewSelection ?? throw new ArgumentNullException(nameof(syncPreviewSelection));
            ShowParts = refresh;
            ShowMaterials = refresh;
        }

        public Action Refresh { get; }
        public Action Rebuild { get; }
        public Action ClearCaches { get; }
        public Action Repaint { get; }
        public Action SyncPreviewSelection { get; }
        public Action ShowParts { get; set; }
        public Action ShowMaterials { get; set; }
        public Func<IReadOnlyList<string>> GetExcludedPartPrefixes { get; set; } = () => Array.Empty<string>();
        public Func<GameObject, IEnumerable<Mesh>, IAvatarShapeNaming> CreateShapeNaming { get; set; } = AvatarShapeNaming.Create;
        public Action<AvatarEditorPanel> InvalidateControls { get; set; } = _ => { };
        public Action InvalidateMaterialData { get; set; } = () => { };
        public Action<int, bool, bool> RefreshPrefabGroupVisibility { get; set; } = (_, __, ___) => { };
        public Action<int, bool> RefreshPrefabHeaderActiveSelf { get; set; } = (_, __) => { };
        public Action<int> RefreshPrefabPreviewVisibilityControls { get; set; } = _ => { };
        public Func<int, VisualElement, HashSet<int>, bool, VisualElement> BuildPrefabGroup { get; set; } =
            (_, content, __, ___) => content;
    }
}
