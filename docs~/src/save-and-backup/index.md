# SaveAndBackup メモ

## 現在の責務

SaveAndBackupはAvatarModifyから分離した独立moduleです。登録済みPrefabとその依存assetをsnapshotへ保存し、対象ごとのlocal Git repositoryへcommitします。

次の処理を所有します。

- 保存対象とバックアップタイミングの記録
- snapshotの作成、再試行、破棄
- VRChat SDKのbuild・upload通知に応じたcommit

remote、push、global Git設定は操作しません。

## 現在の未接続部分

保存対象を登録するUIと呼び出し元は未実装です。`SaveAndBackupService.RegisterTarget(...)`は利用できますが、AvatarModifyはこのAPIを呼びません。そのため新規環境では対象recordが存在せず、自動バックアップは実行されません。

旧AvatarModify recordからのmigrationも実装していません。

## 次に決めること

保存対象を誰が登録するかを決める必要があります。候補は専用Settings UI、Project上のPrefab context menu、外部module向けの登録contractです。決定後もAvatarModifyへ依存させず、SaveAndBackup側の入口として実装します。
