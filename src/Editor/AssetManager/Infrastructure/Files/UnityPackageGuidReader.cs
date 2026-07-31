using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Ee4v.AssetManager.Infrastructure.Files
{
    internal static class UnityPackageGuidReader
    {
        internal static IReadOnlyList<string> ReadGuids(
            string packagePath)
        {
            if (string.IsNullOrWhiteSpace(packagePath) ||
                !File.Exists(packagePath))
            {
                return Array.Empty<string>();
            }

            try
            {
                return UnityPackageContentReader
                    .Read(
                        packagePath,
                        CancellationToken.None)
                    .Guids;
            }
            catch (InvalidDataException)
            {
                return Array.Empty<string>();
            }
            catch (IOException)
            {
                return Array.Empty<string>();
            }
            catch (OverflowException)
            {
                return Array.Empty<string>();
            }
        }
    }
}
