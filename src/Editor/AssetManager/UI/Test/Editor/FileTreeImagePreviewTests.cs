using System.Collections.Generic;
using System.IO;
using System.Threading;
using Ee4v.AssetManager.Contracts;
using Ee4v.Testing.Contracts;
using NUnit.Framework;

namespace Ee4v.AssetManager.UI.Tests
{
    public sealed class FileTreeImagePreviewTests
    {
        [TestCase("preview.png")]
        [TestCase("preview.jpg")]
        [TestCase("preview.jpeg")]
        [TestCase("preview.psd")]
        [FeatureTestCase(
            "File Tree の画像 tooltip 対象を判定する",
            "PNG、JPEG、PSD が画像 preview source として認識されることを確認します。",
            order: 420,
            category: FeatureTestCategory.Ui)]
        public void ImageSource_RecognizesSupportedImageExtensions(string fileName)
        {
            Assert.That(FileTreeImageSource.IsSupported(fileName), Is.True);
        }

        [Test]
        public void PreviewLoader_AllowsLargeStreamingPsdFiles()
        {
            const long gravityPsdBytes = 224765944L;

            Assert.That(FileTreeImagePreviewLoader.ResolveMaximumSourceBytes("Gravity.psd"), Is.GreaterThan(gravityPsdBytes));
            Assert.That(FileTreeImagePreviewLoader.ResolveMaximumSourceBytes("preview.png"), Is.EqualTo(64L * 1024L * 1024L));
        }

        [Test]
        public void PreviewLoader_ReadsSourceThroughInjectedFileSystemPort()
        {
            var reader = new RecordingFileSystemReader
            {
                FileContent = new byte[] { 1, 2, 3, 4 }
            };
            var source = FileTreeImageSource.FromFile(
                "preview.png",
                "virtual/preview.png");

            var result = FileTreeImagePreviewLoader.Load(
                source,
                reader,
                CancellationToken.None);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.EncodedData, Is.EqualTo(reader.FileContent));
            Assert.That(
                reader.OpenedPath,
                Is.EqualTo("virtual/preview.png"));
        }

        [Test]
        public void CancelImagePreview_ClearsDisposedCancellationSource()
        {
            var cancellation = new CancellationTokenSource();
            cancellation.Dispose();

            Assert.DoesNotThrow(() => SearchableFileTree.CancelImagePreview(ref cancellation));
            Assert.That(cancellation, Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PsdDecoder_ReadsRgbCompositeImage(bool useRleCompression)
        {
            using (var stream = CreateRgbPsd(useRleCompression))
            {
                var result = PsdCompositeImageDecoder.Decode(stream, CancellationToken.None);

                Assert.That(result, Is.Not.Null);
                Assert.That(result.Width, Is.EqualTo(2));
                Assert.That(result.Height, Is.EqualTo(1));
                Assert.That(result.RgbaData, Is.EqualTo(new byte[]
                {
                    255, 0, 0, 255,
                    0, 255, 0, 255
                }));
            }
        }

        private static MemoryStream CreateRgbPsd(bool useRleCompression)
        {
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            writer.Write(new[] { (byte)'8', (byte)'B', (byte)'P', (byte)'S' });
            WriteUInt16(writer, 1);
            writer.Write(new byte[6]);
            WriteUInt16(writer, 3);
            WriteUInt32(writer, 1);
            WriteUInt32(writer, 2);
            WriteUInt16(writer, 8);
            WriteUInt16(writer, 3);
            WriteUInt32(writer, 0);
            WriteUInt32(writer, 0);
            WriteUInt32(writer, 0);
            WriteUInt16(writer, useRleCompression ? (ushort)1 : (ushort)0);
            if (useRleCompression)
            {
                WriteUInt16(writer, 3);
                WriteUInt16(writer, 3);
                WriteUInt16(writer, 3);
                writer.Write(new byte[]
                {
                    1, 255, 0,
                    1, 0, 255,
                    1, 0, 0
                });
            }
            else
            {
                writer.Write(new byte[]
                {
                    255, 0,
                    0, 255,
                    0, 0
                });
            }
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        private static void WriteUInt16(BinaryWriter writer, ushort value)
        {
            writer.Write((byte)(value >> 8));
            writer.Write((byte)value);
        }

        private static void WriteUInt32(BinaryWriter writer, uint value)
        {
            writer.Write((byte)(value >> 24));
            writer.Write((byte)(value >> 16));
            writer.Write((byte)(value >> 8));
            writer.Write((byte)value);
        }

        private sealed class RecordingFileSystemReader
            : IAssetFileSystemReader
        {
            internal byte[] FileContent { get; set; }
            internal string OpenedPath { get; private set; }

            public bool FileExists(string path) => false;
            public bool DirectoryExists(string path) => false;

            public IReadOnlyList<AssetFileSystemEntry> GetDirectoryEntries(
                string path,
                CancellationToken cancellationToken) =>
                new AssetFileSystemEntry[0];

            public Stream OpenFile(string path, long maximumBytes)
            {
                OpenedPath = path;
                return new MemoryStream(
                    FileContent ?? new byte[0],
                    false);
            }

            public Stream OpenZipEntry(
                string archivePath,
                string entryPath,
                long maximumBytes) =>
                null;
        }
    }
}
