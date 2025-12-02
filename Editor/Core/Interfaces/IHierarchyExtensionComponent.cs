using UnityEngine;

namespace _4OF.ee4v.Core.Interfaces {
    public interface IHierarchyExtensionComponent {
        int Priority { get; }
        string Name { get; }
        string Description { get; }
        string Trigger { get; }
        void OnGUI(ref Rect currentRect, GameObject gameObject, int instanceID, Rect fullRect);
    }
}