using UnityEngine;

[System.Serializable]
public class PieceInstance
{
    public PieceData data;
    public Team team;
    public Vector2Int boardPosition;
    public int currentHP;
    public bool isAlive;
    public bool isPromoted;
    public int linkedGroupId = -1;  // 提督・深海・艦娘のリンクグループID
    public int bonusATK;            // 僕バフ・軍将バフ・ステージ強化
    public int bonusDEF;            // 僕バフ・ステージ強化
    public int bonusMaxHP;          // ステージ強化による最大HP増加
    public int turnsNearBoku;       // 僕の近くにいたターン数
    public int gundaishouStacks;    // 軍将バフを受けた回数（上限管理用）
    public int stageBonusATK;       // bonusATK のうち局による強化の分（魔王の雷撃には足さない）
    public bool stunned;            // きぷ・へるに冷笑され、次の自分の手番は動けない
    public int fullCount;           // 中華で「満腹」になった回数（攻撃+1、上限あり）
    public int sympathyCount;       // 小錦への「同情」で攻撃が上がった回数（上限あり）
    public bool isFlagship;         // 深海の旗艦（沈めると作戦完了）
    public bool kai2;               // 艦娘の改二（改のまま敵陣を出た）
    public int fleetDamage;         // 艦娘が深海に与えたダメージ（MVP の判定）
    public int fleetDamageAll;      // 艦娘が与えたダメージの合計（深海以外も。自動プレイの集計用）
    public bool damageAnnounced;    // 艦娘の中破を知らせた
    public bool promotionSpent;     // この局ではもう成らない（作戦完了で生還した物鉄）
    public PieceInstance summoner;  // この駒を作った駒（中華を作ったヲツ。練度の活躍に数える）
    public bool awakened;           // 覚醒した（練度★3の部員が、その局で活躍を重ねた）
    public int awakenCharge;        // この局の活躍の数（覚醒まで）
    public bool isSealed;             // けいの「デバッグ完了」で能力を封じられた（その局のあいだ）
    public int bokuStacks;          // 僕に強化された量（覚醒した僕の強化の上限）
    public bool substituteUsed;     // 覚醒した僕が身代わりを使った（1局に1回）
    public bool frozen;             // 覚醒したきぷに凍らされた（冷笑のあいだ防御−1）
    public int poetStacks;          // 覚醒した李白が裏返して得た攻撃の上乗せ

    public int ATK { get { return (isPromoted ? data.promotedATK : data.baseATK) + bonusATK; } }
    public int DEF { get { return (isPromoted ? data.promotedDEF : data.baseDEF) + bonusDEF; } }
    public int MaxHP { get { return (isPromoted ? data.promotedHP : data.baseHP) + bonusMaxHP; } }
    public string DisplayName { get { return isPromoted ? data.promotedDisplayName : data.displayName; } }
    public string FullName
    {
        get
        {
            if (awakened && !string.IsNullOrEmpty(data.awakenedName)) return data.awakenedName;
            return isPromoted ? data.promotedName : (IsVeteran && !string.IsNullOrEmpty(data.veteranName) ? data.veteranName : data.pieceName);
        }
    }
    public string Description
    {
        get
        {
            if (awakened && !string.IsNullOrEmpty(data.awakenedDescription)) return data.awakenedDescription;
            return isPromoted ? data.promotedDescription : (IsVeteran && !string.IsNullOrEmpty(data.veteranDescription) ? data.veteranDescription : data.description);
        }
    }
    /// <summary>作戦完了で生還した物鉄（物鉄・改）。この局ではもう成らない</summary>
    public bool IsVeteran { get { return promotionSpent && !isPromoted; } }

    public Rarity CurrentRarity
    {
        get
        {
            if (isPromoted && data.hasPromotedRarity) return data.promotedRarity;
            return data.rarity;
        }
    }

    public PieceInstance(PieceData data, Team team, Vector2Int position)
    {
        this.data = data;
        this.team = team;
        this.boardPosition = position;
        this.currentHP = data.baseHP;
        this.isAlive = true;
        this.isPromoted = false;
    }

    // 陣営に合わせた移動方向のキャッシュ（AIの探索で毎回配列を作らないため）
    [System.NonSerialized] private MoveDirection[] baseDirsCache;
    [System.NonSerialized] private MoveDirection[] promotedDirsCache;
    [System.NonSerialized] private MoveDirection[] veteranDirsCache;
    [System.NonSerialized] private MoveDirection[] awakenedDirsCache;

    /// <summary>この駒の移動方向（敵駒はY反転済み）。返した配列は書き換えないこと</summary>
    public MoveDirection[] GetMoveDirections()
    {
        if (awakened && data.awakenedMoveDirections != null && data.awakenedMoveDirections.Length > 0)
        {
            if (awakenedDirsCache == null) awakenedDirsCache = BuildDirections(data.awakenedMoveDirections);
            return awakenedDirsCache;
        }
        bool usePromoted = isPromoted && data.promotedMoveDirections != null && data.promotedMoveDirections.Length > 0;
        if (usePromoted)
        {
            if (promotedDirsCache == null) promotedDirsCache = BuildDirections(data.promotedMoveDirections);
            return promotedDirsCache;
        }
        if (IsVeteran && data.veteranMoveDirections != null && data.veteranMoveDirections.Length > 0)
        {
            if (veteranDirsCache == null) veteranDirsCache = BuildDirections(data.veteranMoveDirections);
            return veteranDirsCache;
        }
        if (baseDirsCache == null) baseDirsCache = BuildDirections(data.moveDirections);
        return baseDirsCache;
    }

    private MoveDirection[] BuildDirections(MoveDirection[] dirs)
    {
        if (dirs == null) return new MoveDirection[0];
        if (team == Team.Player) return dirs;

        // Enemy側はY方向を反転
        var flipped = new MoveDirection[dirs.Length];
        for (int i = 0; i < dirs.Length; i++)
        {
            var md = dirs[i];
            flipped[i] = new MoveDirection(md.direction.x, -md.direction.y, md.maxDistance, md.canJump);
        }
        return flipped;
    }

    /// <summary>ステージ強化などで最大HPと現在HPを同時に増やす</summary>
    public void AddMaxHP(int amount)
    {
        if (amount <= 0) return;
        bonusMaxHP += amount;
        currentHP += amount;
    }

    /// <summary>けいの「バグ修正」で下げられるか（攻撃・防御は0未満にならず、体力は0にならない）</summary>
    public bool CanLower(StatKind stat)
    {
        switch (stat)
        {
            case StatKind.ATK: return ATK > 0;
            case StatKind.DEF: return DEF > 0;
            default: return currentHP > 1;
        }
    }

    /// <summary>数値を1下げる（その局のあいだ残る）。体力は最大と現在を両方下げ、どちらも1は残す</summary>
    public void Lower(StatKind stat)
    {
        if (!CanLower(stat)) return;
        switch (stat)
        {
            case StatKind.ATK: bonusATK--; break;
            case StatKind.DEF: bonusDEF--; break;
            default:
                currentHP--;
                if (MaxHP > 1) bonusMaxHP--;
                if (currentHP > MaxHP) currentHP = MaxHP;
                break;
        }
    }

    public void Promote()
    {
        if (!data.canPromote || isPromoted) return;
        isPromoted = true;
        // 成り時にHP差分を追加
        int hpDiff = data.promotedHP - data.baseHP;
        if (hpDiff > 0) currentHP += hpDiff;
    }

    /// <summary>成りを解除する（李白の裏返し）。成りで増えたHPを差し引く（最低1）</summary>
    public void Demote()
    {
        if (!isPromoted) return;
        isPromoted = false;
        int hpDiff = data.promotedHP - data.baseHP;
        if (hpDiff > 0) currentHP -= hpDiff;
        if (currentHP > MaxHP) currentHP = MaxHP;
        if (currentHP < 1) currentHP = 1;
    }

    public bool CanPromoteAt(int boardRow, int boardSize)
    {
        if (!data.canPromote || isPromoted || promotionSpent) return false;
        int promoteRows = BoardCell.GetPromoteRows(boardSize);
        if (team == Team.Player)
            return boardRow >= boardSize - promoteRows;
        else
            return boardRow < promoteRows;
    }
}
