# Core リファレンス

Core は機能横断の契約、共通実装、Unity Editor との接続部を提供します。機能固有の状態遷移、VRChat 固有の判断、DB schema は扱いません。

## 機能

| 機能 | リファレンス | 主な公開インターフェース |
|---|---|---|
| 設定の定義と永続化 | [Setting](./setting.md) | `ISettingsService`、`SettingDefinition<T>` |
| 翻訳カタログと文字列取得 | [I18n](./i18n.md) | `ILocalizationService`、`I18N` |
| 非同期処理のライフサイクル | [Background](./background.md) | `IBackgroundTaskManager` |
| 画像表示とTexture cache | [Images](./images.md) | `CachedImage`、`CachedImageCache` |
| 共通UIコンポーネント | [Core UI](./ui.md) | `UiButton`、`InputField`、`SearchField`、`Icon`、`CustomPopup` |
| Project Browser への表示追加 | [Project](./project.md) | `InjectorApi`、`ItemInjectionRegistration` |
| Hierarchy への表示追加 | [Hierarchy](./hierarchy.md) | `InjectorApi`、`ItemInjectionRegistration` |
| Unity Editor の操作 | [EditorIntegration](./editor-integration.md) | `Ee4v.Core.EditorIntegration` |
| AvatarのPreview描画 | [Avatar Preview](./avatar-preview.md) | `AvatarPreviewRenderer` |
| NDMF / MAを考慮したAvatar評価 | [Avatar Evaluation](./avatar-evaluation.md) | `AvatarPreviewRenderer`、`AvatarAuthoringMenu`、`AvatarAuthoringParameters` |

各ページでは公開型とメンバーに加え、既定実装が変更する状態、永続化、イベント登録などの副作用を記載します。

`CoreSettings.Current`、`CoreLocalization.Current`、`CoreBackgroundActivities.Current`と表示注入の既定実装はEditorのdomain内で同じインスタンスを保持します。テストで状態を分離するときは各serviceの独立したインスタンスを使用します。

## assembly

| assembly | 役割 | Unity 依存 |
|---|---|---|
| `Ee4v.Core.Contracts.Editor` | 各機能の契約と状態型 | なし |
| `Ee4v.Core.Services.Editor` | 契約の標準実装 | なし |
| `Ee4v.Core.Unity.Editor` | Settings の保存と JSON 変換 | あり |
| `Ee4v.Core.Editor` | Unity Editor 操作と内部 API の隔離 | あり |
| `Ee4v.Core.AvatarEvaluation.Editor` | NDMF Preview描画・MA骨対応を反映したClip再生、MAを考慮した編集用情報 | あり |
| `Ee4v.Core.Presentation.Editor` | Settings 画面、表示注入、状態表示 | あり |
| `Ee4v.UI.Editor` | 機能横断のUI Toolkitコンポーネント | あり |

`Ee4v.Core.Contracts.Editor` と `Ee4v.Core.Services.Editor` は `noEngineReferences: true` です。Unity への接続は外側の assembly にあります。

## 利用上の境界

- 機能の中心処理は `ISettingsService` などの契約を受け取ります。
- static API は Editor lifecycle や UI を組み立てる場所で使います。
- `Core/Internal` の型は公開インターフェースではありません。
- Unity 内部 API の差異は `Core/Internal/EditorAPI/Backends` で吸収します。
