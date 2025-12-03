using System;

namespace _4OF.ee4v.Core.Interfaces {
    public interface IEditorService : IDisposable {
        string Name { get; }
        string Description { get; }
        string Trigger { get; }
        void Initialize();
    }
}