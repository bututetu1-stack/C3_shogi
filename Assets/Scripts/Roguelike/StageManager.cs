using UnityEngine;
using System.Collections.Generic;

public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    public int currentStage = 1;
    public int maxStages = 15;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        // Force override in case serialized value is stale
        maxStages = 15;
    }

    public int GetBoardSize()
    {
        switch (currentStage)
        {
            case 1: return 5;
            case 2: return 5;
            case 3: return 7;
            case 4: return 7;
            default: return 9;
        }
    }

    public string GetStageName(int stage)
    {
        switch (stage)
        {
            case 1: return "\u521D\u9663";
            case 2: return "\u8A66\u7DF4";
            case 3: return "\u4E2D\u539F\u306E\u6226\u3044";
            case 4: return "\u653B\u9632";
            case 5: return "\u5C06\u68CB\u306E\u9663";
            case 6: return "\u9B3C\u5C06\u306E\u9663";
            case 7: return "\u5F71\u306E\u8ECD\u52E2";
            case 8: return "\u9244\u58C1\u306E\u8981\u585E";
            case 9: return "\u5929\u72D7\u306E\u5D50";
            case 10: return "\u96F7\u5E1D\u964D\u81E8";
            case 11: return "\u98A8\u3068\u9AA8\u306E\u7AE0";
            case 12: return "\u9F8D\u306E\u76EE\u899A\u3081";
            case 13: return "\u9EC4\u6CC9\u306E\u9580";
            case 14: return "\u95BB\u9B54\u306E\u5BE9\u5224";
            case 15: return "\u9B54\u738B\u964D\u81E8";
            default: return "Stage " + stage;
        }
    }

    public void SetupEnemyForStage(int stage)
    {
        BoardManager bm = BoardManager.Instance;
        int size = bm.CurrentBoardSize;
        int center = size / 2;
        int top = size - 1;

        // C3 is always at top center
        bm.SpawnPiece(bm.GetPieceDataByType(PieceType.C3), Team.Enemy, new Vector2Int(center, top));

        if (stage == 1)
        {
            for (int x = 1; x < size - 1; x++)
                bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Pawn), Team.Enemy, new Vector2Int(x, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gold), Team.Enemy, new Vector2Int(center - 1, top));
        }
        else if (stage == 2)
        {
            for (int x = 0; x < size - 1; x++)
                bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Pawn), Team.Enemy, new Vector2Int(x, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gold), Team.Enemy, new Vector2Int(center - 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gold), Team.Enemy, new Vector2Int(center + 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Silver), Team.Enemy, new Vector2Int(0, top));
        }
        else if (stage == 3)
        {
            for (int x = 1; x < size - 1; x++)
                bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Pawn), Team.Enemy, new Vector2Int(x, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gold), Team.Enemy, new Vector2Int(center - 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gold), Team.Enemy, new Vector2Int(center + 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Silver), Team.Enemy, new Vector2Int(0, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Silver), Team.Enemy, new Vector2Int(size - 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Bishop), Team.Enemy, new Vector2Int(center, top - 1));
        }
        else if (stage == 4)
        {
            for (int x = 0; x < size - 1; x++)
                bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Pawn), Team.Enemy, new Vector2Int(x, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gold), Team.Enemy, new Vector2Int(center - 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gold), Team.Enemy, new Vector2Int(center + 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Silver), Team.Enemy, new Vector2Int(0, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Silver), Team.Enemy, new Vector2Int(size - 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Rook), Team.Enemy, new Vector2Int(center - 1, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Bishop), Team.Enemy, new Vector2Int(center + 1, top - 1));
        }
        else if (stage == 5)
        {
            SetupStandard9x9(bm, size, center, top);
        }
        else if (stage == 6)
        {
            // "鬼将の陣" - Knights replaced by Kishou
            SpawnPawnRow(bm, size, top - 2);
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gold), Team.Enemy, new Vector2Int(center - 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gold), Team.Enemy, new Vector2Int(center + 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Silver), Team.Enemy, new Vector2Int(center - 2, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Silver), Team.Enemy, new Vector2Int(center + 2, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Kishou), Team.Enemy, new Vector2Int(center - 3, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Kishou), Team.Enemy, new Vector2Int(center + 3, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Lance), Team.Enemy, new Vector2Int(0, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Lance), Team.Enemy, new Vector2Int(size - 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Rook), Team.Enemy, new Vector2Int(center - 1, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Bishop), Team.Enemy, new Vector2Int(center + 1, top - 1));
        }
        else if (stage == 7)
        {
            // "影の軍勢" - Lances to Kagenin, one Knight to Kishou
            SpawnPawnRow(bm, size, top - 2);
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gold), Team.Enemy, new Vector2Int(center - 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gold), Team.Enemy, new Vector2Int(center + 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Silver), Team.Enemy, new Vector2Int(center - 2, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Silver), Team.Enemy, new Vector2Int(center + 2, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Kishou), Team.Enemy, new Vector2Int(center - 3, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Knight), Team.Enemy, new Vector2Int(center + 3, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Kagenin), Team.Enemy, new Vector2Int(0, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Kagenin), Team.Enemy, new Vector2Int(size - 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Rook), Team.Enemy, new Vector2Int(center - 1, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Bishop), Team.Enemy, new Vector2Int(center + 1, top - 1));
        }
        else if (stage == 8)
        {
            // "鉄壁の要塞" - Teppeki guards C3
            SpawnPawnRow(bm, size, top - 2);
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gold), Team.Enemy, new Vector2Int(center - 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gold), Team.Enemy, new Vector2Int(center + 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Silver), Team.Enemy, new Vector2Int(center - 2, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Silver), Team.Enemy, new Vector2Int(center + 2, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Kishou), Team.Enemy, new Vector2Int(center - 3, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Kishou), Team.Enemy, new Vector2Int(center + 3, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Lance), Team.Enemy, new Vector2Int(0, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Lance), Team.Enemy, new Vector2Int(size - 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Rook), Team.Enemy, new Vector2Int(center - 1, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Bishop), Team.Enemy, new Vector2Int(center + 1, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Teppeki), Team.Enemy, new Vector2Int(center, top - 1));
        }
        else if (stage == 9)
        {
            // "天狗の嵐" - Tengu + Kagenin + Teppeki
            SpawnPawnRow(bm, size, top - 2);
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gold), Team.Enemy, new Vector2Int(center - 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gold), Team.Enemy, new Vector2Int(center + 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Silver), Team.Enemy, new Vector2Int(center - 2, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Silver), Team.Enemy, new Vector2Int(center + 2, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Tengu), Team.Enemy, new Vector2Int(center - 3, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Tengu), Team.Enemy, new Vector2Int(center + 3, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Kagenin), Team.Enemy, new Vector2Int(0, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Kagenin), Team.Enemy, new Vector2Int(size - 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Rook), Team.Enemy, new Vector2Int(center - 1, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Bishop), Team.Enemy, new Vector2Int(center + 1, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Teppeki), Team.Enemy, new Vector2Int(center, top - 1));
        }
        else if (stage == 10)
        {
            // "雷帝降臨" - Raitei + full elite army
            SpawnPawnRow(bm, size, top - 2);
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gold), Team.Enemy, new Vector2Int(center - 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gold), Team.Enemy, new Vector2Int(center + 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Teppeki), Team.Enemy, new Vector2Int(center - 2, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Teppeki), Team.Enemy, new Vector2Int(center + 2, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Kishou), Team.Enemy, new Vector2Int(center - 3, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Kishou), Team.Enemy, new Vector2Int(center + 3, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Kagenin), Team.Enemy, new Vector2Int(0, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Kagenin), Team.Enemy, new Vector2Int(size - 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Raitei), Team.Enemy, new Vector2Int(center - 1, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Bishop), Team.Enemy, new Vector2Int(center + 1, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Tengu), Team.Enemy, new Vector2Int(center - 2, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Tengu), Team.Enemy, new Vector2Int(center + 2, top - 1));
        }
        else if (stage == 11)
        {
            // "風と骨の章" - Fujin + Dokuro
            SpawnPawnRow(bm, size, top - 2);
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Lance), Team.Enemy, new Vector2Int(0, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Fujin), Team.Enemy, new Vector2Int(center - 3, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Silver), Team.Enemy, new Vector2Int(center - 2, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gold), Team.Enemy, new Vector2Int(center - 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gold), Team.Enemy, new Vector2Int(center + 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Silver), Team.Enemy, new Vector2Int(center + 2, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Fujin), Team.Enemy, new Vector2Int(center + 3, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Lance), Team.Enemy, new Vector2Int(size - 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Dokuro), Team.Enemy, new Vector2Int(0, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Dokuro), Team.Enemy, new Vector2Int(1, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Rook), Team.Enemy, new Vector2Int(center - 1, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Bishop), Team.Enemy, new Vector2Int(center + 1, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Dokuro), Team.Enemy, new Vector2Int(size - 2, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Dokuro), Team.Enemy, new Vector2Int(size - 1, top - 1));
        }
        else if (stage == 12)
        {
            // "龍の目覚め" - Ryuujin + Enmashi
            SpawnPawnRow(bm, size, top - 2);
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Kagenin), Team.Enemy, new Vector2Int(0, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Kishou), Team.Enemy, new Vector2Int(center - 3, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Teppeki), Team.Enemy, new Vector2Int(center - 2, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gold), Team.Enemy, new Vector2Int(center - 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gold), Team.Enemy, new Vector2Int(center + 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Teppeki), Team.Enemy, new Vector2Int(center + 2, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Kishou), Team.Enemy, new Vector2Int(center + 3, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Kagenin), Team.Enemy, new Vector2Int(size - 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Enmashi), Team.Enemy, new Vector2Int(center - 1, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Ryuujin), Team.Enemy, new Vector2Int(center, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Bishop), Team.Enemy, new Vector2Int(center + 1, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Enmashi), Team.Enemy, new Vector2Int(center + 2, top - 1));
        }
        else if (stage == 13)
        {
            // "黄泉の門" - Yomigaeru + Gundaishou
            SpawnPawnRow(bm, size, top - 2);
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Tengu), Team.Enemy, new Vector2Int(0, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Kishou), Team.Enemy, new Vector2Int(center - 3, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gundaishou), Team.Enemy, new Vector2Int(center - 2, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gold), Team.Enemy, new Vector2Int(center - 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gold), Team.Enemy, new Vector2Int(center + 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gundaishou), Team.Enemy, new Vector2Int(center + 2, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Kishou), Team.Enemy, new Vector2Int(center + 3, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Tengu), Team.Enemy, new Vector2Int(size - 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Dokuro), Team.Enemy, new Vector2Int(0, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Yomigaeru), Team.Enemy, new Vector2Int(1, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Raitei), Team.Enemy, new Vector2Int(center, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Kagenin), Team.Enemy, new Vector2Int(center + 1, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Yomigaeru), Team.Enemy, new Vector2Int(size - 2, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Dokuro), Team.Enemy, new Vector2Int(size - 1, top - 1));
        }
        else if (stage == 14)
        {
            // "閻魔の審判" - Full gauntlet
            SpawnPawnRow(bm, size, top - 2);
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Kagenin), Team.Enemy, new Vector2Int(0, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Fujin), Team.Enemy, new Vector2Int(center - 3, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Enmashi), Team.Enemy, new Vector2Int(center - 2, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Teppeki), Team.Enemy, new Vector2Int(center - 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Teppeki), Team.Enemy, new Vector2Int(center + 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Enmashi), Team.Enemy, new Vector2Int(center + 2, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Fujin), Team.Enemy, new Vector2Int(center + 3, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Kagenin), Team.Enemy, new Vector2Int(size - 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Tengu), Team.Enemy, new Vector2Int(0, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gundaishou), Team.Enemy, new Vector2Int(1, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Ryuujin), Team.Enemy, new Vector2Int(center, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Raitei), Team.Enemy, new Vector2Int(center + 1, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gundaishou), Team.Enemy, new Vector2Int(size - 2, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Tengu), Team.Enemy, new Vector2Int(size - 1, top - 1));
        }
        else
        {
            // Stage 15: "魔王降臨" - Final Boss
            // Front row: Dokuro x8 (no pawns) with center gap
            for (int x = 0; x < size; x++)
            {
                if (x == center) continue; // leave center open
                bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Dokuro), Team.Enemy, new Vector2Int(x, top - 2));
            }
            // Back row
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Ryuujin), Team.Enemy, new Vector2Int(0, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Kishou), Team.Enemy, new Vector2Int(center - 3, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Teppeki), Team.Enemy, new Vector2Int(center - 2, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Enmashi), Team.Enemy, new Vector2Int(center - 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Enmashi), Team.Enemy, new Vector2Int(center + 1, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Teppeki), Team.Enemy, new Vector2Int(center + 2, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Kishou), Team.Enemy, new Vector2Int(center + 3, top));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Ryuujin), Team.Enemy, new Vector2Int(size - 1, top));
            // Second row
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gundaishou), Team.Enemy, new Vector2Int(0, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Fujin), Team.Enemy, new Vector2Int(1, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Maou), Team.Enemy, new Vector2Int(center, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Raitei), Team.Enemy, new Vector2Int(center + 1, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Fujin), Team.Enemy, new Vector2Int(size - 2, top - 1));
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gundaishou), Team.Enemy, new Vector2Int(size - 1, top - 1));
        }

        // Apply stage scaling to all enemy pieces
        ApplyStageScaling(bm, stage);
    }

    private void SpawnPawnRow(BoardManager bm, int size, int row)
    {
        for (int x = 0; x < size; x++)
            bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Pawn), Team.Enemy, new Vector2Int(x, row));
    }

    private void SetupStandard9x9(BoardManager bm, int size, int center, int top)
    {
        SpawnPawnRow(bm, size, top - 2);
        bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gold), Team.Enemy, new Vector2Int(center - 1, top));
        bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Gold), Team.Enemy, new Vector2Int(center + 1, top));
        bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Silver), Team.Enemy, new Vector2Int(center - 2, top));
        bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Silver), Team.Enemy, new Vector2Int(center + 2, top));
        bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Knight), Team.Enemy, new Vector2Int(center - 3, top));
        bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Knight), Team.Enemy, new Vector2Int(center + 3, top));
        bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Lance), Team.Enemy, new Vector2Int(0, top));
        bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Lance), Team.Enemy, new Vector2Int(size - 1, top));
        bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Rook), Team.Enemy, new Vector2Int(center - 1, top - 1));
        bm.SpawnPiece(bm.GetPieceDataByType(PieceType.Bishop), Team.Enemy, new Vector2Int(center + 1, top - 1));
    }

    private void ApplyStageScaling(BoardManager bm, int stage)
    {
        List<PieceInstance> enemies = bm.GetTeamPieces(Team.Enemy);
        int hpBonus = stage / 3;
        int atkBonus = stage / 5;
        foreach (var enemy in enemies)
        {
            if (enemy.data.pieceType == PieceType.C3) continue;
            enemy.currentHP += hpBonus;
            enemy.bonusATK += atkBonus;
            PieceController pc = bm.GetPieceController(enemy.boardPosition);
            if (pc != null) pc.GetRenderer().UpdateAllStats();
        }
    }

    /// <summary>プレイヤー駒にステージに応じた小規模強化を付与</summary>
    public void ApplyPlayerScaling()
    {
        BoardManager bm = BoardManager.Instance;
        List<PieceInstance> players = bm.GetTeamPieces(Team.Player);
        int hpBonus = (currentStage - 1) / 4;
        int defBonus = (currentStage - 1) / 5;
        foreach (var p in players)
        {
            // 全員にHP強化（C3含む）
            p.currentHP += hpBonus;
            // C3以外にDEF強化
            if (p.data.pieceType != PieceType.C3)
                p.bonusDEF += defBonus;
            PieceController pc = bm.GetPieceController(p.boardPosition);
            if (pc != null) pc.GetRenderer().UpdateAllStats();
        }
    }

    public void AdvanceStage() { currentStage++; }
    public bool IsLastStage() { return currentStage >= maxStages; }
}
