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
            return Register(window, groupId, false);
        }

        public static IDisposable Register(
            EditorWindow window,
            string groupId,
            bool isFollower)
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

            return WindowGroupBootstrap.Register(
                window,
                groupId,
                isFollower);
        }
    }
}
