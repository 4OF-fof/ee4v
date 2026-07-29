using System;
using System.Collections.Generic;
using Ee4v.AssetManager.Application.Ports;
using Ee4v.AssetManager.Contracts;
using Ee4v.Testing.Contracts;
using NUnit.Framework;

[assembly: FeatureTestSuite(
    "AssetManager Application",
    "AssetManager",
    "Ee4v.AssetManager.Application.Tests.Editor",
    "AssetManager use case の transaction 境界と通知順序を確認します。",
    order: 302)]

namespace Ee4v.AssetManager.Application.Tests
{
    public sealed class SetFileImportTargetsUseCaseTests
    {
        [Test]
        [FeatureTestCase(
            "Import Target の置換後に粒度別通知を発行する",
            "正規化済み path を一度の置換へ渡し、commit 後に Import Target と File Tree の通知を順に発行することを確認します。",
            order: 1)]
        public void Execute_ReplacesTargetsBeforePublishingChanges()
        {
            var store = new RecordingImportTargetStore
            {
                ReadResult = new[]
                {
                    new AssetFileImportTarget
                    {
                        Id = "target-1",
                        FileId = "file-1",
                        RelativePath = "Packages/avatar.unitypackage"
                    }
                }
            };
            var changes = new List<AssetManagerChange>();
            var useCase = new SetFileImportTargetsUseCase(store, store, changes.Add);

            useCase.Execute(
                "file-1",
                new[]
                {
                    new AssetFileImportTargetRequest
                    {
                        RelativePath = "\\Packages\\avatar.unitypackage/"
                    },
                    new AssetFileImportTargetRequest
                    {
                        RelativePath = "packages/avatar.unitypackage"
                    },
                    null
                });

            Assert.That(store.ReplaceCallCount, Is.EqualTo(1));
            Assert.That(store.LastFileId, Is.EqualTo("file-1"));
            Assert.That(store.LastPaths, Is.EqualTo(new[] { "Packages/avatar.unitypackage" }));
            Assert.That(store.ReadObservedAfterReplace, Is.True);
            Assert.That(
                changes.ConvertAll(change => change.Kind),
                Is.EqualTo(new[]
                {
                    AssetManagerChangeKind.FileImportTargets,
                    AssetManagerChangeKind.FileTree
                }));
            Assert.That(changes[0].ImportTargets, Is.SameAs(store.ReadResult));
        }

        [Test]
        [FeatureTestCase(
            "Import Target の置換失敗時は通知しない",
            "transactional store が失敗した場合に読み戻しと change 通知を行わないことを確認します。",
            order: 2)]
        public void Execute_StoreFailure_DoesNotPublishChanges()
        {
            var store = new RecordingImportTargetStore
            {
                ReplaceException = new InvalidOperationException("transaction failed")
            };
            var changes = new List<AssetManagerChange>();
            var useCase = new SetFileImportTargetsUseCase(store, store, changes.Add);

            Assert.Throws<InvalidOperationException>(
                () => useCase.Execute(
                    "file-1",
                    new[]
                    {
                        new AssetFileImportTargetRequest { RelativePath = "Textures/albedo.png" }
                    }));

            Assert.That(store.ReplaceCallCount, Is.EqualTo(1));
            Assert.That(store.ReadCallCount, Is.Zero);
            Assert.That(changes, Is.Empty);
        }

        [Test]
        [FeatureTestCase(
            "不正な Import Target は永続化前に拒否する",
            "domain validation が失敗した場合に store を呼ばず InvalidRequest を返すことを確認します。",
            order: 3)]
        public void Execute_InvalidPath_DoesNotCallStore()
        {
            var store = new RecordingImportTargetStore();
            var useCase = new SetFileImportTargetsUseCase(store, store, _ => { });

            var exception = Assert.Throws<AssetManagerException>(
                () => useCase.Execute(
                    "file-1",
                    new[]
                    {
                        new AssetFileImportTargetRequest { RelativePath = "../outside" }
                    }));

            Assert.That(exception.Code, Is.EqualTo(AssetManagerErrorCode.InvalidRequest));
            Assert.That(store.ReplaceCallCount, Is.Zero);
            Assert.That(store.ReadCallCount, Is.Zero);
        }

        private sealed class RecordingImportTargetStore :
            IAssetImportTargetReadStore,
            IAssetImportTargetCommandStore
        {
            internal IReadOnlyList<AssetFileImportTarget> ReadResult { get; set; } =
                Array.Empty<AssetFileImportTarget>();
            internal Exception ReplaceException { get; set; }
            internal int ReplaceCallCount { get; private set; }
            internal int ReadCallCount { get; private set; }
            internal string LastFileId { get; private set; }
            internal IReadOnlyList<string> LastPaths { get; private set; }
            internal bool ReadObservedAfterReplace { get; private set; }

            public IReadOnlyList<AssetFileImportTarget> GetFileImportTargets(string fileId)
            {
                ReadCallCount++;
                ReadObservedAfterReplace = ReplaceCallCount > 0;
                return ReadResult;
            }

            public void ReplaceFileImportTargets(
                string fileId,
                IReadOnlyList<string> normalizedRelativePaths)
            {
                ReplaceCallCount++;
                LastFileId = fileId;
                LastPaths = normalizedRelativePaths;
                if (ReplaceException != null)
                {
                    throw ReplaceException;
                }
            }
        }
    }

    public sealed class AssetManagerChangePublisherTests
    {
        [Test]
        [FeatureTestCase(
            "Change subscriber の失敗を command から隔離する",
            "一つの subscriber が例外を投げても後続 subscriber へ通知し、診断 port へ失敗を報告することを確認します。",
            order: 4)]
        public void Publish_SubscriberFailure_DoesNotStopOtherSubscribers()
        {
            var diagnostics = new RecordingDiagnostics();
            var publisher = new AssetManagerChangePublisher(diagnostics);
            var observed = 0;
            publisher.Changed += _ =>
                throw new InvalidOperationException("broken view");
            publisher.Changed += _ => observed++;

            Assert.DoesNotThrow(() => publisher.Publish(
                new AssetManagerChange(AssetManagerChangeKind.Catalog)));
            Assert.That(observed, Is.EqualTo(1));
            Assert.That(diagnostics.Failures.Count, Is.EqualTo(1));
            Assert.That(
                diagnostics.Failures[0].Message,
                Is.EqualTo("broken view"));
        }

        private sealed class RecordingDiagnostics
            : IAssetManagerDiagnostics
        {
            internal List<Exception> Failures { get; } =
                new List<Exception>();

            public void ReportChangeSubscriberFailure(Exception exception)
            {
                Failures.Add(exception);
            }
        }
    }

}
