using UnityEngine;

namespace _4OF.ee4v.Core.Interfaces {
    public interface IProjectExtensionComponent {
        int Priority { get; }
        string Name { get; }
        string Description { get; }
        string Trigger { get; }
        void OnGUI(ref Rect currentRect, string guid, Rect fullRect);
    }
}