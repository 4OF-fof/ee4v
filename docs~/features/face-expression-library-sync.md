# 表情ライブラリの共有保存と自動Import

表情はAssetManagerと同じ`asset-manager-v1.db`内の専用テーブルで管理します。datasourceはee4v固定で、通常アセットのdatasource設定から独立しています。表示・編集・割り当ての入口は表情タブのライブラリです。

## 所有とDB

AssetManagerのInfrastructureが共有保存・同期・DB・Import・GUID関連付けを所有します。SQLは既存の`SqliteAssetManagerStore`のpartialとして実装し、接続・schema初期化・transactionを共用します。表情用の別DBファイルやFaceExpression側のSQL管理層はありません。

| テーブル | 内容 |
| --- | --- |
| `face_expression` | 表情ID・名前・論理フォルダー・主対象FBX・ルートClip GUID・revision・metadata |
| `face_expression_target` | 使用FBX GUID・Mesh local ID・元Renderer path・BlendShape名 |
| `face_expression_asset` | 構成する実体のGUID・相対path・本体とmetaのhash |
| `face_expression_dependency` | Clip間の依存と、通常Sourceへの依存情報 |
| `face_expression_import` | Project識別値・表情ID・実体GUIDごとのImport済みrevisionと基準hash |

表情はItem／File・制作物Item・Variantへ登録しません。通常AssetManagerの一覧・検索・Collection・Tag候補・未所属File・Archive・素材Pickerへ混在させるための変更もありません。通常Sourceの同期は既存テーブル、表情同期は専用テーブルだけを更新します。

表情IDはUUID、ClipのUnity GUIDはImport時の識別です。Project識別値は現在のAssetsディレクトリの絶対pathを正規化したhashです。別ProjectのImport状態を流用せず、実行時に現在のAssetDatabaseでもGUIDを照合します。

schemaはv1のままです。表情の定義・実体・対象・依存は共有metadataから再構築できます。DB再生成後にImportの基準が不明な既存Clipは、内容が共有版と一致すれば再利用し、不一致なら自動上書きしません。

## 共有データ

保存先は`GlobalDataSettings.RootDirectory`の下です。

```text
Assets/FaceExpression/<表情ID>/
  metadata.json
  content/<revisionの先頭16文字>/
    <元Clipファイル名>.anim
    <元Clipファイル名>.anim.meta
    Dependencies/<GUID>/<参照Clipファイル名>.anim
    Dependencies/<GUID>/<参照Clipファイル名>.anim.meta
    preview.png
```

metadataにはschemaVersion 1、名前・論理フォルダー・主対象FBX・channelごとの対象、ルートGUID、実体と依存、内容revisionを保存します。ClipはUnityの形式を維持し、カーブ・補間・frame rate・Loop・`FaceExpressionSequenceData`のポーズ名と参照Clipを独自形式へ変換しません。`.meta`はUnityが生成したものをコピーします。

作成直後の空Clipも主対象FBXを持つ表情として保存します。この場合channel対象は空で、Import時は主対象FBXを検証します。カーブを追加して保存すると使用channelの対象情報も更新します。

保存はGUIDごとの排他ロック内で同期と期待revisionの照合を使用します。実体を一時領域へ完成させてから内容ディレクトリへコピーし、最後にmetadataを原子的に確定します。初回公開はディレクトリのrenameに依存せず、metadata未確定の新規entryは一覧へ出しません。公開失敗時の未完成内容は削除します。revisionは内容のSHA-256で、短縮した内容ディレクトリが既存の場合も実体hashを照合します。同じ内容ではmetadataを書き直しません。

Previewは保存時の対応アバターから顔画像を生成し、カードで再利用します。Previewは内容revisionから除外します。共有データへの書き込み失敗ではローカル編集を維持し、共有保存成功・DB更新失敗では完成した正本を残して再同期で修復します。

## 対応FBXと参照

channelはFBX GUID・Mesh local ID・元Renderer path・正確なBlendShape名で照合します。主対象はBodyのFBXを既定とし、Bodyがなく使用FBXが1件ならそのFBXを使用します。複数FBXで主対象が決まらない場合は公開APIの`primaryFbxGuid`で指定します。同じFBX名やShape名だけで互換とみなしません。

Import前後の対応検証は保存された対象のRenderer・Mesh・BlendShapeを直接照合し、候補に無関係な全BlendShapeの表示データは生成しません。

複数Mesh・FBXを使う表情は1つの表情として保持し、すべてのchannelが一致してから割り当てます。対応FBXは識別情報であり、アバターPrefab・FBX・Material一式を表情へコピーする依存ではありません。Renderer pathが違う場合は既存の明示的な表情変換を使用します。

参照Clipは必要な実体として保存します。通常SourceからImport済みのClipは、保存したSourceType・SourceId・取り込み対象から既存のFile Importへ接続します。独自の参照Clipは依存実体として含め、循環や曖昧な取り込み対象を拒否します。未登録のFBX内Clipを独自の.animとして保存することはできません。

表情として公開するClipは既存validationを使用し、対象外curve・Object Reference Curve・Animation Event・不一致bindingを拒否します。通常SourceからImportしたルートClipを共有表情として編集する場合は、先にコピーして別GUIDを作成します。

## 表情画面

ライブラリは現在のアバターのFBXに対応する共有表情を基本表示にし、Project内の未共有Clipも同じ一覧へ表示します。共有済みClipと依存Clipの実体は重複表示しません。未共有Clipは現在のアバターへの既存binding検証で分類します。論理フォルダーとProject内の相対フォルダーをまとめ、検索語で一覧を絞ります。未Importの共有Clipもカードとして表示します。

設定済みの`Animation/Facial`配下にある未登録の`.anim`は、下位フォルダーを含めて一覧更新時に自動登録の対象にします。現在のアバターでbindingとFBX対象を確定できるClipを、Project内の相対フォルダーを維持して共有ライブラリへ登録します。対象が一致しないClipはローカル候補として残し、対応アバター選択時に再判定します。対象フォルダー外のClipはこの自動登録には含めません。

一覧を先に表示し、自動登録はEditor更新ごとに最大1件ずつ進めます。画像は画面の隔離したサムネイル用Previewを再利用して生成し、描画元のsignatureを登録バッチの先頭で確認します。登録済みのルート・依存GUIDは再登録せず、対象Clip・参照する独立`.anim`・それらのsub assetに未保存の編集があれば自動確定しません。登録失敗はClip名と理由を表示してローカル候補を維持し、同じ内容での自動再試行を繰り返しません。Clipの内容変更・対象アバター変更・共通保存先変更で再判定します。画面終了・対象変更で保留中の登録を取り消します。共有登録の完了時は候補選択と割り当てを維持し、ダブルクリックの判定中はカードを差し替えません。

別アバター向けの候補がある場合、一覧ルートの末尾にフォルダーカード「その他のアバターの表情」を配置します。開くと現在のアバター以外の候補とそのフォルダーを表示し、「戻る」で元の一覧へ戻ります。アバター切り替え時はそのアバターの一覧ルートへ戻ります。共有／Project内・現在／全アバターの切り替えボタンや再読込ボタンは設けません。

単クリックとEnter／Spaceは候補選択だけを行い、ProjectへImportしません。左右の条件が揃う操作やメニュー登録で必要なClipを取り込みます。ダブルクリック・編集・コピーして編集でも、必要なら同じImport入口を使用します。既存の5列Gridと割り当てバッジを使用します。

フォルダー・「その他のアバターの表情」・「戻る」・「新規表情」はシングルクリックまたはEnter／Spaceで実行します。選択と編集をクリック回数で分けるのは表情カードだけです。

選択状態は既存カードのclassを更新し、選択だけでカードを再生成しません。同じ候補の再選択ではPickerやImport要求を再初期化しません。共有PreviewのTextureは表情IDとPreview hashで再利用し、該当画像が変わった場合や削除・保存先変更・画面終了時に解放します。共有済みClipは未共有Clip用のbinding分類検証から除外します。

表情の保存はローカルClipの確定と共有ライブラリへの保存を同じ処理で行います。新規作成・コピー・変換したClipは作成時に保存し、編集したすべてのClipは「保存」、対象変更前の確定、Window終了、assembly reload、Play Mode開始時の確定でも共有版へ保存します。共有保存だけの右クリック操作は設けません。スライダー操作ごとには共有版を書き換えず、未保存変更がないClipを選択しただけでも更新しません。共有保存失敗では編集Clipを保留状態に維持して再試行します。

カードの右クリックには「ライブラリから削除」を配置します。共有済み表情は期待revisionを確認し、共有metadata・内容ディレクトリ・専用DBの定義とImport記録、現在のProjectにあるルートClipを削除します。未Importの候補も共有ライブラリから削除できます。Projectにだけ存在するClipはProject実体を削除します。参照元の別Clipや通常Source素材を同時削除する操作ではありません。現在のアバターにある対象ClipのGesture・メニュー割り当ても解除し、選択・編集用snapshotを整理します。

共有表情の削除は通常Importと同じ排他ゲートとGUIDロックを使います。共有ディレクトリを索引対象外へ移してからProject実体とDBを削除し、失敗時は保存したClip・metaと共有ディレクトリを戻します。古いrevisionの削除は拒否します。他Projectにある実体を直接削除する操作は行いません。

Gestureの64組、メニュー名・登録、まばたき・口固定とFacialSet生成物は使用先アバターの設定です。表情の同期やカード選択で変更しません。

## 同期・Importと失敗

`AssetManagerFactory.OpenExpressionSession(databasePath)`は通常CatalogのSource設定から独立した表情サービスを提供します。既定の入口は共通データの同じDBへ接続します。通常AssetManager Windowの起動は不要です。

初回表示・一覧の再構築・フォルダー移動・アバター切り替え・共有保存時に共有metadataを読みます。Sourceが不正・不足・読込不能ならsnapshotを適用せず、既存の表情索引を維持します。正常な空ライブラリと失敗を区別します。共通保存先変更ではサービスを再取得し、旧選択と保留中のImport要求を取り消します。

同期では検証済みmetadata・本体・metaの長さ、更新時刻、作成時刻とディレクトリ属性を確認し、変更のない実体の再hashとmetadata解析を省きます。全snapshotの検証後に変更・追加・削除がある定義だけをDBへ適用し、変更がなければDB更新とChanged通知を省きます。保存・Import・削除では対象の内容hashを改めて照合し、時刻を維持した改変も処理前に拒否します。PreviewのhashもFile情報が変わるまで再利用します。一覧のキャッシュは外部ファイル編集による情報変更を次回同期で反映し、ディレクトリ・ファイルのリンクを許可しません。

割り当ては次の順で処理します。

1. 表情と対象アバターを検証する。
2. AssetManagerでImport済みGUID・内容・基準hashを確認する。
3. 未Importまたはローカル未変更の古い内容なら、必要な依存とClipを自動Importする。
4. 読込結果、参照・対象・Source revisionと要求の有効性を再確認する。
5. 既存のGesture／メニュー割り当てへ渡す。

Import先は共通生成ルートの`Animation/Facial/<表情ID>/`です。GUIDを維持してコピーし、移動済みAssetもGUIDで再利用します。同じ表情への要求を集約し、通常File Importと同じ排他ゲートを使用します。コピー・衝突検証・Unity refreshは`AssetTargetImporter`に集約し、仮のItem／Fileを作りません。

基準hashには本体と正規化したmeta設定を含めます。dirtyなClip、未共有変更、別GUIDによるpath占有、基準不明の不一致を自動上書きしません。コピー前に対象を検査し、コピー失敗は既存Fileを退避内容から復元します。

Import失敗・キャンセル・アバター切替・候補変更では割り当てを変更しません。Import中は入力を無効化し、古い要求を新しい条件へ適用しません。Import完了後に要求が取り消された場合、Import済み実体は再利用できる状態で残り、割り当てだけを中止します。

表情GUIDは専用Importテーブルで管理し、通常の`file_imported_asset_guid`へ登録しません。表情は編集可能とし、通常Sourceの依存素材は既存のAsset Protectionを維持します。

## モジュールと公開入口

`src/Editor/Feature/Shared/AssetLibrary`の`Ee4v.AssetLibrary.Editor`をAssetManager InfrastructureとFaceExpressionが使用します。同じDB・共有保存・Importを利用するための契約で、機能間の直接依存は追加しません。

`IExpressionLibrary`は同期・一覧・GUID逆引き・編集元revision・公開・削除・Import・Preview取得を提供します。AssetManagerが実装を登録し、FaceExpressionはこの契約だけを使用します。

`FaceExpressionLibraryApi.Publish`はAvatarとClipから対象情報を作り、共有保存へ渡します。`ValidateTarget`と`Import`はUIと同じ対象検証・自動Importを外部integrationへ公開します。Clip編集・対象検証・Preview・割り当て・表示はFaceExpressionが所有します。

MCPも静止Clip・アニメーション・変換のwriterから同じ共有保存へ接続します。新規作成の`name`と任意の`libraryFolder`は共通生成ルートの`Animation/Facial`配下へ解決します。共有一覧とImportのtoolを提供し、未Importの候補は返却された実体pathで既存の編集・割り当てへ接続します。MCP assemblyは共有のAssetLibrary契約を直接参照し、SQLや独自の表情保存基盤を持ちません。

既存のItem／File、Prefab Variant、Gallery、通常Source選択と通常AssetManager画面の契約は維持します。
