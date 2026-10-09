# Avatarの評価・プレビュー

`Core/AvatarEvaluation`の`Ee4v.Core.AvatarEvaluation.Editor`は、NDMF Preview APIを使うアバタープレビューと、MAを考慮したExpression Menu・パラメーターの取得APIを提供します。プレビューはUIのPrefabScenePreview・FaceExpression・AssetManager、情報取得はExpression Menuが使用します。機能モジュール、UI、編集対象の保存やUndoへ依存しません。

| 責務 | 公開API | 入力・結果 |
|---|---|---|
| 描画・アニメーション | `AvatarPreviewRenderer` | アバターとClipからNDMF Preview対応の描画結果を提供 |
| 編集用Expression Menu | `AvatarAuthoringMenu.Read` | MAの合成メニューと編集元の参照を取得 |
| 編集用パラメーター | `AvatarAuthoringParameters.Read` | Descriptor・Animator・MAの宣言を集計 |

Preview sessionの接続は`AvatarPreviewSession`、表示用ポーズとMA骨対応は`AvatarPreviewPose`へ分けます。情報取得APIはプレビューの生成・再生状態から独立して呼び出せます。

## アニメーションプレビュー

`AvatarPreviewRenderer.SetAnimation(clip, loop: null)`はEdit Modeで再生するClipを指定し、`SampleAnimation(time)`は秒単位で姿勢を評価します。`Animation`と`AnimationTime`は現在のClipと評価時間を返します。loop未指定ではClipのループ設定を使用し、true／false指定では再生用コピーのloopTimeだけを上書きします。ループ時は時間を折り返し、非ループ時は0〜長さへ制限して末尾を保持します。元ClipとImporterは変更しません。Hierarchy再構築でもloop指定を引き継ぎます。再生時計、再描画、再生・停止などのUIは利用側が所有します。`SetAnimation(null)`は停止し、サンプリング用コピーとGraphを解放します。Clip切替・停止時はNDMF sessionも再接続し、古いアニメーション値をProxyへ残しません。

内部の`AvatarPreviewAnimation`は専用Preview Sceneにスクリプトを除いたコピーを一つ保持し、Unityの手動更新PlayableGraphでClipを評価します。MA Merge Armatureの対応骨はmerge先へ、Bone Proxyはtarget・attachment mode・matchScaleに従ってコピー内で付け替えます。Clipはメモリ内のコピーで参照pathを変更し、Merge ArmatureのTransform曲線はmerge先へ向けます。変更後に同じプロパティへ複数の曲線が向く場合は例外を返します。素材ClipとSceneのアバターへ書き込みません。

`SetAnimationSampler(Action<AvatarAnimationFrame>)`は複数のGeneric Clipと直接指定するポーズ値を合成する入口です。各描画で基準ポーズに戻した再生用コピーへcallbackを呼び、`frame.Sample(clip, time)`は参照先を解決したメモリ内Clipをサンプリングします。`frame.ResolveTransform(path)`は元のアバターpathから、Merge Armature・Bone Proxyを反映した再生用Transformを返します。callbackで得たFrameとTransformはそのプレビュー専用です。Clipの準備結果はプレビュー内で再利用し、再生用Materialのコピーもモジュールが所有します。`SetAnimationSampler(null)`は合成再生を終了します。`SetAnimation`と`SetAnimationSampler`は互いの再生設定を置き換えます。

Expression MenuはToggle・数値・Puppetの値と経過時間を計算して、このcallbackへ渡します。Puppetの直接Transform指定も`ResolveTransform`を使用します。Generic合成では仮ポーズを基準として残し、サンプリングした曲線・直接指定した値を優先します。Humanoid Clip単体には`SetAnimation`を使用します。

Generic・有効なAvatarを持つHumanoidのClip、Transform、BlendShape、Rendererの有効状態、子GameObjectの有効状態、Material slotの参照切替とMaterial数値プロパティを扱います。共有MaterialとClipのMaterial参照はコピーしてから評価し、素材へ書き込みません。部分的なTransform曲線では未指定軸の基準値を保持します。Animation Event・IK・root motionの累積は実行しません。Generic合成のComponent有効状態は再生用コピー上で評価し、隔離スナップショットでは描画中だけ適用して復元します。除去したスクリプトや物理の実行は再現しません。未対応の曲線、不明なpathは例外を返します。

Scene入力ではサンプリングした姿勢・Renderer値を最終段のNDMF描画filterでProxyへ適用します。NDMF Preview対応PluginのMesh・Material加工を継承します。Material数値曲線はPropertyBlockのアニメーション対象プロパティだけを転送し、色空間情報と他のプロパティを保持します。隔離スナップショットではコピーへ描画中だけ適用し、PropertyBlockも復元します。骨格スケール・BlendShape・表示状態の編集overrideは併用できます。Humanoid Clip単体の再生中は仮ポーズ回転を適用しません。Play Modeへ移行すると次の描画でアニメーションプレビューを終了します。`Dispose`はcallback・Clip・Material・再生用SceneとGraphを解放します。

描画・表示用override・カメラと資源の契約は[Avatar Preview](./avatar-preview.md)を参照します。

## NDMF Preview接続

`AvatarPreviewSession.Connect(camera, root, filter, inheritPluginPreview)`はEdit ModeでCameraへNDMF Preview sessionを接続します。既定では`PreviewSession.Current`をforkし、対象外Rendererの除外と最終段の表示用filterを登録します。親sessionがないときは空のsessionを使い、親の差し替え時に接続し直します。同じ入力と親sessionでは再生成せず、接続が変わった場合だけtrueを返します。

`inheritPluginPreview: false`はPrefab・隔離スナップショット用の空sessionを使い、Sceneの加工とfilterを継承しません。Play Modeへの移行中・実行中はsessionを解除します。`IsConnected`は現在の接続状態を返し、`Dispose`はsessionとCamera overrideを解放します。

`AvatarPreviewRenderer`はCamera・照明・描画用override・元RendererとProxyの対応を所有します。Scene入力、Prefab入力、隔離スナップショットの選択と描画タイミングは利用側が決めます。

## Expression Menu

`AvatarAuthoringMenu.Read(avatar, out sources, excludeItem)`はDescriptorとMA Menu Installer・Menu Item・Children等を解決したメニューと編集元候補を返します。`AvatarMenuPage`はasset・Children root・項目を、`AvatarMenuEntry`は合成Control・元Control・Owner・Index・Submenuを保持します。循環・共有サブメニューは同じpage参照を再利用します。

`AvatarMenuEntry.Submenu`はControlTypeが`SubMenu`の場合だけ設定します。Toggle・Button・Puppetなどに未使用の`subMenu`参照やMA Children設定が残っていても、階層として読み取りません。元asset・元Control・MA componentの設定は変更しません。この判定は合成メニューと表示・編集元の候補の両方へ適用します。

MAの内部`VirtualMenu.ForAvatar`、`RootMenuNode`とChildrenのnode keyへのreflectionはこのモジュールへ隔離します。resolverが利用できない場合は例外を返します。合成Controlと元Controlの照合が曖昧な場合はOwnerを確定しません。元Controlはコピーせず参照を保持し、利用側の変更検出に使います。

`excludeItem`は編集元候補から除くMA Menu Itemの条件です。Expression Menuは未登録draftの判定を渡し、`AvatarMenuPage`・`AvatarMenuEntry`を直接使用します。`Controls`は変更できないcollectionで、項目検索と並び替え先のindex取得にも使用します。編集許可、書き換え、並び替え、Undoと保存はExpression Menuが所有します。

## パラメーター

`AvatarAuthoringParameters.Read(avatar, ignore)`はDescriptorの非既定Animator layer、MA Merge Animator、Expression ParametersとMA Parametersを集計し、名前順の`AvatarParameterInfo`を返します。名前・型・Expression宣言・既定値の宣言を保持し、既定値の優先順位は既存カタログと共通です。指定したAnimator Controllerは集計から除きます。

MA Parametersのprivate・prefix・NotSynced宣言と組み込み名は候補から除き、共有宣言のremapToを考慮します。Animator側の全スコープの名前変換やビルド結果の解析は行いません。`IsBuiltIn`は同じ組み込み名判定を公開します。Expression Menuは`AvatarParameterInfo`を直接使用し、操作に応じた候補の絞り込みと宣言の検索を行います。

## 骨対応・表示用ポーズ

`AvatarPreviewPose.GetBindings(avatar)`はMA Merge Armatureの公開`GetBonesMapping`とMA Bone Proxyを読み取り、アバター内のmerge先・attachment mode・matchScale・mergeかproxyかの区別を返します。循環する対応は登録しません。Clip再生ではこの情報をサンプリング用コピーへ適用します。

`AvatarPreviewPose.GetWorldMatrix(source, bindings, rotations, scales, cache)`はClipを使用しない表示用ポーズの計算です。回転・スケールのoverrideがある場合にMAの骨対応を適用し、ない場合は元Transformの階層から計算します。渡されたcacheは一回のポーズ計算内で共有し、次のフレームでは利用側が破棄します。元TransformやArmatureへ書き込みません。

Prefab用コピーではMonoBehaviourを取り除く前に骨対応を取得し、Scene入力では`RefreshHierarchy`で更新します。Humanoidの仮ポーズ、表示用骨の生成と描画後の復元は`AvatarPreviewRenderer`が所有します。

## 制約

対応範囲はNDMF 1.14.8以降のPreview APIと、`src/package.json`に指定したMAの範囲です。NDMFのビルド処理は実行しません。メニュー・パラメーターAPIはMAを考慮したビルド前の編集用情報を返し、任意のNDMF Pluginがビルド時に生成・変更する最終情報は取得しません。パラメーターの全スコープのprivate名・prefix変換も未対応です。

骨対応のClip再生は表示上の付け替えとpath解決を行い、MAの全ビルド処理を再現しません。Constraint・PhysBone固有の骨保持条件、Merge Animatorから生成するFX実行、VRChatのParameter Driver・Behaviourの実行は対象外です。NDMF PluginによるMeshの構造変更でBone順序やMaterial slot番号が変わった結果への対応には制限があります。

読み取りAPIはnullのアバターを拒否し、Menu解決にはVRCAvatarDescriptorが必要です。永続アセットとSceneの変更は利用側の編集APIへ限定します。
