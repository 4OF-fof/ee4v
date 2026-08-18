using System.Collections.Generic;
using Ee4v.Core.Injector;
using Ee4v.ItemStyle;
using UnityEngine;

namespace Ee4v.ProjectStyle
{
    public static class ProjectStyleApi
    {
        public static ItemStyleValue Get(string folderGuid)
        {
            return ProjectStyleBootstrap.Service.Get(folderGuid);
        }

        public static void SetColor(
            IReadOnlyList<string> folderGuids,
            Color color)
        {
            ProjectStyleBootstrap.Service.SetColor(
                folderGuids,
                color);
            Repaint();
        }

        public static void SetIcon(
            IReadOnlyList<string> folderGuids,
            string iconGuid)
        {
            ProjectStyleBootstrap.Service.SetIcon(
                folderGuids,
                iconGuid);
            Repaint();
        }

        public static void Clear(
            IReadOnlyList<string> folderGuids)
        {
            ProjectStyleBootstrap.Service.Clear(folderGuids);
            Repaint();
        }

        private static void Repaint()
        {
            InjectorApi.Repaint(InjectionChannel.ProjectItem);
        }
    }
}
