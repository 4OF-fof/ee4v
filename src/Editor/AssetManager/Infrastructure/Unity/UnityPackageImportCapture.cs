using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Ee4v.AssetManager.Infrastructure.Unity
{
    internal static class UnityPackageImportCapture
    {
        private static HashSet<string> _expected;
        private static HashSet<string> _captured;

        internal static void Begin(
            IReadOnlyList<string> expectedAssetGuids)
        {
            _expected = new HashSet<string>(
                expectedAssetGuids ??
                Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            _captured = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
        }

        internal static void Record(
            IReadOnlyList<string> importedAssetPaths)
        {
            if (_expected == null ||
                _captured == null ||
                importedAssetPaths == null)
            {
                return;
            }

            for (var i = 0;
                 i < importedAssetPaths.Count;
                 i++)
            {
                var guid = AssetDatabase.AssetPathToGUID(
                    importedAssetPaths[i]);
                if (!string.IsNullOrWhiteSpace(guid) &&
                    _expected.Contains(guid))
                {
                    _captured.Add(
                        guid.ToLowerInvariant());
                }
            }
        }

        internal static IReadOnlyList<string> End(
            bool succeeded,
            bool fallbackToExpected)
        {
            var result = !succeeded
                ? Array.Empty<string>()
                : _captured != null &&
                  _captured.Count > 0
                    ? _captured
                        .OrderBy(
                            guid => guid,
                            StringComparer.Ordinal)
                        .ToArray()
                    : fallbackToExpected &&
                      _expected != null
                        ? _expected
                            .OrderBy(
                                guid => guid,
                                StringComparer.Ordinal)
                            .ToArray()
                        : Array.Empty<string>();
            _expected = null;
            _captured = null;
            return result;
        }
    }

    internal sealed class
        UnityPackageImportCapturePostprocessor :
            AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            UnityPackageImportCapture.Record(
                importedAssets);
        }
    }
}
