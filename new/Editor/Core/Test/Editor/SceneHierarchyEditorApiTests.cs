using Ee4v.Core.Internal.EditorAPI;
using Ee4v.Testing.Contracts;
using NUnit.Framework;

namespace Ee4v.Core.Tests
{
    public sealed class SceneHierarchyEditorApiTests
    {
        [Test]
        [FeatureTestCase(
            "SceneHierarchy item icon APIを検出できる",
            "Unity 2022.3のHierarchy TreeView itemへiconを設定するinternal APIが利用可能なことを確認します。",
            order: 269)]
        public void ItemIconApi_IsAvailable()
        {
            Assert.That(
                SceneHierarchyItemIcon
                    .IsItemIconSupported,
                Is.True);
        }

        [Test]
        [FeatureTestCase(
            "SceneHierarchy facadeは無効なitemを拒否する",
            "instance IDが0の場合はHierarchy windowの状態に触れず失敗を返すことを確認します。",
            order: 270)]
        public void TrySetItemIcon_RejectsInvalidInstanceId()
        {
            Assert.That(
                SceneHierarchyItemIcon
                    .TrySetItemIcon(0, null),
                Is.False);
        }
    }
}
