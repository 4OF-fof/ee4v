# Core

Coreはfeature横断の契約とUnity Editorへの接続部を所有します。機能固有のdomain modelやDB schemaは置きません。

開発時の正本は[`../../../docs~/core/README.md`](../../../docs~/core/README.md)です。公開API、副作用、失敗時の動作は同資料から変更対象別のページを参照してください。module全体の依存方向は[`../ARCHITECTURE.md`](../ARCHITECTURE.md)に記載します。

## このdirectoryで守る境界

- `Contracts`と`Services`からUnityを参照しない。
- 機能の中心処理には必要な契約をcomposition rootから渡す。
- static APIはEditor lifecycleとUIの入口で使用する。
- Unity version差分とreflectionは`Internal/EditorAPI/Backends`で吸収し、公開型へ漏らさない。
