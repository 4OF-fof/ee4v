using System;
using Ee4v.AvatarEditing;
using nadena.dev.modular_avatar.core;
using UnityEngine.UIElements;

namespace Ee4v.ExpressionMenu
{
    internal sealed class ExpressionMenuGimmickCreator : VisualElement
    {
        internal ExpressionMenuGimmickCreator(AvatarEditingContext context, ModularAvatarMenuItem selected,
            Action typeChanged)
        {
            name = "gimmickCreator";
            AddToClassList("ee4v-gimmick-creator");
            if (selected == null || selected.PortableControl.Type != PortableControlType.SubMenu &&
                !ExpressionMenuTemplateModel.HasActions(selected)) return;

            var editor = new ScrollView(ScrollViewMode.Vertical);
            editor.AddToClassList("ee4v-gimmick-creator__editor");
            Add(editor);
            editor.Add(new ExpressionMenuTemplateEditor(context, selected, typeChanged));
        }
    }
}
