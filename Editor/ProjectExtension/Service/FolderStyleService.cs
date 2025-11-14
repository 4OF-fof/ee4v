using _4OF.ee4v.ProjectExtension.Data;

namespace _4OF.ee4v.ProjectExtension.Service {
    public static class FolderStyleService {
        public static int IndexOfPath(string path) {
            if (string.IsNullOrEmpty(path)) return -1;
            for (var i = 0; i < FolderStyleList.instance.Contents.Count; i++)
                if (FolderStyleList.instance.Contents[i].path == path)
                    return i;
            return -1;
        }

        public static string NormalizePath(string path) {
            if (string.IsNullOrEmpty(path)) return path;
            var p = path.Trim().Replace('\\', '/');
            while (p.Length > 1 && p.EndsWith("/")) p = p[..^1];
            return p.ToLowerInvariant();
        }
    }
}