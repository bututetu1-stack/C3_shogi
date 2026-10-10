# エディタで使う小さなスクリプト

`isuzu-unity-cli call execute_code --file <ファイル>` で流す。プレイ中に使うものは `play_mode_play` → `play_mode_step --seconds 2` のあとに流す。

| ファイル | 使いどころ |
|---|---|
| `title_skip.cs` | タイトル画面を閉じて仲間選びへ（タイトルが出ていなければ何もしない） |
| `setup_battle.cs` | 第六局の盤を作り、新しい部員4人とヲツを仲間にして対局を始める（裏金は5）。駒や局を書き換えて、能力の確かめに使う |
| `cap_setup.cs` | 画面の確認の準備。PanelSettings とカメラの両方に RenderTexture を設定する。このあと `play_mode_step --seconds 0.3` などで数フレーム進める |
| `cap_save_template.cs` | 画面を PNG で保存する。`OUTPATH` を保存先に書き換えたコピーを作って流す（例: `sed 's#OUTPATH#C:\\\\tmp\\\\shot.png#' cap_save_template.cs > shot.cs`） |
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
