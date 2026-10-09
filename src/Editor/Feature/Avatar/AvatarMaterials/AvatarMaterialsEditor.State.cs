using System.Collections.Generic;
using Ee4v.Core.EditorIntegration;
using Ee4v.UI;
using Ee4v.AvatarEditing;
using UnityEngine;

namespace Ee4v.AvatarMaterials
{
    public sealed partial class AvatarMaterialsEditor
    {
        private readonly AvatarEditingContext _context;

        private UiButton _allMaterialsVisibilityButton;

        private EmbeddedMaterialInspector _materialInspector;

        private Vector2 _materialListScrollOffset;

        private bool _showOnlySelectedMaterial;

        private readonly HashSet<int> _expandedMaterialPrefabGroups =
            new HashSet<int>();

        private readonly Dictionary<Material, List<UiButton>>
            _materialVisibilityButtons =
                new Dictionary<Material, List<UiButton>>();

        private IReadOnlyList<AvatarMaterialEntry> _avatarMaterialsCache;

        private readonly HashSet<Material> _materialsOutsideSelectedPrefab =
            new HashSet<Material>();

        private readonly Dictionary<SkinnedMeshRenderer,
            MaterialGeometryCacheEntry> _materialGeometryCache =
                new Dictionary<SkinnedMeshRenderer,
                    MaterialGeometryCacheEntry>();

        private IReadOnlyDictionary<Transform, BodyPartCategory>
            _materialBoneCategoriesCache;

        private readonly Dictionary<Transform, BodyPartCategory>
            _materialResolvedBoneCategoriesCache =
                new Dictionary<Transform, BodyPartCategory>();

        private readonly HashSet<Material> _hiddenMaterials =
            new HashSet<Material>();
    }
}
