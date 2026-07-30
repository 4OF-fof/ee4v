using Ee4v.AssetManager.Contracts;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Ee4v.AssetManager.Infrastructure.Files;
using Ee4v.Testing.Contracts;
using NUnit.Framework;

namespace Ee4v.AssetManager.Infrastructure.Tests
{
    public sealed class AssetFileImportServiceTests
    {
        private string _tempRoot;
        private string _assetsDirectory;

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "ee4v-file-import-tests-" + Guid.NewGuid().ToString("N"));
            _assetsDirectory = Path.Combine(_tempRoot, "Assets");
            Directory.CreateDirectory(_assetsDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrWhiteSpace(_tempRoot) && Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
        }

        [Test]
        [FeatureTestCase(
            "通常 file を asset/file folder 配下へ取り込む",
            "Import 対象の相対 path を維持し、Assets/<asset name>/<file name>/ 配下へ copy することを確認します。",
            order: 331)]
        public void Import_CopiesRegularFileBelowAssetAndFileFolders()
        {
            var source = Path.Combine(_tempRoot, "source");
            var sourceFile = Path.Combine(source, "Textures", "albedo.png");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceFile));
            File.WriteAllText(sourceFile, "texture");
            var environment = new RecordingImportEnvironment(_assetsDirectory);

            AssetFileImportResult result = null;
            AssetFileImportService.Import(
                "Fancy Asset",
                "avatar.zip",
                source,
                new[] { "Textures/albedo.png" },
                environment,
                showUnityPackageImportDialog: true,
                completed: value => result = value);

            var imported = Path.Combine(_assetsDirectory, "Fancy Asset", "avatar", "Textures", "albedo.png");
            Assert.That(File.ReadAllText(imported), Is.EqualTo("texture"));
            Assert.That(environment.RefreshCount, Is.EqualTo(1));
            Assert.That(environment.ImportedPackages, Is.Empty);
            Assert.That(
                result,
                Is.Not.Null);
            Assert.That(
                result.AssetGuids,
                Is.EquivalentTo(new[]
                {
                    RecordingImportEnvironment.RootGuid,
                    RecordingImportEnvironment.FileGuid
                }));
        }

        [Test]
        [FeatureTestCase(
            "ZIP 内 unitypackage を確認画面なしの import へ渡す",
            "ZIP entry を一時展開し、interactive=false を渡して import 完了後に一時 file を削除することを確認します。",
            order: 332)]
        public void Import_ExtractsUnityPackageAndUsesPackageImporter()
        {
            var zipPath = Path.Combine(_tempRoot, "avatar.zip");
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("avatar/Packages/avatar.unitypackage");
                using (var writer = new StreamWriter(entry.Open()))
                {
                    writer.Write("package");
                }
            }

            var environment = new RecordingImportEnvironment(_assetsDirectory);
            AssetFileImportService.Import(
                "Fancy Asset",
                "avatar.zip",
                zipPath,
                new[] { "Packages/avatar.unitypackage" },
                environment,
                showUnityPackageImportDialog: false,
                completed: _ => { });

            Assert.That(environment.ImportedPackages, Is.EqualTo(new[] { "package" }));
            Assert.That(environment.PackageInteractiveValues, Is.EqualTo(new[] { false }));
            Assert.That(File.Exists(environment.PackagePaths[0]), Is.False);
            Assert.That(environment.RefreshCount, Is.Zero);
        }

        [Test]
        [FeatureTestCase(
            "ZIP と同名の root folder を除いて通常 file を import する",
            "File Tree で省略された relative path から実 entry を解決し、同名 folder を destination に作らないことを確認します。",
            order: 334)]
        public void Import_CopiesArchiveEntryWithoutMatchingRootFolder()
        {
            var zipPath = Path.Combine(_tempRoot, "avatar.zip");
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("avatar/Textures/albedo.png");
                using (var writer = new StreamWriter(entry.Open()))
                {
                    writer.Write("texture");
                }
            }

            var environment = new RecordingImportEnvironment(_assetsDirectory);
            AssetFileImportService.Import(
                "Fancy Asset",
                "avatar.zip",
                zipPath,
                new[] { "Textures/albedo.png" },
                environment,
                showUnityPackageImportDialog: true,
                completed: _ => { });

            var imported = Path.Combine(_assetsDirectory, "Fancy Asset", "avatar", "Textures", "albedo.png");
            Assert.That(File.ReadAllText(imported), Is.EqualTo("texture"));
            Assert.That(Directory.Exists(Path.Combine(_assetsDirectory, "Fancy Asset", "avatar", "avatar")), Is.False);
            Assert.That(environment.RefreshCount, Is.EqualTo(1));
        }

        [Test]
        [FeatureTestCase(
            "Import entry の path traversal を拒否する",
            "File Tree 外を指す相対 path が Assets や source の外へ copy されないことを確認します。",
            order: 335)]
        public void Import_RejectsTraversalPath()
        {
            var source = Path.Combine(_tempRoot, "source");
            Directory.CreateDirectory(source);
            var environment = new RecordingImportEnvironment(_assetsDirectory);

            var exception = Assert.Throws<AssetManagerException>(() =>
                AssetFileImportService.Import(
                    "Asset",
                    "file.zip",
                    source,
                    new[] { "../outside.txt" },
                    environment,
                    showUnityPackageImportDialog: true,
                    completed: _ => { }));

            Assert.That(exception.Code, Is.EqualTo(AssetManagerErrorCode.InvalidRequest));
            Assert.That(environment.RefreshCount, Is.Zero);
        }

        [Test]
        [FeatureTestCase(
            "UnityPackage の GUID を import 前に抽出する",
            "UnityPackage の TAR entry から重複しない Unity GUID を取得し、Project との関連付けに利用できることを確認します。",
            order: 336)]
        public void UnityPackageGuidReader_ReadsDistinctGuids()
        {
            var packagePath = Path.Combine(
                _tempRoot,
                "avatar.unitypackage");
            var firstGuid =
                "ABCDEF0123456789ABCDEF0123456789";
            var secondGuid =
                "22222222222222222222222222222222";
            WriteUnityPackage(
                packagePath,
                firstGuid,
                firstGuid,
                secondGuid);

            var guids =
                UnityPackageGuidReader.ReadGuids(packagePath);

            Assert.That(
                guids,
                Is.EqualTo(new[]
                {
                    firstGuid.ToLowerInvariant(),
                    secondGuid
                }));
        }

        private static void WriteUnityPackage(
            string path,
            params string[] guids)
        {
            using (var file = File.Create(path))
            using (var gzip = new GZipStream(
                       file,
                       CompressionMode.Compress))
            {
                for (var i = 0; i < guids.Length; i++)
                {
                    WriteTarEntry(
                        gzip,
                        guids[i] + "/pathname",
                        Encoding.UTF8.GetBytes(
                            "Assets/Test" + i + ".asset"));
                }

                gzip.Write(new byte[1024], 0, 1024);
            }
        }

        private static void WriteTarEntry(
            Stream stream,
            string name,
            byte[] data)
        {
            var header = new byte[512];
            var nameBytes = Encoding.ASCII.GetBytes(name);
            Array.Copy(
                nameBytes,
                header,
                Math.Min(nameBytes.Length, 100));
            var sizeText = Convert.ToString(
                                   data.Length,
                                   8)
                               .PadLeft(11, '0') + "\0";
            var sizeBytes = Encoding.ASCII.GetBytes(sizeText);
            Array.Copy(
                sizeBytes,
                0,
                header,
                124,
                sizeBytes.Length);
            stream.Write(header, 0, header.Length);
            stream.Write(data, 0, data.Length);
            var padding =
                (512 - data.Length % 512) % 512;
            if (padding > 0)
            {
                stream.Write(
                    new byte[padding],
                    0,
                    padding);
            }
        }

        private sealed class RecordingImportEnvironment : IAssetFileImportEnvironment
        {
            internal const string RootGuid =
                "11111111111111111111111111111111";
            internal const string FileGuid =
                "22222222222222222222222222222222";

            public RecordingImportEnvironment(string assetsDirectory)
            {
                AssetsDirectory = assetsDirectory;
            }

            public string AssetsDirectory { get; }

            public List<string> ImportedPackages { get; } = new List<string>();

            public List<string> PackagePaths { get; } = new List<string>();

            public List<bool> PackageInteractiveValues { get; } = new List<bool>();

            public int RefreshCount { get; private set; }

            public void ImportPackage(
                string packagePath,
                bool interactive,
                Action<bool> onFinished)
            {
                PackagePaths.Add(packagePath);
                PackageInteractiveValues.Add(interactive);
                ImportedPackages.Add(File.ReadAllText(packagePath));
                onFinished?.Invoke(true);
            }

            public void Refresh()
            {
                RefreshCount++;
            }

            public string GetAssetGuid(string absolutePath)
            {
                if (Directory.Exists(absolutePath))
                {
                    return RootGuid;
                }

                return File.Exists(absolutePath)
                    ? FileGuid
                    : string.Empty;
            }

            public bool AssetGuidExists(string assetGuid)
            {
                return true;
            }
        }
    }
}
