using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using Ee4v.AssetManager.Application.Ports;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.Infrastructure
{
    internal sealed class AssetFileAnalyzer : IAssetFileAnalyzer
    {
        public AssetFileAnalysis Analyze(
            AssetFile file,
            CancellationToken cancellationToken = default)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            if (string.IsNullOrWhiteSpace(file.SourcePath) ||
                !File.Exists(file.SourcePath))
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.NotFound,
                    "The source file was not found.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var extension = Path.GetExtension(file.SourcePath);
                if (string.Equals(
                        extension,
                        ".zip",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return new AssetFileAnalysis
                    {
                        FileId = file.Id,
                        Kind = AssetFileAnalysisKind.Zip,
                        Entries = ReadZip(
                            file.SourcePath,
                            cancellationToken)
                    };
                }

                if (string.Equals(
                        extension,
                        ".unitypackage",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return new AssetFileAnalysis
                    {
                        FileId = file.Id,
                        Kind = AssetFileAnalysisKind.UnityPackage,
                        Entries = UnityPackageReader.ReadEntries(
                            file.SourcePath,
                            cancellationToken)
                    };
                }
            }
            catch (AssetManagerException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is InvalidDataException ||
                exception is OverflowException)
            {
                throw new AssetManagerException(
                    AssetManagerErrorCode.DatasourceError,
                    "The asset file could not be analyzed.",
                    exception);
            }

            throw new AssetManagerException(
                AssetManagerErrorCode.InvalidRequest,
                "Only ZIP and UnityPackage files can be analyzed.");
        }

        private static IReadOnlyList<AssetFileContentEntry> ReadZip(
            string path,
            CancellationToken cancellationToken)
        {
            using (var stream = File.Open(
                       path,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite))
            using (var archive = new ZipArchive(
                       stream,
                       ZipArchiveMode.Read))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var source = archive.Entries
                    .Select(entry =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return new
                        {
                            Entry = entry,
                            Path = NormalizePath(entry.FullName)
                        };
                    })
                    .Where(value => value.Path.Length > 0)
                    .ToArray();
                cancellationToken.ThrowIfCancellationRequested();
                var root = Path.GetFileNameWithoutExtension(path);
                var prefix = root + "/";
                var omitRoot = source.Length > 0 && source.All(value =>
                    string.Equals(
                        value.Path,
                        root,
                        StringComparison.OrdinalIgnoreCase) ||
                    value.Path.StartsWith(
                        prefix,
                        StringComparison.OrdinalIgnoreCase));
                return source
                    .Select(value =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return new
                        {
                            value.Entry,
                            Path = omitRoot
                                ? RemoveRoot(value.Path, root, prefix)
                                : value.Path
                        };
                    })
                    .Where(value => value.Path.Length > 0)
                    .GroupBy(
                        value => value.Path,
                        StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .OrderBy(value => value.Path, StringComparer.Ordinal)
                    .Select(value => new AssetFileContentEntry
                    {
                        Path = value.Path,
                        Kind = string.IsNullOrEmpty(value.Entry.Name)
                            ? AssetFileContentEntryKind.Directory
                            : AssetFileContentEntryKind.File,
                        SizeBytes = string.IsNullOrEmpty(value.Entry.Name)
                            ? 0L
                            : value.Entry.Length,
                        AssetGuid = string.Empty
                    })
                    .ToArray();
            }
        }

        private static string RemoveRoot(
            string path,
            string root,
            string prefix)
        {
            return string.Equals(
                    path,
                    root,
                    StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : path.Substring(prefix.Length);
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty)
                .Replace('\\', '/')
                .Trim()
                .Trim('/');
        }
    }

    internal static class UnityPackageReader
    {
        private const int TarBlockSize = 512;
        private const int TarNameLength = 100;
        private const int TarSizeOffset = 124;
        private const int TarSizeLength = 12;
        private const int MaximumPathBytes = 1024 * 1024;

        internal static IReadOnlyList<AssetFileContentEntry> ReadEntries(
            string path,
            CancellationToken cancellationToken = default)
        {
            using (var stream = File.Open(
                       path,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite))
            {
                return ReadEntries(stream, cancellationToken);
            }
        }

        internal static IReadOnlyList<string> ReadGuids(string path)
        {
            try
            {
                return ReadEntries(path)
                    .Select(entry => entry.AssetGuid)
                    .Where(guid => !string.IsNullOrWhiteSpace(guid))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is InvalidDataException ||
                exception is OverflowException)
            {
                return Array.Empty<string>();
            }
        }

        private static IReadOnlyList<AssetFileContentEntry> ReadEntries(
            Stream packageStream,
            CancellationToken cancellationToken)
        {
            var records = new Dictionary<string, PackageRecord>(
                StringComparer.OrdinalIgnoreCase);
            var order = new List<PackageRecord>();
            using (var gzip = new GZipStream(
                       packageStream,
                       CompressionMode.Decompress,
                       true))
            {
                var header = new byte[TarBlockSize];
                while (ReadBlock(gzip, header))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (IsEmptyBlock(header))
                    {
                        break;
                    }

                    var entryName = ReadString(
                        header,
                        0,
                        TarNameLength);
                    var size = ReadOctal(
                        header,
                        TarSizeOffset,
                        TarSizeLength);
                    var separator = entryName.IndexOf('/');
                    var guid = separator < 0
                        ? entryName
                        : entryName.Substring(0, separator);
                    PackageRecord record = null;
                    if (IsGuid(guid))
                    {
                        guid = guid.ToLowerInvariant();
                        if (!records.TryGetValue(guid, out record))
                        {
                            record = new PackageRecord(guid);
                            records.Add(guid, record);
                            order.Add(record);
                        }
                    }

                    var child = separator < 0
                        ? string.Empty
                        : entryName.Substring(separator + 1);
                    if (record != null &&
                        string.Equals(
                            child,
                            "pathname",
                            StringComparison.Ordinal))
                    {
                        record.Path = ReadPath(gzip, size);
                        Skip(gzip, RoundUp(size) - size);
                        continue;
                    }

                    if (record != null &&
                        string.Equals(
                            child,
                            "asset",
                            StringComparison.Ordinal))
                    {
                        record.HasAsset = true;
                        record.Size = size;
                    }

                    Skip(gzip, RoundUp(size));
                }
            }

            return order
                .Where(record =>
                    !string.IsNullOrWhiteSpace(record.Path))
                .Select(record => new AssetFileContentEntry
                {
                    Path = NormalizePath(record.Path),
                    Kind = record.HasAsset
                        ? AssetFileContentEntryKind.File
                        : AssetFileContentEntryKind.Directory,
                    SizeBytes = record.HasAsset ? record.Size : 0L,
                    AssetGuid = record.Guid
                })
                .Where(entry => entry.Path.Length > 0)
                .OrderBy(entry => entry.Path, StringComparer.Ordinal)
                .ToArray();
        }

        private static string ReadPath(Stream stream, long size)
        {
            if (size < 0 ||
                size > MaximumPathBytes ||
                size > int.MaxValue)
            {
                throw new InvalidDataException(
                    "The UnityPackage pathname is too large.");
            }

            var bytes = new byte[(int)size];
            ReadExactly(stream, bytes);
            return Encoding.UTF8
                .GetString(bytes)
                .Trim('\0', '\r', '\n', ' ');
        }

        private static bool ReadBlock(Stream stream, byte[] buffer)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = stream.Read(
                    buffer,
                    offset,
                    buffer.Length - offset);
                if (read == 0)
                {
                    if (offset == 0)
                    {
                        return false;
                    }

                    throw new InvalidDataException(
                        "The UnityPackage ended unexpectedly.");
                }

                offset += read;
            }

            return true;
        }

        private static void ReadExactly(Stream stream, byte[] buffer)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = stream.Read(
                    buffer,
                    offset,
                    buffer.Length - offset);
                if (read == 0)
                {
                    throw new InvalidDataException(
                        "The UnityPackage ended unexpectedly.");
                }

                offset += read;
            }
        }

        private static void Skip(Stream stream, long count)
        {
            var buffer = new byte[8192];
            while (count > 0)
            {
                var read = stream.Read(
                    buffer,
                    0,
                    (int)Math.Min(buffer.Length, count));
                if (read == 0)
                {
                    throw new InvalidDataException(
                        "The UnityPackage ended unexpectedly.");
                }

                count -= read;
            }
        }

        private static long RoundUp(long value)
        {
            return value <= 0
                ? 0L
                : checked(
                    ((value + TarBlockSize - 1) / TarBlockSize) *
                    TarBlockSize);
        }

        private static bool IsEmptyBlock(byte[] buffer)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static string ReadString(
            byte[] buffer,
            int offset,
            int length)
        {
            var end = offset;
            var maximum = Math.Min(buffer.Length, offset + length);
            while (end < maximum && buffer[end] != 0)
            {
                end++;
            }

            return Encoding.ASCII.GetString(
                buffer,
                offset,
                end - offset);
        }

        private static long ReadOctal(
            byte[] buffer,
            int offset,
            int length)
        {
            var value = ReadString(buffer, offset, length)
                .Trim('\0', ' ');
            long result = 0L;
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] < '0' || value[i] > '7')
                {
                    throw new InvalidDataException(
                        "The UnityPackage contains an invalid entry size.");
                }

                result = checked(result * 8L + value[i] - '0');
            }

            return result;
        }

        private static bool IsGuid(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 32)
            {
                return false;
            }

            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (!(character >= '0' && character <= '9') &&
                    !(character >= 'a' && character <= 'f') &&
                    !(character >= 'A' && character <= 'F'))
                {
                    return false;
                }
            }

            return true;
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty)
                .Replace('\\', '/')
                .Trim()
                .Trim('/');
        }

        private sealed class PackageRecord
        {
            internal PackageRecord(string guid)
            {
                Guid = guid;
            }

            internal string Guid { get; }
            internal string Path { get; set; }
            internal bool HasAsset { get; set; }
            internal long Size { get; set; }
        }
    }
}
