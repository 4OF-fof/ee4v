# NDMF / Modular Avatar連携

`Core/Ndmf`の`Ee4v.Core.Ndmf.Editor`は、NDMF Previewの接続と、Modular Avatarを考慮したメニュー・パラメーター・骨対応の読み取りを共有します。`Core/Preview`とExpression Menuが公開APIを使用します。機能モジュール、UI、編集対象の保存やUndoへ依存しません。

## Preview接続

`NdmfPreviewSession.Connect(camera, root, filter, inheritPluginPreview)`はEdit ModeでCameraへNDMF Preview sessionを接続します。既定では`PreviewSession.Current`をforkし、対象外Rendererの除外と最終段の表示用filterを登録します。親sessionがないときは空のsessionを使い、親の差し替え時に接続し直します。同じ入力と親sessionでは再生成せず、接続が変わった場合だけtrueを返します。

`inheritPluginPreview: false`はPrefab・隔離スナップショット用の空sessionを使い、Sceneの加工とfilterを継承しません。Play Modeへの移行中・実行中はsessionを解除します。`IsConnected`は現在の接続状態を返し、`Dispose`はsessionとCamera overrideを解放します。

`AvatarPreviewRenderer`はCamera・照明・描画用override・元RendererとProxyの対応を所有し、この接続APIを使用します。Scene入力、Prefab入力、隔離スナップショットの選択と描画タイミングは利用側が決めます。

## Expression Menu

`NdmfIntegration.GetExpressionMenu(avatar, out sources, excludeItem)`はDescriptorとMA Menu Installer・Menu Item・Children等を解決したメニューと編集元候補を返します。`NdmfMenuPage`はasset・Children root・項目を、`NdmfMenuEntry`は合成Control・元Control・Owner・Index・Submenuを保持します。循環・共有サブメニューは同じpage参照を再利用します。

MAの内部`VirtualMenu.ForAvatar`、`RootMenuNode`とChildrenのnode keyへのreflectionはこのモジュールへ隔離します。resolverが利用できない場合は例外を返します。合成Controlと元Controlの照合が曖昧な場合はOwnerを確定しません。元Controlはコピーせず参照を保持し、利用側の変更検出に使います。

`excludeItem`は編集元候補から除くMA Menu Itemの条件です。Expression Menuは未登録draftの判定を渡し、戻り値を機能のMenuPage / MenuEntryへ変換します。編集許可、書き換え、並び替え、Undoと保存はExpression Menuが所有します。

## パラメーター

`NdmfIntegration.GetParameters(avatar, ignore)`はDescriptorの非既定Animator layer、MA Merge Animator、Expression ParametersとMA Parametersを集計し、名前順の`NdmfParameterInfo`を返します。名前・型・Expression宣言・既定値の宣言を保持し、既定値の優先順位は既存カタログと共通です。指定したAnimator Controllerは集計から除きます。

MA Parametersのprivate・prefix・NotSynced宣言と組み込み名は候補から除き、共有宣言のremapToを考慮します。Animator側の全スコープの名前変換やビルド結果の解析は行いません。`IsBuiltInParameter`は同じ組み込み名判定を公開し、Expression Menuは結果を編集用カタログへ変換します。

## 骨対応

`NdmfIntegration.GetBoneBindings(avatar)`はMA Merge Armatureの公開`GetBonesMapping`とMA Bone Proxyを読み取り、アバター内のmerge先・attachment mode・matchScaleを返します。循環する対応は登録しません。

`GetBoneWorldMatrix(source, bindings, rotations, scales, cache)`は表示用回転・スケールのoverrideがある場合にMAの骨対応を適用します。overrideがない場合は元Transformの階層から計算します。渡されたcacheは一回のポーズ計算内で共有し、次のフレームでは利用側が破棄します。元TransformやArmatureへ書き込みません。

Prefab用コピーではMonoBehaviourを取り除く前に骨対応を取得し、Scene入力では`RefreshHierarchy`で更新します。Humanoidの仮ポーズ、表示用骨の生成と描画後の復元は`AvatarPreviewRenderer`が所有します。

## 制約

対応範囲はNDMF 1.14.8以降のPreview APIと、`src/package.json`に指定したMAの範囲です。NDMFのビルド処理を実行せず、Preview APIが適用する加工だけを描画へ反映します。読み取りAPIはnullのアバターを拒否し、Menu解決にはVRCAvatarDescriptorが必要です。永続アセットとSceneの変更は利用側の編集APIへ限定します。
