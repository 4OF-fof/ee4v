using System;
using System.Collections.Generic;
using System.Linq;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using UnityEditor;

namespace Ee4v.AssetManager.UI
{
    internal static class CollectionDeletionConfirmation
    {
        public static bool Confirm(
            IReadOnlyList<AssetCollection> collections)
        {
            var targets = (collections ??
                           Array.Empty<AssetCollection>())
                .Where(collection => collection != null)
                .ToArray();
            if (targets.Length == 0)
            {
                return false;
            }

            var message = targets.Length == 1
                ? I18N.Get(
                    "assetManager.navigation.collections.delete.message",
                    targets[0].Name)
                : I18N.Get(
                    "assetManager.navigation.collections.delete.messageMultiple",
                    targets.Length);
            return EditorUtility.DisplayDialog(
                       I18N.Get(
                           "assetManager.navigation.collections.delete.title"),
                       message,
                       I18N.Get(
                           "assetManager.navigation.collections.delete.confirm"),
                       I18N.Get(
                           "assetManager.navigation.collections.delete.cancel"));
        }
    }
}
