# C3将棋 第9版 計画（敵の種類と布陣・歩兵を消す・RN の体幹・説明の見せ方）

## Context（背景）
2026-10-11 夜、ノート PC から戻ったあとのユーザーの要望:
- 強化カードに「歩兵を消す」。守りは薄くなるが、攻めやすくなる。
- RN の弾きにもうひねりがほしい（何回か弾くと高火力・体幹ゲージ・相手の火力依存 など）。
- 敵に「能力無効」の駒があってもよい。
- ランの前半はスムーズにしたい。駒の体力・攻撃の上乗せで難しくするのではなく、敵駒の種類を増やす。「似たような駒と似たような盤面ばかりで面白みがない」。
- キャラの説明と駒の能力の説明を、色分け・枠分けで分かりやすく。

いまの作り（`Roguelike/StageManager.cs`）:
- 15局の布陣は固定で、周ごとに同じ盤面。
- 第一〜五局は将棋の駒だけ。後半もほとんど「歩の列＋後ろの段」の形で、特殊な敵は12種（鬼将・影忍・鉄壁・天狗・風神・雷帝・龍神・閻魔・軍将・黄泉・髑髏・魔王）。そのうち能力があるのは閻魔・軍将・黄泉・髑髏・ボスだけ。
- 敵は第二局から体力、第四局から攻撃が上乗せされる。

### ユーザーが決めたこと（2026-10-11）
- RN の弾きは「体幹ゲージ」: ブロックで防ぐたびに、攻撃してきた敵の体幹が防いだ量だけたまる（強い敵ほど早く崩れる）。満ちたら忍殺。
- 「歩兵を消す」は、取ったら周のあいだずっと（1回だけ出るカード）。
- 前半（第一〜七局）は敵の体力・攻撃の上乗せをやめる。難しさは新しい敵駒と布陣の組み合わせで作る。後半は上乗せを少し遅らせて残す。全クリア 20〜35% に合わせる。
- 布陣は局ごとに2〜3通り用意し、周ごとにランダムで選ぶ。岩などの地形と、C3 の位置もばらす。将棋を名乗る第五局とボスの局は、芯を変えない。

### いつもの約束
- 返答は日本語。人物の絵は使わない。セリフはむやみに増やさない。
- バランスは自動プレイで前後を測る。確認は短く（60〜100周、10分を超えるなら裏で回す）。
- マージはユーザーの指示があったときだけ。

---

## 1. 歩兵を消す ＋ 説明の色分け・枠分け（`feat/no-pawns-and-text`）

### 歩兵を消す
- `PiecePool.cs` の `UpgradeKind` に `NoPawns` を足す。
  - 名前「歩兵を消す」、印「歩」。
  - 説明「次の局から最後まで、自軍の歩兵が出なくなる。守りは薄くなるが、駒が前に出やすくなる。」
  - 1周に1回だけ（取ったら候補に出ない。`UpgradeKinds` で除く）。
- `GameManager` に `RunNoPawns` を持たせ、`SetupStageBoard` で歩兵を置かない。
  - `ApplyOption` で立てる。`RunSave` に保存、`ResetRun` で戻す。
- 自動プレイ（`BalanceSimulator`）でも選ばれるので、通常100周で影響を見る。

### 説明の色分け・枠分け
- `PieceData` に `flavor`・`promotedFlavor` を足す（キャラの説明）。`description`・`promotedDescription` は能力の説明だけにする。
- いまの説明文を、キャラの説明と能力の説明に分けて書き直す。`execute_code` でアセットに直接書く（ノート PC のメモのとおり `\uXXXX` を避ける）。
  - 対象: 部員18人、艦娘・深海、敵駒、将棋の駒。
  - ユーザーが書いた文はそのまま `flavor` に入れる。
- 新しい `UIFactory.PieceText(flavor, ability, size)` で2つの枠を作る。
  - 上: キャラの説明。薄い色の文字、左に細い線、背景なし。
  - 下: 能力。金色の見出し「能力」と、うっすら色の付いた枠の中に本文。
  - 覚醒の説明は、能力の枠の中で見出し「覚醒」を付けて分ける。
- 使う場所: `PieceDetailUI`（いまの説明文と「覚醒する前からの能力」）、`PieceSelectionUI`（仲間選びの詳しい説明）、`PieceCatalogUI`（駒一覧）。

## 2. RN の体幹（`feat/rn-sekiro`）
成った・覚醒した RN（3本同時プレイ、`AbilitySystem.Gamers.cs` の `RnMultiPlay`）の SEKIRO を作り直す。

- **体幹**: `PieceInstance.posture`。RN のブロックが敵の通常の攻撃を防ぐと、その敵の体幹が「防いだ量」だけたまる。
  - 相手の攻撃が強いほど多く防ぐので早くたまる＝相手の火力依存。
  - 全部防いだ（弾き）ときは、さらに +1。
  - 入れる場所: `CombatResolver.ConsumeGuard` → `AbilitySystem.OnBlocked` / `OnParry`。
- **体幹崩し → 忍殺**: 体幹が敵の最大体力に届いたら、その場で忍殺。
  - ふつうの敵は、そのまま撃破（防御無視で残りの体力ぶん）。
  - ボス（雷帝・龍神・魔王）は、最大体力の 1/3（`RnDeathblowBossRatio`）を削り、1手番動けない（`stunned`）。体幹は0に戻る。
- いまの「弾きで1ダメージ返す」はやめる（忍殺に置き換える）。数値は `BalanceTuning`（`RnPostureParryBonus` など）。
- **見せ方**:
  - 体幹がたまると「体幹 4/8」と浮かぶ。駒の情報の状態の行にも出す（`GamerStatusLine` の default）。
  - 忍殺は赤い一閃とカットイン風の文字「忍殺」。
  - 画像 `Deathblow` があれば使う（なければ `Slash` と赤い光）。
- RN の説明文を書き直す（成った後・覚醒）。

## 3. 敵の種類・能力無効・布陣（`feat/enemy-variety`）

### 新しい敵駒（8種。`PieceType` の末尾に足す。データは `Assets/Resources/Pieces`）
数値は仮。測って決める。

| 駒 | 攻/防/体 | 動き | 能力 | 初めて出る局 |
|---|---|---|---|---|
| 巨岩 | 0/1/3 | 動けない | 地形。敵味方どちらも通れない。壊せる（勝敗・撃破数・練度に数えない） | 第一局 |
| 傀儡 | 0/0/5 | 動けない | 挑発（隣の自軍は攻撃するなら傀儡から。小錦と同じ `isTauntPiece`） | 第二局 |
| 弓兵 | 1/0/2 | 前と横に1 | 手番の終わりに、前の直線上いちばん近い自軍の駒に1（防御で減る） | 第二局 |
| 大盾 | 2/2/4 | 前と横に1 | 正面（前）から受ける通常の攻撃は −2 | 第三局 |
| 猪突 | 2/0/3 | 前にどこまでも＋横1 | 助走（ユウと同じく、走ったマスだけ攻撃+1） | 第四局 |
| 妖狐 | 2/1/2 | 全方向1 | 倒されると、隣に狐火を2つ残す | 第四局 |
| 狐火 | 1/0/1 | 全方向1 | 妖狐が残す（布陣には置かない） | — |
| 結界 | 0/2/4 | 縦横1 | **能力無効**: 周り2マスの自軍の駒は能力が止まる（★・覚醒の数値は残る） | 第六局 |

- 能力の場所:
  - 新しい `Core/AbilitySystem.Enemies.cs`: 弓兵の射撃、妖狐の分身、結界の判定。
  - `CombatResolver`: 大盾の正面・猪突の助走（`RunUpBonus` を広げる）。
  - `MoveValidator`: 巨岩は動けない（`isImmovable`）。
- 巨岩:
  - `GameManager.HasFightingPieces` と `RunRoster.OnKill` で数えない。
  - AI は値打ち0（`SimpleAI.PieceValue`）。
  - 灰色の石の見た目（`PieceSkin`）。
- **能力無効（結界）**:
  - `AbilitySystem.IsSuppressed(p)` = `p.isSealed` か、周り2マスに生きた敵の結界がいる。
  - いま `isSealed` を見ている自軍の能力（〆鯖・翡翠・RN・寝癖・無糖など）はこれに置き換える。
  - 見ていない部員の能力（ヲツ・門人・なこ・けい・きぷ・僕・李白・ユウの助走・小錦・艦隊・光春・ニコ）にも、それぞれの入口で足す。
  - 見た目は2つ。
    - 結界の周りにうっすら輪（範囲）。画像 `KekkaiDome` があれば使う。
    - 止まっている駒は、封印と同じ色味（`PieceRenderer` の `SealedTint`）。
  - 駒の情報に「結界で能力が止まっている」。
- AI（`SimpleAI`）:
  - 自軍は結界・弓兵を優先して倒す（`PieceValue` / `AbilityValue` に足す）。
  - 傀儡・巨岩は後回し。

### 布陣を局ごとに2〜3通り（`StageManager.GetEnemyLayout(stage, size, variant)`）
- 周の初めに、局ごとの型を決める。`GameManager.StageVariants`（`int[15]`）。
  - `RunSave` に保存する（続きからでも同じ盤面）。
  - 自動プレイは種から決まり、記録に `variant` を残す。
- 局の名前の横に型の名前（例「初陣 ― 岩場」）を出す（`StageTitleUI`）。
- 型の例（座標は実装で決める。前半は駒を少なめにして短く終わるように）:
  - 第一局（5×5）: 歩の壁（いまの形）／岩場（巨岩2・C3 を隅に）／傀儡（歩の前に傀儡）
  - 第二局（5×5）: いまの形／弓兵の陣（弓兵2）／端の砦（C3 を隅に、金銀と巨岩）
  - 第三局（7×7）: いまの形／大盾の列（大盾3＋後ろに弓兵2）／岩の中原（巨岩4を散らす）
  - 第四局（7×7）: いまの形／猪突の突撃（猪突3）／妖狐の森（妖狐2＋巨岩）
  - 第五局「将棋の陣」: 本将棋のまま（1通り）
  - 第六〜九局: いまの形に、大盾・結界・妖狐・弓兵・猪突・傀儡・巨岩を組み合わせた型を各2つ足す（第六局で結界が初めて出る）
  - 第十・十二局（ボス）: ボスと芯は同じで、護衛を結界・弓兵などに替えた型を足す
  - 第十一・十三・十四局: 各2つ足す
  - 第十五局（魔王）: いまのまま（1通り）
- C3 の位置は、上の段のどこか（隅も）にばらす。`GetEnemyLayout` は C3 の位置も型ごとに持つ。

### 前半は上乗せなし
- `BalanceTuning` に `EnemyScaleFromStage = 8` を足す。
  - 第一〜七局は、敵の体力・攻撃の上乗せをしない（`StageManager.ScaleEnemy`）。
  - 第八局からは `(局 − 7)` をもとに上乗せし、割る数は測って決める。
  - 敵 C3 の体力の上乗せは残す（難しさの芯）。
- 測って全クリア 20〜35% に合わせる。前半（第一〜七局）は勝率9割以上、手数は短めを目安にする。

### 画像（ユーザーに頼む。素材依頼帳に文言を足す）
- `KekkaiDome`（結界の半透明のドーム）
- `Arrow`（弓兵の矢）
- `Kitsunebi`（青白い狐火）
- `Deathblow`（忍殺の赤い一閃）
- 人物・文字なし。届くまでは光で代用する。

---

## 主に触るファイル
- **データ・数値**: `Data/PieceType.cs`、`Data/PieceData.cs`（`flavor`）、`Data/PieceInstance.cs`（`posture`）、`Roguelike/BalanceTuning.cs`、新しいアセット（`Assets/Resources/Pieces/` に8つ）
- **局と周**: `Roguelike/StageManager.cs`（型つきの布陣・上乗せ）、`Roguelike/PiecePool.cs`（`NoPawns`）、`Roguelike/RunSave.cs`、`Core/GameManager.cs`（歩兵・型・保存）
- **能力**: `Core/AbilitySystem.cs` と各 partial（`IsSuppressed` の入口）、新規 `Core/AbilitySystem.Enemies.cs`、`Core/AbilitySystem.Gamers.cs`（体幹・忍殺）、`Core/CombatResolver.cs`（大盾・猪突・体幹）、`Piece/MoveValidator.cs`
- **AI と自動プレイ**: `AI/SimpleAI.cs`、`Sim/BalanceSimulator.cs`（型の記録）
- **UI**: `UI/UIFactory.cs`（`PieceText`）、`UI/PieceDetailUI.cs`、`UI/PieceSelectionUI.cs`、`UI/PieceCatalogUI.cs`、局の名前の UI
- **見た目**: `Board/PieceRenderer.cs`、`Visual/PieceSkin.cs`、`Effects/BattleEffects.cs`
- **ドキュメント**: `docs/HANDOFF.md`、`CLAUDE.md`

## 使い回すもの
- 挑発: `MoveValidator.ApplyTaunt`（どちらの陣営でも効く）
- 封印の見た目: `PieceInstance.isSealed` と `PieceRenderer` の `SealedTint`
- 対局の途中で出る駒の強化: `GameManager.ApplySummonBonuses`（狐火）
- `CombatResolver.ApplyDamage` / `AbilityDamage`、ユウの助走 `RunUpBonus`
- `BattleEffects.ArtFx`（画像がなければ光）、`FloatingText`、`CutInUI`
- `run_parallel.js --set`（ビルドし直さずに数値の候補を試す）

## 確かめ方
- **コンパイルとテスト**: `isuzu-unity-cli verify --test --filter '.'`。次のテストを足す。
  - 全部の局・型の布陣が盤に収まり、重ならず、C3 が1つ
  - 型の選び方が種で決まる
  - 「歩兵を消す」で歩兵が置かれない
  - 中断データに型と歩兵なしが残る
- **プレイモード**: `docs/snippets` の流れで盤を用意して1つずつ確かめる。
  - 体幹がたまって忍殺（ふつうの敵・ボス）
  - 結界の範囲で能力が止まる
  - 弓兵・大盾・猪突・妖狐→狐火・傀儡・巨岩
  - 説明の枠（駒の情報・仲間選び・駒一覧）
  - 歩兵なしの布陣
- **自動プレイ**（速報、ビルドを作り直してから）:
  - 1と2のあと: 通常100周（全クリアが大きく動いていないか）、RN を第一局から持つ60周。
  - 3のあと: 通常100周で、全クリア 20〜35%、第一〜七局の勝率と手数（短くなったか）。外れたら `EnemyScaleFromStage` と割る数で合わせる。
- **ユーザーの試遊**: 段階ごとに遊んでもらう。

## 進め方
1 → 2 → 3 の順に、ブランチを積んで PR にする（マージはユーザーの指示で）。3 がいちばん大きいので、敵駒・能力無効を作ってから布陣と上乗せを測る。
