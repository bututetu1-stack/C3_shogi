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
    public int bonusATK;            // 僕バフ
    public int bonusDEF;            // 僕バフ
    public int turnsNearBoku;       // 僕の近くにいたターン数

    public int ATK { get { return (isPromoted ? data.promotedATK : data.baseATK) + bonusATK; } }
    public int DEF { get { return (isPromoted ? data.promotedDEF : data.baseDEF) + bonusDEF; } }
    public int MaxHP { get { return isPromoted ? data.promotedHP : data.baseHP; } }
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

    public MoveDirection[] GetMoveDirections()
    {
        MoveDirection[] dirs = isPromoted && data.promotedMoveDirections != null && data.promotedMoveDirections.Length > 0
            ? data.promotedMoveDirections
            : data.moveDirections;

        if (team == Team.Player)
            return dirs;

        // Enemy側はY方向を反転
        var flipped = new MoveDirection[dirs.Length];
        for (int i = 0; i < dirs.Length; i++)
        {
            var md = dirs[i];
            flipped[i] = new MoveDirection(md.direction.x, -md.direction.y, md.maxDistance, md.canJump);
        }
        return flipped;
    }

    public void Promote()
    {
        if (!data.canPromote || isPromoted) return;
        isPromoted = true;
        // 成り時にHP差分を追加
        int hpDiff = data.promotedHP - data.baseHP;
        if (hpDiff > 0) currentHP += hpDiff;
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
