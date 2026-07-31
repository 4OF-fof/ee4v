using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.Infrastructure.Files
{
    internal sealed class UnityPackageContentSnapshot
    {
        internal UnityPackageContentSnapshot(
            IReadOnlyList<string> guids,
            IReadOnlyList<AssetArchiveContentEntry> entries)
        {
            Guids = guids ?? Array.Empty<string>();
            Entries = entries ??
                      Array.Empty<AssetArchiveContentEntry>();
        }

        internal IReadOnlyList<string> Guids { get; }

        internal IReadOnlyList<AssetArchiveContentEntry>
            Entries { get; }
    }

    internal static class UnityPackageContentReader
    {
        private const int TarBlockSize = 512;
        private const int TarNameLength = 100;
        private const int TarSizeOffset = 124;
        private const int TarSizeLength = 12;
        private const int UnityGuidLength = 32;
        private const int MaximumPathnameBytes =
            1024 * 1024;

        internal static UnityPackageContentSnapshot Read(
            string packagePath,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
            {
                throw new ArgumentException(
                    "Package path is required.",
                    nameof(packagePath));
            }

            using (var file = File.Open(
                       packagePath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite))
            {
                return Read(
                    file,
                    cancellationToken);
            }
        }

        internal static UnityPackageContentSnapshot Read(
            Stream packageStream,
            CancellationToken cancellationToken)
        {
            if (packageStream == null ||
                !packageStream.CanRead)
            {
                throw new ArgumentException(
                    "A readable package stream is required.",
                    nameof(packageStream));
            }

            var records =
                new Dictionary<string, PackageRecord>(
                    StringComparer.OrdinalIgnoreCase);
            var orderedRecords =
                new List<PackageRecord>();
            using (var gzip = new GZipStream(
                       packageStream,
                       CompressionMode.Decompress,
                       true))
            {
                ReadTar(
                    gzip,
                    records,
                    orderedRecords,
                    cancellationToken);
            }

            var entries =
                new List<AssetArchiveContentEntry>(
                    orderedRecords.Count);
            var guids =
                new List<string>(orderedRecords.Count);
            for (var i = 0; i < orderedRecords.Count; i++)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();
                var record = orderedRecords[i];
                guids.Add(record.Guid);
                if (string.IsNullOrWhiteSpace(record.Path))
                {
                    continue;
                }

                entries.Add(
                    new AssetArchiveContentEntry(
                        NormalizeAssetPath(record.Path),
                        record.HasAsset
                            ? AssetArchiveContentEntryKind
                                .File
                            : AssetArchiveContentEntryKind
                                .Directory,
                        record.AssetSize,
                        record.AssetEntryPath));
            }

            return new UnityPackageContentSnapshot(
                guids,
                entries);
        }

        private static void ReadTar(
            Stream stream,
            IDictionary<string, PackageRecord> records,
            ICollection<PackageRecord> orderedRecords,
            CancellationToken cancellationToken)
        {
            var header = new byte[TarBlockSize];
            while (ReadBlock(stream, header))
            {
                cancellationToken
                    .ThrowIfCancellationRequested();
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
                var separatorIndex =
                    entryName.IndexOf('/');
                var candidate =
                    separatorIndex >= 0
                        ? entryName.Substring(
                            0,
                            separatorIndex)
                        : entryName;
                PackageRecord record = null;
                if (IsUnityGuid(candidate))
                {
                    var guid =
                        candidate.ToLowerInvariant();
                    if (!records.TryGetValue(
                            guid,
                            out record))
                    {
                        record = new PackageRecord(guid);
                        records.Add(guid, record);
                        orderedRecords.Add(record);
                    }
                }

                var childName =
                    separatorIndex >= 0
                        ? entryName.Substring(
                            separatorIndex + 1)
                        : string.Empty;
                if (record != null &&
                    string.Equals(
                        childName,
                        "pathname",
                        StringComparison.Ordinal))
                {
                    record.Path = ReadPathname(
                        stream,
                        size,
                        cancellationToken);
                    SkipPadding(stream, size, cancellationToken);
                    continue;
                }

                if (record != null &&
                    string.Equals(
                        childName,
                        "asset",
                        StringComparison.Ordinal))
                {
                    record.HasAsset = true;
                    record.AssetSize = size;
                    record.AssetEntryPath = entryName;
                }

                Skip(
                    stream,
                    RoundUpToTarBlock(size),
                    cancellationToken);
            }
        }

        internal static byte[] ReadEntry(
            Stream packageStream,
            string entryPath,
            long maximumBytes,
            CancellationToken cancellationToken)
        {
            if (packageStream == null ||
                !packageStream.CanRead ||
                string.IsNullOrWhiteSpace(entryPath) ||
                maximumBytes < 0L)
            {
                return null;
            }

            using (var gzip = new GZipStream(
                       packageStream,
                       CompressionMode.Decompress,
                       true))
            {
                var header = new byte[TarBlockSize];
                while (ReadBlock(gzip, header))
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();
                    if (IsEmptyBlock(header))
                    {
                        return null;
                    }

                    var candidate = ReadString(
                        header,
                        0,
                        TarNameLength);
                    var size = ReadOctal(
                        header,
                        TarSizeOffset,
                        TarSizeLength);
                    if (!string.Equals(
                            candidate,
                            entryPath,
                            StringComparison.Ordinal))
                    {
                        Skip(
                            gzip,
                            RoundUpToTarBlock(size),
                            cancellationToken);
                        continue;
                    }

                    if (size > maximumBytes ||
                        size > int.MaxValue)
                    {
                        return null;
                    }

                    var bytes = new byte[(int)size];
                    ReadExactly(
                        gzip,
                        bytes,
                        cancellationToken);
                    return bytes;
                }
            }

            return null;
        }

        private static string ReadPathname(
            Stream stream,
            long size,
            CancellationToken cancellationToken)
        {
            if (size < 0 ||
                size > MaximumPathnameBytes)
            {
                throw new InvalidDataException(
                    "The UnityPackage pathname is too large.");
            }

            var bytes = new byte[(int)size];
            ReadExactly(
                stream,
                bytes,
                cancellationToken);
            return Encoding.UTF8
                .GetString(bytes)
                .Trim('\0', '\r', '\n', ' ');
        }

        private static void SkipPadding(
            Stream stream,
            long contentSize,
            CancellationToken cancellationToken)
        {
            Skip(
                stream,
                RoundUpToTarBlock(contentSize) -
                contentSize,
                cancellationToken);
        }

        private static bool ReadBlock(
            Stream stream,
            byte[] buffer)
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

        private static void ReadExactly(
            Stream stream,
            byte[] buffer,
            CancellationToken cancellationToken)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();
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
            var maximum =
                Math.Min(
                    buffer.Length,
                    offset + length);
            while (end < maximum &&
                   buffer[end] != 0)
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
            var value =
                ReadString(buffer, offset, length)
                    .Trim('\0', ' ');
            if (string.IsNullOrEmpty(value))
            {
                return 0L;
            }

            long result = 0L;
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (character < '0' ||
                    character > '7')
                {
                    throw new InvalidDataException(
                        "The UnityPackage contains an invalid TAR entry size.");
                }

                result = checked(
                    result * 8L +
                    character -
                    '0');
            }

            return result;
        }

        private static long RoundUpToTarBlock(
            long value)
        {
            return value <= 0
                ? 0
                : checked(
                    ((value + TarBlockSize - 1) /
                     TarBlockSize) *
                    TarBlockSize);
        }

        private static void Skip(
            Stream stream,
            long count,
            CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            while (count > 0)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();
                var read = stream.Read(
                    buffer,
                    0,
                    (int)Math.Min(
                        buffer.Length,
                        count));
                if (read == 0)
                {
                    throw new InvalidDataException(
                        "The UnityPackage ended unexpectedly.");
                }

                count -= read;
            }
        }

        private static bool IsUnityGuid(string value)
        {
            if (string.IsNullOrEmpty(value) ||
                value.Length != UnityGuidLength)
            {
                return false;
            }

            for (var i = 0;
                 i < value.Length;
                 i++)
            {
                var character = value[i];
                if (!(character >= '0' &&
                      character <= '9') &&
                    !(character >= 'a' &&
                      character <= 'f') &&
                    !(character >= 'A' &&
                      character <= 'F'))
                {
                    return false;
                }
            }

            return true;
        }

        private static string NormalizeAssetPath(
            string path)
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
            internal long AssetSize { get; set; }
            internal string AssetEntryPath { get; set; }
        }
    }
}
