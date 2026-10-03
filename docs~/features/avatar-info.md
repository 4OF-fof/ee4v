# AvatarInfo

`src/Editor/Feature/Avatar/AvatarInfo`がアバターの詳細タブを所有します。表示と解析は`Ee4v.AvatarInfo.Editor`、NDMFとの接続は`Ee4v.AvatarInfo.Ndmf.Editor`に分けます。

`AvatarInfoAnalysis`は装着警告、AAOの有無とVRChatの性能情報を取得します。`AvatarPlayModePerformanceCache`はPlay Modeへ入る前のアバターを記録し、NDMFビルド後のPC／Quest性能結果をSessionStateへ保持します。`Get`と`Changed`を通して結果を読む・更新を受け取ることができます。ビルドの成功と最終対象は遅延評価し、Play Modeへ入った後に集計します。

`AvatarInfoView`は名前、装着警告、性能結果とプラットフォーム選択を表示します。プラットフォーム変更と装着設定を開く操作はホストへ通知し、View自身はSelectionやInspectorを操作しません。スタイルは`UI/avatar-info.uss`を自身で読み込みます。

`FindPlayModeAvatar(scenePath, prefabPath)`は呼び出し側から渡されたScene内でビルド前の記録に対応するアバターを検索します。AssetManager固有の作業Sceneパスを生成せず、AssetManagerへの参照はありません。他のAvatar機能にも依存しません。

Catalogは`Domain/AvatarInfo/Displays/AvatarInfoView`へ登録します。Storyはサンプルの性能情報を使用し、ビルドやPlay Mode切り替えを実行しません。
