using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.UI
{
    internal sealed partial class CatalogWindow
    {
        private sealed class AssetPickerCatalogRegistrar : ICatalogRegistrar
        {
            public int Order => 35;

            public void Register(CatalogRegistry registry)
            {
                registry.RegisterStory(new StoryRegistration(
                    "asset-picker", "Inputs", "AssetPicker",
                    "Material・Textureは参照MaterialのUV使用先などで整理し、AnimationClipは表情の分類で絞り込むブラウザーPopupです。",
                    "種類「すべて」ではPrefab・Mesh・Shader・Scriptなども含めてItem内の取り込み済みアセットを確認できます。候補へ型名を表示し、同じPopupの位置・サイズ・Item・検索を維持したまま種類を切り替えられます。種類・参照元・分類には共通の標準Fieldスタイルを使います。候補metadataとUV解析はLibraryへ永続化し、Editor再起動後も変更のないファイルの結果を再利用します。検索・分類では実assetを読み込まず、表示・操作時に解決します。" +
                    "詳細画面の最上部に戻るボタンと所属名を置き、その下に種類と検索を配置します。AnimationClipの右ペインは表情・その他で共通し、再生時間・Loop・全カーブのキーフレーム数を上に固定し、対象パスとコンポーネントごとのプロパティ一覧をスクロールして確認します。" +
                    "各プロパティには固定値または開始値から終了値への変化・キー値の範囲・キー数を表示します。行を選ぶと補間カーブとキー位置を表示し、時間Sliderやグラフのクリックで時刻と値を確認できます。単一キーは固定状態として表示し、Clipの編集は行いません。" +
                    "標準入力欄を維持し、Popupの候補を参照先へドラッグ＆ドロップします。所属カードから候補を開き、左の一覧と右の大きなプレビューで確認します。MaterialとTextureの参照元はMaterialです。Textureは同じUV0のMaterialが参照するTexture、主Texture、参照元で使用するTexture、同じフォルダー、同じItemの順で関連候補を並べます。Materialは同じUV0、同じ主Texture、同じフォルダー、同じItemの順です。AnimationClipは参照元を持たず、すべて・表情のドロップダウンで絞り込みます。表情はBodyへのBlendShapeカーブだけを持ち、イベントやオブジェクト参照カーブを含まないClipです。関連アセットと分類なしのカードは固定の検索・フォルダーアイコンを表示します。調査中のスピナーはカード一覧または候補一覧の中央へ重ねます。関連候補以外はフォルダーのパスと件数の見出しでまとめ、名前順に表示します。ホバーと選択の背景は見出しを除いた候補部分だけに付けます。フォルダーの絞り込みは右上の入力欄から行います。解析中も行とサムネイルを再利用します。クリックはプレビュー切り替えで、参照先への代入はドロップ先が処理します。フォーカスが移っても閉じず、Esc・閉じるボタンで終了します。Material詳細は回転可能な描画と主Textureを切り替えます。サンプルへのドロップはProjectへ保存しません。",
                    new[] { "NavigationItem", "SearchField", "PreviewContainer", "PreviewPane", "CustomPopup", "StatusOverlay", "UiButton" },
                    ComponentImplementationKind.UiToolkit,
                    (window, parent) =>
                    {
                        var controls = window.CreatePlainControlsSection(parent, "Popupから標準入力欄へドラッグして確認します。");
                        foreach (var type in new[] { typeof(Object), typeof(Material), typeof(Texture), typeof(AnimationClip) })
                        {
                            var field = UiTextFactory.CreateObjectField(string.Empty);
                            field.objectType = type;
                            field.allowSceneObjects = false;
                            var typeName = type == typeof(Object) ? UiLocalization.Get("ui.assetPicker.allTypes") : type.Name;
                            controls.Content.Add(new FormInput(typeName, field));
                            var reference = field;
                            if (type == typeof(Texture))
                            {
                                reference = UiTextFactory.CreateObjectField(string.Empty);
                                reference.objectType = typeof(Material);
                                reference.allowSceneObjects = false;
                                controls.Content.Add(new FormInput(UiLocalization.Get("ui.assetPicker.reference"), reference));
                            }
                            UiButton open = null;
                            open = new UiButton(UiLocalization.Get("ui.assetPicker.open") + " · " + typeName,
                                () => AssetPicker.Show(open, type,
                                    type == typeof(AnimationClip) || type == typeof(Object) ? null : reference.value));
                            controls.Content.Add(open);
                        }
                    },
                    new[]
                    {
                        "Editor/AssetManager/UI/AssetModificationWorkflowView.Workspace.cs",
                        "Editor/Feature/Avatar/FaceExpression/UI/FaceExpressionEditor.Library.cs"
                    }));
            }
        }
    }
}
