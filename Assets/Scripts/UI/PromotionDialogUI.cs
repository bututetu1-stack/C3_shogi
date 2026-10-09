using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>「成りますか？」の確認ダイアログ</summary>
public class PromotionDialogUI : MonoBehaviour
{
    private static PromotionDialogUI instance;
    private UIDocument uiDocument;

    /// <summary>プレイヤーに成るかどうかを尋ねる。答えが出るまで待つコルーチン</summary>
    public static IEnumerator Ask(PieceInstance piece, Action<bool> onAnswer)
    {
        PromotionDialogUI ui = Ensure();
        if (ui == null)
        {
            onAnswer(!piece.data.diesOnPromotion);
            yield break;
        }

        bool? answer = null;
        ui.Show(piece, a => answer = a);
        while (!answer.HasValue)
        {
            // 待っている間に決着・ステージ切替が起きたら成らずに終える
            if (GameManager.Instance == null || GameManager.Instance.currentPhase != GamePhase.Battle || !piece.isAlive)
                answer = false;
            yield return null;
        }
        ui.Hide();
        onAnswer(answer.Value);
    }

    private static PromotionDialogUI Ensure()
    {
        if (instance != null) return instance;
        var obj = new GameObject("PromotionDialogUI");
        instance = obj.AddComponent<PromotionDialogUI>();
        instance.uiDocument = obj.AddComponent<UIDocument>();
        UIDocument[] docs = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
        foreach (var d in docs)
        {
            if (d != instance.uiDocument && d.panelSettings != null)
            {
                instance.uiDocument.panelSettings = d.panelSettings;
                break;
            }
        }
        instance.uiDocument.sortingOrder = 70;
        return instance;
    }

    private void Show(PieceInstance piece, Action<bool> onAnswer)
    {
        VisualElement root = UIFactory.SetupRoot(uiDocument);
        root.pickingMode = PickingMode.Position;
        root.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
        root.style.alignItems = Align.Center;
        root.style.justifyContent = Justify.Center;

        PieceData d = piece.data;
        var panel = UIFactory.Panel();
        panel.style.alignItems = Align.Center;
        panel.style.paddingLeft = 36;
        panel.style.paddingRight = 36;
        panel.style.paddingTop = 24;
        panel.style.paddingBottom = 24;
        panel.style.maxWidth = 560;
        root.Add(panel);

        panel.Add(UIFactory.Label("成りますか？", 30, Palette.GoldLight, "c3-mincho"));

        // 成る前 → 成った後
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginTop = 14;
        row.style.marginBottom = 10;
        row.Add(FormColumn(d, false));
        var arrow = UIFactory.Label("→", 34, Palette.Gold, "c3-bold");
        arrow.style.marginLeft = 18;
        arrow.style.marginRight = 18;
        row.Add(arrow);
        row.Add(FormColumn(d, true));
        panel.Add(row);

        if (!string.IsNullOrEmpty(d.promotedDescription))
        {
            var desc = UIFactory.Label(KinsokuHelper.Apply(d.promotedDescription), 14, Palette.TextSub);
            desc.style.whiteSpace = WhiteSpace.Normal;
            desc.style.marginBottom = 10;
            panel.Add(desc);
        }

        if (d.diesOnPromotion)
        {
            var warn = UIFactory.Label("成ると力尽きて盤から退場します！", 16, Palette.EnemyLight, "c3-bold");
            warn.style.marginBottom = 10;
            panel.Add(warn);
        }

        var buttons = new VisualElement();
        buttons.style.flexDirection = FlexDirection.Row;
        buttons.style.marginTop = 6;
        buttons.Add(UIFactory.Button("成る", () => onAnswer(true), d.diesOnPromotion ? "c3-button--danger" : "c3-button--primary"));
        buttons.Add(UIFactory.Button("成らない", () => onAnswer(false)));
        panel.Add(buttons);
    }

    private static VisualElement FormColumn(PieceData d, bool promoted)
    {
        var col = new VisualElement();
        col.style.alignItems = Align.Center;
        col.Add(UIFactory.PieceIcon(d, promoted, 80));
        string name = promoted ? d.promotedName : d.pieceName;
        col.Add(UIFactory.Label(name, 16, Palette.Text, "c3-mincho"));
        int atk = promoted ? d.promotedATK : d.baseATK;
        int def = promoted ? d.promotedDEF : d.baseDEF;
        int hp = promoted ? d.promotedHP : d.baseHP;
        var stats = UIFactory.Label("攻" + atk + "  防" + def + "  体" + UIFactory.FormatHP(hp), 13, Palette.TextSub);
        stats.style.marginTop = 2;
        col.Add(stats);
        return col;
    }

    private void Hide()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;
        uiDocument.rootVisualElement.Clear();
        uiDocument.rootVisualElement.pickingMode = PickingMode.Ignore;
        uiDocument.rootVisualElement.style.backgroundColor = new Color(0, 0, 0, 0);
    }
}
