# C3将棋（Unity）— 作業の約束

## 基本
- ユーザーへの返答・質問・途中報告はすべて日本語で書く。
- Unity 6000.3.3f1 / URP 2D。UI は UI Toolkit をコードで組み立て、`Resources/UI/Theme.uss` で見た目を付ける。駒や盤などの見た目は `SpriteFactory` で作り、`Resources/Art`・`Resources/Effects` の画像があればそれを使う。
- 現状と次の作業は `docs/HANDOFF.md` にまとめてある。最初に読む。

## git
- 作業ごとにブランチを切って PR を出す（#1〜#6 は main にマージ済み。これからの PR は main 向け）。マージはユーザーの指示があればこちらで行う。
- スクリプトの改行は LF（`core.autocrlf=false`、`core.eol=lf`）。PowerShell やエディタで書くと CRLF になることがあるので、コミット前に `sed -i 's/\r$//' <file>` でそろえる。
- `BalanceReports/` は自動プレイの出力なので git に入れない。

## Unity の操作
- シェルから `isuzu-unity-cli` で操作する（`verify`、`verify --test --filter '.'`、`call execute_code --file`、`call play_mode_play` / `play_mode_step --seconds` / `play_mode_stop`、`call console_read_logs`）。
- エディタが背面にあると時間が進まないので、プレイ中は `play_mode_step` で進める。
- 画面の確認は、PanelSettings とカメラの両方に RenderTexture を設定して数フレーム進め、ReadPixels で保存する。

## デザインの方針（ユーザーと決めたこと）
- 「オリジナルの駒を使った将棋ライク」。迷ったら将棋のルール・動きに寄せる。将棋を名乗る局（第五局「将棋の陣」など）は本将棋の配置にする。
- 成りは強制（敵陣に入る・出る手で成る）。選択ダイアログは出さない。
- 駒のステータスは駒から見て 左肩=攻撃、右肩=防御、右下=体力（数字は正立）。体力の色は緑のまま。
- 文字は大きめ。説明はカードに詰め込まず、クリックで画面下部に出す。日本語の改行は禁則と文節を守る（`JapaneseLineBreaker` / `WrappedLabel`）。
- 駒は実在の部員がモチーフだが、実在の人物とは無関係という扱い。人物・キャラクターのイラストは使わない。紋章（家紋）も使わない。
- C3 は「サークル」ではなく「部」。表記は「部」「部員」。
- 「物鉄（→提督）」はユーザー自身がモチーフ。提督まわりの演出は凝ってよいが、ほかの部員との演出の差は大きくしない。演出は豪華な方向を好む（セリフは `PieceLines.cs`、カットインは `CutInUI`）。
- 画像はユーザーが ChatGPT で作る。頼むときは ChatGPT 用の文言を用意する（素材依頼帳: https://claude.ai/artifact/QBqCRYvrW3jkU7kxMk5h18）。素材は `Assets/Resources` に直接置かれるので、作業前に `git status` で新しいファイルを確かめる（zip のまま置かれることもある）。
- バランスは、駒ごとの数値と能力の強弱を一覧にして、相談しながら決める。変えたら自動プレイで前後を比べる。

## バランステスト（自動プレイ）
- `GameSim.Headless` の間は、本番のロジックを演出・待ち時間なしで回す。`BalanceSimulator.RunBatch(周回数, 最初の種, 思考ms, 出力jsonl, null, SimOptions)`。
- 局の流れと駒の能力の数値は `BalanceTuning`（駒の攻撃・防御・体力・動きは PieceData のアセット）。
- ふだんは並列で回す（エディタを使わないので、回している間も Unity で作業できる）。
  1. コードか駒のデータを変えたら、メニュー「C3将棋 > 自動プレイ用のビルドを作る」（または `isuzu-unity-cli call execute_code --code 'return SimBuild.Build();'`。プレイ中はできない）。`Builds/Sim/` に出る。
  2. 速報: `node docs/balance/run_parallel.js`（通常40周、1分ほど）。本番: `--full`（通常300周＋駒ごとの実験、20〜30分）。候補を試すときは `--out BalanceReports/候補名 --set EnemyAtkDivisor=6` のように、ビルドし直さずに `BalanceTuning` や駒のデータ（`--piece C3={"baseHP":10}`）を変えられる。
  3. 集計は `node docs/balance/aggregate.js out.json BalanceReports/exp`。
- エディタの中で回すとき: メニュー「C3将棋 > バランステスト（自動プレイ）」、またはプレイ中に `BalanceSimulator.RunBatch(周回数, 最初の種, 思考ms, 出力jsonl, null, SimOptions)`。シーン（`Assets/Scenes/SampleScene.unity`）を開いてからプレイする。`run_experiments.ps1` もあるが、1本ずつなので本番だと3〜4時間かかる。
- 調整前の基準値は `docs/balance/baseline_2026-10-10.json`。報告ページ: https://claude.ai/artifact/Jp2KngEe49sWEQRFNfBRDo
- 同じ種ならほぼ同じ結果になる。AI の読みが時間切れになった手だけは PC の負荷で変わることがあり、そこから先の展開が分かれる（同じ種で回し直すと、だいたい9割の周が完全に一致する）。
