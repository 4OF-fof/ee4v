# Play Mode Component Suppression

## 機能

Play Mode向けのNDMF処理で、指定したコンポーネント型をビルド対象アバターから除去します。Scene上の元アバターは変更せず、通常のアバタービルドとアップロードにも影響しません。

設定は`Project Settings/4OF/ee4v`の「Play ModeのNDMF処理」に保存します。対象欄では、読み込まれている全具象`MonoBehaviour`を検索可能な一覧から選択します。一覧の階層と表示名には`AddComponentMenu`のパスを使用し、属性を持たない型は`Scripts`にまとめます。NDMFや個別ツールによる候補の絞り込みは行いません。アバターごとの設定コンポーネントは使用しません。

除去処理はNDMFの`FirstChance`フェーズで実行します。このため、対象コンポーネントを処理する通常のNDMFプラグインより前に除去できます。特定のプラグインやコンポーネント型には依存しません。

## 対象と制約

- 選択した型と完全一致するコンポーネントを、非アクティブなGameObjectを含むアバター階層からすべて除去します。
- NDMFの`Apply on Play`が無効な場合は実行されません。
- 対象のパッケージが未導入の場合も型名の設定を保持し、再導入後に再び有効になります。
- NDMF 1.8.0以降が必要です。

AAOの`Trace And Optimize`も選択できますが、AAOへの参照や専用処理は持ちません。

公開APIは設定済みidentity・解決可否と読み込み済みの候補型を取得し、Edit Modeで設定を全置換できます。MCPも同じsettings serviceを使用します。新規identityは読み込み済み具象MonoBehaviourに限定し、既存の未導入型は保持または明示除去できます。設定変更はProject scopeに保存し、Play Mode中・切り替え中には拒否します。
