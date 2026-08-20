using System;
using UnityEditor;

namespace Ee4v.WindowGroup
{
    public static class WindowGroupApi
    {
        public static IDisposable Register(
            EditorWindow window,
            string groupId)
        {
            if (window == null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            if (string.IsNullOrWhiteSpace(groupId))
            {
                throw new ArgumentException(
                    "A window group ID is required.",
                    nameof(groupId));
            }

            return WindowGroupBootstrap.Register(window, groupId);
        }
    }
}
