using System;

namespace Ee4v.AssetManager
{
    internal static class AssetItemGridNodeKey
    {
        private const char Separator = '|';

        public static string Encode(AssetItemGridNodeKind kind, string id)
        {
            return kind.ToString() + Separator + (id ?? string.Empty);
        }

        public static bool TryDecode(string key, out AssetItemGridNodeKind kind, out string id)
        {
            kind = AssetItemGridNodeKind.Item;
            id = key ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            var separatorIndex = key.IndexOf(Separator);
            if (separatorIndex <= 0)
            {
                return false;
            }

            var kindText = key.Substring(0, separatorIndex);
            if (!Enum.TryParse(kindText, out kind))
            {
                kind = AssetItemGridNodeKind.Item;
                return false;
            }

            id = key.Substring(separatorIndex + 1);
            return !string.IsNullOrWhiteSpace(id);
        }
    }
}
