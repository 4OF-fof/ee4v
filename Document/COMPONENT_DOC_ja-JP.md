# Unity Editor 拡張機能コンポーネント一覧

## Hierarchy 拡張機能

| 名称 (Name) | 機能 (Description) | 起動条件 (Trigger) |
| :--- | :--- | :--- |
| **Component Icons** | オブジェクトにアタッチされたコンポーネントのアイコンを表示します。 | 常時(クリックでウィンドウ展開) |
| **CustomStyle: Separator** | 区切り線の見た目をオブジェクトに適用します。 | 特定の文字列を名前の先頭に追加(デフォルト: ':SEPARATOR') |
| **Scene Switcher** | シーン間の切り替えを簡単に行えるウィンドウを展開します。 | シーン名をクリック |
| **CustomStyle: Heading** | 見出しの見た目をオブジェクトに適用します。 | 特定の文字列を名前の先頭に追加(デフォルト: ':HEADING') |
| **DepthLine** | オブジェクトの深さに応じてヒエラルキー内にガイドラインを表示します。 | 常時 |
| **Hidden Object** | 非表示にされたオブジェクトの一覧を表示します。 | 目のアイコンをクリック |
| **GaneObject Window** | オブジェクトの見た目のカスタマイズや情報表示を行うウィンドウを展開します。 | メニューアイコンをクリック |
| **Styled Object** | オブジェクトに色やアイコンを設定し、ヒエラルキーでの視認性を向上させます。 | 専用ウィンドウ(GaneObject Window)から設定 |

## Project 拡張機能

| 名称 (Name) | 機能 (Description) | 起動条件 (Trigger) |
| :--- | :--- | :--- |
| **Folder Overlay** | フォルダに含まれるアイテムのアイコンをオーバーレイ表示します。 | 常時 |
| **Styled Folder** | フォルダに色やアイコンを設定し、プロジェクトウィンドウでの視認性を向上させます。 | フォルダ上でaltキー |

## Project Toolbar 拡張機能

| 名称 (Name) | 機能 (Description) | 起動条件 (Trigger) |
| :--- | :--- | :--- |
| **Tab** | プロジェクトウィンドウにタブを追加します。 | 追加ボタンをクリック。またはタブ領域にフォルダをドラッグ＆ドロップ。 |
| **WorkSpace** | 一時的なワークスペースを作成し、仮想的なフォルダとして使用します。 | 追加ボタンを右クリック。作成されたタブにアセットをドラッグ＆ドロップすることで追加できます。 |

## Asset Manager コンポーネント

| 名称 (Name) | 機能 (Description) |
| :--- | :--- |
| **Inspector** | Displays detailed information about the selected asset or folder. |
| **Asset Grid** | Displays assets in a grid view with toolbar. |
| **Tag List** | Displays list of all tags. |
| **Navigation Tree** | Displays folder hierarchy and filters. |

## Editor Service 拡張機能

| 名称 (Name) | 機能 (Description) | 起動条件 (Trigger) |
| :--- | :--- | :--- |
| **Auto Backup** | 作成したアバターのバックアップを自動的に作成します。 | VRChatへのアップロード成功時 |

## Editor Utility 拡張機能

| 名称 (Name) | 機能 (Description) | 起動条件 (Trigger) |
| :--- | :--- | :--- |
| **Material List** | プレハブに使用されているマテリアルの一覧を表示します。 | `ee4v>Material List`からウィンドウを開きます。ヒエラルキーでプレハブを選択するか、ウィンドウ上でターゲットを指定してください。 |
| **Variant Converter** | アバタープレハブのバリアントを作成します。同時にマテリアルもバリアントに変換されます。 | ヒエラルキーでプレハブを右クリック |
