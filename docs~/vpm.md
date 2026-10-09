# VPM配布

`src/package.json`をパッケージ名・バージョン・依存範囲の正本とし、`.github/workflows/publish-vpm.yml`でGitHub ReleasesとGitHub Pagesへ配布します。Unityの起動や追加のSecrets、Repository Variablesは不要です。

開発用の既定ブランチは`dev`、リリース用ブランチは`master`です。`dev`へのpushでは公開せず、`master`を対象としたPull Requestのマージ完了時に自動リリースします。マージ後のcommit SHAをcheckoutし、同じSHAへリリースタグを作成します。

## GitHubでの初期設定

1. `4OF-fof/ee4v`リポジトリをPublicにします。パッケージZIPとmanifestは認証なしでダウンロードできる必要があります。
2. `Settings → Actions → General`でGitHub Actionsを有効にし、workflowが使用する`actions/*`を許可します。リリースを作成するため、`contents: write`権限を組織・リポジトリのポリシーで許可します。
3. `Settings → Pages → Build and deployment → Source`を`GitHub Actions`に設定します。
4. 変更を`dev`でcommitし、`git push -u origin dev`でGitHubへブランチを作成します。`Settings → General → Default branch`を`dev`へ変更します。
5. `Settings → Environments → github-pages`にブランチ制限がある場合は`master`を許可します。マージだけで公開する場合、Required reviewersは設定しません。
6. `dev`から`master`へのPull Requestを作成してマージします。Pull Request作成画面では既定のbaseが`dev`になるため、リリース時はbaseを`master`へ変更します。

最初のマージで`src/package.json`の`0.1.0`から`v0.1.0`リリースが作成され、ZIPと`package.json`が添付されます。同じworkflow内で公開済みリリースからVPM一覧を生成し、Pagesへデプロイします。結果はGitHubの`Actions → Publish VPM`で確認します。

通常のPages URLでは登録用URLは`https://4of-fof.github.io/ee4v/index.json`、案内ページは`https://4of-fof.github.io/ee4v/`です。URLは`actions/configure-pages`の出力から生成するため、カスタムドメインを設定した場合もPages側の公開URLを使います。

## 更新と再実行

- `master`を対象としたPull Requestがマージされた場合だけ自動実行します。マージせず閉じたPull Requestや、`dev`・`master`への直接pushでは公開しません。
- 新版を配布するときは`dev`で`src/package.json`の`version`を未公開の値へ変更し、`master`へPull Requestでマージします。タグの作成やReleaseの手動作成は不要です。
- 公開済みの同じversionを再度マージしてもZIPとmanifestは上書きしません。ソース変更を配布するにはversionを上げます。旧版も一覧へ保持します。
- アップロード失敗で残ったDraft Releaseは再実行時に添付ファイルを更新して公開します。公開済みの同名タグに必要な添付がない場合はエラーにし、新しいversionでの公開を求めます。
- Pagesだけ失敗した場合、`Actions → Publish VPM → Re-run jobs`で再実行できます。`Run workflow`でも`master`を指定して実行できます。`dev`を指定した手動実行は公開jobをスキップします。

## 配布物

`.github/scripts/vpm.mjs`はNode.js標準APIのみを使います。`prepare`でGit管理された`src`のファイルを配布用の一時フォルダーへコピーし、ZIP直下に`package.json`、`Editor`、`Runtime`を配置します。ローカルの未追跡ファイルと`Generated`は含めません。既存の`.meta`は保持し、新規作成しません。ルートの`LICENSE.md`と必要な第三者の権利表示も同梱します。第三者の権利表示内のリンクは配布物の配置に合わせます。

配布用manifestへZIPの`url`と一覧の`repo`を追加します。リリース添付manifestとVPM一覧にはZIPの`zipSHA256`も記載し、ZIP内のmanifestには含めません。成果物はrunnerの一時フォルダーへ生成し、元のソースファイルを書き換えません。`VPM_OUTPUT`は各stepの`env`で`runner.temp`から指定します。

`publish`はDraftへZIPとmanifestをアップロードしてから公開します。`listing`はGitHub APIで全公開リリースを取得し、各ReleaseのmanifestとZIPを照合して`index.json`を生成します。DraftとPrereleaseは一覧へ含めません。ZIPやmanifestが壊れている場合は公開一覧の更新を停止します。

## インストール

案内ページには依存先の[Modular AvatarのVPM導入ページ](https://modular-avatar.nadena.dev/docs/intro)、[Gesture Managerを含むVRChat Curated VPMページ](https://vrchat-community.github.io/vpm-listing-curated/)、[AAOのVPMページ](https://vpm.anatawa12.com/)への通常リンクだけを掲載します。依存先の登録方法は各配布元のページを参照します。`src/package.json`の`vpmDependencies`は維持します。

ee4v自身は案内ページの「ee4vをVCCに追加」または一覧URLから登録し、Avatarsプロジェクトの`Manage Project`でインストールします。

## 参考

- [VRChat: Creating a Package Listing](https://vcc.docs.vrchat.com/guides/create-listing/)
- [VRChat: Packages](https://vcc.docs.vrchat.com/vpm/packages/)
- [VRChat: Repos](https://vcc.docs.vrchat.com/vpm/repos/)
- [GitHub: Using custom workflows with GitHub Pages](https://docs.github.com/en/pages/getting-started-with-github-pages/using-custom-workflows-with-github-pages)
