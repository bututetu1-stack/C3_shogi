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
        int variant = GameManager.Instance != null ? GameManager.Instance.VariantFor(stage) : 0;
        foreach (var p in GetEnemyLayout(stage, bm.CurrentBoardSize, variant))
            bm.SpawnPiece(bm.GetPieceDataByType(p.type), Team.Enemy, p.position);

        ApplyStageScaling(bm, stage);
    }

    // ================================================================
    // 布陣の型（局ごとに2〜3通り。周の初めにどれを使うか決める。0 はもとの布陣）
    // ================================================================
    private static readonly string[][] VariantNames =
    {
        null,
        new[] { null, "岩場", "傀儡" },             // 1
        new[] { null, "弓兵の陣", "端の砦" },       // 2
        new[] { null, "大盾の列", "岩の中原" },     // 3
        new[] { null, "猪突の突撃", "妖狐の森" },   // 4
        new string[] { null },                      // 5 将棋の陣は本将棋のまま
        new[] { null, "大盾の構え", "結界の陣" },   // 6
        new[] { null, "影と狐", "影と弓" },         // 7
        new[] { null, "岩の城", "結界と傀儡" },     // 8
        new[] { null, "天狗と弓", "天狗と猪" },     // 9
        new[] { null, "結界の護り", "弓と盾" },     // 10
        new[] { null, "狐と骨", "結界の風" },       // 11
        new[] { null, "龍の結界", "猪と盾" },       // 12
        new[] { null, "傀儡の門", "狐と結界" },     // 13
        new[] { null, "盾と弓", "結界の審判" },     // 14
        new string[] { null },                      // 15 魔王降臨はそのまま
    };

    /// <summary>その局の布陣の型の数</summary>
    public static int VariantCount(int stage)
    {
        return stage >= 1 && stage < VariantNames.Length ? VariantNames[stage].Length : 1;
    }

    /// <summary>型の名前（もとの布陣は null）</summary>
    public static string VariantName(int stage, int variant)
    {
        if (stage < 1 || stage >= VariantNames.Length) return null;
        string[] names = VariantNames[stage];
        return variant >= 0 && variant < names.Length ? names[variant] : null;
    }

    /// <summary>局の名前に、型の名前を添える（例「初陣 ― 岩場」）</summary>
    public string GetStageTitle(int stage)
    {
        string name = GetStageName(stage);
        string v = VariantName(stage, GameManager.Instance != null ? GameManager.Instance.VariantFor(stage) : 0);
        return v == null ? name : name + " ― " + v;
    }

    /// <summary>ステージごとの敵の初期配置（もとの型）</summary>
    public static List<Placement> GetEnemyLayout(int stage, int size)
    {
        return GetEnemyLayout(stage, size, 0);
    }

    /// <summary>ステージごとの敵の初期配置（盤上端が敵陣）。variant が 0 ならもとの布陣、ほかは型の布陣</summary>
    public static List<Placement> GetEnemyLayout(int stage, int size, int variant)
    {
        if (variant > 0 && variant < VariantCount(stage)) return GetVariantLayout(stage, size, variant);
        return GetBaseLayout(stage, size);
    }

    private static List<Placement> GetBaseLayout(int stage, int size)
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
                list.Add(new Placement(PieceType.Kishou, c + 1, top - 1));   // 雷帝は第十・十三・十五局だけにして、第十四局の壁を下げる
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

    /// <summary>型の布陣。前半は駒を少なめにして、新しい敵を1つずつ見せる</summary>
    private static List<Placement> GetVariantLayout(int stage, int size, int variant)
    {
        int c = size / 2;
        int top = size - 1;
        var list = new List<Placement>();
        switch (stage * 10 + variant)
        {
            // 第一局（5×5）
            case 11: // 岩場: C3 は隅、巨岩が道をふさぐ
                list.Add(new Placement(PieceType.C3, 0, top));
                list.Add(new Placement(PieceType.Gold, 1, top));
                list.Add(new Placement(PieceType.Pawn, 0, top - 1));
                list.Add(new Placement(PieceType.Pawn, 1, top - 1));
                list.Add(new Placement(PieceType.Kyogan, c, top - 1));
                list.Add(new Placement(PieceType.Kyogan, size - 1, top - 1));
                break;
            case 12: // 傀儡: C3 の前に傀儡（傀儡に攻撃を吸われるぶん、金将は置かない。置くと勝率94%と第一局にしては重かった）
                list.Add(new Placement(PieceType.C3, c, top));
                list.Add(new Placement(PieceType.Kugutsu, c, top - 1));
                list.Add(new Placement(PieceType.Pawn, c - 1, top - 1));
                list.Add(new Placement(PieceType.Pawn, c + 1, top - 1));
                break;

            // 第二局（5×5）
            case 21: // 弓兵の陣（弓兵2体だと5×5の盤では毎手番2列が撃たれて重すぎた: 勝率82%。1体に）
                list.Add(new Placement(PieceType.C3, c, top));
                list.Add(new Placement(PieceType.Yumihei, c - 1, top));
                list.Add(new Placement(PieceType.Gold, c + 1, top));
                list.Add(new Placement(PieceType.Silver, 0, top));
                list.Add(new Placement(PieceType.Pawn, 0, top - 1));
                list.Add(new Placement(PieceType.Pawn, c, top - 1));
                list.Add(new Placement(PieceType.Pawn, size - 1, top - 1));
                break;
            case 22: // 端の砦: C3 は隅、金銀と巨岩で守る
                list.Add(new Placement(PieceType.C3, size - 1, top));
                list.Add(new Placement(PieceType.Gold, size - 2, top));
                list.Add(new Placement(PieceType.Silver, size - 1, top - 1));
                list.Add(new Placement(PieceType.Kyogan, size - 2, top - 1));
                list.Add(new Placement(PieceType.Pawn, 0, top - 1));
                list.Add(new Placement(PieceType.Pawn, 1, top - 1));
                list.Add(new Placement(PieceType.Pawn, 2, top - 1));
                break;

            // 第三局（7×7）
            case 31: // 大盾の列: 大盾を前に（横から攻めれば崩せる。大盾2体と弓兵では勝率79%と重すぎたので、大盾1体と銀将に）
                list.Add(new Placement(PieceType.C3, c, top));
                list.Add(new Placement(PieceType.Ootate, c - 1, top - 2));
                list.Add(new Placement(PieceType.Pawn, c + 1, top - 2));
                list.Add(new Placement(PieceType.Pawn, c, top - 2));
                list.Add(new Placement(PieceType.Pawn, 0, top - 2));
                list.Add(new Placement(PieceType.Pawn, size - 1, top - 2));
                list.Add(new Placement(PieceType.Silver, 1, top - 1));   // 弓兵だと大盾1体でも勝率84%と重かった
                list.Add(new Placement(PieceType.Gold, c - 1, top));
                list.Add(new Placement(PieceType.Gold, c + 1, top));
                break;
            case 32: // 岩の中原: 盤の真ん中に巨岩を散らす
                list.Add(new Placement(PieceType.C3, c, top));
                list.Add(new Placement(PieceType.Kyogan, 1, c));
                list.Add(new Placement(PieceType.Kyogan, size - 2, c));
                list.Add(new Placement(PieceType.Kyogan, c, c));
                list.Add(new Placement(PieceType.Bishop, c, top - 1));
                list.Add(new Placement(PieceType.Silver, 0, top));
                list.Add(new Placement(PieceType.Silver, size - 1, top));
                list.Add(new Placement(PieceType.Gold, c - 1, top));
                list.Add(new Placement(PieceType.Gold, c + 1, top));
                list.Add(new Placement(PieceType.Pawn, 1, top - 2));
                list.Add(new Placement(PieceType.Pawn, c, top - 2));
                list.Add(new Placement(PieceType.Pawn, size - 2, top - 2));
                break;

            // 第四局（7×7）
            case 41: // 猪突の突撃
                list.Add(new Placement(PieceType.C3, c, top));
                list.Add(new Placement(PieceType.Chototsu, 1, top - 1));
                list.Add(new Placement(PieceType.Chototsu, size - 2, top - 1));
                list.Add(new Placement(PieceType.Pawn, c, top - 2));
                list.Add(new Placement(PieceType.Gold, c - 1, top));
                list.Add(new Placement(PieceType.Gold, c + 1, top));
                list.Add(new Placement(PieceType.Silver, 1, top));
                list.Add(new Placement(PieceType.Silver, size - 2, top));
                list.Add(new Placement(PieceType.Pawn, 0, top - 2));
                list.Add(new Placement(PieceType.Pawn, c - 1, top - 2));
                list.Add(new Placement(PieceType.Pawn, c + 1, top - 2));
                list.Add(new Placement(PieceType.Pawn, size - 1, top - 2));
                break;
            case 42: // 妖狐の森: 巨岩の木立に妖狐が潜む
                list.Add(new Placement(PieceType.C3, c, top));
                list.Add(new Placement(PieceType.Youko, c - 1, top - 1));
                list.Add(new Placement(PieceType.Youko, c + 1, top - 1));
                list.Add(new Placement(PieceType.Kyogan, 1, c));
                list.Add(new Placement(PieceType.Kyogan, c, c));
                list.Add(new Placement(PieceType.Kyogan, size - 2, c));
                list.Add(new Placement(PieceType.Gold, c - 1, top));
                list.Add(new Placement(PieceType.Gold, c + 1, top));
                list.Add(new Placement(PieceType.Silver, 0, top));
                list.Add(new Placement(PieceType.Silver, size - 1, top));
                list.Add(new Placement(PieceType.Pawn, 1, top - 2));
                list.Add(new Placement(PieceType.Pawn, size - 2, top - 2));
                break;

            // 第六局から（9×9）
            case 61: // 大盾の構え
                list.Add(new Placement(PieceType.C3, c, top));
                AddStandardArmy(list, size, PieceType.Kishou, PieceType.Kishou, PieceType.Lance, PieceType.Lance);
                for (int x = c - 1; x <= c + 1; x++) Replace(list, x, top - 2, PieceType.Ootate);
                break;
            case 62: // 結界の陣（結界が初めて出る）
                list.Add(new Placement(PieceType.C3, c, top));
                AddStandardArmy(list, size, PieceType.Kishou, PieceType.Kishou, PieceType.Lance, PieceType.Lance);
                list.Add(new Placement(PieceType.Kekkai, c, top - 1));
                break;
            case 71: // 影と狐
                list.Add(new Placement(PieceType.C3, c, top));
                AddStandardArmy(list, size, PieceType.Youko, PieceType.Youko, PieceType.Kagenin, PieceType.Kagenin);
                list.Add(new Placement(PieceType.Kyogan, 2, c));
                list.Add(new Placement(PieceType.Kyogan, size - 3, c));
                break;
            case 72: // 影と弓
                list.Add(new Placement(PieceType.C3, c, top));
                AddStandardArmy(list, size, PieceType.Kishou, PieceType.Knight, PieceType.Kagenin, PieceType.Kagenin);
                Replace(list, 2, top - 2, PieceType.Yumihei);
                Replace(list, size - 3, top - 2, PieceType.Yumihei);
                list.Add(new Placement(PieceType.Kyogan, c, c));
                break;
            case 81: // 岩の城
                list.Add(new Placement(PieceType.C3, c, top));
                AddStandardArmy(list, size, PieceType.Kishou, PieceType.Kishou, PieceType.Lance, PieceType.Lance);
                list.Add(new Placement(PieceType.Teppeki, c, top - 1));
                Replace(list, c - 1, top - 2, PieceType.Ootate);
                Replace(list, c + 1, top - 2, PieceType.Ootate);
                list.Add(new Placement(PieceType.Kyogan, 2, top - 3));
                list.Add(new Placement(PieceType.Kyogan, size - 3, top - 3));
                list.Add(new Placement(PieceType.Kyogan, c, c));
                break;
            case 82: // 結界と傀儡
                list.Add(new Placement(PieceType.C3, c, top));
                AddStandardArmy(list, size, PieceType.Kishou, PieceType.Kishou, PieceType.Lance, PieceType.Lance);
                list.Add(new Placement(PieceType.Teppeki, c, top - 1));
                Replace(list, size - 2, top - 1, PieceType.Kekkai);
                list.Add(new Placement(PieceType.Kugutsu, c, top - 3));
                break;
            case 91: // 天狗と弓
                list.Add(new Placement(PieceType.C3, c, top));
                AddStandardArmy(list, size, PieceType.Tengu, PieceType.Tengu, PieceType.Kagenin, PieceType.Kagenin);
                list.Add(new Placement(PieceType.Teppeki, c, top - 1));
                Replace(list, 1, top - 2, PieceType.Yumihei);
                Replace(list, size - 2, top - 2, PieceType.Yumihei);
                break;
            case 92: // 天狗と猪（猪突が端から走り出す）
                list.Add(new Placement(PieceType.C3, c, top));
                AddStandardArmy(list, size, PieceType.Tengu, PieceType.Tengu, PieceType.Chototsu, PieceType.Chototsu);
                list.Add(new Placement(PieceType.Teppeki, c, top - 1));
                Remove(list, 0, top - 2);
                Remove(list, size - 1, top - 2);
                break;

            // ボスの局と後半: もとの布陣の護衛を入れ替える
            default:
                list = GetBaseLayout(stage, size);
                switch (stage * 10 + variant)
                {
                    case 101: // 結界の護り
                        Replace(list, c - 2, top - 1, PieceType.Kekkai);
                        Replace(list, c + 2, top - 1, PieceType.Kekkai);
                        break;
                    case 102: // 弓と盾
                        Replace(list, 0, top, PieceType.Yumihei);
                        Replace(list, size - 1, top, PieceType.Yumihei);
                        Replace(list, c - 2, top, PieceType.Ootate);
                        Replace(list, c + 2, top, PieceType.Ootate);
                        break;
                    case 111: // 狐と骨
                        Replace(list, 0, top - 1, PieceType.Youko);
                        Replace(list, size - 1, top - 1, PieceType.Youko);
                        break;
                    case 112: // 結界の風
                        Replace(list, c - 1, top - 1, PieceType.Kekkai);
                        Replace(list, c + 1, top - 1, PieceType.Yumihei);
                        break;
                    case 121: // 龍の結界
                        Replace(list, c - 1, top - 1, PieceType.Kekkai);
                        Replace(list, c + 2, top - 1, PieceType.Yumihei);
                        break;
                    case 122: // 猪と盾
                        Replace(list, 0, top, PieceType.Chototsu);
                        Replace(list, size - 1, top, PieceType.Chototsu);
                        Remove(list, 0, top - 2);
                        Remove(list, size - 1, top - 2);
                        Replace(list, c - 2, top, PieceType.Ootate);
                        Replace(list, c + 2, top, PieceType.Ootate);
                        break;
                    case 131: // 傀儡の門
                        Replace(list, 0, top - 1, PieceType.Kugutsu);
                        Replace(list, size - 1, top - 1, PieceType.Kugutsu);
                        break;
                    case 132: // 狐と結界
                        Replace(list, 0, top, PieceType.Youko);
                        Replace(list, size - 1, top, PieceType.Youko);
                        Replace(list, c + 1, top - 1, PieceType.Kekkai);
                        break;
                    case 141: // 盾と弓
                        Replace(list, c - 1, top, PieceType.Ootate);
                        Replace(list, c + 1, top, PieceType.Ootate);
                        Replace(list, 0, top - 1, PieceType.Yumihei);
                        Replace(list, size - 1, top - 1, PieceType.Yumihei);
                        break;
                    case 142: // 結界の審判
                        Replace(list, c + 1, top - 1, PieceType.Kekkai);
                        Replace(list, 1, top - 1, PieceType.Youko);
                        Replace(list, size - 2, top - 1, PieceType.Youko);
                        break;
                }
                break;
        }
        return list;
    }

    /// <summary>(x, y) にいる駒を別の種類に替える（いなければ置く）</summary>
    private static void Replace(List<Placement> list, int x, int y, PieceType type)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].position.x != x || list[i].position.y != y) continue;
            list[i] = new Placement(type, x, y);
            return;
        }
        list.Add(new Placement(type, x, y));
    }

    /// <summary>(x, y) の駒を取り除く</summary>
    private static void Remove(List<Placement> list, int x, int y)
    {
        list.RemoveAll(p => p.position.x == x && p.position.y == y);
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
        foreach (var enemy in bm.GetTeamPieces(Team.Enemy))
            ScaleEnemy(enemy, stage);
    }

    /// <summary>敵の駒1体に局による強化を付ける（対局の途中で出てくる深海・歩にも同じものを付ける）</summary>
    public static void ScaleEnemy(PieceInstance enemy, int stage)
    {
        if (enemy.data.pieceType == PieceType.C3)
        {
            // 後半ほど敵C3も打たれ強くなる
            enemy.AddMaxHP((stage - 1) / BalanceTuning.EnemyC3HpDivisor + BalanceTuning.EnemyC3ExtraHP);
        }
        else
        {
            // 前半（EnemyScaleFromStage より前）は上乗せしない
            int late = Mathf.Max(0, stage - BalanceTuning.EnemyScaleFromStage + 1);
            enemy.AddMaxHP(late / Mathf.Max(1, BalanceTuning.EnemyLateHpDivisor));
            enemy.stageBonusATK = Mathf.FloorToInt(late / Mathf.Max(0.5f, BalanceTuning.EnemyLateAtkDivisor));
            enemy.bonusATK += enemy.stageBonusATK;
        }
        CombatResolver.RefreshStats(enemy);
    }

    /// <summary>局による敵の攻撃の上乗せ（艦娘の「提督の練度」も同じ値）</summary>
    public static int EnemyStageAtk(int stage)
    {
        return Mathf.Max(0, stage - BalanceTuning.EnemyAtkDelay) / BalanceTuning.EnemyAtkDivisor;
    }

    /// <summary>プレイヤー駒にステージに応じた小規模強化を付与</summary>
    public void ApplyPlayerScaling()
    {
        foreach (var p in BoardManager.Instance.GetTeamPieces(Team.Player))
            ScalePlayer(p, currentStage);
    }

    /// <summary>自軍の駒1体に局による強化を付ける（全員に体力、C3以外に防御。対局の途中で出てくる艦娘にも付ける）</summary>
    public static void ScalePlayer(PieceInstance p, int stage)
    {
        p.AddMaxHP((stage - 1) / BalanceTuning.PlayerHpDivisor);
        if (p.data.pieceType != PieceType.C3)
        {
            p.bonusDEF += (stage - 1) / BalanceTuning.PlayerDefDivisor;
            if (BalanceTuning.PlayerAtkDivisor > 0) p.bonusATK += (stage - 1) / BalanceTuning.PlayerAtkDivisor;
        }
        CombatResolver.RefreshStats(p);
    }

    public void AdvanceStage() { currentStage++; }
    public bool IsLastStage() { return currentStage >= maxStages; }
}
