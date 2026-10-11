# エディタで使う小さなスクリプト

`isuzu-unity-cli call execute_code --file <ファイル>` で流す。

**注意**: 盤を用意するスクリプト（`setup_battle.cs`・`test_*.cs`・`visual_autoplay.cs` など）は、対局を始めるときに中断データ（タイトルの「続きから」）を上書きする。ユーザーが中断した周があるときは、先に `RunSave` のファイル（`Application.persistentDataPath`）を控えておく。プレイ中に使うものは `play_mode_play` → `play_mode_step --seconds 2` のあとに流す。

| ファイル | 使いどころ |
|---|---|
| `title_skip.cs` | タイトル画面を閉じて仲間選びへ（タイトルが出ていなければ何もしない） |
| `setup_battle.cs` | 第六局の盤を作り、新しい部員4人とヲツを仲間にして対局を始める（裏金は5）。駒や局を書き換えて、能力の確かめに使う |
| `cap_setup.cs` | 画面の確認の準備。PanelSettings とカメラの両方に RenderTexture を設定する。このあと `play_mode_step --seconds 0.3` などで数フレーム進める |
| `cap_save_template.cs` | 画面を PNG で保存する。`OUTPATH` を保存先に書き換えたコピーを作って流す（例: `sed 's#OUTPATH#C:\\\\tmp\\\\shot.png#' cap_save_template.cs > shot.cs`） |
| `test_gamers.cs` | プレイ中に第六局の盤を作り、RN・寝癖・無糖の能力（ブロック・弾き・デイリー・えちち連合・スタッガー・篝火）を1つずつ動かして数値を返す。能力を変えたときの確かめに使う |
| `test_decoy.cs` | プレイ中に第六局の盤を作り、潜水艦の潜航（ほかの駒が上を通り抜ける・駒の下をくぐって2マス先へ・組み止めなし）と、挑発の引きつけ（小錦に近づく手の点）を確かめる |
| `test_fleet3.cs` → `test_fleet3_check.cs` | 艦隊の第三回の仕組み: 対潜（歩兵の攻撃が潜水艦に当たる・金将は当たらない）、基地航空隊、陣形と提督の軽減、制空権。1つ目のあと数秒進めてから2つ目を流す |
| `test_fleet4.cs` | 盤の外からの攻撃が覚醒前のボスを狙わないこと、生還した物鉄・改のケッコンカッコカリ（数秒進めてから ringed と体力を見る） |
| `test_ai_tactics.cs` | AI の読みの確かめ（ただで取れる駒・C3 を狙う飛車への受け）。工夫なしと全部入りで1手を選び、候補の点も返す（`SimpleAI.dumpRoot`） |
| `bench_ai.cs` | AI の速さ（評価1回・手の生成・工夫ごとに1手を選ぶ時間と局面数） |
| `visual_autoplay.cs` | 演出つきで AI どうしに1局指させる（第十局・物鉄★3をすぐ成らせる）。play_mode_step で進め、console_read_logs で演出の側の例外を探す |
| `dump_pieces.cs` | 駒のデータ（数値・動き・説明・覚醒）を `BalanceReports/pieces_dump.json` に書き出す。駒一覧のページ（https://claude.ai/artifact/FQyUcJnkbyCc9NGA14utpw）を作り直すときに使う |

画面の確認の流れ（PowerShell）:

```powershell
function u { isuzu-unity-cli @args; if ($LASTEXITCODE) { throw "isuzu-unity-cli $args failed ($LASTEXITCODE)" } }
try {
  u call play_mode_play
  u call play_mode_step --seconds 2
  u call execute_code --file docs/snippets/title_skip.cs
  u call play_mode_step --seconds 1
  u call execute_code --file docs/snippets/setup_battle.cs
  u call play_mode_step --seconds 4
  u call execute_code --file docs/snippets/cap_setup.cs
  u call play_mode_step --seconds 0.3
  u call execute_code --file shot.cs
} finally { isuzu-unity-cli call play_mode_stop }
```

`setup_battle.cs` の中では `GameManager` の非公開の `SetupStageBoard` と `StartBattle` をリフレクションで呼んでいる。内部の値（〆鯖の世界が終わるまでの手番など）を確かめたいときも、リフレクションで `AbilitySystem` の非公開のフィールドを読む。
