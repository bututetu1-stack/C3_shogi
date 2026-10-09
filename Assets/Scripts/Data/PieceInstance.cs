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

    public int ATK { get { return (isPromoted ? data.promotedATK : data.baseATK) + bonusATK; } }
    public int DEF { get { return (isPromoted ? data.promotedDEF : data.baseDEF) + bonusDEF; } }
    public int MaxHP { get { return (isPromoted ? data.promotedHP : data.baseHP) + bonusMaxHP; } }
    public string DisplayName { get { return isPromoted ? data.promotedDisplayName : data.displayName; } }
    public string FullName { get { return isPromoted ? data.promotedName : data.pieceName; } }
    public string Description { get { return isPromoted ? data.promotedDescription : data.description; } }

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

    /// <summary>この駒の移動方向（敵駒はY反転済み）。返した配列は書き換えないこと</summary>
    public MoveDirection[] GetMoveDirections()
    {
        bool usePromoted = isPromoted && data.promotedMoveDirections != null && data.promotedMoveDirections.Length > 0;
        if (usePromoted)
        {
            if (promotedDirsCache == null) promotedDirsCache = BuildDirections(data.promotedMoveDirections);
            return promotedDirsCache;
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
        if (!data.canPromote || isPromoted) return false;
        int promoteRows = BoardCell.GetPromoteRows(boardSize);
        if (team == Team.Player)
            return boardRow >= boardSize - promoteRows;
        else
            return boardRow < promoteRows;
    }
}
