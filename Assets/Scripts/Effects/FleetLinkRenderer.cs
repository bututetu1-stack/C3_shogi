using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 提督と艦娘を結ぶ点線（艦隊のつながり）を描く。
/// 点が提督から艦娘へ流れ続け、「提督が沈めば艦娘も沈む」関係がひと目で分かるようにする。
/// </summary>
public class FleetLinkRenderer : MonoBehaviour
{
    private const float DotSpacing = 0.2f;
    private const float FlowSpeed = 0.5f;
    private const int SortingOrder = 8;

    private static readonly Color LinkColor = new Color(0.62f, 0.86f, 1f);

    private readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();
    private readonly List<PieceInstance> players = new List<PieceInstance>();
    private Transform root;

    void LateUpdate()
    {
        BoardManager bm = BoardManager.Instance;
        int used = 0;
        if (bm != null && bm.CurrentBoardSize > 0)
        {
            bm.GetTeamPieces(Team.Player, players);
            foreach (var teitoku in players)
            {
                if (teitoku.data.pieceType != PieceType.Monotetsu || !teitoku.isPromoted || teitoku.linkedGroupId < 0) continue;
                Vector3 a = PositionOf(bm, teitoku);
                foreach (var ship in players)
                {
                    if (!PieceTypes.IsKanmusu(ship.data.pieceType) || ship.linkedGroupId != teitoku.linkedGroupId) continue;
                    used = DrawLink(a, PositionOf(bm, ship), used);
                }
            }
        }

        for (int i = used; i < pool.Count; i++)
            if (pool[i].enabled) pool[i].enabled = false;
    }

    private int DrawLink(Vector3 a, Vector3 b, int used)
    {
        Vector3 d = b - a;
        float length = d.magnitude;
        if (length < 0.8f) return used;
        Vector3 dir = d / length;

        // 駒に重ならない範囲だけ点を打つ
        const float margin = 0.42f;
        float offset = Mathf.Repeat(Time.time * FlowSpeed, DotSpacing);
        float pulse = 0.55f + 0.25f * Mathf.Sin(Time.time * 3f);
        for (float s = margin + offset; s < length - margin; s += DotSpacing)
        {
            SpriteRenderer dot = GetDot(used++);
            dot.transform.position = a + dir * s;
            // 端に近いほど薄くする
            float edge = Mathf.Min(s - margin, length - margin - s);
            float fade = Mathf.Clamp01(edge / 0.3f);
            dot.color = new Color(LinkColor.r, LinkColor.g, LinkColor.b, pulse * fade);
            dot.enabled = true;
        }
        return used;
    }

    private static Vector3 PositionOf(BoardManager bm, PieceInstance piece)
    {
        PieceController pc = bm.GetPieceController(piece.boardPosition);
        return pc != null ? pc.transform.position : new Vector3(piece.boardPosition.x, piece.boardPosition.y, 0f);
    }

    private SpriteRenderer GetDot(int index)
    {
        if (root == null)
        {
            root = new GameObject("FleetLinks").transform;
            root.SetParent(transform, false);
        }
        while (pool.Count <= index)
        {
            var obj = new GameObject("LinkDot");
            obj.transform.SetParent(root, false);
            obj.transform.localScale = new Vector3(0.08f, 0.08f, 1f);
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Circle;
            sr.sortingOrder = SortingOrder;
            sr.enabled = false;
            pool.Add(sr);
        }
        return pool[index];
    }
}
