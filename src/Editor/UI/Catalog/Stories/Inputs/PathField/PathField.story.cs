using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class PathFieldCatalogRegistrar : ICatalogRegistrar
        {
            public int Order => 34;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStory(new StoryRegistration(
                    "path-field", "Inputs", "PathField",
                    "手入力とOSの選択ダイアログでフォルダー・ファイルのパスを指定する入力欄です。",
                    "文字入力と枠線のないフォルダーアイコンを一つの下線内へ配置します。手入力はEnterまたはフォーカスを外した時に確定し、選択のキャンセルでは値を変更しません。内容の必要幅を基準に、最小240pxから枠内の利用可能幅まで伸縮します。サンプルの値は設定へ保存しません。",
                    new[] { "InputField", "UiButton" },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) => window.BuildPathFieldStory(parent),
                    new[] { "Editor/Core/Presentation/Settings/PathSettingDrawer.cs" }));
            }
        }

        private void BuildPathFieldStory(VisualElement parent)
        {
            var controls = CreatePlainControlsSection(parent,
                "フォルダーとDBファイルの選択、手入力、Window幅に応じた伸縮を確認します。");
            controls.Content.Add(new FormInput("フォルダー", new PathField()));
            controls.Content.Add(new FormInput("DBファイル",
                new PathField(PathFieldKind.File, extension: "db")));
        }
    }
}
