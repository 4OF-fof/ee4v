using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using Ee4v.AssetManager.Contracts;

namespace Ee4v.AssetManager.Infrastructure.Files
{
    internal sealed class AssetFileSystemReader : IAssetFileSystemReader
    {
        public bool FileExists(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }

        public bool DirectoryExists(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);
        }

        public IReadOnlyList<AssetFileSystemEntry> GetDirectoryEntries(
            string path,
            CancellationToken cancellationToken)
        {
            var entries = new List<AssetFileSystemEntry>();
            foreach (var directory in Directory.EnumerateDirectories(path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                entries.Add(new AssetFileSystemEntry(
                    directory,
                    Path.GetFileName(directory),
                    AssetFileSystemEntryKind.Directory));
            }

            foreach (var file in Directory.EnumerateFiles(path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                entries.Add(new AssetFileSystemEntry(
                    file,
                    Path.GetFileName(file),
                    AssetFileSystemEntryKind.File));
            }

            return entries;
        }

        public Stream OpenFile(string path, long maximumBytes)
        {
            if (string.IsNullOrWhiteSpace(path) || maximumBytes < 0)
            {
                return null;
            }

            var file = new FileInfo(path);
            return file.Exists && file.Length <= maximumBytes
                ? file.OpenRead()
                : null;
        }

        public Stream OpenZipEntry(
            string archivePath,
            string entryPath,
            long maximumBytes)
        {
            if (string.IsNullOrWhiteSpace(archivePath) ||
                string.IsNullOrWhiteSpace(entryPath) ||
                maximumBytes < 0)
            {
                return null;
            }

            var archiveStream = File.OpenRead(archivePath);
            ZipArchive archive = null;
            try
            {
                archive = new ZipArchive(
                    archiveStream,
                    ZipArchiveMode.Read,
                    false);
                ZipArchiveEntry matched = null;
                for (var i = 0; i < archive.Entries.Count; i++)
                {
                    if (string.Equals(
                            archive.Entries[i].FullName,
                            entryPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        matched = archive.Entries[i];
                        break;
                    }
                }

                if (matched == null || matched.Length > maximumBytes)
                {
                    archive.Dispose();
                    return null;
                }

                return new OwnedArchiveEntryStream(archive, matched.Open());
            }
            catch
            {
                if (archive != null)
                {
                    archive.Dispose();
                }
                else
                {
                    archiveStream.Dispose();
                }

                throw;
            }
        }

        private sealed class OwnedArchiveEntryStream : Stream
        {
            private readonly ZipArchive _archive;
            private readonly Stream _entryStream;

            internal OwnedArchiveEntryStream(
                ZipArchive archive,
                Stream entryStream)
            {
                _archive = archive;
                _entryStream = entryStream;
            }

            public override bool CanRead => _entryStream.CanRead;
            public override bool CanSeek => _entryStream.CanSeek;
            public override bool CanWrite => false;
            public override long Length => _entryStream.Length;

            public override long Position
            {
                get { return _entryStream.Position; }
                set { _entryStream.Position = value; }
            }

            public override void Flush() => _entryStream.Flush();

            public override int Read(
                byte[] buffer,
                int offset,
                int count) =>
                _entryStream.Read(buffer, offset, count);

            public override long Seek(long offset, SeekOrigin origin) =>
                _entryStream.Seek(offset, origin);

            public override void SetLength(long value) =>
                throw new NotSupportedException();

            public override void Write(
                byte[] buffer,
                int offset,
                int count) =>
                throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _entryStream.Dispose();
                    _archive.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}
