// ゲームの基本の流れを撮る（プレイ中に、cap_frames_setup.cs のあとに流す。サムネイル・スクリーンショット用）。
// 第六局で、自軍も AI が指す。1手ごとに駒を選んだ状態（動けるマス・予想ダメージ・駒の情報）を少し見せてから指す。
// 物鉄（艦隊）は入れない。なこ・鶏炭は練度★3（覚醒もありうる）。
// 同時にコマ撮りもする（3フレームごとに Builds/Media/core/frames に 640x360、core/square に盤を正方形に切り抜いた 512x512、10コマごとに core/stills に 1920x1080）。
// このあと play_mode_step --seconds 34 --wait-timeout 600 を1回（呼び出しをくり返すと Windows の通信の口が足りなくなるため）
var gm = GameManager.Instance; var bm = BoardManager.Instance;
var sel = UnityEngine.Object.FindFirstObjectByType<PieceSelectionUI>(); if (sel != null) sel.Hide();
var title = UnityEngine.Object.FindFirstObjectByType<TitleScreenUI>(); if (title != null) UnityEngine.Object.Destroy(title.gameObject);
var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
gm.SimBeginRun();
StageManager.Instance.currentStage = 6;
typeof(GameManager).GetMethod("SetupStageBoard", bf).Invoke(gm, null);
foreach (var t in new[] { PieceType.Nako, PieceType.Wotsu, PieceType.Boku, PieceType.Konishiki, PieceType.Kei, PieceType.Rihaku })
    gm.SimApplyOption(DraftOption.Piece(bm.GetPieceDataByType(t)));
foreach (var t in new[] { PieceType.Nako, PieceType.Kei })
{
    var m = gm.Roster.Get(t);
    if (m != null) gm.Roster.AddXp(m, 999);
}
typeof(GameManager).GetMethod("StartBattle", bf).Invoke(gm, null);

var ai = SimpleAI.Instance;
var input = InputManager.Instance;
var select = typeof(InputManager).GetMethod("SelectPiece", bf);
System.Collections.IEnumerator Play(PieceInstance pc, MoveValidator.MoveResult mv, float look)
{
    select.Invoke(input, new object[] { pc });
    yield return new WaitForSeconds(look);
    input.ClearSelection();
    yield return CombatResolver.ExecuteMove(pc, mv);
}
System.Collections.IEnumerator Loop()
{
    while (gm.currentPhase != GamePhase.Battle) yield return null;
    int turns = 0;
    while (turns < 40 && gm.currentPhase == GamePhase.Battle)
    {
        if (gm.currentTurn == Team.Player && !gm.IsTurnProcessing)
        {
            PieceInstance pc; MoveValidator.MoveResult mv;
            if (ai.TryChooseMove(Team.Player, 150f, out pc, out mv)) yield return Play(pc, mv, 0.9f);
            PieceInstance again = gm.TakeBonusMove();
            if (again != null && ai.TryChooseMove(Team.Player, 150f, out pc, out mv, again)) yield return Play(pc, mv, 0.6f);
            gm.EndTurn();
            turns++;
        }
        yield return null;
    }
}

var ps = UnityEngine.Object.FindFirstObjectByType<UnityEngine.UIElements.UIDocument>().panelSettings;
string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Builds/Media/core"));
System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dir, "frames"));
System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dir, "stills"));
System.IO.Directory.CreateDirectory(System.IO.Path.Combine(dir, "square"));
// 盤（座標の数字も入れて）を正方形に切り抜く範囲: 1920x1080 の x 455〜1415、y 25〜985（下から）
var cropScale = new Vector2(960f / 1920f, 960f / 1080f);
var cropOffset = new Vector2(455f / 1920f, 25f / 1080f);
System.Collections.IEnumerator Capture()
{
    int frame = 0, n = 0;
    var small = new Texture2D(640, 360, TextureFormat.RGB24, false);
    var square = new Texture2D(512, 512, TextureFormat.RGB24, false);
    while (n < 340)
    {
        yield return null;   // フレームの初め: RenderTexture には前のフレームの絵が残っている
        if (++frame % 3 != 0) continue;
        var rt = ps.targetTexture;
        if (rt == null) yield break;
        var prev = RenderTexture.active;
        if (n % 10 == 0)
        {
            RenderTexture.active = rt;
            var full = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            full.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            full.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "stills", "frame_" + n.ToString("D3") + ".png"), full.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(full);
        }
        var mid = RenderTexture.GetTemporary(1280, 720, 0, RenderTextureFormat.ARGB32);
        var low = RenderTexture.GetTemporary(640, 360, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(rt, mid);
        Graphics.Blit(mid, low);
        RenderTexture.active = low;
        small.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
        small.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(mid);
        RenderTexture.ReleaseTemporary(low);
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "frames", n.ToString("D3") + ".rgb"), small.GetRawTextureData());
        var crop = RenderTexture.GetTemporary(960, 960, 0, RenderTextureFormat.ARGB32);
        var sq = RenderTexture.GetTemporary(512, 512, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(rt, crop, cropScale, cropOffset);
        Graphics.Blit(crop, sq);
        RenderTexture.active = sq;
        square.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
        square.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(crop);
        RenderTexture.ReleaseTemporary(sq);
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "square", n.ToString("D3") + ".rgb"), square.GetRawTextureData());
        n++;
    }
    Debug.Log("[visual_autoplay_core] コマ撮り終わり: " + n + "コマ");
}
gm.StartCoroutine(Loop());
gm.StartCoroutine(Capture());
return dir;
