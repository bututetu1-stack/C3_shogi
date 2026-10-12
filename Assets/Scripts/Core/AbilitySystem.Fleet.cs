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
        public Formation formation;
        public bool nightContact;   // 夜間触接（夜戦に入ったとき軽巡か空母がいた）
    }

    /// <summary>陣形（出撃した艦娘の位置で決まる）</summary>
    public enum Formation { None, LineAhead, LineAbreast, Vanguard }

    private static string FormationName(Formation f)
    {
        switch (f)
        {
            case Formation.LineAhead: return "単縦陣";
            case Formation.LineAbreast: return "単横陣";
            case Formation.Vanguard: return "警戒陣";
            default: return "";
        }
    }

    private readonly Dictionary<int, Fleet> fleets = new Dictionary<int, Fleet>();
    // 作戦完了して帰投した艦隊の支援射撃（その局のあいだ、自軍の手番の終わりごとの回数）
    private int supportShots;
    // 支援艦隊の艦種（空母は航空支援、駆逐・軽巡・潜水は長距離雷撃、戦艦・重巡は支援射撃）
    private readonly List<PieceType> supportClasses = new List<PieceType>();

    private static readonly PieceType[] ShipClasses =
    {
        PieceType.KanmusuDD, PieceType.KanmusuCL, PieceType.KanmusuCA,
        PieceType.KanmusuBB, PieceType.KanmusuCV, PieceType.KanmusuSS
    };

    private void ResetFleets()
    {
        fleets.Clear();
        supportShots = 0;
        supportClasses.Clear();
        if (BattleEffects.Instance != null) BattleEffects.Instance.SetNight(false);
    }

    /// <summary>潜航中の潜水艦。ほかの駒はその上を通り抜けられ（止まれはしない）、弓兵の矢も上を越えていく</summary>
    public static bool IsSubmerged(PieceInstance p)
    {
        return p != null && p.isAlive && p.data.pieceType == PieceType.KanmusuSS;
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
        GameSim.RecordFleet("着任", fleet.arrivalMove);

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
            Vector2Int? spawnPos = PickSortieSpot(teitoku, halfBoard);
            if (!spawnPos.HasValue) continue;
            if (fx != null) fx.PlaySortieEffect(spawnPos.Value);
            PieceInstance ship = bm.Spawn(data, Team.Player, spawnPos.Value);
            if (ship == null) continue;
            GameManager.Instance.ApplySummonBonuses(ship);
            ship.linkedGroupId = groupId;
            ships.Add(ship);
            // 練度★3の提督の艦娘は、最初から改で出撃する（歴戦の艦隊: その周で一度S勝利していても）
            bool veteranFleet = BalanceTuning.FleetVeteranKai && GameManager.Instance.FleetSWins > 0;
            if (GameManager.Instance.RingedClasses.Contains(classes[i]))
                ApplyRingedSortie(ship);   // ケッコンカッコカリした艦種は改二で
            else if ((GameManager.Instance.Roster.StarsOf(teitoku.data.pieceType) >= 3 || veteranFleet) && !ship.isPromoted)
            {
                ship.Promote();
                CombatResolver.PlayFlip(ship);
            }
            FloatingText.Spawn(spawnPos.Value, ClassName(classes[i]), CutInUI.SeaLight, 3.6f);
            SpeechBubble.Say(ship, PieceLines.FleetSortie);
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog("建造完了: " + BattleLogUI.ColorName(ClassName(classes[i]), Team.Player) + "、出撃！");
            yield return new WaitForSeconds(0.3f);
        }
        RecordSortie(ships, teitoku);
        yield return DecideFormation(fleet, ships, teitoku);
        yield return AnnounceAirSuperiority(ships);

        // 開幕航空戦: 空母がいれば、深海すべてに先制の航空攻撃
        foreach (var carrier in ships)
        {
            if (carrier.data.pieceType != PieceType.KanmusuCV || !carrier.isAlive) continue;
            var abyss = AliveShinkai();
            if (abyss.Count == 0) break;
            if (BattleLogUI.Instance != null) BattleLogUI.Instance.AddLog("開幕航空戦！");
            // 全機を発艦させ、爆弾が落ちてから当てる
            if (fx != null)
            {
                foreach (var s in abyss) fx.PlayAirRaidEffect(carrier.boardPosition, s.boardPosition, true);
                yield return new WaitForSeconds(BattleEffects.AirRaidImpactTime);
            }
            var source = GameSim.BeginSource(carrier);
            foreach (var s in abyss) FleetHit(carrier, s, ShipDamage(carrier, BalanceTuning.CarrierOpeningDamage, fleet));
            GameSim.EndSource(source);
            yield return new WaitForSeconds(0.4f);
        }

        // 開幕雷撃: 改で出撃した潜水艦（練度★3の提督）は、着任のときに先制の雷撃（直線上の最初の敵に、いなければ深海に）
        foreach (var sub in ships)
        {
            if (sub.data.pieceType != PieceType.KanmusuSS || !sub.isAlive || !sub.isPromoted || BalanceTuning.SubOpeningTorpedo <= 0) continue;
            var abyss = AliveShinkai();
            if (abyss.Count == 0) break;
            PieceInstance target = PickShipTarget(abyss);
            PieceInstance hit = FirstEnemyOnLine(sub, target) ?? target;
            if (BattleLogUI.Instance != null) BattleLogUI.Instance.AddLog("開幕雷撃！");
            if (fx != null)
            {
                FloatingText.Spawn(sub.boardPosition, "開幕雷撃", CutInUI.SeaLight, 3.6f, 0.2f);
                fx.PlayTorpedoEffect(sub.boardPosition, hit.boardPosition);
                yield return new WaitForSeconds(BattleEffects.TorpedoImpactTime);
            }
            var source = GameSim.BeginSource(sub);
            int damage = ShipDamage(sub, BalanceTuning.SubOpeningTorpedo, fleet);
            LogAttack(sub, "開幕雷撃", hit, damage);
            FleetHit(sub, hit, damage);
            GameSim.EndSource(source);
            yield return new WaitForSeconds(0.3f);
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
                if (BattleEffects.Instance != null) BattleEffects.Instance.PlayMvp(mvp.boardPosition);
                if (BattleLogUI.Instance != null)
                    BattleLogUI.Instance.AddLog("MVP: " + BattleLogUI.ColorName(ClassName(mvp.data.pieceType), Team.Player) + "（深海に" + mvp.fleetDamage + "ダメージ）");
                if (sRank) GiveRing(mvp);   // 改二のMVPならケッコンカッコカリ
            }

            string rank = sRank ? "S勝利" : "A勝利";
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog("旗艦撃沈！ 作戦完了（" + rank + "）。" + BattleLogUI.ColorName("提督", Team.Player) + "の艦隊は帰投する"
                    + (sRank ? "。次の局は強化の選択肢が1枚増える" : ""));
            if (sRank && GameManager.Instance != null) GameManager.Instance.GrantFleetBonus();
            RunRoster.Feat(teitoku);
            if (BattleEffects.Instance != null)
            {
                BattleEffects.Instance.PlayRetreatHorn();
                BattleEffects.Instance.SetNight(false);
            }
            int battleMoves = fleet != null && GameManager.Instance != null ? GameManager.Instance.MoveCount - fleet.arrivalMove : -1;
            GameSim.RecordFleet(sRank ? "S" : "A", battleMoves);
            RecordShipDamage(ships);
            if (sRank || !BalanceTuning.FleetSupportNeedsS)
            {
                supportShots += BalanceTuning.FleetSupportShots;
                foreach (var ship in ships) supportClasses.Add(ship.data.pieceType);
            }
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
            GameSim.RecordFleet("轟沈", fleet != null && GameManager.Instance != null ? GameManager.Instance.MoveCount - fleet.arrivalMove : -1);
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("提督", Team.Player) + "轟沈……" + BattleLogUI.ColorName("艦娘", Team.Player) + "たちも海へ消えた");
            foreach (var ship in ships)
            {
                if (BattleEffects.Instance != null) BattleEffects.Instance.PlaySinkEffect(ship.boardPosition, true);
                CombatResolver.RemoveWithExit(ship, PieceController.ExitStyle.Sink);
            }
            if (BattleEffects.Instance != null) BattleEffects.Instance.SetNight(false);
            fleets.Remove(groupId);
        }
    }

    private static readonly Color AirGreen = new Color(0.55f, 0.95f, 0.6f);

    /// <summary>
    /// 陣形を決める: 艦娘が提督の縦の列にいる → 単縦陣、横の列にいる → 単横陣、両方か斜めだけ → 警戒陣。
    /// 単横陣は艦娘の防御を上げる（ほかの効果は ShipDamage・FormationGuard・SubEvades で見る）
    /// </summary>
    private IEnumerator DecideFormation(Fleet fleet, List<PieceInstance> ships, PieceInstance teitoku)
    {
        if (!BalanceTuning.FleetFormations || ships.Count == 0 || !teitoku.isAlive) yield break;
        int column = 0, row = 0;
        foreach (var ship in ships)
        {
            Vector2Int d = ship.boardPosition - teitoku.boardPosition;
            if (d.x == 0) column++;
            else if (d.y == 0) row++;
        }
        fleet.formation = column > 0 && row == 0 ? Formation.LineAhead
            : row > 0 && column == 0 ? Formation.LineAbreast : Formation.Vanguard;
        string name = FormationName(fleet.formation);
        string effect = fleet.formation == Formation.LineAhead ? "艦娘の攻撃+" + BalanceTuning.FormationLineAheadDamage
            : fleet.formation == Formation.LineAbreast ? "艦娘の防御+" + BalanceTuning.FormationAbreastDef + "・潜水艦は爆雷を避けやすい"
            : "提督の受けるダメージ−" + BalanceTuning.FormationVanguardGuard;
        if (fleet.formation == Formation.LineAbreast)
            foreach (var ship in ships) { ship.bonusDEF += BalanceTuning.FormationAbreastDef; CombatResolver.RefreshStats(ship); }
        GameSim.RecordFleet("陣形:" + name, 0);
        FloatingText.Spawn(teitoku.boardPosition, name, CutInUI.SeaLight, 4.2f, 0.25f);
        if (BattleEffects.Instance != null)
            foreach (var ship in ships) BattleEffects.Instance.PlayStream(teitoku.boardPosition, ship.boardPosition, CutInUI.SeaLight);
        if (BattleLogUI.Instance != null) BattleLogUI.Instance.AddLog("陣形: " + name + "（" + effect + "）");
        yield return new WaitForSeconds(0.5f);
    }

    /// <summary>警戒陣の提督は、受けるダメージが減る（CombatResolver.AfterGuard から）</summary>
    public static int FormationGuard(PieceInstance target)
    {
        if (Instance == null || target.data.pieceType != PieceType.Monotetsu || !target.isPromoted || target.linkedGroupId < 0) return 0;
        Fleet fleet;
        return Instance.fleets.TryGetValue(target.linkedGroupId, out fleet) && fleet.formation == Formation.Vanguard ? BalanceTuning.FormationVanguardGuard : 0;
    }

    /// <summary>単横陣の潜水艦は、爆雷をときどき避ける</summary>
    public bool SubEvades(PieceInstance sub)
    {
        Fleet fleet;
        if (sub.linkedGroupId < 0 || !fleets.TryGetValue(sub.linkedGroupId, out fleet) || fleet.formation != Formation.LineAbreast) return false;
        return Random.Range(0, 100) < BalanceTuning.FormationAbreastEvade;
    }

    /// <summary>制空権確保: 中破していない空母が盤にいる（結界で止まっていない）</summary>
    private static bool HasAirSuperiority()
    {
        if (BalanceTuning.AirSuperiorityGuard <= 0) return false;
        foreach (var p in BoardManager.Instance.GetTeamPieces(Team.Player))
            if (p.data.pieceType == PieceType.KanmusuCV && p.isAlive && !IsDamaged(p) && !IsSuppressed(p)) return true;
        return false;
    }

    private IEnumerator AnnounceAirSuperiority(List<PieceInstance> ships)
    {
        PieceInstance carrier = ships.Find(s => s.data.pieceType == PieceType.KanmusuCV && s.isAlive);
        if (carrier == null || BalanceTuning.AirSuperiorityGuard <= 0) yield break;
        FloatingText.Spawn(carrier.boardPosition, "制空権確保！", AirGreen, 4.2f, 0.25f);
        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog("制空権確保！ 空母がいるあいだ、深海から艦娘へのダメージ−" + BalanceTuning.AirSuperiorityGuard);
        yield return new WaitForSeconds(0.4f);
    }

    /// <summary>
    /// 基地航空隊: 艦隊がS勝利した次の局の初めに、盤の外から敵（C3・地形を除く）を爆撃する。
    /// ダメージは支援艦隊と同じく、元の値＋局の攻撃の上乗せ（防御無視）
    /// </summary>
    public IEnumerator LandBaseRaid()
    {
        if (BalanceTuning.LandBaseTargets <= 0) yield break;
        BoardManager bm = BoardManager.Instance;
        BattleEffects fx = BattleEffects.Instance;
        var targets = new List<PieceInstance>();
        foreach (var p in bm.GetTeamPieces(Team.Enemy))
            if (IsRandomFleetTarget(p)) targets.Add(p);
        if (targets.Count == 0) yield break;
        ShuffleList(targets);
        if (targets.Count > BalanceTuning.LandBaseTargets) targets.RemoveRange(BalanceTuning.LandBaseTargets, targets.Count - BalanceTuning.LandBaseTargets);

        int stage = StageManager.Instance != null ? StageManager.Instance.currentStage : 1;
        int damage = BalanceTuning.LandBaseDamage + StageManager.EnemyStageAtk(stage);
        if (BattleLogUI.Instance != null) BattleLogUI.Instance.AddLog("基地航空隊、発進！（前の局の艦隊のS勝利）");
        if (fx != null)
        {
            Vector2Int origin = new Vector2Int(bm.CurrentBoardSize / 2, -2);
            foreach (var t in targets) fx.PlayAirRaidEffect(origin, t.boardPosition, true);
            FloatingText.Spawn(new Vector2Int(bm.CurrentBoardSize / 2, bm.CurrentBoardSize / 2), "基地航空隊", AirGreen, 4.6f, 0.3f);
            yield return new WaitForSeconds(BattleEffects.AirRaidImpactTime);
        }
        var source = GameSim.BeginSource(null);
        int hit = 0;
        foreach (var t in targets)
        {
            if (!t.isAlive) continue;
            CombatResolver.AbilityHit(t, damage);
            hit++;
        }
        GameSim.EndSource(source);
        GameSim.RecordFleet("基地航空隊", hit);
        if (fx != null) yield return new WaitForSeconds(0.3f);
    }

    /// <summary>
    /// 盤の外からの爆撃・支援射撃が狙う敵: C3・地形と、まだ覚醒していないボス（雷帝・龍神・魔王）は狙わない。
    /// ボスを削ると早く第二形態になって、かえって負けやすくなったため（第十局の雷帝の覚醒が 39/85 → 70/98、中央13手 → 9手）
    /// </summary>
    private static bool IsRandomFleetTarget(PieceInstance p)
    {
        if (p == null || !p.isAlive || p.data.pieceType == PieceType.C3 || IsObstacle(p.data.pieceType)) return false;
        return !(p.team == Team.Enemy && p.data.canAwaken && !p.awakened);
    }

    /// <summary>艦娘の出撃位置: 提督の周り（入渠できるマス）。空いていなければ、または FleetSortieNearTeitoku でなければ自陣のどこか</summary>
    private Vector2Int? PickSortieSpot(PieceInstance teitoku, int halfBoard)
    {
        if (BalanceTuning.FleetSortieNearTeitoku)
        {
            BoardManager bm = BoardManager.Instance;
            var spots = new List<Vector2Int>();
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    Vector2Int p = teitoku.boardPosition + new Vector2Int(dx, dy);
                    if ((dx != 0 || dy != 0) && bm.IsInBounds(p) && bm.IsEmpty(p)) spots.Add(p);
                }
            if (spots.Count > 0) return spots[Random.Range(0, spots.Count)];
        }
        return FindEmptyInRange(0, halfBoard + 1);
    }

    /// <summary>自動プレイの集計用: 出撃した艦娘と提督・いちばん近い深海との距離（"位置:提督@1"・"位置:深海@4"）</summary>
    private static void RecordSortie(List<PieceInstance> ships, PieceInstance teitoku)
    {
        if (!GameSim.Headless) return;
        var abyss = AliveShinkai();
        foreach (var ship in ships)
        {
            GameSim.RecordFleet("位置:提督", Chebyshev(ship.boardPosition, teitoku.boardPosition));
            int near = 99;
            foreach (var s in abyss) near = Mathf.Min(near, Chebyshev(ship.boardPosition, s.boardPosition));
            if (near < 99) GameSim.RecordFleet("位置:深海", near);
        }
    }

    private static int Chebyshev(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }

    /// <summary>自動プレイの集計用: 艦隊戦が終わったときの、艦種ごとの与ダメージ（"艦:駆逐艦@5"）</summary>
    private static void RecordShipDamage(List<PieceInstance> ships)
    {
        if (!GameSim.Headless) return;
        foreach (var ship in ships) GameSim.RecordFleet("艦:" + ClassName(ship.data.pieceType), ship.fleetDamageAll);
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
            BattleLogUI.Instance.AddLog(BattleLogUI.ColorName("提督", Team.Player) + "は生還し、" + BattleLogUI.ColorName("物鉄・改", Team.Player) + "として前線に戻った（攻撃+1・体力+1、全方向に動ける）");
    }

    /// <summary>
    /// 支援艦隊: 帰投した艦隊が、自軍の手番の終わりにランダムな敵（C3以外）を攻撃する。
    /// 艦種で種類が変わる（空母=航空支援、駆逐・軽巡・潜水=長距離雷撃、戦艦・重巡=支援射撃）。ダメージはどれも同じ
    /// </summary>
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
                if (IsRandomFleetTarget(p)) targets.Add(p);
            if (targets.Count == 0) break;
            PieceInstance target = targets[Random.Range(0, targets.Count)];
            PieceType cls = supportClasses.Count > 0 ? supportClasses[Random.Range(0, supportClasses.Count)] : PieceType.KanmusuBB;
            string kind;
            if (cls == PieceType.KanmusuCV)
            {
                kind = "航空支援";
                if (fx != null) fx.PlayAirRaidEffect(origin, target.boardPosition);
                yield return new WaitForSeconds(BattleEffects.AirRaidImpactTime);
            }
            else if (cls == PieceType.KanmusuDD || cls == PieceType.KanmusuCL || cls == PieceType.KanmusuSS)
            {
                // 魚雷は手前から、狙った敵の列をまっすぐ走ってくる
                kind = "長距離雷撃";
                if (fx != null) fx.PlayTorpedoEffect(new Vector2Int(target.boardPosition.x, -1), target.boardPosition);
                yield return new WaitForSeconds(BattleEffects.TorpedoImpactTime);
            }
            else
            {
                kind = "支援射撃";
                if (fx != null) fx.PlayBombardmentEffect(origin, target.boardPosition);
                yield return new WaitForSeconds(BattleEffects.BombardmentImpactTime);
            }
            if (!target.isAlive) continue;
            int damage = BalanceTuning.FleetSupportDamage + StageManager.EnemyStageAtk(stage);
            if (BattleLogUI.Instance != null)
                BattleLogUI.Instance.AddLog("支援艦隊の" + kind + "！" + BattleLogUI.ColorName(target.DisplayName, target.team) + " に" + CombatResolver.AbilityHitPreview(target, damage) + "ダメージ");
            CombatResolver.AbilityHit(target, damage);
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
    private static readonly Color RingPink = new Color(1f, 0.72f, 0.82f);

    /// <summary>
    /// ケッコンカッコカリ: S勝利でMVPになった改二の艦娘と指輪を交わす。
    /// その周のあいだ、その艦種は最初から改二で出撃し、最大体力+RingMaxHp・能力のダメージ+RingDamage（指輪の印が付く）
    /// </summary>
    private void GiveRing(PieceInstance ship)
    {
        GameManager gm = GameManager.Instance;
        if (BalanceTuning.RingMaxHp <= 0 || ship == null || !ship.isAlive || !ship.kai2 || gm == null) return;
        PieceType type = ship.data.pieceType;
        if (gm.RingedClasses.Contains(type)) return;
        gm.RingedClasses.Add(type);
        ship.ringed = true;
        CombatResolver.RefreshStats(ship);
        GameSim.RecordFleet("ケッコン", gm.MoveCount);
        FloatingText.Spawn(ship.boardPosition, "ケッコンカッコカリ", RingPink, 4.4f, 0.3f);
        if (BattleEffects.Instance != null) BattleEffects.Instance.PlayRing(ship.boardPosition);
        if (BattleLogUI.Instance != null)
            BattleLogUI.Instance.AddLog("MVPの" + BattleLogUI.ColorName(ClassName(type) + "改二", Team.Player) + " とケッコンカッコカリ！ この周のあいだ、"
                + ClassName(type) + "は最初から改二で出撃する（体力+" + BalanceTuning.RingMaxHp + "・能力のダメージ+" + BalanceTuning.RingDamage + "）");
    }

    /// <summary>ケッコンカッコカリした艦種の艦娘: 改二で出撃し、最大体力を上げて指輪の印を付ける</summary>
    private static void ApplyRingedSortie(PieceInstance ship)
    {
        if (!ship.isPromoted) ship.Promote();
        if (!ship.kai2)
        {
            ship.kai2 = true;
            ship.bonusATK += 1;
            ship.bonusDEF += 1;
        }
        ship.ringed = true;
        ship.AddMaxHP(BalanceTuning.RingMaxHp);
        ship.currentHP = ship.MaxHP;
        CombatResolver.PlayFlip(ship);
        CombatResolver.RefreshStats(ship);
        FloatingText.Spawn(ship.boardPosition, "指輪", RingPink, 3.2f, 0.4f);
    }

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
            GameSim.RecordFleet("夜戦", gm.MoveCount - fleet.arrivalMove);
            if (BattleLogUI.Instance != null) BattleLogUI.Instance.AddLog("夜戦突入！ 駆逐・軽巡・潜水の攻撃が2倍になり、空母は攻撃できない");
            if (BattleEffects.Instance != null) BattleEffects.Instance.SetNight(true);
            yield return CutInUI.Play("夜戦突入", "我、夜戦に突入す！", null, CutInUI.Navy, 1.2f);
            // 夜間触接: 軽巡か空母がいれば夜偵が飛び、夜戦の艦娘の攻撃が上がる
            if (BalanceTuning.NightContactDamage > 0)
            {
                PieceInstance scout = null;
                foreach (var p in BoardManager.Instance.GetTeamPieces(Team.Player))
                    if (p.isAlive && (p.data.pieceType == PieceType.KanmusuCL || p.data.pieceType == PieceType.KanmusuCV) && !IsSuppressed(p)) { scout = p; break; }
                if (scout != null)
                {
                    fleet.nightContact = true;
                    GameSim.RecordFleet("夜間触接", gm.MoveCount - fleet.arrivalMove);
                    FloatingText.Spawn(scout.boardPosition, "夜間触接", CutInUI.SeaLight, 4f, 0.2f);
                    if (BattleEffects.Instance != null) BattleEffects.Instance.PlayHaloEffect(scout.boardPosition);
                    if (BattleLogUI.Instance != null) BattleLogUI.Instance.AddLog("夜間触接！ 夜戦のあいだ艦娘の攻撃+" + BalanceTuning.NightContactDamage);
                }
            }
        }

        var ships = new List<PieceInstance>();
        foreach (var p in BoardManager.Instance.GetTeamPieces(Team.Player))
            if (PieceTypes.IsKanmusu(p.data.pieceType) && p.isAlive) ships.Add(p);
        foreach (var ship in ships)
        {
            if (!ship.isAlive || IsSuppressed(ship)) continue;
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
        int damage = CombatResolver.AbilityDamage(ship, baseDamage) + (ship.isPromoted ? 1 : 0) + (ship.ringed ? BalanceTuning.RingDamage : 0) + BalanceTuning.FleetDamageBonus;
        if (fleet != null && fleet.formation == Formation.LineAhead) damage += BalanceTuning.FormationLineAheadDamage;
        if (IsDamaged(ship)) damage -= 1;
        PieceType type = ship.data.pieceType;
        if (fleet != null && fleet.night && (type == PieceType.KanmusuDD || type == PieceType.KanmusuCL || type == PieceType.KanmusuSS))
            damage *= 2;
        if (fleet != null && fleet.night && fleet.nightContact) damage += BalanceTuning.NightContactDamage;
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
            if (BattleEffects.Instance != null)
            {
                // いちばん近い随伴から障壁が張られる
                PieceInstance escort = null;
                foreach (var s in AliveShinkai())
                    if (!s.isFlagship && (escort == null || (s.boardPosition - target.boardPosition).sqrMagnitude < (escort.boardPosition - target.boardPosition).sqrMagnitude))
                        escort = s;
                if (escort != null) BattleEffects.Instance.PlayAbyssGuard(target.boardPosition, escort.boardPosition);
            }
        }
        // 深海には防御無視。ほかの駒には防御が効く（LegacyPierce なら昔どおり、どれにも防御無視）
        bool pierce = BalanceTuning.LegacyPierce || PieceTypes.IsShinkai(target.data.pieceType);
        int dealt = pierce ? damage : Mathf.Max(0, damage - target.DEF);
        if (PieceTypes.IsShinkai(target.data.pieceType)) ship.fleetDamage += Mathf.Min(dealt, target.currentHP);
        ship.fleetDamageAll += Mathf.Min(dealt, target.currentHP);
        CombatResolver.ApplyDamage(target, damage, pierce);
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
                // 空爆: 深海に1、深海の周り2マス以内の敵（CarrierSplashTargets 体）に2
                if (fx != null) fx.PlayAirRaidEffect(ship.boardPosition, target.boardPosition);
                yield return new WaitForSeconds(BattleEffects.AirRaidImpactTime);
                Vector2Int center = target.boardPosition;
                int bomb = ShipDamage(ship, BalanceTuning.CarrierStrikeDamage, fleet);
                LogAttack(ship, "空爆", target, bomb);
                FleetHit(ship, target, bomb);
                var nearby = GetEnemiesInRange(center, 2, Team.Enemy, target);
                ShuffleList(nearby);
                for (int i = 0; i < Mathf.Min(BalanceTuning.CarrierSplashTargets, nearby.Count); i++) FleetHit(ship, nearby[i], ShipDamage(ship, BalanceTuning.CarrierSplashDamage, fleet));
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
            if (!shinkai.isAlive || shinkai.isSealed) continue;
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
            int dealt = damage;
            if (PieceTypes.IsKanmusu(victim.data.pieceType) && HasAirSuperiority())
            {
                dealt = Mathf.Max(0, damage - BalanceTuning.AirSuperiorityGuard);
                FloatingText.Spawn(victim.boardPosition, "制空権", AirGreen, 2.6f, 0.1f);
            }
            CombatResolver.ApplyDamage(victim, dealt, false);
            yield return new WaitForSeconds(0.2f);
        }
    }
}
