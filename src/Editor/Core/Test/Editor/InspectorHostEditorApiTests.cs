using Ee4v.Core.Internal.EditorAPI;
using Ee4v.Testing.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Ee4v.Core.Tests
{
    public sealed class InspectorHostEditorApiTests
    {
        [Test]
        [FeatureTestCase(
            "Inspector host facade は対象外 window を変更しない",
            "InspectorWindow 以外を明示した場合に snapshot を返さないことを確認します。",
            order: 262)]
        public void NonInspectorWindow_ReturnsFalse()
        {
            var window =
                ScriptableObject.CreateInstance<
                    TestEditorWindow>();
            try
            {
                Assert.That(
                    InspectorHost.TryGetSnapshot(
                        window,
                        out _),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        private sealed class TestEditorWindow : EditorWindow
        {
        }
    }
}
