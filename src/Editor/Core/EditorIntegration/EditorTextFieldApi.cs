using Ee4v.Core.Internal.EditorAPI.Backends;
using UnityEngine.UIElements;

namespace Ee4v.Core.EditorIntegration
{
    public static class EditorTextFieldApi
    {
        public static bool ConfigureMultilineScroll(
            TextField textField,
            bool useVerticalScroll,
            float maxHeight)
        {
            return TextFieldMultilineScrollBackend.Configure(
                textField,
                useVerticalScroll,
                maxHeight);
        }
    }
}
