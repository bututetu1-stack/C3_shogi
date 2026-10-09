using UnityEngine;

[System.Serializable]
public struct MoveDirection
{
    public Vector2Int direction;
    public int maxDistance; // 1=1マス, 9=直線(盤面最大)
    public bool canJump;   // true=障害物を飛び越えられる(桂馬用)

    public MoveDirection(int dx, int dy, int dist, bool jump = false)
    {
        direction = new Vector2Int(dx, dy);
        maxDistance = dist;
        canJump = jump;
    }
}
