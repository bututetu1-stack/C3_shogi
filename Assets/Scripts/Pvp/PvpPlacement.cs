using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// 対人戦の並べ方: 対局の前に、自陣（手前の3段）の駒を並べ替える。
/// 自分の駒をクリックして選び、別のマスをクリックすると動く（駒があれば入れ替え）。並べているあいだ、相手の駒は隠す。
/// </summary>
public class PvpPlacement : MonoBehaviour
{
    private static PvpPlacement instance;
    private UIDocument uiDocument;
    private VisualElement panel;
    private Team team;
    private Action onDone;
    private Vector2Int? selected;
    private readonly System.Collections.Generic.List<BoardCell> marked = new System.Collections.Generic.List<BoardCell>();

    public static void Begin(Team team, Action onDone)
    {
        if (instance == null)
        {
            var obj = new GameObject("PvpPlacement");
            instance = obj.AddComponent<PvpPlacement>();
            instance.uiDocument = obj.AddComponent<UIDocument>();
            foreach (var d in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                if (d != instance.uiDocument && d.panelSettings != null)
                {
                    instance.uiDocument.panelSettings = d.panelSettings;
                    break;
                }
            }
            instance.uiDocument.sortingOrder = 60;
        }
        instance.Open(team, onDone);
    }

    /// <summary>自陣のマスか（C3 のマスは除く）</summary>
    private static bool InCamp(Team team, Vector2Int pos)
    {
        int size = BoardManager.Instance.CurrentBoardSize;
        int rows = BoardCell.GetPromoteRows(size);
        bool camp = team == Team.Player ? pos.y < rows : pos.y >= size - rows;
        PieceInstance p = BoardManager.Instance.GetPieceAt(pos);
        if (p != null && (p.data.pieceType == PieceType.C3 || p.team != team || AbilitySystem.IsObstacle(p.data.pieceType))) return false;
        return camp;
    }

    private void Open(Team team, Action onDone)
    {
        this.team = team;
        this.onDone = onDone;
        selected = null;
        if (GameManager.Instance != null) GameManager.Instance.SetPhase(GamePhase.Placement);
        ShowSide(team, true);
        ShowSide(team == Team.Player ? Team.Enemy : Team.Player, false);
        MarkCamp();

        VisualElement root = UIFactory.SetupRoot(uiDocument);
        root.pickingMode = PickingMode.Ignore;
        panel = UIFactory.Panel();
        panel.style.position = Position.Absolute;
        // 案内は相手の陣の側に出す（自分の自陣に重ならないように）
        if (team == Team.Player) panel.style.top = 96; else panel.style.bottom = 20;
        panel.style.left = Length.Percent(50);
        panel.style.translate = new Translate(Length.Percent(-50), 0);
        panel.style.flexDirection = FlexDirection.Row;
        panel.style.alignItems = Align.Center;
        panel.style.paddingTop = panel.style.paddingBottom = 10;
        panel.style.paddingLeft = panel.style.paddingRight = 18;
        panel.pickingMode = PickingMode.Position;
        Color c = team == Team.Player ? Palette.PlayerLight : Palette.EnemyLight;
        var text = new VisualElement();
        text.Add(UIFactory.Label(PvpMatch.SideLong(team) + "の並べ方", 24, c, "c3-mincho"));
        text.Add(UIFactory.Label("自分の駒をクリックしてから、動かしたいマスをクリック（駒があれば入れ替え）", 16, Palette.TextSub));
        panel.Add(text);
        var done = UIFactory.Button("この並びで決める", Finish, "c3-button--primary");
        done.style.marginLeft = 18;
        panel.Add(done);
        root.Add(panel);
    }

    /// <summary>その陣営の駒（C3・地形を除く）を見せる・隠す</summary>
    private static void ShowSide(Team side, bool visible)
    {
        BoardManager bm = BoardManager.Instance;
        foreach (var p in bm.GetTeamPieces(side))
        {
            if (p.data.pieceType == PieceType.C3 || AbilitySystem.IsObstacle(p.data.pieceType)) continue;
            PieceController pc = bm.GetPieceController(p.boardPosition);
            if (pc != null) pc.gameObject.SetActive(visible);
        }
    }

    private void MarkCamp()
    {
        BoardManager bm = BoardManager.Instance;
        if (HighlightManager.Instance != null) HighlightManager.Instance.ClearHighlights();
        ClearMarks();
        BoardRenderer renderer = bm.GetRenderer();
        if (renderer == null) return;
        int size = bm.CurrentBoardSize;
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                var pos = new Vector2Int(x, y);
                if (!InCamp(team, pos) || !bm.IsEmpty(pos)) continue;
                BoardCell cell = renderer.GetCell(pos);
                if (cell != null) { cell.SetMark(CellMark.Move); marked.Add(cell); }
            }
        if (selected.HasValue && HighlightManager.Instance != null) HighlightManager.Instance.ShowSelectedHighlight(selected.Value);
    }

    private void ClearMarks()
    {
        foreach (var c in marked) if (c != null) c.SetMark(CellMark.None);
        marked.Clear();
    }

    void Update()
    {
        if (panel == null || Mouse.current == null || GameManager.Instance == null || GameManager.Instance.currentPhase != GamePhase.Placement) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        Vector2 mouse = Mouse.current.position.ReadValue();
        if (OverPanel(mouse) || Camera.main == null) return;
        Vector3 world = Camera.main.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, 0));
        var pos = new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.y));
        if (!BoardManager.Instance.IsInBounds(pos)) return;
        Click(pos);
    }

    private bool OverPanel(Vector2 screen)
    {
        if (panel.panel == null) return false;
        Vector2 p = RuntimePanelUtils.ScreenToPanel(panel.panel, new Vector2(screen.x, Screen.height - screen.y));
        return panel.worldBound.Contains(p);
    }

    /// <summary>マスをクリックした（自分の駒なら選ぶ・選んだ駒があれば動かすか入れ替える）</summary>
    public void Click(Vector2Int pos)
    {
        BoardManager bm = BoardManager.Instance;
        if (!InCamp(team, pos))
        {
            selected = null;
            MarkCamp();
            return;
        }
        PieceInstance p = bm.GetPieceAt(pos);
        if (!selected.HasValue)
        {
            if (p != null && p.team == team) selected = pos;
        }
        else if (selected.Value == pos)
        {
            selected = null;
        }
        else
        {
            bm.SwapPieces(selected.Value, pos);
            if (BattleEffects.Instance != null) BattleEffects.Instance.PlayClick();
            selected = null;
        }
        MarkCamp();
    }

    private void Finish()
    {
        selected = null;
        if (HighlightManager.Instance != null) HighlightManager.Instance.ClearHighlights();
        ClearMarks();
        if (uiDocument != null && uiDocument.rootVisualElement != null) uiDocument.rootVisualElement.Clear();
        panel = null;
        // 次の人のために、いま並べた側も隠す（最後に両方見せる）
        ShowSide(team, false);
        var done = onDone;
        onDone = null;
        if (done != null) done();
    }

    /// <summary>並べ終わったら両方の駒を見せる</summary>
    public static void RevealAll()
    {
        ShowSide(Team.Player, true);
        ShowSide(Team.Enemy, true);
    }
}
