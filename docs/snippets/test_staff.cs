// 運営会議（運営の部員が2人以上で攻撃+1）と鯖缶の管理者権限（能力無効にかからない）を盤の上で確かめる（プレイ中に流す）
var gm = GameManager.Instance; var bm = BoardManager.Instance; var ab = AbilitySystem.Instance;
var sel = UnityEngine.Object.FindFirstObjectByType<PieceSelectionUI>(); if (sel != null) sel.Hide();
var title = UnityEngine.Object.FindFirstObjectByType<TitleScreenUI>(); if (title != null) UnityEngine.Object.Destroy(title.gameObject);
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
gm.SimBeginRun();
StageManager.Instance.currentStage = 6;
typeof(GameManager).GetMethod("SetupStageBoard", bf).Invoke(gm, null);
foreach (var t in new[] { PieceType.Nako, PieceType.Rihaku, PieceType.Boku })
    gm.SimApplyOption(DraftOption.Piece(bm.GetPieceDataByType(t)));
typeof(GameManager).GetMethod("StartBattle", bf).Invoke(gm, null);

var sb = new System.Text.StringBuilder();
// なこの突撃は演出を待つので、手番の初めの能力は流さずに運営会議だけを数え直す
var refresh = typeof(AbilitySystem).GetMethod("RefreshStaffMeeting", bf);
System.Action meet = () => refresh.Invoke(ab, new object[] { Team.Player });
System.Func<PieceType, PieceInstance> P = t => bm.GetTeamPieces(Team.Player).FirstOrDefault(p => p.data.pieceType == t && p.isAlive);
var nako = P(PieceType.Nako); var rihaku = P(PieceType.Rihaku); var boku = P(PieceType.Boku);
int size = bm.CurrentBoardSize;
System.Action<PieceInstance, Vector2Int> put = (p, c) => { bm.RemovePieceFromBoard(p.boardPosition); bm.PlacePieceOnBoard(p, c); };

// 1) 運営会議: なこ・李白の2人 → どちらも攻撃+1
int n0 = nako.ATK, r0 = rihaku.ATK;
meet();
sb.Append("1 運営2人: なこ 攻 " + n0 + "→" + nako.ATK + "（会議 +" + nako.staffATK + "）・李白 攻 " + r0 + "→" + rihaku.ATK + "（+" + rihaku.staffATK + "）\n");

// 2) 管理者権限: 封印された李白は、僕が離れていれば止まる・隣にいれば止まらない。僕自身は封印されても止まらない
var spots = new System.Collections.Generic.List<Vector2Int>();
for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) { var c = new Vector2Int(x, y); if (bm.IsEmpty(c) && bm.IsInBounds(c + Vector2Int.right) && bm.IsEmpty(c + Vector2Int.right)) spots.Add(c); }
var a = spots.First();
var far = spots.First(d => Mathf.Abs(d.x - a.x) >= 3 || Mathf.Abs(d.y - a.y) >= 3);
put(rihaku, a); put(boku, far);
rihaku.isSealed = true;
sb.Append("2 李白 封印・僕は遠い: 止まる " + AbilitySystem.IsSuppressed(rihaku) + "\n");
put(boku, a + Vector2Int.right);
sb.Append("2b 僕が隣: 止まる " + AbilitySystem.IsSuppressed(rihaku) + "（状態: " + AbilitySystem.StatusLine(rihaku) + "）\n");
boku.isSealed = true;
sb.Append("2c 僕も封印: 僕が止まる " + AbilitySystem.IsSuppressed(boku) + "\n");
boku.isSealed = false;

// 3) 封印されて僕が離れた李白は運営会議から外れる
put(boku, far);
meet();
sb.Append("3 李白が止まる: 李白 会議 +" + rihaku.staffATK + "・なこ +" + nako.staffATK + "\n");
rihaku.isSealed = false;

// 4) 運営が1人になると、お開き
rihaku.isAlive = false;
meet();
sb.Append("4 運営1人: なこ 会議 +" + nako.staffATK + "（攻 " + nako.ATK + "）\n");
return sb.ToString();
