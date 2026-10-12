using UnityEngine;
using UnityEngine.UIElements;

/// <summary>決着画面（勝利／敗北と戦績）</summary>
public class GameOverUI : MonoBehaviour
{
    private UIDocument uiDocument;
    private VisualElement root;
    private bool isSubscribed;

    void OnEnable() { TrySubscribe(); }

    void OnDisable()
    {
        if (isSubscribed && GameManager.Instance != null)
            GameManager.Instance.OnGameOver -= ShowGameOver;
        isSubscribed = false;
    }

    private void TrySubscribe()
    {
        if (isSubscribed || GameManager.Instance == null) return;
        GameManager.Instance.OnGameOver += ShowGameOver;
        isSubscribed = true;
    }

    void Start()
    {
        TrySubscribe();
        uiDocument = GetComponent<UIDocument>();
        if (uiDocument != null)
        {
            root = UIFactory.SetupRoot(uiDocument);
            root.style.display = DisplayStyle.None;
        }
    }

    private void ShowGameOver(Team winner)
    {
        if (root == null) return;
        if (PvpMatch.Active) { ShowPvpResult(winner); return; }

        GameManager gm = GameManager.Instance;
        StageManager sm = StageManager.Instance;
        bool won = winner == Team.Player;

        root.Clear();
        root.style.display = DisplayStyle.Flex;
        root.pickingMode = PickingMode.Position;
        root.style.alignItems = Align.Center;
        root.style.justifyContent = Justify.Center;
        root.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);

        var panel = UIFactory.Panel();
        panel.style.alignItems = Align.Center;
        // スマホの表示は文字が大きいぶん、上下を詰めて画面に収める
        panel.style.paddingTop = MobileUI.Active ? 10 : 30;
        panel.style.paddingBottom = MobileUI.Active ? 12 : 28;
        panel.style.paddingLeft = 56;
        panel.style.paddingRight = 56;
        panel.style.maxWidth = 720;
        panel.style.borderTopWidth = panel.style.borderBottomWidth = panel.style.borderLeftWidth = panel.style.borderRightWidth = 2;
        Color accent = won ? Palette.GoldLight : Palette.Enemy;
        panel.style.borderTopColor = panel.style.borderBottomColor = panel.style.borderLeftColor = panel.style.borderRightColor = accent;

        var result = UIFactory.Label(won ? "制覇" : "敗北", MobileUI.Active ? 52 : 84, accent, "c3-mincho");
        result.style.letterSpacing = 24;
        panel.Add(result);

        string sub = won
            ? "全" + UIFactory.Kanji(StageManager.MaxStages) + "局を勝ち抜きました"
            : (sm != null ? "第" + UIFactory.Kanji(sm.currentStage) + "局「" + sm.GetStageName(sm.currentStage) + "」にて敗退" : "");
        var subLabel = UIFactory.Label(sub, 20, Palette.TextSub, "c3-mincho");
        subLabel.style.marginTop = 2;
        subLabel.style.marginBottom = MobileUI.Active ? 6 : 18;
        panel.Add(subLabel);

        // 戦績
        if (gm != null)
        {
            var stats = new VisualElement();
            stats.style.flexDirection = FlexDirection.Row;
            stats.style.marginBottom = MobileUI.Active ? 4 : 16;
            stats.Add(StatBox("突破した局", gm.StagesCleared.ToString()));
            stats.Add(StatBox("撃破した駒", gm.TotalKills.ToString()));
            stats.Add(StatBox("総手数", gm.TotalMoves.ToString()));
            panel.Add(stats);

            // 連れていた仲間
            if (gm.OwnedPieces.Count > 0)
            {
                bool mobile = MobileUI.Active;
                float iconSize = mobile ? 46 : 56;
                panel.Add(UIFactory.Label("共に戦った仲間", 15, Palette.Gold, "c3-bold"));
                var team = new VisualElement();
                team.style.flexDirection = FlexDirection.Row;
                team.style.flexWrap = Wrap.Wrap;
                team.style.justifyContent = Justify.Center;
                team.style.flexShrink = 0;   // 縦に詰められて駒の下が切れないように
                if (mobile)
                {
                    // スマホの表示は画面が低いので、仲間が多いときは仲間の欄だけ縦に動かす（決着のボタンが隠れないように）
                    var scroll = UIFactory.Scroll();
                    scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                    scroll.style.width = 600;
                    scroll.style.maxHeight = 2 * MobileTeamRow + 4;   // 2段まではそのまま見える
                    scroll.style.flexShrink = 0;
                    scroll.style.marginTop = 4;
                    scroll.style.marginBottom = 6;
                    scroll.contentContainer.style.flexDirection = FlexDirection.Row;
                    scroll.contentContainer.style.flexWrap = Wrap.Wrap;
                    scroll.contentContainer.style.justifyContent = Justify.Center;
                    team = scroll.contentContainer;
                    panel.Add(scroll);
                }
                else
                {
                    team.style.marginTop = 6;
                    team.style.marginBottom = 18;
                    panel.Add(team);
                }
                foreach (var data in gm.OwnedPieces)
                {
                    // 駒の下に練度の★と、この周で倒した数（列の幅を決めて、駒の真ん中にそろえる）
                    var col = new VisualElement();
                    col.style.alignItems = Align.Center;
                    col.style.width = iconSize + 12;
                    col.style.flexShrink = 0;
                    col.style.marginBottom = 4;
                    if (mobile) col.style.height = MobileTeamRow;
                    col.Add(UIFactory.PieceIcon(data, false, iconSize));
                    RunMember m = gm.Roster.Get(data.pieceType);
                    if (m != null)
                    {
                        var stars = UIFactory.CenteredLabel(PieceSelectionUI.StarText(m.stars), mobile ? 12 : 13, Palette.GoldLight, "c3-bold");
                        var kills = UIFactory.CenteredLabel(m.kills + "体", mobile ? 11 : 12, Palette.TextSub);
                        if (mobile)
                        {
                            // スマホの表示は行を詰める（2段で収まるように）
                            stars.style.paddingTop = stars.style.paddingBottom = 0;
                            kills.style.paddingTop = kills.style.paddingBottom = 0;
                        }
                        col.Add(stars);
                        col.Add(kills);
                    }
                    team.Add(col);
                }
            }

            string bonus = "";
            if (gm.RunBonusATK > 0) bonus += "攻撃+" + gm.RunBonusATK + "  ";
            if (gm.RunBonusDEF > 0) bonus += "防御+" + gm.RunBonusDEF + "  ";
            if (gm.RunBonusHP > 0) bonus += "体力+" + gm.RunBonusHP + "  ";
            if (gm.RunBonusC3HP > 0) bonus += "C3体力+" + gm.RunBonusC3HP;
            if (bonus.Length > 0)
            {
                var b = UIFactory.Label("全軍強化: " + bonus.Trim(), 14, Palette.TextSub);
                b.style.marginBottom = 16;
                panel.Add(b);
            }
        }

        var buttons = new VisualElement();
        buttons.style.flexDirection = FlexDirection.Row;
        buttons.Add(UIFactory.Button("もう一度挑む", OnRetryClicked, "c3-button--primary", "c3-button--big"));
        buttons.Add(UIFactory.Button("タイトルへ", OnTitleClicked, "c3-button--big"));
        panel.Add(buttons);
        root.Add(panel);

        // ふわっと出す
        panel.style.opacity = 0f;
        panel.style.scale = new Scale(new Vector3(0.92f, 0.92f, 1f));
        panel.schedule.Execute(() =>
        {
            panel.style.transitionProperty = new System.Collections.Generic.List<StylePropertyName> { new StylePropertyName("opacity"), new StylePropertyName("scale") };
            panel.style.transitionDuration = new System.Collections.Generic.List<TimeValue> { new TimeValue(0.35f, TimeUnit.Second), new TimeValue(0.35f, TimeUnit.Second) };
            panel.style.opacity = 1f;
            panel.style.scale = new Scale(Vector3.one);
        }).StartingIn(30);
    }

    /// <summary>対人戦の決着（先手の勝ち・後手の勝ち・引き分け）</summary>
    private void ShowPvpResult(Team winner)
    {
        GameManager gm = GameManager.Instance;
        bool draw = gm != null && gm.PvpDraw;
        root.Clear();
        root.style.display = DisplayStyle.Flex;
        root.pickingMode = PickingMode.Position;
        root.style.alignItems = Align.Center;
        root.style.justifyContent = Justify.Center;
        root.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);

        var panel = UIFactory.Panel();
        panel.style.alignItems = Align.Center;
        // スマホの表示は文字が大きいぶん、上下を詰めて画面に収める
        panel.style.paddingTop = MobileUI.Active ? 10 : 30;
        panel.style.paddingBottom = MobileUI.Active ? 12 : 28;
        panel.style.paddingLeft = 56;
        panel.style.paddingRight = 56;
        panel.style.maxWidth = 720;
        Color accent = draw ? Palette.GoldLight : winner == Team.Player ? Palette.PlayerLight : Palette.EnemyLight;
        panel.style.borderTopWidth = panel.style.borderBottomWidth = panel.style.borderLeftWidth = panel.style.borderRightWidth = 2;
        panel.style.borderTopColor = panel.style.borderBottomColor = panel.style.borderLeftColor = panel.style.borderRightColor = accent;

        string head = draw ? "引き分け" : PvpMatch.Online ? (winner == PvpMatch.LocalTeam ? "あなたの勝ち" : "あなたの負け") : PvpMatch.SideName(winner) + "の勝ち";
        var result = UIFactory.Label(head, MobileUI.Active ? 48 : 72, accent, "c3-mincho");
        result.style.letterSpacing = 12;
        panel.Add(result);
        var sub = UIFactory.Label("対人戦（" + (PvpMatch.CurrentRule == PvpMatch.Rule.Cost ? "コスト制" : "ランダム配布") + "）・"
            + (gm != null ? gm.MoveCount + "手" : ""), 20, Palette.TextSub, "c3-mincho");
        sub.style.marginTop = 2;
        sub.style.marginBottom = 18;
        panel.Add(sub);

        // 2人の編成
        foreach (var side in new[] { PvpMatch.First, PvpMatch.Second })
        {
            panel.Add(UIFactory.Label(PvpMatch.SideLong(side.team) + "の編成", 15, side.team == Team.Player ? Palette.PlayerLight : Palette.EnemyLight, "c3-bold"));
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.justifyContent = Justify.Center;
            row.style.marginTop = 4;
            row.style.marginBottom = 12;
            foreach (var data in side.pieces)
            {
                var icon = UIFactory.PieceIcon(data, false, 44);
                icon.style.marginLeft = icon.style.marginRight = 2;
                row.Add(icon);
            }
            panel.Add(row);
        }

        var buttons = new VisualElement();
        buttons.style.flexDirection = FlexDirection.Row;
        if (!PvpMatch.Online)
        {
            buttons.Add(UIFactory.Button("もう一度（同じルール）", () =>
            {
                PvpMatch.RestartPending = true;
                GameManager.SkipTitleOnce = false;
                ReloadScene();
            }, "c3-button--primary", "c3-button--big"));
        }
        buttons.Add(UIFactory.Button("タイトルへ", () =>
        {
            PvpMatch.End();
            ViewFlip.Set(false);
            if (PvpOnline.Instance != null) PvpOnline.Instance.Leave();
            OnTitleClicked();
        }, "c3-button--big"));
        panel.Add(buttons);
        root.Add(panel);
    }

    /// <summary>スマホの表示の「共に戦った仲間」1段の高さ（駒・★・倒した数）</summary>
    private const float MobileTeamRow = 96f;

    private static VisualElement StatBox(string label, string value)
    {
        var box = new VisualElement();
        box.style.alignItems = Align.Center;
        box.style.marginLeft = 18;
        box.style.marginRight = 18;
        if (MobileUI.Active)
        {
            // スマホの表示は「突破した局 15」のように1行にして、仲間の欄の場所を空ける
            box.style.flexDirection = FlexDirection.Row;
            box.Add(UIFactory.Label(label, 14, Palette.TextSub));
            var v = UIFactory.Label(value, 24, Palette.Text, "c3-bold");
            v.style.marginLeft = 6;
            box.Add(v);
            return box;
        }
        box.Add(UIFactory.Label(value, 34, Palette.Text, "c3-bold"));
        box.Add(UIFactory.Label(label, 13, Palette.TextSub));
        return box;
    }

    private void OnRetryClicked()
    {
        GameManager.SkipTitleOnce = true;
        ReloadScene();
    }

    private void OnTitleClicked()
    {
        GameManager.SkipTitleOnce = false;
        ReloadScene();
    }

    private static void ReloadScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
}
