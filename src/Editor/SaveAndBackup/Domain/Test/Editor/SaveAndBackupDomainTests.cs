using Ee4v.SaveAndBackup.Domain;
using Ee4v.Testing.Contracts;
using NUnit.Framework;

[assembly: FeatureTestSuite(
    "SaveAndBackup Domain",
    "SaveAndBackup",
    "Ee4v.SaveAndBackup.Domain.Tests.Editor",
    "バックアップタイミングの状態遷移を確認します。",
    order: 308)]

namespace Ee4v.SaveAndBackup.Domain.Tests
{
    public sealed class SaveAndBackupSessionTests
    {
        [Test]
        public void UploadSuccessTrigger_WaitsForUpload()
        {
            var session = new SaveAndBackupSession(
                "record",
                "snapshot",
                SaveAndBackupTrigger.UploadSuccessOnly);

            Assert.That(session.OnBuildSucceeded(), Is.False);
            Assert.That(session.OnUploadSucceeded(), Is.True);
            Assert.That(session.OnUploadSucceeded(), Is.False);
        }

        [Test]
        public void Failure_PreventsLaterCommit()
        {
            var session = new SaveAndBackupSession(
                "record",
                "snapshot",
                SaveAndBackupTrigger.BuildSuccess);

            Assert.That(session.OnFailed(), Is.True);
            Assert.That(session.OnBuildSucceeded(), Is.False);
        }
    }
}
