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
        private sealed class MaterialUsage
        {
            internal Renderer Renderer { get; set; }
            internal string RendererPath { get; set; }
            internal int PrefabSiblingIndex { get; set; }
            internal int SlotIndex { get; set; }
            internal IReadOnlyCollection<BodyPartCategory> Categories { get; set; }
        }

        private sealed class MaterialGeometryCacheEntry
        {
            internal Mesh Mesh { get; set; }
            internal IReadOnlyCollection<BodyPartCategory>[] Slots { get; set; }
        }

        private sealed class AvatarMaterialEntry
        {
            internal Material Material { get; set; }
            internal List<MaterialUsage> Usages { get; } =
                new List<MaterialUsage>();
        }
    }
}
