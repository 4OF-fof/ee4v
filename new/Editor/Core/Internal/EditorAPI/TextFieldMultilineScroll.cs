using Ee4v.Core.Internal.EditorAPI.Backends;
using UnityEngine.UIElements;

namespace Ee4v.Core.Internal.EditorAPI
{
    internal static class TextFieldMultilineScroll
    {
        public static bool Configure(TextField textField, bool useVerticalScroll, float maxHeight)
        {
            return TextFieldMultilineScrollBackend.Configure(textField, useVerticalScroll, maxHeight);
        }
    }
}
