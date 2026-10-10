using UnityEngine;
using System.Collections.Generic;

public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    public const int MaxStages = 15;

    public int currentStage = 1;
    public int maxStages = MaxStages;

    public struct Placement
    {
        public PieceType type;
        public Vector2Int position;

        public Placement(PieceType type, int x, int y)
        {
            this.type = type;
            position = new Vector2Int(x, y);
        }
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        // シリアライズ値が古い場合に備えて上書き
        maxStages = MaxStages;
    }

    public int GetBoardSize()
    {
        return GetBoardSizeForStage(currentStage);
    }

    public static int GetBoardSizeForStage(int stage)
    {
        if (stage <= 2) return 5;
        if (stage <= 4) return 7;
        return 9;
    }

    public string GetStageName(int stage)
    {
        switch (stage)
        {
            case 1: return "初陣";
            case 2: return "試練";
            case 3: return "中原の戦い";
            case 4: return "攻防";
            case 5: return "将棋の陣";
            case 6: return "鬼将の陣";
            case 7: return "影の軍勢";
            case 8: return "鉄壁の要塞";
            case 9: return "天狗の嵐";
            case 10: return "雷帝降臨";
            case 11: return "風と骨の章";
            case 12: return "龍の目覚め";
            case 13: return "黄泉の門";
            case 14: return "閻魔の審判";
            case 15: return "魔王降臨";
            default: return "第" + stage + "局";
        }
    }

    public void SetupEnemyForStage(int stage)
    {
        BoardManager bm = BoardManager.Instance;
        foreach (var p in GetEnemyLayout(stage, bm.CurrentBoardSize))
            bm.SpawnPiece(bm.GetPieceDataByType(p.type), Team.Enemy, p.position);

        ApplyStageScaling(bm, stage);
    }

    /// <summary>ステージごとの敵の初期配置（盤上端が敵陣）</summary>
    public static List<Placement> GetEnemyLayout(int stage, int size)
    {
        var list = new List<Placement>();
        int c = size / 2;
        int top = size - 1;

        // C3は常に最上段中央
        list.Add(new Placement(PieceType.C3, c, top));

        switch (stage)
        {
            case 1:
                for (int x = 1; x < size - 1; x++) list.Add(new Placement(PieceType.Pawn, x, top - 1));
                list.Add(new Placement(PieceType.Gold, c - 1, top));
                break;

            case 2:
                for (int x = 0; x < size; x++) list.Add(new Placement(PieceType.Pawn, x, top - 1));
                list.Add(new Placement(PieceType.Gold, c - 1, top));
                list.Add(new Placement(PieceType.Gold, c + 1, top));
                list.Add(new Placement(PieceType.Silver, 0, top));
                break;

            case 3:
                for (int x = 1; x < size - 1; x++) list.Add(new Placement(PieceType.Pawn, x, top - 2));
                list.Add(new Placement(PieceType.Gold, c - 1, top));
                list.Add(new Placement(PieceType.Gold, c + 1, top));
                list.Add(new Placement(PieceType.Silver, 0, top));
                list.Add(new Placement(PieceType.Silver, size - 1, top));
                list.Add(new Placement(PieceType.Bishop, c, top - 1));
                break;

            case 4:
                for (int x = 0; x < size; x++) list.Add(new Placement(PieceType.Pawn, x, top - 2));
                list.Add(new Placement(PieceType.Gold, c - 1, top));
                list.Add(new Placement(PieceType.Gold, c + 1, top));
                list.Add(new Placement(PieceType.Silver, 0, top));
                list.Add(new Placement(PieceType.Silver, size - 1, top));
                list.Add(new Placement(PieceType.Rook, c - 1, top - 1));
                list.Add(new Placement(PieceType.Bishop, c + 1, top - 1));
                break;

            case 5:
                AddStandardArmy(list, size, PieceType.Knight, PieceType.Knight, PieceType.Lance, PieceType.Lance);
                break;

            case 6: // 鬼将の陣 - 桂馬が鬼将に
                AddStandardArmy(list, size, PieceType.Kishou, PieceType.Kishou, PieceType.Lance, PieceType.Lance);
                break;

            case 7: // 影の軍勢 - 香車が影忍に、片方の桂馬が鬼将に
                AddStandardArmy(list, size, PieceType.Kishou, PieceType.Knight, PieceType.Kagenin, PieceType.Kagenin);
                break;

            case 8: // 鉄壁の要塞 - 鉄壁がC3を守る
                AddStandardArmy(list, size, PieceType.Kishou, PieceType.Kishou, PieceType.Lance, PieceType.Lance);
                list.Add(new Placement(PieceType.Teppeki, c, top - 1));
                break;

            case 9: // 天狗の嵐
                AddStandardArmy(list, size, PieceType.Tengu, PieceType.Tengu, PieceType.Kagenin, PieceType.Kagenin);
                list.Add(new Placement(PieceType.Teppeki, c, top - 1));
                break;

            case 10: // 雷帝降臨
                AddPawnRow(list, size, top - 2);
                list.Add(new Placement(PieceType.Gold, c - 1, top));
                list.Add(new Placement(PieceType.Gold, c + 1, top));
                list.Add(new Placement(PieceType.Teppeki, c - 2, top));
                list.Add(new Placement(PieceType.Teppeki, c + 2, top));
                list.Add(new Placement(PieceType.Kishou, c - 3, top));
                list.Add(new Placement(PieceType.Kishou, c + 3, top));
                list.Add(new Placement(PieceType.Kagenin, 0, top));
                list.Add(new Placement(PieceType.Kagenin, size - 1, top));
                list.Add(new Placement(PieceType.Raitei, c - 1, top - 1));
                list.Add(new Placement(PieceType.Bishop, c + 1, top - 1));
                list.Add(new Placement(PieceType.Tengu, c - 2, top - 1));
                list.Add(new Placement(PieceType.Tengu, c + 2, top - 1));
                break;

            case 11: // 風と骨の章
                AddPawnRow(list, size, top - 2);
                list.Add(new Placement(PieceType.Lance, 0, top));
                list.Add(new Placement(PieceType.Fujin, c - 3, top));
                list.Add(new Placement(PieceType.Silver, c - 2, top));
                list.Add(new Placement(PieceType.Gold, c - 1, top));
                list.Add(new Placement(PieceType.Gold, c + 1, top));
                list.Add(new Placement(PieceType.Silver, c + 2, top));
                list.Add(new Placement(PieceType.Fujin, c + 3, top));
                list.Add(new Placement(PieceType.Lance, size - 1, top));
                list.Add(new Placement(PieceType.Dokuro, 0, top - 1));
                list.Add(new Placement(PieceType.Dokuro, 1, top - 1));
                list.Add(new Placement(PieceType.Rook, c - 1, top - 1));
                list.Add(new Placement(PieceType.Bishop, c + 1, top - 1));
                list.Add(new Placement(PieceType.Dokuro, size - 2, top - 1));
                list.Add(new Placement(PieceType.Dokuro, size - 1, top - 1));
                break;

            case 12: // 龍の目覚め
                AddPawnRow(list, size, top - 2);
                list.Add(new Placement(PieceType.Kagenin, 0, top));
                list.Add(new Placement(PieceType.Kishou, c - 3, top));
                list.Add(new Placement(PieceType.Teppeki, c - 2, top));
                list.Add(new Placement(PieceType.Gold, c - 1, top));
                list.Add(new Placement(PieceType.Gold, c + 1, top));
                list.Add(new Placement(PieceType.Teppeki, c + 2, top));
                list.Add(new Placement(PieceType.Kishou, c + 3, top));
                list.Add(new Placement(PieceType.Kagenin, size - 1, top));
                list.Add(new Placement(PieceType.Enmashi, c - 1, top - 1));
                list.Add(new Placement(PieceType.Ryuujin, c, top - 1));
                list.Add(new Placement(PieceType.Bishop, c + 1, top - 1));
                list.Add(new Placement(PieceType.Enmashi, c + 2, top - 1));
                break;

            case 13: // 黄泉の門
                AddPawnRow(list, size, top - 2);
                list.Add(new Placement(PieceType.Tengu, 0, top));
                list.Add(new Placement(PieceType.Kishou, c - 3, top));
                list.Add(new Placement(PieceType.Gundaishou, c - 2, top));
                list.Add(new Placement(PieceType.Gold, c - 1, top));
                list.Add(new Placement(PieceType.Gold, c + 1, top));
                list.Add(new Placement(PieceType.Gundaishou, c + 2, top));
                list.Add(new Placement(PieceType.Kishou, c + 3, top));
                list.Add(new Placement(PieceType.Tengu, size - 1, top));
                list.Add(new Placement(PieceType.Dokuro, 0, top - 1));
                list.Add(new Placement(PieceType.Yomigaeru, 1, top - 1));
                list.Add(new Placement(PieceType.Raitei, c, top - 1));
                list.Add(new Placement(PieceType.Kagenin, c + 1, top - 1));
                list.Add(new Placement(PieceType.Yomigaeru, size - 2, top - 1));
                list.Add(new Placement(PieceType.Dokuro, size - 1, top - 1));
                break;

            case 14: // 閻魔の審判
                AddPawnRow(list, size, top - 2);
                list.Add(new Placement(PieceType.Kagenin, 0, top));
                list.Add(new Placement(PieceType.Fujin, c - 3, top));
                list.Add(new Placement(PieceType.Enmashi, c - 2, top));
                list.Add(new Placement(PieceType.Teppeki, c - 1, top));
                list.Add(new Placement(PieceType.Teppeki, c + 1, top));
                list.Add(new Placement(PieceType.Enmashi, c + 2, top));
                list.Add(new Placement(PieceType.Fujin, c + 3, top));
                list.Add(new Placement(PieceType.Kagenin, size - 1, top));
                list.Add(new Placement(PieceType.Tengu, 0, top - 1));
                list.Add(new Placement(PieceType.Gundaishou, 1, top - 1));
                list.Add(new Placement(PieceType.Ryuujin, c, top - 1));
                list.Add(new Placement(PieceType.Raitei, c + 1, top - 1));
                list.Add(new Placement(PieceType.Gundaishou, size - 2, top - 1));
                list.Add(new Placement(PieceType.Tengu, size - 1, top - 1));
                break;

            default: // 15: 魔王降臨 - 最終ボス
                // 前列: 歩の代わりに髑髏8体（中央は空ける）
                for (int x = 0; x < size; x++)
                    if (x != c) list.Add(new Placement(PieceType.Dokuro, x, top - 2));
                list.Add(new Placement(PieceType.Ryuujin, 0, top));
                list.Add(new Placement(PieceType.Kishou, c - 3, top));
                list.Add(new Placement(PieceType.Teppeki, c - 2, top));
                list.Add(new Placement(PieceType.Enmashi, c - 1, top));
                list.Add(new Placement(PieceType.Enmashi, c + 1, top));
                list.Add(new Placement(PieceType.Teppeki, c + 2, top));
                list.Add(new Placement(PieceType.Kishou, c + 3, top));
                list.Add(new Placement(PieceType.Ryuujin, size - 1, top));
                list.Add(new Placement(PieceType.Gundaishou, 0, top - 1));
                list.Add(new Placement(PieceType.Fujin, 1, top - 1));
                list.Add(new Placement(PieceType.Maou, c, top - 1));
                list.Add(new Placement(PieceType.Raitei, c + 1, top - 1));
                list.Add(new Placement(PieceType.Fujin, size - 2, top - 1));
                list.Add(new Placement(PieceType.Gundaishou, size - 1, top - 1));
                break;
        }
        return list;
    }

    private static void AddPawnRow(List<Placement> list, int size, int row)
    {
        for (int x = 0; x < size; x++)
            list.Add(new Placement(PieceType.Pawn, x, row));
    }

    /// <summary>9x9の平手の布陣（桂・香の位置の駒を差し替え可能）。飛車・角は本将棋と同じく、敵から見て右に飛車・左に角</summary>
    private static void AddStandardArmy(List<Placement> list, int size,
        PieceType leftKnight, PieceType rightKnight, PieceType leftLance, PieceType rightLance)
    {
        int c = size / 2;
        int top = size - 1;
        AddPawnRow(list, size, top - 2);
        list.Add(new Placement(PieceType.Gold, c - 1, top));
        list.Add(new Placement(PieceType.Gold, c + 1, top));
        list.Add(new Placement(PieceType.Silver, c - 2, top));
        list.Add(new Placement(PieceType.Silver, c + 2, top));
        list.Add(new Placement(leftKnight, c - 3, top));
        list.Add(new Placement(rightKnight, c + 3, top));
        list.Add(new Placement(leftLance, 0, top));
        list.Add(new Placement(rightLance, size - 1, top));
        // 後手の飛車は8二、角は2二（こちらから見て左が飛車、右が角）
        list.Add(new Placement(PieceType.Rook, 1, top - 1));
        list.Add(new Placement(PieceType.Bishop, size - 2, top - 1));
    }

    private void ApplyStageScaling(BoardManager bm, int stage)
    {
        int hpBonus = stage / BalanceTuning.EnemyHpDivisor;
        int atkBonus = stage / BalanceTuning.EnemyAtkDivisor;
        int c3Bonus = (stage - 1) / BalanceTuning.EnemyC3HpDivisor + BalanceTuning.EnemyC3ExtraHP;   // 後半ほど敵C3も打たれ強くなる
        foreach (var enemy in bm.GetTeamPieces(Team.Enemy))
        {
            if (enemy.data.pieceType == PieceType.C3)
            {
                enemy.AddMaxHP(c3Bonus);
                CombatResolver.RefreshStats(enemy);
                continue;
            }
            enemy.AddMaxHP(hpBonus);
            enemy.bonusATK += atkBonus;
            CombatResolver.RefreshStats(enemy);
        }
    }

    /// <summary>プレイヤー駒にステージに応じた小規模強化を付与</summary>
    public void ApplyPlayerScaling()
    {
        BoardManager bm = BoardManager.Instance;
        int hpBonus = (currentStage - 1) / BalanceTuning.PlayerHpDivisor;
        int defBonus = (currentStage - 1) / BalanceTuning.PlayerDefDivisor;
        foreach (var p in bm.GetTeamPieces(Team.Player))
        {
            // 全員にHP強化（C3含む）
            p.AddMaxHP(hpBonus);
            // C3以外にDEF強化
            if (p.data.pieceType != PieceType.C3)
                p.bonusDEF += defBonus;
            CombatResolver.RefreshStats(p);
        }
    }

    public void AdvanceStage() { currentStage++; }
    public bool IsLastStage() { return currentStage >= maxStages; }
}
