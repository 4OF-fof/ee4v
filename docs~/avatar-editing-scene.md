# アバター改変用Scene

同梱のSceneテンプレートと環境設定用Assetは、Editor共通の`src/Editor/Template`に配置します。AssetManagerの`DerivedAssetCreator.EnsureWorkingScene`は、`src/Editor/Template/AvatarEditing.unity`を複製して作業Sceneを生成します。テンプレートには必須外部パッケージのGestureManager Prefabを接続した状態で含め、複製後はVariant Prefabだけを配置します。プログラムからGestureManagerを追加したり、`settings.favourite`へアバター参照を書き込んだりしません。Play Modeの接続時に作業Scene内のGestureManagerを検索し、対象アバターのModuleを接続します。

## VRChat向けの判断

- Scene Viewは自身のCameraで描画するため、改変操作だけならScene上のCameraは必須ではありません。Game ViewとPlay Modeでの外観・動作・音声確認のため、テンプレートにはCameraとAudioListenerを配置します。VRChat内の視点はAvatar DescriptorのView Positionで設定し、このCameraの位置では決まりません。
- VRChat公式はアバターごとにSceneを分け、アバターを原点へ配置することを推奨しています。Cameraの初期位置は原点に立つ約1.7mのアバターを正面から見る設定です。PrefabのTransformは生成処理で変更しないため、身長、姿勢、配置に合わせて複製したSceneのCameraを調整します。
- 照明は改変時に色と陰影を比較する基準です。白色Realtime Directional Lightと無彩色のAmbient Lightを使用し、Skybox由来の色かぶりを避けます。ベイクは不要です。金属・光沢のあるMaterial向けに無彩色の反射用Cubemapを含めます。
- VRChatへアップロードする対象はアバターです。アバター階層外の改変用Camera、Light、環境設定は、そのアバターが訪れるワールドの照明やCamera設定を変更しません。実際の見え方はワールドの照明、Light Probe、反射、使用Shader等に依存するため、最終確認にはSDKのBuild & Testを使用します。

## テンプレートの構成

`Editing Environment`をSceneの独立したrootとし、その子に`Main Camera`、`Directional Light`とGestureManager Prefabを置きます。環境rootには`EditorOnly`タグを付け、アバターの子にはしません。アバターのPrefab保存時に環境を取り込まない構成です。GestureManager以外の追加Script、SDK component、床、Light Probe Group、Reflection Probe、Post Processingは含めません。

| 対象 | 初期設定 | 意図 |
| --- | --- | --- |
| Camera | 位置`(0, 0.85, 3.2)`、回転`(0, 180, 0)`、Perspective、FOV 35° | +Z側から原点のアバター正面を見る |
| Camera描画 | Solid Color、背景RGB`(0.18, 0.18, 0.18)`、near 0.01m、far 100m、HDR/MSAA有効 | 無彩色の背景で近距離のMaterialも確認できる |
| Camera識別・音声 | `MainCamera`タグ、AudioListener 1個 | Game ViewとPlay Modeの描画・音声確認 |
| Directional Light | 白色、強度1、回転`(35, -145, 0)`、Realtime、Important、Soft Shadows | アバター正面斜め上から照らし、陰影と影を確認する |
| Ambient Light | Flat、RGB`(0.5, 0.5, 0.5)` | 光源の反対側も確認できる無彩色の環境光 |
| Environment Reflections | Custom、`NeutralReflection.cubemap`、強度1 | 全面RGB`(0.5, 0.5, 0.5)`の16px HDR Cubemapで反射の基準を用意する |
| Scene環境 | Skyboxなし、Fog無効、Realtime GI/Baked GI無効、Lighting Dataなし | ベイクやワールド用環境Assetを必要としない |

Camera、Light、LightingSettingsとCubemapはUnity 2022.3で作成・保存します。`.meta`はUnityが生成したものを同梱します。Cubemapへの参照は生成Sceneにも保持し、テンプレート自体を作業Sceneとして開いて編集しません。

## 生成・保存の契約

`PackageAssetApi.GetPackageRootAssetPath`からテンプレートを解決するため、UPMの`Packages/`配置と`Assets/`配置に対応します。出力はVariant Prefabと同名の`Assets/`内`.unity`です。既存Sceneがあればそのまま使用し、CameraやLightの自動補完は行いません。Sceneがない既存Variant、保存版にSceneがない場合も同じテンプレートを使用します。

生成時は`AssetDatabase.CopyAsset`で名前付きScene Assetを先に作り、同期Import後にAdditiveで開きます。無名・未保存Sceneを含む他のSceneの内容、dirty状態とactive Sceneを維持します。生成途中のSceneは完了後に閉じ、生成に失敗した場合は途中の出力Sceneを削除します。テンプレートがなければ明示的に失敗し、空Sceneへのfallbackは行いません。既存ファイルや`Assets/`外の生成先へは書き込みません。

Variantの内容差分はPrefab、Material、作業Sceneなどの保存対象Asset・metadataと依存素材で判定します。Unity version、`Packages/manifest.json`と`packages-lock.json`は保存版の環境情報として保持しますが、内容ハッシュには含めません。復元で維持する現在の環境との差だけで保存ボタンを有効にせず、環境の違いは復元時の警告で案内します。保存版を読むときも現在の内容ハッシュの定義で計算します。

改変画面への接続時は作業SceneをSingleで開き、作業Sceneだけを通常の編集Sceneとして保持します。既に作業Sceneが開いている場合も他の通常Sceneを閉じ、対象内の未保存変更は保持します。他Sceneに未保存変更がある場合はUnity標準の保存確認を使用し、キャンセルした場合は接続を中止します。破棄後にUnityが作成するUntitled Sceneも作業Sceneの再読み込みで置き換えます。UnityのPreview SceneとPlay Mode中のScene構成は変更しません。既存のセッション処理で作業Sceneをactiveにし、そのSceneの環境設定を使用します。Scene ViewではScene Lightingを有効にするとSceneの照明を確認できます。

ee4v中央Previewは`AvatarPreviewRenderer`の専用Cameraと照明を使用します。テンプレートのCamera・LightはScene View／Game View向けで、中央Previewと同じ見え方を保証する設定ではありません。生成後の環境設定変更は各VariantのSceneへ保存され、テンプレートの更新を既存Sceneへ自動反映しません。

## 一次資料

- [VRChat: Creating Your First Avatar](https://creators.vrchat.com/avatars/creating-your-first-avatar/) — Sceneと原点配置、View Position、Build & Test。
- [Unity 2022.3: Camera](https://docs.unity3d.com/2022.3/Documentation/Manual/class-Camera.html) — Cameraの描画と画角。
- [Unity 2022.3: Scene view View Options toolbar](https://docs.unity3d.com/2022.3/Documentation/Manual/ViewModes.html) — Scene Lightingの表示切り替え。
- [Unity 2022.3: Ambient light](https://docs.unity3d.com/2022.3/Documentation/Manual/lighting-ambient-light.html) — 環境光の役割。
- [Unity 2022.3: Light](https://docs.unity3d.com/2022.3/Documentation/Manual/class-Light.html) — Directional Light、Realtime、影。
- [Unity 2022.3: RenderSettings.customReflectionTexture](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/RenderSettings-customReflectionTexture.html) — Custom反射Cubemap。
- [Unity 2022.3: SceneManager.SetActiveScene](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SceneManagement.SceneManager.SetActiveScene.html) — active Sceneの環境設定。

白色Lightとグレーの環境光・反射、および表の数値はee4vの改変作業用の設計判断であり、VRChat公式が指定した必須値ではありません。
