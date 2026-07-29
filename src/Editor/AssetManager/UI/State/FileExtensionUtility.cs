namespace Ee4v.AssetManager.UI
{
    internal static class FileExtensionUtility
    {
        public static string Normalize(
            string extension)
        {
            var normalized = (extension ?? string.Empty)
                .Trim()
                .Replace('\\', '/');
            var slashIndex = normalized.LastIndexOf('/');
            if (slashIndex >= 0)
            {
                normalized = normalized.Substring(slashIndex + 1);
            }

            var dotIndex = normalized.LastIndexOf('.');
            if (dotIndex >= 0)
            {
                normalized = normalized.Substring(dotIndex + 1);
            }

            return normalized
                .TrimStart('.')
                .ToLowerInvariant();
        }
    }
}
