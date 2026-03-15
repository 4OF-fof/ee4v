using UnityEditor;

namespace Ee4v.Core.Injector
{
    public sealed class VisualHostContext
    {
        internal VisualHostContext(InjectionChannel channel, EditorWindow window)
        {
            Channel = channel;
            Window = window;
        }

        public InjectionChannel Channel { get; }

        public EditorWindow Window { get; }
    }
}
