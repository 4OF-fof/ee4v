using System;
using System.Collections.Generic;
using Ee4v.AssetManager.Contracts;
using Ee4v.Core.I18n;
using Ee4v.UI;
using UnityEngine.UIElements;

namespace Ee4v.AssetManager.UI
{
    internal sealed class TagListPage : VisualElement
    {
        private const string RootClassName = "ee4v-asset-manager-tag-list";
        private const string HeaderClassName = "ee4v-asset-manager-tag-list__header";
        private const string ContentClassName = "ee4v-asset-manager-tag-list__content";
        private const string ItemClassName = "ee4v-asset-manager-tag-list__item";
        private const string ItemTextClassName = "ee4v-asset-manager-tag-list__item-text";
        private const string EmptyClassName = "ee4v-asset-manager-tag-list__empty";
        private readonly UiTextElement _header;
        private readonly VisualElement _content;
        private readonly UiTextElement _empty;
        private IReadOnlyList<AssetTag> _tags = Array.Empty<AssetTag>();

        public TagListPage()
        {
            AddToClassList(RootClassName);

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1f;

            _header = UiTextFactory.Create(string.Empty, HeaderClassName);
            _content = new VisualElement();
            _content.AddToClassList(ContentClassName);
            _empty = UiTextFactory.Create(
                I18N.Get("assetManager.mainView.tags.empty"),
                EmptyClassName);

            scrollView.Add(_header);
            scrollView.Add(_content);
            scrollView.Add(_empty);
            Add(scrollView);

            SetTags(null);
        }

        internal IReadOnlyList<AssetTag> Tags
        {
            get { return _tags; }
        }

        internal bool IsEmpty
        {
            get { return _content.childCount == 0; }
        }

        public void SetTags(IReadOnlyList<AssetTag> tags)
        {
            _tags = tags ?? Array.Empty<AssetTag>();
            _header.SetText(string.Format(
                I18N.Get("assetManager.mainView.tags.title"),
                _tags.Count));
            _content.Clear();

            for (var i = 0; i < _tags.Count; i++)
            {
                var tag = _tags[i];
                if (tag == null)
                {
                    continue;
                }

                var item = new VisualElement();
                item.AddToClassList(ItemClassName);
                item.Add(UiTextFactory.Create(tag.Name, ItemTextClassName));
                _content.Add(item);
            }

            _empty.style.display = _content.childCount == 0
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        public void SetLoading()
        {
            _tags = Array.Empty<AssetTag>();
            _header.SetText(string.Format(
                I18N.Get("assetManager.mainView.tags.title"),
                0));
            _content.Clear();
            _empty.style.display = DisplayStyle.None;
        }
    }
}
