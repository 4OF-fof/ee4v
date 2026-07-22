using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using UnityEngine;

namespace Ee4v.AssetManager
{
    internal sealed class FileTreeImageSource
    {
        private FileTreeImageSource(string fileName, string filePath, string archivePath, string archiveEntryPath)
        {
            FileName = fileName ?? string.Empty;
            FilePath = filePath ?? string.Empty;
            ArchivePath = archivePath ?? string.Empty;
            ArchiveEntryPath = archiveEntryPath ?? string.Empty;
        }

        public string FileName { get; }

        public string FilePath { get; }

        public string ArchivePath { get; }

        public string ArchiveEntryPath { get; }

        public string CacheKey
        {
            get
            {
                return string.IsNullOrWhiteSpace(ArchivePath)
                    ? "file:" + FilePath
                    : "zip:" + ArchivePath + "|" + ArchiveEntryPath;
            }
        }

        public static FileTreeImageSource FromFile(string fileName, string filePath)
        {
            return IsSupported(fileName) && !string.IsNullOrWhiteSpace(filePath)
                ? new FileTreeImageSource(fileName, filePath, null, null)
                : null;
        }

        public static FileTreeImageSource FromArchive(string fileName, string archivePath, string archiveEntryPath)
        {
            return IsSupported(fileName) &&
                   !string.IsNullOrWhiteSpace(archivePath) &&
                   !string.IsNullOrWhiteSpace(archiveEntryPath)
                ? new FileTreeImageSource(fileName, null, archivePath, archiveEntryPath)
                : null;
        }

        public static bool IsSupported(string fileName)
        {
            switch (Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant())
            {
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".psd":
                    return true;
                default:
                    return false;
            }
        }
    }

    internal sealed class FileTreeImagePreviewData
    {
        internal const int MaximumPreviewWidth = 300;
        internal const int MaximumPreviewHeight = 240;

        private FileTreeImagePreviewData(byte[] encodedData, byte[] rgbaData, int width, int height)
        {
            EncodedData = encodedData ?? Array.Empty<byte>();
            RgbaData = rgbaData ?? Array.Empty<byte>();
            Width = width;
            Height = height;
        }

        public byte[] EncodedData { get; }

        public byte[] RgbaData { get; }

        public int Width { get; }

        public int Height { get; }

        public static FileTreeImagePreviewData FromEncoded(byte[] data)
        {
            return data == null || data.Length == 0
                ? null
                : new FileTreeImagePreviewData(data, null, 0, 0);
        }

        public static FileTreeImagePreviewData FromRgba(byte[] data, int width, int height)
        {
            return data == null || data.Length == 0 || width <= 0 || height <= 0
                ? null
                : new FileTreeImagePreviewData(null, data, width, height);
        }

        public Texture2D CreateTexture()
        {
            if (RgbaData.Length > 0)
            {
                var rawTexture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                rawTexture.LoadRawTextureData(RgbaData);
                rawTexture.Apply(false, true);
                return rawTexture;
            }

            if (EncodedData.Length == 0)
            {
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            if (!texture.LoadImage(EncodedData, true))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                return null;
            }

            return ResizeIfNeeded(texture);
        }

        private static Texture2D ResizeIfNeeded(Texture2D source)
        {
            var scale = Mathf.Min(1f, Mathf.Min(
                MaximumPreviewWidth / (float)source.width,
                MaximumPreviewHeight / (float)source.height));
            var width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            var height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
            if (width == source.width && height == source.height)
            {
                return source;
            }

            var previous = RenderTexture.active;
            RenderTexture renderTexture = null;
            Texture2D resized = null;
            try
            {
                renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(source, renderTexture);
                RenderTexture.active = renderTexture;
                resized = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                resized.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                resized.Apply(false, true);
                UnityEngine.Object.DestroyImmediate(source);
                return resized;
            }
            catch
            {
                if (resized != null)
                {
                    UnityEngine.Object.DestroyImmediate(resized);
                }

                return source;
            }
            finally
            {
                RenderTexture.active = previous;
                if (renderTexture != null)
                {
                    RenderTexture.ReleaseTemporary(renderTexture);
                }
            }
        }
    }

    internal static class FileTreeImagePreviewLoader
    {
        private const long MaximumEncodedBytes = 64L * 1024L * 1024L;

        public static FileTreeImagePreviewData Load(FileTreeImageSource source, CancellationToken cancellationToken)
        {
            if (source == null)
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();
            using (var stream = OpenSource(source))
            {
                if (stream == null)
                {
                    return null;
                }

                if (string.Equals(Path.GetExtension(source.FileName), ".psd", StringComparison.OrdinalIgnoreCase))
                {
                    return PsdCompositeImageDecoder.Decode(stream, cancellationToken);
                }

                return FileTreeImagePreviewData.FromEncoded(ReadAllBytes(stream, cancellationToken));
            }
        }

        private static Stream OpenSource(FileTreeImageSource source)
        {
            if (string.IsNullOrWhiteSpace(source.ArchivePath))
            {
                var file = new FileInfo(source.FilePath);
                return file.Exists && file.Length <= MaximumEncodedBytes
                    ? file.OpenRead()
                    : null;
            }

            var archiveStream = File.OpenRead(source.ArchivePath);
            ZipArchive archive = null;
            try
            {
                archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, false);
                ZipArchiveEntry matched = null;
                for (var i = 0; i < archive.Entries.Count; i++)
                {
                    if (string.Equals(archive.Entries[i].FullName, source.ArchiveEntryPath, StringComparison.OrdinalIgnoreCase))
                    {
                        matched = archive.Entries[i];
                        break;
                    }
                }

                if (matched == null || matched.Length > MaximumEncodedBytes)
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

        private static byte[] ReadAllBytes(Stream stream, CancellationToken cancellationToken)
        {
            using (var output = new MemoryStream())
            {
                var buffer = new byte[81920];
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                    {
                        break;
                    }

                    if (output.Length + read > MaximumEncodedBytes)
                    {
                        return null;
                    }

                    output.Write(buffer, 0, read);
                }

                return output.ToArray();
            }
        }

        private sealed class OwnedArchiveEntryStream : Stream
        {
            private readonly ZipArchive _archive;
            private readonly Stream _entryStream;

            public OwnedArchiveEntryStream(ZipArchive archive, Stream entryStream)
            {
                _archive = archive;
                _entryStream = entryStream;
            }

            public override bool CanRead => _entryStream.CanRead;
            public override bool CanSeek => _entryStream.CanSeek;
            public override bool CanWrite => false;
            public override long Length => _entryStream.Length;
            public override long Position { get => _entryStream.Position; set => _entryStream.Position = value; }
            public override void Flush() => _entryStream.Flush();
            public override int Read(byte[] buffer, int offset, int count) => _entryStream.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => _entryStream.Seek(offset, origin);
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

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

    internal static class PsdCompositeImageDecoder
    {
        private const int MaximumDimension = 30000;
        private const int MaximumChannels = 56;

        public static FileTreeImagePreviewData Decode(Stream stream, CancellationToken cancellationToken)
        {
            try
            {
                var signature = ReadAscii(stream, 4);
                var version = ReadUInt16(stream);
                if (!string.Equals(signature, "8BPS", StringComparison.Ordinal) || (version != 1 && version != 2))
                {
                    return null;
                }

                Skip(stream, 6, cancellationToken);
                var channels = ReadUInt16(stream);
                var height = checked((int)ReadUInt32(stream));
                var width = checked((int)ReadUInt32(stream));
                var depth = ReadUInt16(stream);
                var colorMode = ReadUInt16(stream);
                if (channels <= 0 || channels > MaximumChannels ||
                    width <= 0 || height <= 0 || width > MaximumDimension || height > MaximumDimension ||
                    (depth != 8 && depth != 16) ||
                    (colorMode != 1 && colorMode != 3 && colorMode != 4))
                {
                    return null;
                }

                SkipSection(stream, ReadUInt32(stream), cancellationToken);
                SkipSection(stream, ReadUInt32(stream), cancellationToken);
                SkipSection(stream, version == 1 ? ReadUInt32(stream) : ReadUInt64(stream), cancellationToken);

                var compression = ReadUInt16(stream);
                if (compression != 0 && compression != 1)
                {
                    return null;
                }

                var previewSize = CalculatePreviewSize(width, height);
                var requiredChannels = ResolveRequiredChannelCount(colorMode, channels);
                var planes = new byte[requiredChannels][];
                for (var i = 0; i < planes.Length; i++)
                {
                    planes[i] = new byte[previewSize.x * previewSize.y];
                }

                var sourceX = CreateSourceSampleMap(width, previewSize.x);
                var destinationY = CreateDestinationRowMap(height, previewSize.y);
                var bytesPerSample = depth / 8;
                if (compression == 0)
                {
                    ReadRawPlanes(stream, channels, width, height, bytesPerSample, sourceX, destinationY, planes, previewSize.x, cancellationToken);
                }
                else
                {
                    ReadRlePlanes(stream, version, channels, width, height, bytesPerSample, sourceX, destinationY, planes, previewSize.x, cancellationToken);
                }

                return FileTreeImagePreviewData.FromRgba(
                    ConvertToRgba(planes, colorMode, channels, previewSize.x, previewSize.y),
                    previewSize.x,
                    previewSize.y);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return null;
            }
        }

        private static void ReadRawPlanes(
            Stream stream,
            int channels,
            int width,
            int height,
            int bytesPerSample,
            int[] sourceX,
            int[] destinationY,
            byte[][] planes,
            int previewWidth,
            CancellationToken cancellationToken)
        {
            var row = new byte[checked(width * bytesPerSample)];
            for (var channel = 0; channel < channels; channel++)
            {
                for (var y = 0; y < height; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ReadExactly(stream, row, 0, row.Length);
                    if (channel < planes.Length && destinationY[y] >= 0)
                    {
                        CopySampledRow(row, bytesPerSample, sourceX, planes[channel], destinationY[y] * previewWidth);
                    }
                }
            }
        }

        private static void ReadRlePlanes(
            Stream stream,
            int version,
            int channels,
            int width,
            int height,
            int bytesPerSample,
            int[] sourceX,
            int[] destinationY,
            byte[][] planes,
            int previewWidth,
            CancellationToken cancellationToken)
        {
            var rowCount = checked(channels * height);
            var rowLengths = new int[rowCount];
            for (var i = 0; i < rowLengths.Length; i++)
            {
                var length = version == 1 ? ReadUInt16(stream) : checked((int)ReadUInt32(stream));
                if (length < 0 || length > width * bytesPerSample * 2 + 1024)
                {
                    throw new InvalidDataException("Invalid PSD RLE row length.");
                }

                rowLengths[i] = length;
            }

            var decodedRow = new byte[checked(width * bytesPerSample)];
            for (var channel = 0; channel < channels; channel++)
            {
                for (var y = 0; y < height; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var length = rowLengths[channel * height + y];
                    if (channel >= planes.Length || destinationY[y] < 0)
                    {
                        Skip(stream, length, cancellationToken);
                        continue;
                    }

                    var encodedRow = new byte[length];
                    ReadExactly(stream, encodedRow, 0, encodedRow.Length);
                    DecodePackBits(encodedRow, decodedRow);
                    CopySampledRow(decodedRow, bytesPerSample, sourceX, planes[channel], destinationY[y] * previewWidth);
                }
            }
        }

        private static void DecodePackBits(byte[] encoded, byte[] decoded)
        {
            var source = 0;
            var destination = 0;
            while (source < encoded.Length && destination < decoded.Length)
            {
                var header = unchecked((sbyte)encoded[source++]);
                if (header >= 0)
                {
                    var count = header + 1;
                    if (source + count > encoded.Length || destination + count > decoded.Length)
                    {
                        throw new InvalidDataException("Invalid PSD PackBits literal run.");
                    }

                    Buffer.BlockCopy(encoded, source, decoded, destination, count);
                    source += count;
                    destination += count;
                }
                else if (header >= -127)
                {
                    var count = 1 - header;
                    if (source >= encoded.Length || destination + count > decoded.Length)
                    {
                        throw new InvalidDataException("Invalid PSD PackBits repeated run.");
                    }

                    var value = encoded[source++];
                    for (var i = 0; i < count; i++)
                    {
                        decoded[destination++] = value;
                    }
                }
            }

            if (destination != decoded.Length)
            {
                throw new InvalidDataException("PSD PackBits row is incomplete.");
            }
        }

        private static void CopySampledRow(byte[] source, int bytesPerSample, int[] sourceX, byte[] destination, int destinationOffset)
        {
            for (var x = 0; x < sourceX.Length; x++)
            {
                destination[destinationOffset + x] = source[sourceX[x] * bytesPerSample];
            }
        }

        private static byte[] ConvertToRgba(byte[][] planes, int colorMode, int sourceChannelCount, int width, int height)
        {
            var pixels = checked(width * height);
            var rgba = new byte[checked(pixels * 4)];
            for (var i = 0; i < pixels; i++)
            {
                var output = i * 4;
                if (colorMode == 1)
                {
                    rgba[output] = planes[0][i];
                    rgba[output + 1] = planes[0][i];
                    rgba[output + 2] = planes[0][i];
                    rgba[output + 3] = sourceChannelCount > 1 ? planes[1][i] : (byte)255;
                }
                else if (colorMode == 3)
                {
                    rgba[output] = planes[0][i];
                    rgba[output + 1] = planes[1][i];
                    rgba[output + 2] = planes[2][i];
                    rgba[output + 3] = sourceChannelCount > 3 ? planes[3][i] : (byte)255;
                }
                else
                {
                    var black = planes[3][i];
                    rgba[output] = (byte)(255 - Math.Min(255, planes[0][i] + black));
                    rgba[output + 1] = (byte)(255 - Math.Min(255, planes[1][i] + black));
                    rgba[output + 2] = (byte)(255 - Math.Min(255, planes[2][i] + black));
                    rgba[output + 3] = sourceChannelCount > 4 ? planes[4][i] : (byte)255;
                }
            }

            return rgba;
        }

        private static int ResolveRequiredChannelCount(int colorMode, int sourceChannelCount)
        {
            var colorChannels = colorMode == 1 ? 1 : colorMode == 3 ? 3 : 4;
            if (sourceChannelCount < colorChannels)
            {
                throw new InvalidDataException("PSD composite image does not contain enough color channels.");
            }

            return Math.Min(sourceChannelCount, colorChannels + 1);
        }

        private static (int x, int y) CalculatePreviewSize(int width, int height)
        {
            var scale = Math.Min(1d, Math.Min(
                FileTreeImagePreviewData.MaximumPreviewWidth / (double)width,
                FileTreeImagePreviewData.MaximumPreviewHeight / (double)height));
            return (
                Math.Max(1, (int)Math.Round(width * scale)),
                Math.Max(1, (int)Math.Round(height * scale)));
        }

        private static int[] CreateSourceSampleMap(int sourceSize, int destinationSize)
        {
            var map = new int[destinationSize];
            for (var i = 0; i < destinationSize; i++)
            {
                map[i] = Math.Min(sourceSize - 1, (int)((i + 0.5d) * sourceSize / destinationSize));
            }

            return map;
        }

        private static int[] CreateDestinationRowMap(int sourceHeight, int destinationHeight)
        {
            var map = new int[sourceHeight];
            for (var i = 0; i < map.Length; i++)
            {
                map[i] = -1;
            }

            for (var destinationY = 0; destinationY < destinationHeight; destinationY++)
            {
                var sourceY = Math.Min(sourceHeight - 1, (int)((destinationY + 0.5d) * sourceHeight / destinationHeight));
                map[sourceY] = destinationHeight - 1 - destinationY;
            }

            return map;
        }

        private static string ReadAscii(Stream stream, int count)
        {
            var bytes = new byte[count];
            ReadExactly(stream, bytes, 0, bytes.Length);
            return System.Text.Encoding.ASCII.GetString(bytes);
        }

        private static ushort ReadUInt16(Stream stream)
        {
            var a = stream.ReadByte();
            var b = stream.ReadByte();
            if (a < 0 || b < 0)
            {
                throw new EndOfStreamException();
            }

            return (ushort)((a << 8) | b);
        }

        private static uint ReadUInt32(Stream stream)
        {
            return ((uint)ReadUInt16(stream) << 16) | ReadUInt16(stream);
        }

        private static ulong ReadUInt64(Stream stream)
        {
            return ((ulong)ReadUInt32(stream) << 32) | ReadUInt32(stream);
        }

        private static void SkipSection(Stream stream, ulong length, CancellationToken cancellationToken)
        {
            if (length > long.MaxValue)
            {
                throw new InvalidDataException("PSD section is too large.");
            }

            Skip(stream, (long)length, cancellationToken);
        }

        private static void Skip(Stream stream, long count, CancellationToken cancellationToken)
        {
            if (count < 0)
            {
                throw new InvalidDataException("Negative stream skip length.");
            }

            var buffer = new byte[81920];
            while (count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, count));
                if (read <= 0)
                {
                    throw new EndOfStreamException();
                }

                count -= read;
            }
        }

        private static void ReadExactly(Stream stream, byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                var read = stream.Read(buffer, offset, count);
                if (read <= 0)
                {
                    throw new EndOfStreamException();
                }

                offset += read;
                count -= read;
            }
        }
    }
}
