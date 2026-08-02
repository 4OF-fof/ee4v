using Ee4v.Testing.Contracts;
using NUnit.Framework;

[assembly: FeatureTestSuite(
    "AvatarModify Domain",
    "AvatarModify",
    "Ee4v.AvatarModify.Domain.Tests.Editor",
    "AvatarModify の選択、派生方法、build session の規則を確認します。",
    order: 305)]

namespace Ee4v.AvatarModify.Domain.Tests
{
    public sealed class AvatarSelectionPolicyTests
    {
        [Test]
        [FeatureTestCase(
            "Descriptor を持つ Prefab を自動選択する",
            "複数候補のうち Avatar Descriptor を持つ候補が一件だけなら、その Prefab を選択します。",
            order: 1)]
        public void SelectAutomatically_PrefersSingleAvatarDescriptor()
        {
            var selected = AvatarSelectionPolicy.SelectAutomatically(
                new[]
                {
                    new PrefabCandidate("plain", "Assets/Plain.prefab", false),
                    new PrefabCandidate("avatar", "Assets/Avatar.prefab", true)
                });

            Assert.That(selected, Is.EqualTo("avatar"));
        }

        [Test]
        [FeatureTestCase(
            "複数の Avatar Prefab は利用者が選択する",
            "Descriptor を持つ Prefab が複数ある場合に誤って自動選択しないことを確認します。",
            order: 2)]
        public void SelectAutomatically_DoesNotChooseAmongMultipleDescriptors()
        {
            var selected = AvatarSelectionPolicy.SelectAutomatically(
                new[]
                {
                    new PrefabCandidate("a", "Assets/A.prefab", true),
                    new PrefabCandidate("b", "Assets/B.prefab", true)
                });

            Assert.That(selected, Is.Empty);
        }

    }
}
