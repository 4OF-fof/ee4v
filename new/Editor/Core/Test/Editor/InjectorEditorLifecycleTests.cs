using System.Linq;
using System.Runtime.CompilerServices;
using Ee4v.Core.Injector;
using Ee4v.Testing.Contracts;
using NUnit.Framework;
using UnityEditor;

namespace Ee4v.Core.Tests
{
    public sealed class InjectorEditorLifecycleTests
    {
        [Test]
        [FeatureTestCase(
            "modifier変更時にitem windowを再描画する",
            "非フォーカスのHierarchyとProject windowでもAlt操作を検出できるようmodifier callbackが登録されていることを確認します。",
            order: 255)]
        public void ModifierKeysChanged_RepaintsItemWindows()
        {
            RuntimeHelpers.RunClassConstructor(
                typeof(InjectorEditorLifecycle)
                    .TypeHandle);

            var callbacks =
                EditorApplication.modifierKeysChanged;
            Assert.That(callbacks, Is.Not.Null);
            Assert.That(
                callbacks
                    .GetInvocationList()
                    .Any(callback =>
                        callback.Method.DeclaringType ==
                        typeof(InjectorEditorLifecycle) &&
                        callback.Method.Name ==
                        "RepaintItemWindows"),
                Is.True);
        }
    }
}
