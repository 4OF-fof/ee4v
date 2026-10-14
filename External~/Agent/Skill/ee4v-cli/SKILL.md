---
name: ee4v-cli
description: Unityでee4v固有のVRChatアバター調査、体型・MA装着、表情・Expression Menu、AssetManagerの素材・制作物・履歴をCLIから操作するときに使う。
---

# ee4vをCLIから操作する

ee4vを導入した起動中Unity Editorへ接続する。操作は完了時にJSONを返し、画像はJSONのimages[].pathに保存される。Node.js・Python・MCPクライアントの登録は不要。

CLIはこのskillに同梱されたbin/win-x64/ee4v-cli.exeを使用する。このSKILL.mdのdirectoryを起点にexeの絶対pathを解決する。以下のee4v-cliはこの同梱exeを指し、PATHへの登録は不要。

## 接続と引数

- ee4v-cli projectsで対象を確認し、以後は --project <Projectの絶対パス> を付ける。Editor未起動ならユーザーへ伝える。
- よく使う操作は下表を使う。未掲載操作だけ ee4v-cli operations <検索語>、引数が不明な場合だけ ee4v-cli describe <操作名> を使う。全80操作の定義を毎回読み込まない。
- 通常は --avatar-ref <参照> のように名前付き引数を使う。配列は同じ引数を繰り返す。複雑な配列や空配列はJSONファイルに入れて --args-file <path> を渡す。オプション指定はファイルの同じフィールドを上書きする。
- PowerShellでは空白・カンマを含む値をsingle quoteで囲む。JSON文字列をコマンドラインへ直接渡さない。
- avatarRefはfind-avatarsまたはruntime操作の発行値だけを使う。Domain Reload、対象破棄、Play Mode切り替え後は再取得する。

| 作業 | コマンド |
|---|---|
| Avatarの発見・事実・問題 | find-avatars、inspect-avatar、audit-avatar |
| 体型と装着 | inspect-avatar-authoring、edit-avatar-shape、edit-avatar-attachment |
| 顔・Clip・分類 | inspect-face、inspect-expression-clip、upsert-expression-clip |
| Expression Menu | inspect-expression-menu、create-expression-menu-item |
| 素材検索・詳細・取り込み | asset-search、asset-get-item、asset-import |
| 制作物・差分・保存 | asset-list-variants、asset-get-variant、asset-save-variant |
| 実行時・性能 | get-avatar-runtime、control-avatar-runtime、measure-avatar-performance |

## 作業に応じて読む

- 体型・MA装着・実行時確認は [avatar.md](references/avatar.md)。
- 表情、アニメーション、FacialSet、メニューは [expression.md](references/expression.md)。
- 素材検索・取り込み、制作物・保存版・ギャラリーは [assets.md](references/assets.md)。

## 編集と結果

現在の対象・revisionを調査してから編集する。対応操作はdryRunまたはplanで入力を検証する。CLIの操作がScene編集、Asset保存、制作履歴保存のどこまで行うかは別々なので、必要な保存を選ぶ。汎用Hierarchy、Component、Material、Scene操作は利用可能なUnity CLIへ任せる。

結果はresult、失敗はisErrorと終了コードで判断する。結果が必要な情報を含む場合は確認だけの再取得を繰り返さない。画像の目視確認や、保存後の再読み込みでしか確認できない状態は実際に確認する。

既知の依存した操作をシェルでまとめる場合は、失敗時に後続を止める。

~~~powershell
$ee4vCli = '<このskillの絶対path>/bin/win-x64/ee4v-cli.exe'
function e { & $ee4vCli @args; if ($LASTEXITCODE) { throw "ee4v-cli failed ($LASTEXITCODE)" } }
e inspect-avatar --project 'C:\Unity\MyProject' --avatar-ref '<発行された参照>'
~~~

stderrにジョブIDが出る。終了コード4は待機中断・timeoutで、書き込みの失敗や取消を意味しない。ee4v-cli wait <id> --project <path> で結果を取る。待機を続けないならcancel <id>の結果を確認する。開始済み処理の巻き戻しは保証されない。接続切れ・結果不明・Domain Reloadで失効したジョブを、同じ書き込みの自動再実行で補わない。

取得済みの完了jobはrelease <id>で除去できる。結果の保持はdomain内で最大64件、完了後10分。job_limitの場合は取得済みjobをreleaseする。必要な結果を保持しているjobを先に除去しない。
