using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Ee4v.AssetManager.Application.Ports;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.Infrastructure
{
    internal sealed class AssetFileAnalyzer : IAssetFileAnalyzer
    {
        private readonly AnalysisCache _cache;

        internal AssetFileAnalyzer(string databasePath)
        {
            _cache = new AnalysisCache(databasePath);
        }

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
                var kind = string.Equals(
                    extension,
                    ".zip",
                    StringComparison.OrdinalIgnoreCase)
                    ? AssetFileAnalysisKind.Zip
                    : string.Equals(
                        extension,
                        ".unitypackage",
                        StringComparison.OrdinalIgnoreCase)
                        ? AssetFileAnalysisKind.UnityPackage
                        : (AssetFileAnalysisKind?)null;
                if (!kind.HasValue)
                {
                    throw new AssetManagerException(
                        AssetManagerErrorCode.InvalidRequest,
                        "Only ZIP and UnityPackage files can be analyzed.");
                }

                var sourceStamp = _cache?.GetStamp(file.SourcePath);
                var cached = sourceStamp.HasValue
                    ? _cache.TryLoad(
                        file.Id,
                        kind.Value,
                        sourceStamp.Value,
                        cancellationToken)
                    : null;
                if (cached != null)
                {
                    return cached;
                }

                var analysis = new AssetFileAnalysis
                {
                    FileId = file.Id,
                    Kind = kind.Value,
                    Entries = kind.Value == AssetFileAnalysisKind.Zip
                        ? ReadZip(file.SourcePath, cancellationToken)
                        : UnityPackageReader.ReadEntries(
                            file.SourcePath,
                            cancellationToken)
                };
                if (sourceStamp.HasValue)
                {
                    _cache.TrySave(
                        file.SourcePath,
                        sourceStamp.Value,
                        analysis,
                        cancellationToken);
                }
                return analysis;
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

        internal static string NormalizePath(string path)
        {
            return (path ?? string.Empty)
                .Replace('\\', '/')
                .Trim()
                .Trim('/');
        }

        private sealed class AnalysisCache
        {
            private const string Magic = "ee4v-file-analysis";
            private const int FormatVersion = 1;
            private const int MaximumEntries = 250000;
            private const int MaximumStringBytes = 1024 * 1024;
            private const long MaximumCacheFileBytes = 32L * 1024 * 1024;
            private const long MaximumTotalCacheBytes = 512L * 1024 * 1024;
            private static readonly UTF8Encoding Utf8 =
                new UTF8Encoding(false, true);
            private readonly string _root;

            internal AnalysisCache(string databasePath)
            {
                var directory = Path.GetDirectoryName(
                    Path.GetFullPath(databasePath));
                _root = Path.Combine(
                    string.IsNullOrWhiteSpace(directory)
                        ? Directory.GetCurrentDirectory()
                        : directory,
                    "cache",
                    "asset-manager",
                    "file-analyses");
            }

            internal SourceStamp? GetStamp(string path)
            {
                try
                {
                    var fullPath = Path.GetFullPath(path);
                    var source = new FileInfo(fullPath);
                    return source.Exists
                        ? new SourceStamp(
                            fullPath,
                            source.Length,
                            source.LastWriteTimeUtc.Ticks)
                        : (SourceStamp?)null;
                }
                catch (Exception exception) when (
                    exception is IOException ||
                    exception is UnauthorizedAccessException ||
                    exception is ArgumentException ||
                    exception is NotSupportedException ||
                    exception is System.Security.SecurityException)
                {
                    return null;
                }
            }

            internal AssetFileAnalysis TryLoad(
                string fileId,
                AssetFileAnalysisKind kind,
                SourceStamp stamp,
                CancellationToken cancellationToken)
            {
                try
                {
                    var cachePath = GetCachePath(stamp.Path);
                    var cacheFile = new FileInfo(cachePath);
                    if (!cacheFile.Exists ||
                        cacheFile.Length > MaximumCacheFileBytes)
                    {
                        return null;
                    }

                    using (var stream = new FileStream(
                               cachePath,
                               FileMode.Open,
                               FileAccess.Read,
                               FileShare.ReadWrite | FileShare.Delete))
                    using (var reader = new BinaryReader(stream, Utf8))
                    {
                        if (ReadString(reader) != Magic ||
                            reader.ReadInt32() != FormatVersion ||
                            ReadString(reader) != stamp.Path ||
                            reader.ReadInt64() != stamp.Length ||
                            reader.ReadInt64() != stamp.LastWriteTicks ||
                            reader.ReadInt32() != (int)kind)
                        {
                            return null;
                        }

                        var count = reader.ReadInt32();
                        if (count < 0 || count > MaximumEntries)
                        {
                            return null;
                        }

                        var entries = new AssetFileContentEntry[count];
                        for (var i = 0; i < count; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var path = ReadString(reader);
                            var entryKind = reader.ReadInt32();
                            var size = reader.ReadInt64();
                            var guid = ReadString(reader);
                            if (string.IsNullOrEmpty(path) ||
                                (entryKind != (int)AssetFileContentEntryKind.File &&
                                 entryKind != (int)AssetFileContentEntryKind.Directory) ||
                                size < 0)
                            {
                                return null;
                            }

                            entries[i] = new AssetFileContentEntry
                            {
                                Path = path,
                                Kind = (AssetFileContentEntryKind)entryKind,
                                SizeBytes = size,
                                AssetGuid = guid
                            };
                        }

                        var currentStamp = GetStamp(stamp.Path);
                        return stream.Position == stream.Length &&
                               currentStamp.HasValue &&
                               stamp.Equals(currentStamp.Value)
                            ? new AssetFileAnalysis
                            {
                                FileId = fileId,
                                Kind = kind,
                                Entries = entries
                            }
                            : null;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception) when (
                    exception is IOException ||
                    exception is UnauthorizedAccessException ||
                    exception is ArgumentException ||
                    exception is NotSupportedException ||
                    exception is DecoderFallbackException ||
                    exception is System.Security.SecurityException)
                {
                    return null;
                }
            }

            internal void TrySave(
                string sourcePath,
                SourceStamp stamp,
                AssetFileAnalysis analysis,
                CancellationToken cancellationToken)
            {
                var currentStamp = GetStamp(sourcePath);
                if (analysis.Entries.Count > MaximumEntries ||
                    !currentStamp.HasValue ||
                    !stamp.Equals(currentStamp.Value))
                {
                    return;
                }

                string temporaryPath = null;
                try
                {
                    Directory.CreateDirectory(_root);
                    var cachePath = GetCachePath(stamp.Path);
                    temporaryPath = cachePath + "." +
                                    Guid.NewGuid().ToString("N") + ".tmp";
                    using (var stream = new FileStream(
                               temporaryPath,
                               FileMode.CreateNew,
                               FileAccess.Write,
                               FileShare.None))
                    using (var writer = new BinaryWriter(stream, Utf8))
                    {
                        WriteString(writer, Magic);
                        writer.Write(FormatVersion);
                        WriteString(writer, stamp.Path);
                        writer.Write(stamp.Length);
                        writer.Write(stamp.LastWriteTicks);
                        writer.Write((int)analysis.Kind);
                        writer.Write(analysis.Entries.Count);
                        foreach (var entry in analysis.Entries)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            WriteString(writer, entry.Path);
                            writer.Write((int)entry.Kind);
                            writer.Write(entry.SizeBytes);
                            WriteString(writer, entry.AssetGuid ?? string.Empty);
                            if (stream.Position > MaximumCacheFileBytes)
                            {
                                return;
                            }
                        }
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    currentStamp = GetStamp(sourcePath);
                    if (!currentStamp.HasValue ||
                        !stamp.Equals(currentStamp.Value))
                    {
                        return;
                    }

                    if (File.Exists(cachePath))
                    {
                        File.Replace(temporaryPath, cachePath, null);
                    }
                    else
                    {
                        File.Move(temporaryPath, cachePath);
                    }
                    temporaryPath = null;
                    Prune();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception) when (
                    exception is IOException ||
                    exception is UnauthorizedAccessException ||
                    exception is ArgumentException ||
                    exception is NotSupportedException ||
                    exception is EncoderFallbackException ||
                    exception is System.Security.SecurityException)
                {
                    // The archive remains usable when the optional cache fails.
                }
                finally
                {
                    if (temporaryPath != null)
                    {
                        try
                        {
                            File.Delete(temporaryPath);
                        }
                        catch (IOException)
                        {
                        }
                        catch (UnauthorizedAccessException)
                        {
                        }
                    }
                }
            }

            private string GetCachePath(string path)
            {
                using (var sha256 = SHA256.Create())
                {
                    var hash = sha256.ComputeHash(Utf8.GetBytes(path));
                    return Path.Combine(
                        _root,
                        BitConverter.ToString(hash).Replace("-", "") +
                        ".bin");
                }
            }

            private static string ReadString(BinaryReader reader)
            {
                var length = reader.ReadInt32();
                if (length < 0 ||
                    length > MaximumStringBytes ||
                    length > reader.BaseStream.Length -
                    reader.BaseStream.Position)
                {
                    throw new InvalidDataException("Invalid analysis cache string.");
                }

                var bytes = reader.ReadBytes(length);
                return Utf8.GetString(bytes);
            }

            private static void WriteString(BinaryWriter writer, string value)
            {
                var bytes = Utf8.GetBytes(value);
                if (bytes.Length > MaximumStringBytes)
                {
                    throw new InvalidDataException("Analysis cache string is too large.");
                }

                writer.Write(bytes.Length);
                writer.Write(bytes);
            }

            private void Prune()
            {
                try
                {
                    var files = Directory.GetFiles(_root, "*.bin")
                        .Select(path => new FileInfo(path))
                        .OrderBy(file => file.LastWriteTimeUtc)
                        .ToArray();
                    var total = files.Sum(file => file.Length);
                    foreach (var file in files)
                    {
                        if (total <= MaximumTotalCacheBytes)
                        {
                            break;
                        }

                        total -= file.Length;
                        file.Delete();
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            internal struct SourceStamp : IEquatable<SourceStamp>
            {
                internal SourceStamp(
                    string path,
                    long length,
                    long lastWriteTicks)
                {
                    Path = path;
                    Length = length;
                    LastWriteTicks = lastWriteTicks;
                }

                internal string Path { get; }
                internal long Length { get; }
                internal long LastWriteTicks { get; }

                public bool Equals(SourceStamp other)
                {
                    return string.Equals(Path, other.Path, StringComparison.Ordinal) &&
                           Length == other.Length &&
                           LastWriteTicks == other.LastWriteTicks;
                }
            }
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
                var skipBuffer = new byte[64 * 1024];
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
                        Skip(
                            gzip,
                            RoundUp(size) - size,
                            skipBuffer,
                            cancellationToken);
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

                    Skip(
                        gzip,
                        RoundUp(size),
                        skipBuffer,
                        cancellationToken);
                }
            }

            return order
                .Where(record =>
                    !string.IsNullOrWhiteSpace(record.Path))
                .Select(record => new AssetFileContentEntry
                {
                    Path = AssetFileAnalyzer.NormalizePath(
                        record.Path),
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

        private static void Skip(
            Stream stream,
            long count,
            byte[] buffer,
            CancellationToken cancellationToken)
        {
            while (count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
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
