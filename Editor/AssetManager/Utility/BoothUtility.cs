using System.Text.RegularExpressions;

namespace _4OF.ee4v.AssetManager.Utility {
    public static class BoothUtility {
        public enum BoothUrlType {
            ItemUrl,
            ShopUrl,
            DownloadUrl,
            Other
        }

        public static BoothUrlType ClassifyBoothUrl(string url) {
            if (string.IsNullOrWhiteSpace(url))
                return BoothUrlType.Other;

            if (Regex.IsMatch(url, @"https?://(?:[^\.]+\.booth\.pm|booth\.pm/[^/]+)/items/\d+",
                    RegexOptions.IgnoreCase))
                return BoothUrlType.ItemUrl;

            if (Regex.IsMatch(url, @"https?://[^\.]+\.booth\.pm/?$", RegexOptions.IgnoreCase))
                return BoothUrlType.ShopUrl;

            return Regex.IsMatch(url, @"https?://booth\.pm/downloadables/\d+", RegexOptions.IgnoreCase)
                ? BoothUrlType.DownloadUrl
                : BoothUrlType.Other;
        }


        public static bool TryParseShopItemUrl(string url, out string shopDomain, out string itemId) {
            shopDomain = string.Empty;
            itemId = string.Empty;
            if (string.IsNullOrWhiteSpace(url)) return false;

            var m = Regex.Match(url,
                @"^https?://(?<sub>[^\.]+)\.booth\.pm/items/(?<id>\d+)",
                RegexOptions.IgnoreCase);
            if (!m.Success) return false;

            shopDomain = m.Groups["sub"].Value;
            itemId = m.Groups["id"].Value;
            return true;
        }
    }
}