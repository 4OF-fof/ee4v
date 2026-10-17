# ee4v-cli

Windows x64の単独実行ファイルで、起動中Unity Editorのee4v操作を実行する。Unity Editorにee4vが必要。isuzu-unityは実装・配布の依存ではなく、汎用Unity操作に併用できる。

## Build and distribution

.NET 9 SDKでbuildする。利用側にSDKやNode.jsは不要。

~~~powershell
dotnet publish External~/Cli/ee4v-cli.csproj -c Release -o External~/Cli/dist
~~~

配布単位はdist/ee4v-cli directory。SKILL.md、references、bin/win-x64/ee4v-cli.exeを一つのskillとして同梱する。publishは同じexeをExternal~/Agent/Skill/ee4v-cli/bin/win-x64へも生成し、repositoryに含める。入力contentからbinを除外するため、再publishで配布物が入れ子にならない。

skill directory全体をCodex／Claude Codeのskills directoryへコピーすれば使用できる。同梱exeを絶対pathで実行し、PATH登録は不要。setupもexeを含むskill全体を配置する。MCP設定は変更しない。

~~~powershell
$ee4vCli = (Resolve-Path 'External~/Cli/dist/ee4v-cli/bin/win-x64/ee4v-cli.exe').Path
& $ee4vCli setup --agent both
& $ee4vCli doctor --agent both
& $ee4vCli projects
& $ee4vCli find-avatars --project 'C:\Unity\AvatarProject'
& $ee4vCli inspect-avatar --project 'C:\Unity\AvatarProject' --avatar-ref '<発行値>'
~~~

setupはCodexのCODEX_HOME/skills（未指定時は~/.codex/skills）とClaude CodeのCLAUDE_CONFIG_DIR/skills（未指定時は~/.claude/skills）を使用する。--destinationでskills directoryを明示できる。管理markerのない同名skillを上書き・削除しない。uninstallは管理対象ee4v-cli skillのみを除去する。

配置済みskill内のexeからsetupを再実行する場合は、自分自身へのコピーを省略する。Windowsでは実行中exeを削除できないため、自己uninstallは変更前に拒否する。別コピーの同梱exeから対象agentまたは--destinationを指定して除去する。

## Commands and inputs

operation名はMCP名のee4v_を除き、underscoreをhyphenへ変換する。MCP名も受け付ける。operationsの検索とdescribeは接続先のcatalogを使用する。全操作を同じregistryから公開し、操作の業務処理をCLIへ複製しない。

- projects / health / version
- operations [検索語] / describe <operation>
- <operation> --argument value --args-file input.json
- jobs / job <id> / wait <id> / cancel <id> / release <id>
- setup / doctor / uninstall

引数名はcamelCaseとkebab-caseを受け付ける。配列は同じoptionの反復、objectは--settings.field、object配列・入れ子配列・空配列はargs-fileへ記載する。optionが同じfile fieldを上書きする。JSONをshell argumentへ直接渡す形式は持たない。

--args-file -はstdinからJSON objectを読む。PowerShellではobjectをConvertTo-Json -Depth 50で出力してpipeでき、引用符をargumentへ埋め込む必要がない。

--projectは絶対pathまたは一意なProject名。cwdが一つのProject内ならそのEditorを選び、曖昧な候補は拒否する。--timeoutは待機秒数（既定120）、--no-waitはjobだけを返す。

## Results

stdoutはcompact JSON、stderrはjob ID。通常の完了結果はjobId、state、isError、result、imagesを返す。--rawはbase64画像を含むEditorのjob/result envelopeを返す。

画像は既定でtemp/ee4v-cli/imagesへ保存し、絶対path・mimeType・bytesを返す。--outputでpath指定可能。複数画像は連番suffix、既存fileは上書きしない。画像保存失敗でもEditor操作は再実行せず、jobの結果を再取得する。

終了コードは0=成功または非同期受付、1=操作/ローカル失敗、2=引数不正、3=接続/対象不明/実行結果不明、4=待機中断/timeout。非同期受付の0は操作完了を意味しない。

待機終了とEditor処理取消は別。timeout後はwait、取消はcancel、取得済み完了jobの除去はreleaseを使う。開始済み処理の取消は巻き戻しを保証しない。書き込みの自動再送はしない。

Editorが%LOCALAPPDATA%/ee4v/cli/instancesへ接続情報を公開する。CLIはpid・session・Project一致と認証済みhealthで検証する。EE4V_CLI_STATE_DIRまたは--state-dirでrootを変更できる。API versionは1。接続情報のtokenはstdoutへ出さない。
