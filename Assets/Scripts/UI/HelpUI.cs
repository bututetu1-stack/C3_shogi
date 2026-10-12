using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 遊び方。左に項目、右にその項目の写真と短い箇条書きを出す（文字だけで分かりづらかったため）。
/// 写真は Resources/Help/ の画像（なければ文字だけ）。
/// </summary>
public static class HelpUI
{
    private class Page
    {
        public string title;
        public string image;      // Resources/Help/ の名前（null なら写真なし）
        public string caption;    // 写真の下の一言
        public string[] lines;
    }

    /// <summary>遊び方を開く。閉じたら onClose を呼ぶ。返すのは重ねた要素</summary>
    public static VisualElement Show(VisualElement root, Action onClose)
    {
        var pages = BuildPages();
        int current = 0;

        var overlay = new VisualElement();
        overlay.style.position = Position.Absolute;
        overlay.style.left = overlay.style.right = overlay.style.top = overlay.style.bottom = 0;
        overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.75f);
        overlay.style.alignItems = Align.Center;
        overlay.style.justifyContent = Justify.Center;
        overlay.pickingMode = PickingMode.Position;
        root.Add(overlay);

        var panel = UIFactory.Panel();
        panel.style.width = Length.Percent(92);
        panel.style.maxWidth = 1360;
        panel.style.height = Length.Percent(88);
        panel.style.paddingLeft = panel.style.paddingRight = 24;
        panel.style.backgroundColor = new Color(Palette.PanelBg.r, Palette.PanelBg.g, Palette.PanelBg.b, 1f);   // 後ろのタイトルを透かさない
        overlay.Add(panel);
        panel.Add(UIFactory.PanelTitle("遊び方"));

        var body = new VisualElement();
        body.style.flexDirection = FlexDirection.Row;
        body.style.flexGrow = 1;
        body.style.flexShrink = 1;
        body.style.minHeight = 0;
        panel.Add(body);

        // 左: 項目
        var menuScroll = UIFactory.Scroll();
        menuScroll.style.width = 250;
        menuScroll.style.flexShrink = 0;
        menuScroll.style.marginRight = 18;
        menuScroll.style.paddingRight = 12;
        menuScroll.style.borderRightWidth = 1;
        menuScroll.style.borderRightColor = Palette.PanelBorder;
        body.Add(menuScroll);

        // 右: 写真と説明
        var pageScroll = UIFactory.Scroll();
        pageScroll.style.flexGrow = 1;
        pageScroll.style.flexShrink = 1;
        pageScroll.style.flexBasis = 0;
        body.Add(pageScroll);

        var menuButtons = new List<Button>();
        Button prev = null, next = null;
        Action<int> open = null;
        open = index =>
        {
            current = Mathf.Clamp(index, 0, pages.Count - 1);
            for (int i = 0; i < menuButtons.Count; i++) menuButtons[i].EnableInClassList("c3-button--primary", i == current);
            pageScroll.contentContainer.Clear();
            pageScroll.scrollOffset = Vector2.zero;
            pageScroll.Add(RenderPage(pages[current]));
            prev.SetEnabled(current > 0);
            next.SetEnabled(current < pages.Count - 1);
        };
        for (int i = 0; i < pages.Count; i++)
        {
            int index = i;
            var b = UIFactory.Button(pages[i].title, () => open(index));
            b.style.marginLeft = 0;
            b.style.marginBottom = 6;
            b.style.unityTextAlign = TextAnchor.MiddleLeft;
            menuButtons.Add(b);
            menuScroll.Add(b);
        }

        // 下: 前へ・次へ・閉じる
        var foot = new VisualElement();
        foot.style.flexDirection = FlexDirection.Row;
        foot.style.justifyContent = Justify.Center;
        foot.style.marginTop = 12;
        foot.style.flexShrink = 0;
        prev = UIFactory.Button("◀ 前へ", () => open(current - 1));
        next = UIFactory.Button("次へ ▶", () => open(current + 1));
        var close = UIFactory.Button("閉じる", () =>
        {
            overlay.RemoveFromHierarchy();
            if (onClose != null) onClose();
        }, "c3-button--primary");
        foot.Add(prev);
        foot.Add(next);
        foot.Add(close);
        close.style.marginLeft = 24;
        panel.Add(foot);

        open(0);
        return overlay;
    }

    private static VisualElement RenderPage(Page page)
    {
        var box = new VisualElement();
        var title = UIFactory.Label(page.title, 30, Palette.GoldLight, "c3-mincho");
        title.style.marginBottom = 10;
        box.Add(title);

        Texture2D tex = string.IsNullOrEmpty(page.image) ? null : Resources.Load<Texture2D>("Help/" + page.image);
        var bullets = UIFactory.Bullets(page.lines, 21, Palette.Text);
        if (tex == null)
        {
            bullets.style.maxWidth = 900;
            box.Add(bullets);
            return box;
        }

        // 縦長・正方形の写真は説明と横に並べ、横長の写真は上に置く（どちらも説明が隠れない大きさに）
        float aspect = tex.width / Mathf.Max(1f, (float)tex.height);
        bool side = aspect < 1.3f;
        var figure = new VisualElement();
        figure.style.flexShrink = 0;
        figure.style.alignItems = Align.Center;
        float w = side ? Mathf.Min(440f, 520f * aspect) : Mathf.Min(680f, 300f * aspect);
        var img = new Image();
        img.image = tex;
        img.scaleMode = ScaleMode.ScaleToFit;
        img.style.width = w;
        img.style.height = w / aspect;
        img.style.borderTopWidth = img.style.borderBottomWidth = img.style.borderLeftWidth = img.style.borderRightWidth = 1;
        img.style.borderTopColor = img.style.borderBottomColor = img.style.borderLeftColor = img.style.borderRightColor = Palette.PanelBorder;
        img.style.borderTopLeftRadius = img.style.borderTopRightRadius = img.style.borderBottomLeftRadius = img.style.borderBottomRightRadius = 6;
        figure.Add(img);
        if (!string.IsNullOrEmpty(page.caption))
        {
            var cap = UIFactory.Paragraph(page.caption, 16, Palette.TextSub);
            cap.style.marginTop = 4;
            cap.style.width = w;
            figure.Add(cap);
        }

        if (side)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexStart;
            row.Add(figure);
            bullets.style.flexGrow = 1;
            bullets.style.flexShrink = 1;
            bullets.style.flexBasis = 0;
            bullets.style.marginLeft = 24;
            row.Add(bullets);
            box.Add(row);
        }
        else
        {
            figure.style.alignSelf = Align.FlexStart;
            box.Add(figure);
            bullets.style.marginTop = 12;
            bullets.style.maxWidth = 900;
            box.Add(bullets);
        }
        return box;
    }

    private static List<Page> BuildPages()
    {
        var list = new List<Page>();
        list.Add(new Page
        {
            title = "勝ち負け", image = "board", caption = "下が自分の軍、上が相手の軍。いちばん手前と奥の段の真ん中に、それぞれの「C3」がいる",
            lines = new[]
            {
                "相手の「C3」を倒せば、その局は勝ち。",
                "自分の「C3」が倒されると負け。",
                "全十五局を勝ち抜くとクリア。",
                "1局が" + GameManager.MoveLimit + "手を超えると、残った駒の強さとC3の体力で判定。",
            }
        });
        list.Add(new Page
        {
            title = "動かし方", image = "move", caption = "駒をクリックすると、動けるマスに点が出る",
            lines = new[]
            {
                "自分の駒をクリックすると、動けるマスに点が出る。点をクリックすると動く。",
                "緑の点: 安全に動けるマス。",
                "橙の点: 動けるが、相手に狙われるマス。",
                "赤い枠: 攻撃できる相手（予想ダメージ付き）。",
                "1手番に動かせるのは1枚だけ。",
            }
        });
        list.Add(new Page
        {
            title = "駒の数字と戦い", image = "stats", caption = "左肩＝攻撃、右肩＝防御、右下＝体力、左下＝★（練度）",
            lines = new[]
            {
                "ダメージ ＝ 攻撃 − 防御。体力が0になった駒は倒れる。",
                "倒しきれなかったときは、攻撃した駒はその場に留まる。",
                "「防御無視」と書かれた能力は、防御を引かずにそのまま当たる。",
                "RN のブロック（防御の下の濃い盾「+2」）は、防御とは別の使い捨ての盾。防いだぶん減り、次の自分の手番の初めに消える。",
                "相手の駒も、その駒から見て左肩＝攻撃・右肩＝防御。",
            }
        });
        list.Add(new Page
        {
            title = "成り", image = "promote", caption = "盤の奥の色のついた段が敵陣",
            lines = new[]
            {
                "敵陣（奥の2〜3段）に入るか、敵陣から出ると、駒は必ず成る。",
                "成ると数値や動き、能力が変わる（駒一覧の「成った姿を見る」で確かめられる）。",
                "SN や小錦のように、成ると退場してしまう駒もいるので注意。",
            }
        });
        list.Add(new Page
        {
            title = "仲間と強化", image = "draft", caption = "局の前に、仲間か全軍強化を1つ選ぶ",
            lines = new[]
            {
                "各局の前に、新しい仲間か全軍の強化を1つ選ぶ。仲間は次の局にも連れて行ける。",
                "カードをクリックすると、下に説明が出る（成った姿・覚醒した姿も見られる）。",
                "引き直しは1局につき1回まで。",
                "第三局からは「〇〇を鍛える」札も出る（練度が上がる）。",
            }
        });
        list.Add(new Page
        {
            title = "練度と★", image = "growth", caption = "★ごとの効果と、必要な練度",
            lines = new List<string>
            {
                "仲間にした部員は、周のあいだ練度をためて★3まで育つ。局が変わっても練度は残る。",
                "★1: " + RunRoster.StarEffectText(1),
                "★2: " + RunRoster.StarEffectText(2),
                "★3: " + RunRoster.StarEffectText(3),
                "★に必要な練度  ブロンズ " + RunRoster.ThresholdText(Rarity.Bronze) + "／レア " + RunRoster.ThresholdText(Rarity.Rare)
                    + "／激レア " + RunRoster.ThresholdText(Rarity.SuperRare) + "（激レアほど育つのが遅い）",
            }.Concat(Prefix("練度がたまる: ", RunRoster.XpSourceLines())).ToArray()
        });
        list.Add(new Page
        {
            title = "覚醒", image = "awaken", caption = "★3の部員は、局の中で活躍を重ねると覚醒する",
            lines = new[]
            {
                "★3の部員は、局の中で活躍を" + BalanceTuning.AwakenActivities + "回重ねると、その場で覚醒する（カットインが出る）。",
                "活躍: 敵を倒すこと、またはその部員の能力が決まること（次の「部員ごとの活躍」）。",
                "覚醒すると数値が上がり、能力が強くなる。どんな姿になるかは、仲間選びや駒一覧の「覚醒した姿を見る」で見られる。",
                "覚醒はその局のあいだだけ。次の局も★3のままなので、また活躍を重ねれば覚醒する。",
                "SN は覚醒しない（先代部長）。物鉄は★3になると、着任した艦娘が最初から改になる。",
                "相手の雷帝・龍神・魔王は、体力が半分を切ると第二形態になる。",
            }
        });
        list.Add(new Page { title = "部員ごとの活躍", image = null, lines = FeatLines() });
        list.Add(new Page
        {
            title = "挑発・結界", image = "taunt", caption = "挑発の輪（小錦）と、結界で能力が止まった駒（緑）",
            lines = new[]
            {
                "挑発: 小錦を攻撃できる相手は、小錦しか攻撃できない。小錦の2マス以内の相手は、小錦を狙って寄ってくる。",
                "組み止め: 小錦の隣にいる相手は、小錦の隣から離れられない。",
                "提督の潜水艦も挑発で相手を引き寄せる。相手の傀儡も挑発を持つ。",
                "結界: 相手の結界の周り" + BalanceTuning.KekkaiRange + "マスにいる駒は、自分の能力が止まり、緑っぽくなる。",
                "止まるのはその駒自身の能力だけ。ほかの駒からもらった効果（光春のお土産・僕の強化・中華など）は効いたまま。★や覚醒の数値もそのまま。",
                "結界が倒れるか、範囲から出ると、すぐ元に戻る。",
            }
        });
        list.Add(new Page
        {
            title = "提督の艦隊", image = "fleet", caption = "提督の周りに艦娘が出撃し、深海と撃ち合う",
            lines = new[]
            {
                "物鉄が敵陣に入ると提督に着任し、艦娘2隻が提督の周りから出撃する。",
                "陣形: 縦の列＝単縦陣（艦娘の攻撃+1）、横の列＝単横陣（艦娘の防御+1）、それ以外＝警戒陣（提督の受けるダメージ−1）。",
                "空母がいると制空権確保（深海から艦娘へのダメージ−1）。",
                "潜水艦は通常の攻撃を受けないが、相手の歩兵・香車の爆雷（対潜）には沈む。",
                "深海の旗艦を沈めれば作戦完了。艦娘が1隻も沈まなければS勝利で、支援艦隊が援護し、次の局の初めに基地航空隊が相手を爆撃する。",
            }
        });
        list.Add(new Page
        {
            title = "部の時間と中断", image = null,
            lines = new[]
            {
                "第二局に勝ったあとから、局の合間に「部の時間」があり、2つのできごとから1つを選べる。",
                "対局中は上の「中断」でタイトルに戻れる。タイトルの「続きから」で、その局の初めから再開できる。",
            }
        });
        return list;
    }

    private static IEnumerable<string> Prefix(string head, string[] items)
    {
        foreach (var s in items) yield return head + s;
    }

    /// <summary>部員ごとの「能力が決まる（活躍）」の一覧。敵を倒すのはどの部員も活躍</summary>
    private static string[] FeatLines()
    {
        var members = new[]
        {
            new KeyValuePair<string, PieceType>("門人", PieceType.Monin),
            new KeyValuePair<string, PieceType>("僕", PieceType.Boku),
            new KeyValuePair<string, PieceType>("ヲツ", PieceType.Wotsu),
            new KeyValuePair<string, PieceType>("なこ", PieceType.Nako),
            new KeyValuePair<string, PieceType>("李白", PieceType.Rihaku),
            new KeyValuePair<string, PieceType>("小錦", PieceType.Konishiki),
            new KeyValuePair<string, PieceType>("けい", PieceType.Kei),
            new KeyValuePair<string, PieceType>("きぷ", PieceType.Kipu),
            new KeyValuePair<string, PieceType>("ユウ", PieceType.Yuu),
            new KeyValuePair<string, PieceType>("物鉄", PieceType.Monotetsu),
            new KeyValuePair<string, PieceType>("光春", PieceType.Mitsuharu),
            new KeyValuePair<string, PieceType>("ニコ", PieceType.Niko),
            new KeyValuePair<string, PieceType>("〆鯖", PieceType.Shimesaba),
            new KeyValuePair<string, PieceType>("翡翠", PieceType.Kawasemi),
            new KeyValuePair<string, PieceType>("RN", PieceType.RN),
            new KeyValuePair<string, PieceType>("寝癖", PieceType.Negu),
            new KeyValuePair<string, PieceType>("無糖", PieceType.Mutou),
        };
        var lines = new List<string> { "敵を倒すのは、どの部員でも活躍。それに加えて:" };
        foreach (var m in members) lines.Add(m.Key + ": " + RunRoster.FeatText(m.Value));
        lines.Add("SN: 敵を倒したときだけ（覚醒はしない）");
        lines.Add("活躍で練度がたまるのは1局に" + BalanceTuning.FeatXpMaxPerStage + "回までだが、覚醒に向けた活躍は何回でも数える。");
        return lines.ToArray();
    }
}
