# CLIとskill

ee4v-cliはWindows x64で、起動中のUnity 2022.3 Editorへ接続して[現行MCP](mcp.md)の全80操作を実行する。UI・MCP・CLIは同じMcpToolRegistryと機能公開APIを使用し、入力検証、参照、revision、Undo、保存、副作用、エラーを共有する。CLIへ業務処理を複製しない。

CLIはExternal~/Cli、配布skillはExternal~/Agent/Skill/ee4v-cli。CLI固有のbuild、引数、終了コード、導入方法は[CLI README](../External~/Cli/README.md)を参照する。

CLI build成果物はskillのbin/win-x64/ee4v-cli.exeへ同梱し、SKILL.md・reference・exeを同じ単位で配布・導入する。exeはrepositoryにも含める。skillは同梱exeの絶対pathを使用し、PATH登録や別のCLI導入を前提にしない。

## Editorとの接続

通常起動時の既存loopback serverへCLI用APIを併設し、MCP /mcpと既存session契約を維持する。CLI APIは/api/health、/api/operations、/api/invoke、/api/jobs。接続descriptorにProject path/name、Unity version、pid、domain session、endpoint、CLI API version、ランダムBearer tokenを記載する。CLI APIはtoken必須。MCPの既存接続方式は変更しない。

既定portは48884。占有時は最大31個の後続portを試し、実際のportをdescriptorで公開する。複数Projectは別descriptorとportで共存する。domain reload・server停止・Editor終了でdescriptorとjobが失効し、次の起動で新しいsessionとtokenを発行する。停止したEditorのdescriptorはCLIで候補から除外する。

## ジョブと取消

CLIのinvokeはjob IDを返し、同じregistry操作をUnity main threadへdispatchする。CLI終了・待機timeoutはEditor処理を中断しない。job結果にはMCPと同じstructuredContent/content/isErrorを保持し、別CLI processから取得できる。

状態はqueued/running/completed/failed/cancelled。取消は待機中の操作の開始を防ぎ、開始済み操作へはCancellationTokenを通知する。対応しない同期操作の中断・巻き戻しを保証しない。main-thread dispatcherは開始済み操作が実際に完了するまで結果を完了扱いにしない。

jobはdomain内だけで保持し、共通LibraryやDBへ書き込まない。完了結果は10分経過後に次の受付で除去する。保持数は最大64で、上限到達時は明示的なreleaseを要求する。releaseは完了jobのみ。domain reload後のjob_not_foundは未実行を保証せず、同じ書き込みを自動再送しない。

## 操作とskillの境界

CLIは画像をfileへ保存し、JSONに絶対pathを返す。skillは対象の選択、必要な操作の発見、調査・編集・保存、失敗の扱いを案内する。CodexとClaude Codeに同じskillを配布する。大量の操作schemaを常時読み込ませず、必要なdescribeと分野別referenceだけを読む。

汎用Hierarchy、Component、Material、Scene、Console、Play Mode、build/test、任意C#は利用可能なUnity CLIへ任せる。ee4v-cliはisuzu-unityを必須依存にしない。UI表示状態、Unity未起動のheadless処理、remote接続、VRChat uploadは現行MCP同様に対象外。

## 動作確認

接続・全80操作のcatalog一致、引数変換、構造化error、画像保存、job待機・timeout・取消・release、Domain Reloadを確認する。Project内の隔離Scene/AssetでAvatar調査・体型・装着・表情・Expression MenuとPlay Mode操作を確認する。共通Library、FBX分類、照明preset、制作履歴・GalleryなどProjectをまたいで永続するwriterは、このCLI導入時の実機検証から除外する。新規テストケースは追加しない。
