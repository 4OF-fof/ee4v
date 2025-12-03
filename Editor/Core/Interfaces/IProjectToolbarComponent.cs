using UnityEngine.UIElements;

namespace _4OF.ee4v.Core.Interfaces {
    public interface IProjectToolbarComponent {
        int Priority { get; }
        ToolbarPosition Position { get; }
        string Name { get; }
        string Description { get; }
        string Trigger { get; }
        VisualElement CreateElement();
    }

    public enum ToolbarPosition {
        Left,
        Right
    }
}