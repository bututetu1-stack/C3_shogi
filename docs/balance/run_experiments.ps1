# バランステストの実験をまとめて回す（Unity でこのプロジェクトを開き、isuzu-unity-cli が使える状態で実行）
#   通常の周 300周 + 駒ごとに「第一局で必ず仲間にする」100周 + 「物鉄を毎局最初から提督」100周
# 結果は BalanceReports/exp/<名前>.jsonl に1周1行で追記される。途中で止まっても、もう一度実行すれば続きから回る。
# 集計: node docs/balance/aggregate.js <出力.json> BalanceReports/exp
param(
  [int]$BaseRuns = 300,
  [int]$PieceRuns = 100,
  [int]$Batch = 40,       # 1回の execute_code で回す周（120秒を超えると待ちに切り替わる）
  [string[]]$Only = @()   # 名前を絞るとき（例: -Only base,force_Monotetsu）
)
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$out = Join-Path $project 'BalanceReports\exp'
$work = Join-Path $env:TEMP 'c3_balance_batches'
New-Item -ItemType Directory -Force $out, $work | Out-Null
Remove-Item "$work\*.cs" -ErrorAction SilentlyContinue

$jobs = @(@{ name = 'base'; runs = $BaseRuns; opts = 'null' })
foreach ($p in 'Monin','Boku','Wotsu','Nako','Rihaku','SN','Konishiki','Monotetsu','Lance','Knight','Silver','Gold','Bishop','Rook') {
  $jobs += @{ name = "force_$p"; runs = $PieceRuns; opts = "new SimOptions { forcePick = PieceType.$p }" }
}
$jobs += @{ name = 'force_Teitoku'; runs = $PieceRuns; opts = 'new SimOptions { forcePick = PieceType.Monotetsu, promoteAtStart = true }' }
if ($Only.Count -gt 0) { $jobs = $jobs | Where-Object { $Only -contains $_.name } }

function DoneSeeds($name) {
  $f = Join-Path $out "$name.jsonl"
  if (-not (Test-Path $f)) { return @() }
  return Get-Content $f | ForEach-Object { if ($_ -match '"seed":(\d+)') { [int]$Matches[1] } }
}

# Windows PowerShell 5.1 では、Unity の「応答がない」警告が stderr に出るだけで Stop のもとでは止まってしまう。
# isuzu-unity-cli を呼ぶ間はエラーで止めず、終了コードで判断する
function Invoke-UnityCli { $ErrorActionPreference = 'Continue'; isuzu-unity-cli @args 2>$null | Out-String }

function CallWait($file) {
  $ErrorActionPreference = 'Continue'
  # 長引いたときの「jobs <id> --wait で続きを待てる」という知らせは stderr に出るので、stderr も受け取る
  $o = isuzu-unity-cli call execute_code --file $file --wait-timeout 600 2>&1 | Out-String
  if ($LASTEXITCODE -eq 4) {
    $m = [regex]::Match($o, 'jobs (\S+) --wait')
    $o = isuzu-unity-cli jobs $m.Groups[1].Value --wait --timeout 3600 2>&1 | Out-String   # 1回分が長くても終わるまで待つ
    if ($LASTEXITCODE) { throw "待ちきれませんでした: $file`n$o" }
  } elseif ($LASTEXITCODE) { throw "失敗: $file`n$o" }
  $o.Trim()
}

try {
  Invoke-UnityCli call play_mode_play | Out-Null
  Invoke-UnityCli call play_mode_step --count 5 | Out-Null
  foreach ($j in $jobs) {
    $have = DoneSeeds $j.name
    for ($first = 1; $first -le $j.runs; $first += $Batch) {
      $count = [Math]::Min($Batch, $j.runs - $first + 1)
      if (($have | Where-Object { $_ -ge $first -and $_ -lt $first + $count }).Count -ge $count) { continue }
      $path = (Join-Path $out "$($j.name).jsonl").Replace('\', '\\')
      $cs = Join-Path $work "$($j.name)_$first.cs"
      $code = "int done = BalanceSimulator.RunBatch($count, $first, BalanceSimulator.DefaultThinkMs, @`"$($path.Replace('\\','\'))`", null, $($j.opts));`nreturn `"$($j.name) $first+$count done=`" + done;"
      [IO.File]::WriteAllText($cs, $code, (New-Object System.Text.UTF8Encoding($false)))
      CallWait $cs
    }
  }
  "ALL DONE"
} finally {
  Invoke-UnityCli call play_mode_stop | Out-Null
}
