using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.Core.EditorIntegration;
using Ee4v.Core.I18n;
using Ee4v.UI;
using Ee4v.AvatarEditing;
using static Ee4v.AvatarEditing.AvatarBodyAnalysis;
using static Ee4v.AvatarEditing.AvatarEditingUi;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Ee4v.AvatarMaterials
{
    public sealed partial class AvatarMaterialsEditor
    {
        private readonly AvatarEditingContext _context;

        private UiButton _allMaterialsVisibilityButton;

        private EmbeddedMaterialInspector _materialInspector;

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

        private readonly HashSet<Material> _hiddenMaterials =
            new HashSet<Material>();
    }
}
