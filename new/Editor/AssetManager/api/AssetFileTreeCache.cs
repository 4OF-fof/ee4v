using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Ee4v.Core.Settings;

namespace Ee4v.AssetManager.Api
{
    internal sealed class AssetFileTreeArchiveEntry
    {
        public AssetFileTreeArchiveEntry(string fullName, long length)
        {
            FullName = fullName ?? string.Empty;
            Length = length;
        }

        public string FullName { get; }

        public long Length { get; }
    }

    internal static class AssetFileTreeCache
    {
        private const string CacheMagic = "EE4V-FT";
        private const int CacheVersion = 1;
        private static readonly object CacheLocksGate = new object();
        private static readonly Dictionary<string, object> CacheLocks = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public static string ResolveCacheDirectory()
        {
            var globalPath = Environment.ExpandEnvironmentVariables(SettingApi.Get(AssetManagerDefinitions.Ee4vGlobalPath));
            return Path.GetFullPath(Path.Combine(globalPath, "cache", "file-tree"));
        }

        public static IReadOnlyList<AssetFileTreeArchiveEntry> ReadZipEntries(
            string cacheDirectory,
            string zipPath,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(cacheDirectory))
            {
                throw new ArgumentException("Cache directory is required.", nameof(cacheDirectory));
            }

            var fullPath = Path.GetFullPath(zipPath);
            var cachePath = Path.Combine(cacheDirectory, CreateCacheKey(fullPath) + ".ftc");
            lock (GetCacheLock(cachePath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var source = new FileInfo(fullPath);
                if (!source.Exists)
                {
                    throw new FileNotFoundException("Archive was not found.", fullPath);
                }

                if (TryReadCache(cachePath, source, cancellationToken, out var cachedEntries))
                {
                    return CreateDisplayEntries(fullPath, cachedEntries);
                }

                var entries = ReadArchive(fullPath, cancellationToken);
                try
                {
                    WriteCache(cachePath, source, entries, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    // Cache persistence is best-effort; a writable cache is not required to display the archive.
                }

                return CreateDisplayEntries(fullPath, entries);
            }
        }

        private static IReadOnlyList<AssetFileTreeArchiveEntry> CreateDisplayEntries(
            string zipPath,
            IReadOnlyList<AssetFileTreeArchiveEntry> entries)
        {
            var ignoredRoot = AssetArchivePathUtility.ResolveIgnoredRootFolder(
                zipPath,
                entries.Select(entry => entry.FullName));
            if (string.IsNullOrEmpty(ignoredRoot))
            {
                return entries;
            }

            var displayEntries = new List<AssetFileTreeArchiveEntry>(entries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                var displayPath = AssetArchivePathUtility.ToDisplayPath(entries[i].FullName, ignoredRoot);
                if (!string.IsNullOrEmpty(displayPath))
                {
                    displayEntries.Add(new AssetFileTreeArchiveEntry(displayPath, entries[i].Length));
                }
            }

            return displayEntries;
        }

        private static IReadOnlyList<AssetFileTreeArchiveEntry> ReadArchive(string zipPath, CancellationToken cancellationToken)
        {
            var entries = new List<AssetFileTreeArchiveEntry>();
            using (var stream = File.OpenRead(zipPath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                for (var i = 0; i < archive.Entries.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var entry = archive.Entries[i];
                    entries.Add(new AssetFileTreeArchiveEntry(entry.FullName, entry.Length));
                }
            }

            return entries;
        }

        private static bool TryReadCache(
            string cachePath,
            FileInfo source,
            CancellationToken cancellationToken,
            out IReadOnlyList<AssetFileTreeArchiveEntry> entries)
        {
            entries = null;
            if (!File.Exists(cachePath))
            {
                return false;
            }

            try
            {
                using (var stream = File.OpenRead(cachePath))
                using (var reader = new BinaryReader(stream, Encoding.UTF8, false))
                {
                    if (!string.Equals(reader.ReadString(), CacheMagic, StringComparison.Ordinal) ||
                        reader.ReadInt32() != CacheVersion ||
                        reader.ReadInt64() != source.LastWriteTimeUtc.Ticks ||
                        reader.ReadInt64() != source.Length)
                    {
                        return false;
                    }

                    var count = reader.ReadInt32();
                    if (count < 0 || count > 10000000)
                    {
                        return false;
                    }

                    var cached = new List<AssetFileTreeArchiveEntry>(count);
                    for (var i = 0; i < count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        cached.Add(new AssetFileTreeArchiveEntry(reader.ReadString(), reader.ReadInt64()));
                    }

                    if (stream.Position != stream.Length)
                    {
                        return false;
                    }

                    entries = cached;
                    return true;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void WriteCache(
            string cachePath,
            FileInfo source,
            IReadOnlyList<AssetFileTreeArchiveEntry> entries,
            CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
            var temporaryPath = cachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = File.Create(temporaryPath))
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, false))
                {
                    writer.Write(CacheMagic);
                    writer.Write(CacheVersion);
                    writer.Write(source.LastWriteTimeUtc.Ticks);
                    writer.Write(source.Length);
                    writer.Write(entries.Count);
                    for (var i = 0; i < entries.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        writer.Write(entries[i].FullName);
                        writer.Write(entries[i].Length);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(cachePath))
                {
                    File.Delete(cachePath);
                }

                File.Move(temporaryPath, cachePath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static string CreateCacheKey(string path)
        {
            var normalized = Path.GetFullPath(path).ToUpperInvariant();
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalized));
                var builder = new StringBuilder(hash.Length * 2);
                for (var i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        private static object GetCacheLock(string cachePath)
        {
            lock (CacheLocksGate)
            {
                if (!CacheLocks.TryGetValue(cachePath, out var cacheLock))
                {
                    cacheLock = new object();
                    CacheLocks[cachePath] = cacheLock;
                }

                return cacheLock;
            }
        }
    }
}
