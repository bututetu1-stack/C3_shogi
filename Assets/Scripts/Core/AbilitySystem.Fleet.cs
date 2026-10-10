using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 提督の艦隊（艦これ風）。物鉄が成ると提督に着任し、建造で艦種が決まった艦娘と、旗艦が率いる深海3体が現れる。
///   昼戦: 自軍の手番の終わりに艦娘が艦種ごとの攻撃（雷撃・砲撃・空爆）を、敵の手番の終わりに深海がランクごとの攻撃をする
///   夜戦: 着任から BalanceTuning.FleetNightAfterMoves 手で突入。駆逐・軽巡・潜水は攻撃2倍、空母は攻撃しない
///   損傷: 体力が半分以下で中破（攻撃−1、空母は攻撃しない）、4分の1以下で大破（攻撃しない。成ると轟沈＝大破進軍）
///   改・改二: 敵陣に入ると改（成り）、改のまま敵陣を出ると改二。能力のダメージも上がる
///   入渠: 提督の隣にいる艦娘は、自軍の手番の開始時に回復する
///   作戦完了: 旗艦を沈めると残りの深海は撤退し、艦隊は帰投する。艦娘が1隻も沈まなければS勝利（次の局の選択肢に強化が1枚増える）
/// 艦隊の攻撃はすべて防御を無視し、攻撃の上乗せ（提督の練度＝局による強化・全軍強化・僕）のぶん増える。
/// </summary>
public partial class AbilitySystem
{
    private class Fleet
    {
        public int arrivalMove;
        public bool night;
        public bool shipLost;
    }

    private readonly Dictionary<int, Fleet> fleets = new Dictionary<int, Fleet>();
    // 作戦完了して帰投した艦隊の支援射撃（その局のあいだ、自軍の手番の終わりごとの回数）
    private int supportShots;

    private static readonly PieceType[] ShipClasses =
    {
        PieceType.KanmusuDD, PieceType.KanmusuCL, PieceType.KanmusuCA,
        PieceType.KanmusuBB, PieceType.KanmusuCV, PieceType.KanmusuSS
    };

    private void ResetFleets()
    {
        fleets.Clear();
        supportShots = 0;
        if (BattleEffects.Instance != null) BattleEffects.Instance.SetNight(false);
    }

    /// <summary>中破（体力が最大の半分以下）</summary>
    public static bool IsDamaged(PieceInstance ship) { return ship.currentHP * 2 <= ship.MaxHP; }

    /// <summary>大破（体力が最大の4分の1以下）</summary>
    public static bool IsHeavilyDamaged(PieceInstance ship) { return ship.currentHP * 4 <= ship.MaxHP; }

    /// <summary>大破のまま成ろうとすると轟沈する（大破進軍）</summary>
    public static bool SinksOnPromotion(PieceInstance piece)
    {
        return PieceTypes.IsKanmusu(piece.data.pieceType) && IsHeavilyDamaged(piece);
    }

    private static string ClassName(PieceType type)
    {
        switch (type)
        {
            case PieceType.KanmusuDD: return "駆逐艦";
            case PieceType.KanmusuCL: return "軽巡洋艦";
            case PieceType.KanmusuCA: return "重巡洋艦";
            case PieceType.KanmusuBB: return "戦艦";
            case PieceType.KanmusuCV: return "空母";
            case PieceType.KanmusuSS: return "潜水艦";
            default: return "艦娘";
        }
    }

    // ============================================================
    // 物鉄 → 提督（着任のカットイン → 裏返し → ワープ → 深海浮上 → 建造・出撃 → 開幕航空戦）
    // ============================================================
    public IEnumerator TeitokuPromotionRoutine(PieceInstance monotetsu)
    {
        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("物鉄", monotetsu.team) + " が " + BattleLogUI.ColorName("提督", monotetsu.team) + " に着任！");

        if (BattleEffects.Instance != null) BattleEffects.Instance.PlayTeitokuFanfare();
        yield return CutInUI.Play("提督 着任", "全艦隊、抜錨せよ！",
            monotetsu.data.promotedPortrait != null ? monotetsu.data.promotedPortrait : monotetsu.data.portrait,
            CutInUI.Navy, 1.7f);
        if (!monotetsu.isAlive || monotetsu.isPromoted) yield break;

        monotetsu.Promote();
        GameSim.RecordPromotion(monotetsu);
        CombatResolver.PlayFlip(monotetsu);
        FloatingText.Spawn(monotetsu.boardPosition, "提督", Palette.GoldLight, 4f);
        yield return new WaitForSeconds(0.45f);

        yield return TeitokuArrivalRoutine(monotetsu);
    }

    /// <summary>提督のワープと艦隊の召喚（旗艦と随伴の深海3体・建造した艦娘）</summary>
    public IEnumerator TeitokuArrivalRoutine(PieceInstance teitoku)
    {
        BoardManager bm = BoardManager.Instance;
        GameManager gm = GameManager.Instance;
        int groupId = nextGroupId++;
        teitoku.linkedGroupId = groupId;
        var fleet = new Fleet { arrivalMove = gm != null ? gm.MoveCount : 0 };
        fleets[groupId] = fleet;

        int size = bm.CurrentBoardSize;
        int halfBoard = size / 2;
        int promoteRows = BoardCell.GetPromoteRows(size);
        BattleEffects fx = BattleEffects.Instance;

        // 提督は自陣の奥へ潜航してワープ
        Vector2Int? newPos = FindEmptyInRange(0, promoteRows);
        if (newPos.HasValue && teitoku.isAlive)
        {
            Vector2Int oldPos = teitoku.boardPosition;
            PieceController pc = bm.GetPieceController(oldPos);
            if (fx != null) fx.PlayDiveEffect(oldPos);
            if (pc != null) yield return pc.WarpOutRoutine();

            bm.MovePiece(oldPos, newPos.Value);
            bm.UpdatePieceControllerPosition(oldPos, newPos.Value);
            if (pc != null) pc.SnapTo(newPos.Value);

            if (fx != null) fx.PlaySurfaceEffect(newPos.Value);
            if (pc != null) yield return pc.WarpInRoutine();
            yield return new WaitForSeconds(0.15f);
        }
        if (!teitoku.isAlive) yield break;
        Vector2Int teitokuPos = teitoku.boardPosition;

        // 深海: 旗艦1体と随伴2体が敵陣側に浮上（局が進むほどランクが上がる。提督とは2マス以上離す）
        int stage = StageManager.Instance != null ? StageManager.Instance.currentStage : 1;
        PieceType escortType = stage >= 10 ? PieceType.ShinkaiFlagship : stage >= 5 ? PieceType.ShinkaiElite : PieceType.Shinkai;
        PieceType flagshipType = stage >= 10 ? PieceType.ShinkaiHime : stage >= 5 ? PieceType.ShinkaiFlagship : PieceType.ShinkaiElite;
        int risen = 0;
        if (fx != null) fx.PlayAbyssRumble();
        for (int i = 0; i < 3; i++)
        {
            PieceData data = bm.GetPieceDataByType(i == 0 ? flagshipType : escortType);
            if (data == null) continue;
            Vector2Int? spawnPos = FindEmptyWithMinDistance(halfBoard, size, teitokuPos, 2);
            if (!spawnPos.HasValue) continue;
            if (fx != null) fx.PlayAbyssRiseEffect(spawnPos.Value);
            yield return new WaitForSeconds(0.18f);
            PieceInstance shinkai = bm.Spawn(data, Team.Enemy, spawnPos.Value);
            if (shinkai == null) continue;
            GameManager.Instance.ApplySummonBonuses(shinkai);
            shinkai.linkedGroupId = groupId;
            shinkai.isFlagship = i == 0;
            if (shinkai.isFlagship) FloatingText.Spawn(spawnPos.Value, "旗艦", Palette.GoldLight, 3.4f, 0.2f);
            risen++;
            yield return new WaitForSeconds(0.2f);
        }
        if (risen > 0 && BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("深海", Team.Enemy) + "の艦隊が浮上した（旗艦を沈めれば作戦完了）");

        // 艦娘: 建造で艦種を決め（重複なし）、自陣から出撃
        var classes = new List<PieceType>(ShipClasses);
        for (int i = classes.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            PieceType tmp = classes[i]; classes[i] = classes[j]; classes[j] = tmp;
        }
        var ships = new List<PieceInstance>();
        for (int i = 0; i < BalanceTuning.FleetShips && i < classes.Count; i++)
        {
            PieceData data = bm.GetPieceDataByType(classes[i]);
            if (data == null) continue;
            Vector2Int? spawnPos = FindEmptyInRange(0, halfBoard + 1);
            if (!spawnPos.HasValue) continue;
            if (fx != null) fx.PlaySortieEffect(spawnPos.Value);
            PieceInstance ship = bm.Spawn(data, Team.Player, spawnPos.Value);
            if (ship == null) continue;
            GameManager.Instance.ApplySummonBonuses(ship);
            ship.linkedGroupId = groupId;
            ships.Add(ship);
            FloatingText.Spawn(spawnPos.Value, ClassName(classes[i]), CutInUI.SeaLight, 3.6f);
            SpeechBubble.Say(ship, PieceLines.FleetSortie);
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog("建造完了: " + BattleLogUI.ColorName(ClassName(classes[i]), Team.Player) + "、出撃！");
            yield return new WaitForSeconds(0.3f);
        }

        // 開幕航空戦: 空母がいれば、深海すべてに先制の航空攻撃
        foreach (var carrier in ships)
        {
            if (carrier.data.pieceType != PieceType.KanmusuCV || !carrier.isAlive) continue;
            var abyss = AliveShinkai();
            if (abyss.Count == 0) break;
            if (BattleLogUI.Instance != null) BattleLogUI.Instance.AddLog("開幕航空戦！");
            var source = GameSim.BeginSource(carrier);
            foreach (var s in abyss)
            {
                if (fx != null) fx.PlayAirRaidEffect(carrier.boardPosition, s.boardPosition);
                FleetHit(carrier, s, ShipDamage(carrier, 1, fleet));
            }
            GameSim.EndSource(source);
            yield return new WaitForSeconds(0.4f);
        }
    }

    // ============================================================
    // 艦隊のつながり（旗艦を沈めると作戦完了、提督が沈めば艦娘も沈む）
    // ============================================================
    public void CheckLinkedDeaths(int groupId, PieceInstance victim = null)
    {
        if (groupId < 0) return;
        Fleet fleet;
        fleets.TryGetValue(groupId, out fleet);
        if (fleet != null && victim != null && PieceTypes.IsKanmusu(victim.data.pieceType)) fleet.shipLost = true;

        BoardManager bm = BoardManager.Instance;
        var allAlive = new List<PieceInstance>();
        allAlive.AddRange(bm.GetTeamPieces(Team.Player));
        allAlive.AddRange(bm.GetTeamPieces(Team.Enemy));

        PieceInstance teitoku = null;
        bool flagshipAlive = false;
        var abyss = new List<PieceInstance>();
        var ships = new List<PieceInstance>();
        foreach (var p in allAlive)
        {
            if (p.linkedGroupId != groupId || !p.isAlive) continue;
            if (p.data.pieceType == PieceType.Monotetsu && p.isPromoted) teitoku = p;
            else if (PieceTypes.IsShinkai(p.data.pieceType)) { abyss.Add(p); if (p.isFlagship) flagshipAlive = true; }
            else if (PieceTypes.IsKanmusu(p.data.pieceType)) ships.Add(p);
        }

        // 旗艦撃沈 → 作戦完了。残りの深海は撤退し、提督と艦娘は帰投する
        if (!flagshipAlive && teitoku != null)
        {
            bool sRank = fleet == null || !fleet.shipLost;
            foreach (var s in abyss) CombatResolver.RemoveWithExit(s, PieceController.ExitStyle.Sink);

            PieceInstance mvp = null;
            foreach (var ship in ships) if (mvp == null || ship.fleetDamage > mvp.fleetDamage) mvp = ship;
            if (mvp != null && mvp.fleetDamage > 0)
            {
                FloatingText.Spawn(mvp.boardPosition, "MVP", Palette.GoldLight, 4f, 0.3f);
                if (BattleLogUI.Instance != null)
                    BattleLogUI.Instance.AddLog("MVP: " + BattleLogUI.ColorName(ClassName(mvp.data.pieceType), Team.Player) + "（深海に" + mvp.fleetDamage + "ダメージ）");
            }

            string rank = sRank ? "S勝利" : "A勝利";
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog("旗艦撃沈！ 作戦完了（" + rank + "）。" + BattleLogUI.ColorName("提督", Team.Player) + "の艦隊は帰投する"
                    + (sRank ? "。次の局は強化の選択肢が1枚増える" : ""));
            if (sRank && GameManager.Instance != null) GameManager.Instance.GrantFleetBonus();
            if (BattleEffects.Instance != null)
            {
                BattleEffects.Instance.PlayRetreatHorn();
                BattleEffects.Instance.SetNight(false);
            }
            if (sRank || !BalanceTuning.FleetSupportNeedsS) supportShots += BalanceTuning.FleetSupportShots;
            if (BalanceTuning.FleetStaysAfterVictory)
            {
                // 艦隊はそのまま盤に残って戦い続ける（つながりは残るので、提督が沈めば艦娘も沈む）
                StartCoroutine(CutInUI.Play("作戦完了", sRank ? "S勝利！ 艦隊、このまま進撃せよ" : "A勝利 艦隊、このまま進撃せよ", null, CutInUI.Navy, 1.3f));
                fleets.Remove(groupId);
                return;
            }
            StartCoroutine(CutInUI.Play("作戦完了", sRank ? "S勝利！ 艦隊、帰投せよ" : "A勝利 艦隊、帰投せよ", null, CutInUI.Navy, 1.3f));
            if (BalanceTuning.FleetVeteranReturn) ReturnAsVeteran(teitoku);
            else CombatResolver.RemoveWithExit(teitoku, PieceController.ExitStyle.Retreat);
            foreach (var ship in ships) CombatResolver.RemoveWithExit(ship, PieceController.ExitStyle.Retreat);
            if (supportShots > 0 && BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog("帰投した艦隊が支援艦隊として援護につく");
            fleets.Remove(groupId);
            return;
        }

        // 提督が沈んだら艦娘も沈む
        if (teitoku == null && ships.Count > 0)
        {
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("提督", Team.Player) + "轟沈……" + BattleLogUI.ColorName("艦娘", Team.Player) + "たちも海へ消えた");
            foreach (var ship in ships) CombatResolver.RemoveWithExit(ship, PieceController.ExitStyle.Sink);
            if (BattleEffects.Instance != null) BattleEffects.Instance.SetNight(false);
            fleets.Remove(groupId);
        }
    }

    /// <summary>生還と再配置: 提督は物鉄に戻り、歴戦の物鉄として自陣に残る（この局ではもう着任しない）</summary>
    private void ReturnAsVeteran(PieceInstance teitoku)
    {
        teitoku.Demote();
        teitoku.linkedGroupId = -1;
        teitoku.promotionSpent = true;
        teitoku.bonusATK += 1;
        teitoku.AddMaxHP(1);
        CombatResolver.PlayFlip(teitoku);
        CombatResolver.RefreshStats(teitoku);
        FloatingText.Spawn(teitoku.boardPosition, "生還", Palette.GoldLight, 3.6f, 0.3f);
        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("提督", Team.Player) + "は生還し、" + BattleLogUI.ColorName("物鉄", Team.Player) + "として前線に戻った（攻撃+1・体力+1）");
    }

    /// <summary>支援艦隊: 帰投した艦隊が、自軍の手番の終わりにランダムな敵（C3以外）へ支援射撃をする</summary>
    private IEnumerator SupportFleetFire()
    {
        if (supportShots <= 0) yield break;
        BoardManager bm = BoardManager.Instance;
        BattleEffects fx = BattleEffects.Instance;
        int stage = StageManager.Instance != null ? StageManager.Instance.currentStage : 1;
        // 盤の手前（自陣の外）から撃つ
        Vector2Int origin = new Vector2Int(bm.CurrentBoardSize / 2, -1);
        var source = GameSim.BeginSource(null);
        for (int i = 0; i < supportShots; i++)
        {
            var targets = new List<PieceInstance>();
            foreach (var p in bm.GetTeamPieces(Team.Enemy))
                if (p.isAlive && p.data.pieceType != PieceType.C3) targets.Add(p);
            if (targets.Count == 0) break;
            PieceInstance target = targets[Random.Range(0, targets.Count)];
            if (fx != null) fx.PlayBombardmentEffect(origin, target.boardPosition);
            yield return new WaitForSeconds(BattleEffects.BombardmentImpactTime);
            if (!target.isAlive) continue;
            int damage = BalanceTuning.FleetSupportDamage + StageManager.EnemyStageAtk(stage);
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog("支援艦隊の支援射撃！" + BattleLogUI.ColorName(target.DisplayName, target.team) + " に" + damage + "ダメージ");
            CombatResolver.ApplyDamage(target, damage, true);
        }
        GameSim.EndSource(source);
    }

    // ============================================================
    // 損傷・応急修理要員・入渠・改二
    // ============================================================

    /// <summary>艦娘が攻撃を受けたあと。沈むところなら応急修理要員で全快させて true を返す。中破になったら知らせる</summary>
    public bool OnShipHit(PieceInstance ship)
    {
        if (!PieceTypes.IsKanmusu(ship.data.pieceType)) return false;
        if (ship.currentHP <= 0)
        {
            if (GameManager.Instance == null || !GameManager.Instance.UseDamageControl()) return false;
            ship.currentHP = ship.MaxHP;
            ship.damageAnnounced = false;
            CombatResolver.RefreshStats(ship);
            FloatingText.Spawn(ship.boardPosition, "応急修理要員 発動！", Palette.GoldLight, 3.2f);
            SpeechBubble.Say(ship, PieceLines.ShipSaved);
            if (BattleEffects.Instance != null) BattleEffects.Instance.PlayHealEffect(ship.boardPosition);
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog("応急修理要員が発動し、" + BattleLogUI.ColorName(ClassName(ship.data.pieceType), Team.Player) + " は沈まずに踏みとどまった");
            return true;
        }
        if (!ship.damageAnnounced && IsDamaged(ship))
        {
            ship.damageAnnounced = true;
            FloatingText.Spawn(ship.boardPosition, IsHeavilyDamaged(ship) ? "大破" : "中破", new Color(1f, 0.55f, 0.4f), 3.2f, 0.25f);
            SpeechBubble.Say(ship, PieceLines.ShipDamaged);
        }
        return false;
    }

    /// <summary>入渠: 提督の隣にいる艦娘は、自軍の手番の開始時に回復する</summary>
    private void DockShips()
    {
        BoardManager bm = BoardManager.Instance;
        foreach (var teitoku in bm.GetTeamPieces(Team.Player))
        {
            if (teitoku.data.pieceType != PieceType.Monotetsu || !teitoku.isPromoted) continue;
            foreach (var ship in bm.GetTeamPieces(Team.Player))
            {
                if (!PieceTypes.IsKanmusu(ship.data.pieceType) || ship.linkedGroupId != teitoku.linkedGroupId) continue;
                if (Mathf.Max(Mathf.Abs(ship.boardPosition.x - teitoku.boardPosition.x), Mathf.Abs(ship.boardPosition.y - teitoku.boardPosition.y)) > 1) continue;
                if (CombatResolver.Heal(ship, BalanceTuning.FleetDockHeal) > 0)
                {
                    FloatingText.Spawn(ship.boardPosition, "入渠", CutInUI.SeaLight, 2.8f, 0.2f);
                    if (!IsDamaged(ship)) ship.damageAnnounced = false;
                }
            }
        }
    }

    /// <summary>改のまま敵陣を出た艦娘は改二になる（攻撃+1・防御+1・体力1回復）</summary>
    public void CheckKai2(PieceInstance ship, Vector2Int from)
    {
        if (ship == null || !ship.isAlive || ship.kai2 || !ship.isPromoted || !PieceTypes.IsKanmusu(ship.data.pieceType)) return;
        int size = BoardManager.Instance.CurrentBoardSize;
        if (!InEnemyCamp(ship.team, from.y, size) || InEnemyCamp(ship.team, ship.boardPosition.y, size)) return;

        ship.kai2 = true;
        ship.bonusATK += 1;
        ship.bonusDEF += 1;
        CombatResolver.Heal(ship, 1);
        CombatResolver.PlayFlip(ship);
        FloatingText.Spawn(ship.boardPosition, "改二", Palette.GoldLight, 4f);
        SpeechBubble.Say(ship, PieceLines.ShipKai2);
        if (BattleEffects.Instance != null) BattleEffects.Instance.PlayPromoteEffect(ship.boardPosition);
        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog(BattleLogUI.ColorName(ClassName(ship.data.pieceType), Team.Player) + " が改二に改装された");
    }

    private static bool InEnemyCamp(Team team, int row, int size)
    {
        int rows = BoardCell.GetPromoteRows(size);
        return team == Team.Player ? row >= size - rows : row < rows;
    }

    // ============================================================
    // 艦娘の攻撃（自軍の手番の終わり。艦種ごと）
    // ============================================================
    private IEnumerator ExecuteKanmusuAbilities()
    {
        GameManager gm = GameManager.Instance;
        // 夜戦突入
        foreach (var fleet in fleets.Values)
        {
            if (fleet.night || gm == null || gm.MoveCount - fleet.arrivalMove < BalanceTuning.FleetNightAfterMoves) continue;
            fleet.night = true;
            if (BattleLogUI.Instance != null) BattleLogUI.Instance.AddLog("夜戦突入！ 駆逐・軽巡・潜水の攻撃が2倍になり、空母は攻撃できない");
            if (BattleEffects.Instance != null) BattleEffects.Instance.SetNight(true);
            yield return CutInUI.Play("夜戦突入", "我、夜戦に突入す！", null, CutInUI.Navy, 1.2f);
        }

        var ships = new List<PieceInstance>();
        foreach (var p in BoardManager.Instance.GetTeamPieces(Team.Player))
            if (PieceTypes.IsKanmusu(p.data.pieceType) && p.isAlive) ships.Add(p);
        foreach (var ship in ships)
        {
            if (!ship.isAlive) continue;
            Fleet fleet;
            fleets.TryGetValue(ship.linkedGroupId, out fleet);
            var source = GameSim.BeginSource(ship);
            yield return ShipAttack(ship, fleet);
            GameSim.EndSource(source);
        }
    }

    /// <summary>艦娘の攻撃のダメージ（改+1、中破−1、夜戦は駆逐・軽巡・潜水が2倍。攻撃の上乗せも足す）</summary>
    private static int ShipDamage(PieceInstance ship, int baseDamage, Fleet fleet)
    {
        int damage = CombatResolver.AbilityDamage(ship, baseDamage) + (ship.isPromoted ? 1 : 0) + BalanceTuning.FleetDamageBonus;
        if (IsDamaged(ship)) damage -= 1;
        PieceType type = ship.data.pieceType;
        if (fleet != null && fleet.night && (type == PieceType.KanmusuDD || type == PieceType.KanmusuCL || type == PieceType.KanmusuSS))
            damage *= 2;
        return Mathf.Max(1, damage);
    }

    /// <summary>艦隊の攻撃を当てる（防御を無視。深海に与えたぶんは MVP の判定に数える）</summary>
    private static void FleetHit(PieceInstance ship, PieceInstance target, int damage)
    {
        if (target == null || !target.isAlive) return;
        // 随伴艦の壁: 随伴が残っているあいだ、旗艦へのダメージは半分（切り上げ）
        if (BalanceTuning.FlagshipGuard && target.isFlagship && AliveShinkai().Exists(s => !s.isFlagship))
        {
            damage = (damage + 1) / 2;
            FloatingText.Spawn(target.boardPosition, "かばう", new Color(0.6f, 0.85f, 1f), 2.8f, 0.15f);
        }
        if (PieceTypes.IsShinkai(target.data.pieceType)) ship.fleetDamage += Mathf.Min(damage, target.currentHP);
        CombatResolver.ApplyDamage(target, damage, true);
    }

    private static List<PieceInstance> AliveShinkai()
    {
        var list = new List<PieceInstance>();
        foreach (var p in BoardManager.Instance.GetTeamPieces(Team.Enemy))
            if (PieceTypes.IsShinkai(p.data.pieceType) && p.isAlive) list.Add(p);
        return list;
    }

    /// <summary>ship から target へ向かう直線上で、最初にいる敵（C3は除く）。いなければ null</summary>
    private PieceInstance FirstEnemyOnLine(PieceInstance ship, PieceInstance target)
    {
        BoardManager bm = BoardManager.Instance;
        foreach (var pos in GetBresenhamLine(ship.boardPosition, target.boardPosition))
        {
            PieceInstance p = bm.GetPieceAt(pos);
            if (p != null && p.team != ship.team && p.isAlive && p.data.pieceType != PieceType.C3) return p;
        }
        return null;
    }

    private IEnumerator ShipAttack(PieceInstance ship, Fleet fleet)
    {
        var abyss = AliveShinkai();
        if (abyss.Count == 0 || IsHeavilyDamaged(ship)) yield break;   // 大破は攻撃しない
        PieceType type = ship.data.pieceType;
        if (type == PieceType.KanmusuCV && (IsDamaged(ship) || (fleet != null && fleet.night))) yield break;   // 空母は中破・夜戦で発艦できない

        BoardManager bm = BoardManager.Instance;
        BattleEffects fx = BattleEffects.Instance;
        PieceInstance target = PickShipTarget(abyss);
        SpeechBubble.SayMaybe(ship, PieceLines.ShipAttack(type), 0.35f);
        PieceController pc = bm.GetPieceController(ship.boardPosition);
        if (pc != null) pc.Lunge(target.boardPosition);
        yield return new WaitForSeconds(0.25f);
        if (!ship.isAlive || !target.isAlive) yield break;

        switch (type)
        {
            case PieceType.KanmusuDD:
            case PieceType.KanmusuSS:
            {
                // 雷撃（潜水は先制雷撃）: 直線上の最初の敵に。いなければ深海に
                PieceInstance hit = FirstEnemyOnLine(ship, target);
                int damage = hit != null ? ShipDamage(ship, type == PieceType.KanmusuDD ? 3 : 2, fleet) : ShipDamage(ship, 2, fleet);
                if (fx != null) fx.PlayTorpedoEffect(ship.boardPosition, target.boardPosition);
                yield return new WaitForSeconds(BattleEffects.TorpedoImpactTime);
                LogAttack(ship, "雷撃", hit ?? target, damage);
                FleetHit(ship, hit ?? target, damage);
                break;
            }
            case PieceType.KanmusuCL:
            {
                // 砲雷撃: 深海に砲撃1、直線上の最初の敵に雷撃2
                if (fx != null) fx.PlayTorpedoEffect(ship.boardPosition, target.boardPosition);
                yield return new WaitForSeconds(BattleEffects.TorpedoImpactTime);
                int shell = ShipDamage(ship, 1, fleet);
                LogAttack(ship, "砲撃", target, shell);
                FleetHit(ship, target, shell);
                PieceInstance hit = target.isAlive ? FirstEnemyOnLine(ship, target) : null;
                if (hit != null)
                {
                    int torpedo = ShipDamage(ship, 2, fleet);
                    LogAttack(ship, "雷撃", hit, torpedo);
                    FleetHit(ship, hit, torpedo);
                }
                break;
            }
            case PieceType.KanmusuBB:
            {
                // 砲撃2回（弾着観測）: 深海に3を2回。1発目で沈めば別の深海へ
                for (int shot = 0; shot < 2; shot++)
                {
                    if (!target.isAlive)
                    {
                        abyss = AliveShinkai();
                        if (abyss.Count == 0) break;
                        target = PickShipTarget(abyss);
                    }
                    if (fx != null) fx.PlayBombardmentEffect(ship.boardPosition, target.boardPosition);
                    yield return new WaitForSeconds(BattleEffects.BombardmentImpactTime);
                    int damage = ShipDamage(ship, 3, fleet);
                    LogAttack(ship, "砲撃", target, damage);
                    FleetHit(ship, target, damage);
                }
                break;
            }
            case PieceType.KanmusuCV:
            {
                // 空爆: 深海に1、深海の周り2マス以内の敵2体に2
                if (fx != null) fx.PlayAirRaidEffect(ship.boardPosition, target.boardPosition);
                yield return new WaitForSeconds(BattleEffects.AirRaidImpactTime);
                Vector2Int center = target.boardPosition;
                int bomb = ShipDamage(ship, 1, fleet);
                LogAttack(ship, "空爆", target, bomb);
                FleetHit(ship, target, bomb);
                var nearby = GetEnemiesInRange(center, 2, Team.Enemy, target);
                ShuffleList(nearby);
                for (int i = 0; i < Mathf.Min(2, nearby.Count); i++) FleetHit(ship, nearby[i], ShipDamage(ship, 2, fleet));
                break;
            }
            default:
            {
                // 重巡（と艦種のない艦娘）: 砲撃。深海に3、深海の隣の敵1体に2
                if (fx != null) fx.PlayBombardmentEffect(ship.boardPosition, target.boardPosition);
                yield return new WaitForSeconds(BattleEffects.BombardmentImpactTime);
                Vector2Int center = target.boardPosition;
                int damage = ShipDamage(ship, 3, fleet);
                LogAttack(ship, "砲撃", target, damage);
                FleetHit(ship, target, damage);
                var nearby = GetEnemiesInRange(center, 1, Team.Enemy, target);
                if (nearby.Count > 0) FleetHit(ship, nearby[Random.Range(0, nearby.Count)], ShipDamage(ship, 2, fleet));
                break;
            }
        }
        yield return new WaitForSeconds(0.2f);
    }

    /// <summary>艦娘が狙う深海（FleetEscortsFirst なら随伴が残っているうちは旗艦を狙わない）</summary>
    private static PieceInstance PickShipTarget(List<PieceInstance> abyss)
    {
        if (BalanceTuning.FleetEscortsFirst)
        {
            var escorts = abyss.FindAll(s => !s.isFlagship);
            if (escorts.Count > 0) return escorts[Random.Range(0, escorts.Count)];
        }
        return abyss[Random.Range(0, abyss.Count)];
    }

    private static void LogAttack(PieceInstance ship, string kind, PieceInstance target, int damage)
    {
        if (BattleLogUI.Instance == null) return;
        BattleLogUI.Instance.AddLog(BattleLogUI.ColorName(ClassName(ship.data.pieceType), ship.team) + "の" + kind + "！"
            + BattleLogUI.ColorName(target.DisplayName, target.team) + " に" + damage + "ダメージ");
    }

    // ============================================================
    // 深海の攻撃（敵の手番の終わり。ランクごと）
    // ============================================================
    private IEnumerator ExecuteShinkaiAbilities()
    {
        foreach (var shinkai in AliveShinkai())
        {
            if (!shinkai.isAlive) continue;
            var source = GameSim.BeginSource(shinkai);
            yield return ShinkaiAttack(shinkai);
            GameSim.EndSource(source);
        }
    }

    /// <summary>
    /// 深海の攻撃。艦隊どうしの戦いなので、届く範囲の艦娘を狙う（ノーマル・elite は隣、flagship は2マス、姫級は盤のどこでも。elite は2隻）。
    /// 艦娘が届かなければ、隣の自軍の駒1体に2（ほかの駒を巻き込みすぎないよう、ランクにかかわらず弱い攻撃）
    /// </summary>
    private IEnumerator ShinkaiAttack(PieceInstance shinkai)
    {
        BoardManager bm = BoardManager.Instance;
        PieceType type = shinkai.data.pieceType;
        int range = type == PieceType.ShinkaiHime ? 99 : type == PieceType.ShinkaiFlagship ? 2 : 1;
        int count = type == PieceType.ShinkaiElite ? 2 : 1;
        int baseDamage = type == PieceType.ShinkaiHime ? 5 : type == PieceType.ShinkaiFlagship ? 4 : 2;

        var victims = new List<PieceInstance>();
        foreach (var p in bm.GetTeamPieces(Team.Player))
        {
            if (!PieceTypes.IsKanmusu(p.data.pieceType) || !p.isAlive) continue;
            if (Mathf.Max(Mathf.Abs(p.boardPosition.x - shinkai.boardPosition.x), Mathf.Abs(p.boardPosition.y - shinkai.boardPosition.y)) <= range)
                victims.Add(p);
        }
        if (victims.Count == 0)
        {
            var near = GetEnemiesInRange(shinkai.boardPosition, 1, Team.Player, shinkai);
            if (near.Count > 0) victims.Add(near[Random.Range(0, near.Count)]);
            baseDamage = 2;
            count = 1;
        }
        ShuffleList(victims);
        if (victims.Count > count) victims.RemoveRange(count, victims.Count - count);
        if (victims.Count == 0) yield break;

        int damage = CombatResolver.AbilityDamage(shinkai, baseDamage);
        foreach (var victim in victims)
        {
            if (!victim.isAlive) continue;
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog(BattleLogUI.ColorName(shinkai.DisplayName, shinkai.team) + "が " + BattleLogUI.ColorName(victim.DisplayName, victim.team) + " を海へ引きずり込む");
            if (BattleEffects.Instance != null)
                BattleEffects.Instance.PlayAbyssStrike(shinkai.boardPosition, victim.boardPosition);
            yield return new WaitForSeconds(0.3f);
            CombatResolver.ApplyDamage(victim, damage, false);
            yield return new WaitForSeconds(0.2f);
        }
    }
}
