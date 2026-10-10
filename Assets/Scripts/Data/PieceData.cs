using UnityEngine;

[CreateAssetMenu(fileName = "NewPiece", menuName = "Shogi/PieceData")]
public class PieceData : ScriptableObject
{
    public PieceType pieceType;
    public string displayName;      // 表示名 (2文字: "歩兵", "飛車" 等)
    public string pieceName;        // フルネーム
    [TextArea(2, 4)]
    public string description;      // 駒の説明文
    public int baseATK;
    public int baseDEF;
    public int baseHP;
    public Rarity rarity;
    public MoveDirection[] moveDirections;

    [Header("Promotion (成り)")]
    public bool canPromote;
    public string promotedDisplayName;  // 成り後の表示名
    public string promotedName;         // 成り後のフルネーム
    [TextArea(2, 4)]
    public string promotedDescription;
    public int promotedATK;
    public int promotedDEF;
    public int promotedHP;
    public MoveDirection[] promotedMoveDirections;

    [Header("Veteran")]
    public string veteranName;                      // 作戦完了で生還したあとのフルネーム（物鉄・改）
    [TextArea(2, 4)]
    public string veteranDescription;
    public MoveDirection[] veteranMoveDirections;   // 生還したあとの動き（空なら通常の動き）

    [Header("Promotion Rarity")]
    public bool hasPromotedRarity;                  // 成り後にレアリティが変わる（物鉄→提督）
    public Rarity promotedRarity;

    [Header("Special Abilities")]
    public bool isAutoMove;                        // 手番開始時に自動移動する（門人）
    public bool isManualControllable = true;        // 手動操作可能か
    public bool excludeFromDraft;                   // 駒抽選に出現しない（中華）
    public bool isImmovable;                        // 移動不可（中華）
    public bool immuneToFlip;                       // 李白の裏返し無効（物鉄）

    [Header("Special Movement")]
    public bool losesHPOnMove;                     // 移動でHP-1（SN）
    public bool diesOnPromotion;                   // 成ると死亡（SN過労死/小錦）
    public bool isImmovableWhenPromoted;           // 成り後は移動不可（提督）

    [Header("Taunt")]
    public bool isTauntPiece;                      // 挑発：敵の攻撃を引き付ける（小錦）

    [Header("Art")]
    public Sprite portrait;                        // 立ち絵（選択画面・詳細画面に表示。未設定なら駒のアイコン）
    public Sprite promotedPortrait;                // 成った姿の立ち絵（提督など。未設定なら portrait）
}
