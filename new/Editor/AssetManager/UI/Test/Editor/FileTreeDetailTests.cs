using Ee4v.Core.Testing;
using NUnit.Framework;

namespace Ee4v.AssetManager.Tests
{
    public sealed class FileTreeDetailTests
    {
        [Test]
        [FeatureTestCase(
            "ZIP 内要素を詳細表示 state に変換する",
            "File Tree の ZIP 内 entry でも名前を持つ詳細表示 state を生成できることを確認します。",
            order: 430,
            category: FeatureTestCategory.Ui)]
        public void ZipEntry_CreatesFileDetailState()
        {
            var node = new FileTreeNode(
                "preview.png",
                string.Empty,
                "archive/content/preview.png",
                relativePath: "content/preview.png",
                detailParentName: "archive.zip");

            var state = node.CreateDetailState("item-1");

            Assert.That(state.Name, Is.EqualTo("preview.png"));
            Assert.That(state.ParentName, Is.EqualTo("archive.zip"));
            Assert.That(state.Id, Does.Contain("archive/content/preview.png"));
        }

        [Test]
        public void FileDetailHistory_SupportsBackAndForward()
        {
            var history = new AssetItemGridHistory();
            var list = new AssetItemGridHistoryEntry(
                AssetItemGridHistoryEntryKind.FileList,
                "all-assets",
                "All Assets",
                "item-1",
                "Avatar");
            var detail = new AssetItemGridHistoryEntry(
                AssetItemGridHistoryEntryKind.FileDetail,
                "all-assets",
                "All Assets",
                "item-1",
                "Avatar",
                detailId: "item-1|preview.png",
                detailName: "preview.png",
                detailParentName: "archive.zip");

            history.SetCurrent(list);
            history.SetCurrent(detail);

            Assert.That(history.State.Current.Breadcrumbs, Is.EqualTo(new[]
            {
                "All Assets",
                "Avatar",
                "archive.zip",
                "preview.png"
            }));
            Assert.That(history.TryGoBack(out var previous), Is.True);
            Assert.That(previous.Kind, Is.EqualTo(AssetItemGridHistoryEntryKind.FileList));
            Assert.That(history.TryGoForward(out var next), Is.True);
            Assert.That(next.Kind, Is.EqualTo(AssetItemGridHistoryEntryKind.FileDetail));
            Assert.That(next.DetailName, Is.EqualTo("preview.png"));
            Assert.That(next.DetailParentName, Is.EqualTo("archive.zip"));
        }
    }
}
