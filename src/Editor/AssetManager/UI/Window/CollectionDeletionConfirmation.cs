using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using UnityEditor;

namespace Ee4v.AssetManager.UI
{
    internal static class CollectionDeletionConfirmation
    {
        public static bool Confirm(AssetCollection collection)
        {
            return collection != null &&
                   EditorUtility.DisplayDialog(
                       I18N.Get(
                           "assetManager.navigation.collections.delete.title"),
                       I18N.Get(
                           "assetManager.navigation.collections.delete.message",
                           collection.Name),
                       I18N.Get(
                           "assetManager.navigation.collections.delete.confirm"),
                       I18N.Get(
                           "assetManager.navigation.collections.delete.cancel"));
        }
    }
}
