using System.Text.RegularExpressions;
using _4OF.ee4v.AssetManager.Data;
using _4OF.ee4v.AssetManager.OldData;
using _4OF.ee4v.AssetManager.Utility;
using _4OF.ee4v.Core.Utility;
using UnityEngine;

namespace _4OF.ee4v.AssetManager.Service {
    internal static class AssetBoothService {
        public static void SetBoothShopDomain(Ulid assetId, string shopURL) {
            if (BoothUtility.ClassifyBoothUrl(shopURL) != BoothUtility.BoothUrlType.ShopUrl) {
                Debug.LogError("Invalid Booth shop URL.");
                return;
            }

            var asset = new AssetMetadata(AssetLibrary.Instance.GetAsset(assetId));
            var regex = new Regex(@"https?://([^\.]+)\.booth\.pm", RegexOptions.IgnoreCase);
            var match = regex.Match(shopURL);
            if (!match.Success) return;
            var shopName = match.Groups[1].Value;
            asset.BoothData.SetShopDomain(shopName);
            AssetService.UpdateAsset(asset);
        }

        public static void SetBoothItemId(Ulid assetId, string itemURL) {
            if (BoothUtility.ClassifyBoothUrl(itemURL) != BoothUtility.BoothUrlType.ItemUrl) {
                Debug.LogError("Invalid Booth item URL.");
                return;
            }

            var asset = new AssetMetadata(AssetLibrary.Instance.GetAsset(assetId));
            var regex = new Regex(@"items/(\d+)");
            var match = regex.Match(itemURL);

            if (!match.Success) return;
            var itemId = match.Groups[1].Value;
            asset.BoothData.SetItemID(itemId);
            AssetService.UpdateAsset(asset);
        }

        public static void SetBoothDownloadId(Ulid assetId, string downloadURL) {
            if (BoothUtility.ClassifyBoothUrl(downloadURL) != BoothUtility.BoothUrlType.DownloadUrl) {
                Debug.LogError("Invalid Booth download URL.");
                return;
            }

            var asset = new AssetMetadata(AssetLibrary.Instance.GetAsset(assetId));
            var regex = new Regex(@"downloadables/(\d+)", RegexOptions.IgnoreCase);
            var match = regex.Match(downloadURL);

            if (!match.Success) return;
            var downloadId = match.Groups[1].Value;
            asset.BoothData.SetDownloadID(downloadId);
            AssetService.UpdateAsset(asset);
        }
    }
}