using System;

namespace Ee4v.AssetManager.UI
{
    internal static class AssetManagerCollectionViewId
    {
        private const string Prefix = "collection:";

        public static string Encode(string collectionId)
        {
            return string.IsNullOrWhiteSpace(collectionId)
                ? string.Empty
                : Prefix + collectionId;
        }

        public static bool TryDecode(string viewId, out string collectionId)
        {
            if (!string.IsNullOrWhiteSpace(viewId) &&
                viewId.StartsWith(Prefix, StringComparison.Ordinal) &&
                viewId.Length > Prefix.Length)
            {
                collectionId = viewId.Substring(Prefix.Length);
                return true;
            }

            collectionId = string.Empty;
            return false;
        }
    }
}
