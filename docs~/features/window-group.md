# WindowGroup

`WindowGroup` は、同じグループへ登録した複数の `EditorWindow` の表示を同期します。グループ外から一つのウィンドウへフォーカスが移ると、同じグループのほかのウィンドウも前面へ移し、最初に選択されたウィンドウへフォーカスを戻します。

## 管理ウィンドウ

`ee4v/Window Groups` から管理ウィンドウを開きます。

- 左側の一覧下部にある `新規グループ` でグループを作成します。同名のグループがある場合は連番を付けます。
- 左側の一覧で編集するグループを選びます。グループ名は入力欄からフォーカスが外れた時点で保存し、右クリックメニューからグループを削除できます。
- 右側には開いているウィンドウの種類が表示されます。先頭のトグルで所属を変更し、`表示` で対象ウィンドウを前面へ移します。
- 同じ種類のウィンドウが複数開いている場合は、すべて同じグループとして扱います。
- 閉じたウィンドウの割り当ては保持されます。次回その種類を開いた時点で自動的にグループへ復帰します。

グループ設定は利用者ごとの `EditorPrefs` に保存します。型の識別には完全な型名と assembly 名を使い、Unity の版番号は含めません。

## API

```csharp
IDisposable WindowGroupApi.Register(EditorWindow window, string groupId)
```

`window` を `groupId` へ登録し、登録解除用の `IDisposable` を返します。ウィンドウを閉じるときは返された値を `Dispose` してください。破棄済みのウィンドウは自動的に登録から除かれます。

同じウィンドウを再登録すると所属グループを置き換えます。以前の登録を後から `Dispose` しても新しい登録は解除されません。`groupId` は大文字と小文字を区別します。

管理ウィンドウによる種類単位の割り当てと API による個別登録が同じウィンドウへ指定された場合は、API の個別登録を優先します。

```csharp
private IDisposable _windowGroupRegistration;

private void OnEnable()
{
    _windowGroupRegistration = WindowGroupApi.Register(
        this,
        "avatar-tools");
}

private void OnDisable()
{
    _windowGroupRegistration?.Dispose();
    _windowGroupRegistration = null;
}
```

## フォーカス動作

- グループ外から登録済みウィンドウへフォーカスが移ったときだけ同期します。
- 同期時はほかの登録済みウィンドウを順に前面へ移し、起点のウィンドウを最後にフォーカスします。
- 同じグループ内で生じたフォーカス移動は再同期しません。いったんグループ外へ移動した後に戻ると再び同期します。
- 一つしか登録されていないグループでは追加のフォーカス操作を行いません。

## 依存関係

`Ee4v.WindowGroup.Editor` は管理画面の文字描画とスタイルに `Ee4v.UI.Editor`、翻訳に `Ee4v.Core.Presentation.Editor` を使用します。ほかの機能 assembly には依存しません。
