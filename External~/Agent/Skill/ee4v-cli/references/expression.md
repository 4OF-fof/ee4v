# 表情、FacialSet、Expression Menu

## Clipとアニメーション

inspect-faceでchannelのrendererPathとnameを取得し、書き込みではnameをshapeNameへ渡す。既存Clipはinspect-expression-clipへavatarRefとclipPathを渡してrevisionとポーズを取得する。

- 静止Clipはupsert-expression-clipのcreate/patch/replace。patchは未指定curveを残し、replaceは除去する。複数ポーズClipには使わない。
- アニメーションはedit-expression-animationのactionでポーズ・遷移・Loopを編集する。remove-expression-poseは削除専用。
- set-expression-channel-enabledは曲線全体を有効／無効にする。ポーズ値の変更とは異なる。expectedRevisionを渡し、dryRunで確認する。
- remap-expression-clipは静止Clip専用。未対応channelを結果から確認し、曖昧な分類を勝手に補完しない。
- render-expression-preview --avatar-ref <ref> --clip-path <path> --output <新しいPNG path> で実物を確認する。アニメーションはtimeで時点を指定できる。

対応操作はexpectedRevisionとdryRunを使用する。競合時は現状態を取り直し、変更内容を照合して入力を作り直す。

## 分類とFacialSet

FBX別分類はlist-blendshape-presets / get-blendshape-preset / update-blendshape-preset。未作成分類はensure-blendshape-presetsで明示的に初期化する。分類はProjectをまたぐデータなので、Scene編集に伴って暗黙に変更しない。

Gestureやメニュー専用表情の割り当てはget-facial-configuration → plan-facial-set-apply → apply-facial-set。patchは指定範囲の変更、replaceは全置換。空clipPathは該当割り当ての解除。

## メニュー

inspect-expression-menuのentryPath、registration、exclusionId、revisionを使う。各変更で返る新revisionを後続操作へ渡す。

create/edit/moveはee4vの編集可能な項目に使用する。replace-expression-menu-actionsはaction一覧全体の置換で、空配列は全除去。args-fileを使用し、dryRunで参照・競合を確認する。

既存項目のremoveは元データを残す除外、ee4v生成項目のremoveは生成本体の削除。除外はrestoreで戻せる。SDK共有menuの編集はcopy-expression-menuでAvatar用コピーを作る。

render-expression-menu-previewはisolated authoring preview。最終的なMA/NDMF、遷移、PhysBone挙動はPlay Modeで確認する。
