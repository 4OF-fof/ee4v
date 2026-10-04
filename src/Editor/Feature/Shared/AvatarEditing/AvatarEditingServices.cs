using System;
using UnityEngine;

namespace Ee4v.AvatarEditing
{
    /// <summary>Required editing permissions, persistence and change notifications.</summary>
    public sealed class AvatarEditingServices
    {
        public AvatarEditingServices(
            Func<bool> canEditPrefab, Func<Material, bool> canEditMaterial,
            Func<bool> flushChanges, Func<string> getAssetPath,
            Action<bool> workingSceneDirtyChanged, Action changed,
            Action<string> showAssetError, Action<Material> createMaterialVariant)
        {
            CanEditPrefab = canEditPrefab ?? throw new ArgumentNullException(nameof(canEditPrefab));
            CanEditMaterial = canEditMaterial ?? throw new ArgumentNullException(nameof(canEditMaterial));
            FlushChanges = flushChanges ?? throw new ArgumentNullException(nameof(flushChanges));
            GetAssetPath = getAssetPath ?? throw new ArgumentNullException(nameof(getAssetPath));
            WorkingSceneDirtyChanged = workingSceneDirtyChanged ?? throw new ArgumentNullException(nameof(workingSceneDirtyChanged));
            Changed = changed ?? throw new ArgumentNullException(nameof(changed));
            ShowAssetError = showAssetError ?? throw new ArgumentNullException(nameof(showAssetError));
            CreateMaterialVariant = createMaterialVariant ?? throw new ArgumentNullException(nameof(createMaterialVariant));
        }

        public Func<bool> CanEditPrefab { get; }
        public Func<Material, bool> CanEditMaterial { get; }
        public Func<bool> FlushChanges { get; }
        public Func<string> GetAssetPath { get; }
        public Action<bool> WorkingSceneDirtyChanged { get; }
        public bool WorkingSceneDirty { set => WorkingSceneDirtyChanged(value); }
        public Action Changed { get; }
        public Action<string> ShowAssetError { get; }
        public Action<Material> CreateMaterialVariant { get; }
        public Action<Material> MaterialChanged { get; set; }
    }
}
