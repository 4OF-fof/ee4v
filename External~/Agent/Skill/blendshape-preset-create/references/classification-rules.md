# BlendShape Preset の分類ルール

## フィールドの意味

| フィールド | 用途 | 判断基準 |
| --- | --- | --- |
| `role` | プリセット画面の役割カード、表情編集の左右表示、表情クリップの対応付け | 体型・衣装では操作の用途をまとめる。表情では別の動きを区別する。 |
| `side` | 同じ役割の左右形状を区別 | 実際に左側・右側へ作用する組だけ`L`/`R`。視線を左へ動かす`eye_look_left`の`left`は作用側ではなく方向なので空欄。 |
| `mouthMorph` | 口変形キャンセラーの対象 | Avatar DescriptorのVisemeと同じ区切り内にある非Viseme形状など、実際に口変形として使うものだけ。名称に`mouth`があるだけでは設定しない。既存値は根拠なく変更しない。 |
| `appearancePart` | AssetManagerの見た目一覧に出す部位 | `expression`は見た目一覧から除外。顔立ちの静的調整は`head`。身体の形状や衣装への追従は該当部位。 |
| `appearanceGroup` | 部位内の見出し | 同じ用途を探しやすくする。スライダー自体は統合しない。`Shrink`は部位をまたいでも同じ名前を使い、各部位内に表示する。 |
| `headerText` | FBXに含まれる区切り用BlendShape | 値を編集する形状として扱わず、既存の見出し判定を保持する。 |

`appearancePart`に保存できる値は空欄（自動）、`expression`、`head`、`chest`、`waist`、`shoulders`、`arms`、`hands`、`legs`、`feet`、`other`。空欄は名前による推定へ戻るため、整備済みプリセットでは意図した値を明示する。身体の複数部位をまたぐ場合は、操作の主対象を選ぶ。`Body`というmesh名だけで`expression`と判断しない。

## 用途を判定する順序

1. **実際の効果を調べる。** メッシュとRenderer、見出し、形状名、Avatar Descriptor、対応衣装の形状、同系統FBXの既存プリセットを合わせて判断する。対になる形状でも効果が異なるなら分ける。
2. **表情か静的な見た目調整かを分ける。** Viseme、まばたき、ウィンク、感情表現は原則`expression`。顔輪郭、目・瞳のサイズ、鼻、耳、髪型などの造形調整は`head`。同じ「目」でも表情動作と造形を混ぜない。
3. **縮小と表示切替を区別する。** `Shrink_*`、身体部位の`*_OFF`で実際に衣装貫通対策としてメッシュを縮めるもの、区切りの`Shrink`内で同じ目的のものは`role: Shrink`、`appearanceGroup: Shrink`。髪・眼鏡・装飾の`*_OFF`や`*_x`は非表示・形状切替であり、機械的に`Shrink`へ入れない。区切り見出しは別meshへまたがる場合があるので、見出しだけでも決めない。
4. **残りの体型・衣装を用途でまとめる。** 胸の大小や対応衣装は`Breast`、乳首は`Nipple`、腰幅は`Waist`、ヒップは`Hip`、爪は`Nail`、靴下は`Socks`など。ブラ着用時と非着用時のように同時に使わない条件が明確なら別役割にする。見た目グループでは胸の大小と対応衣装を「胸」にまとめ、縮小は同じ胸部位内の`Shrink`へ分ける。
5. **髪・耳・小物を探しやすくする。** 前髪、後ろ髪、横髪、アホ毛、耳、眼鏡、帽子、尻尾、バッグ、衣装丈などを用途に応じて分ける。同じshape名でもmeshが違えば作用先を確認する。

## 役割を混ぜないための例

| BlendShape | `role` | `appearancePart` | `appearanceGroup` | 理由 |
| --- | --- | --- | --- | --- |
| `Breasts_big` / `Breasts_small` と対応衣装 | `Breast` | `chest` | `胸` | 大小は同じ調整領域。スライダーは個別に残る。 |
| `Nipple_ON` / `Nipple_big` | `Nipple` | `chest` | `胸` | 胸部位だが胸サイズとは別の役割。 |
| `Shrink_Chest1` / 身体を縮める`Chest_01_OFF` | `Shrink` | `chest` | `Shrink` | 衣装貫通対策の縮小。 |
| `Shrink_Upper_arm` | `Shrink` | `arms` | `Shrink` | 同じ縮小用途を腕の部位に表示。 |
| `Hair_front_long_left/right` | `Front Hair` | `head` | `前髪` | 左右が作用側なら`side`を分ける。 |
| `eye_up` / `mouth_up` | それぞれ`eye_up` / `mouth_up` | 用途に応じて`head`または`expression` | 静的調整なら目・瞳／口・鼻 | 共通の`up`へ縮めると別機能が混ざる。 |
| `eye_look_left/right` | それぞれ別役割 | `expression` | 空欄 | 左右は視線の方向なので`side`は空欄。 |

役割カードは同じmeshと区切り見出しの中で同名mappingをまとめる。見た目グループは同じ部位の中で見出しを作り、同名BlendShapeが複数Rendererにある場合を除いて値操作を統合しない。広い役割は表情クリップの再マッピング先を曖昧にし得るため、アニメーションで使う形状は役割の粒度を確認する。
