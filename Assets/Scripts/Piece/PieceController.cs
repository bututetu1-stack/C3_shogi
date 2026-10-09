using UnityEngine;
using System.Collections;

/// <summary>駒の動き（移動・突進・被弾・選択時の持ち上げ・成り・登場・退場）のアニメーション</summary>
public class PieceController : MonoBehaviour
{
    public const float MoveDuration = 0.2f;
    public const float LungeDuration = 0.24f;

    private static readonly Vector3 ShadowOffset = new Vector3(0.035f, -0.055f, 0f);

    private PieceInstance pieceInstance;
    private PieceRenderer pieceRenderer;

    // 盤上の基準位置（移動・スライドで更新）とそこからのずらし
    private Vector3 homePos;
    private Vector3 shakeOffset;
    private Vector3 lungeOffset;
    private float hop;            // 移動中の浮き上がり 0..1
    private float lift;           // 選択中の持ち上げ 0..1
    private float liftTarget;
    private float spawnScale = 1f;
    private float flipScale = 1f;
    private float nodY = 1f;

    // 残像（なこの突撃・門人の突進）
    private Color trailTint;
    private float trailUntil;
    private Vector3 lastTrailPos;

    private Coroutine moveRoutine;
    private bool isShaking;
    private bool isDying;
    private float bobPhase;

    /// <summary>撃破以外で盤を去るときの演出</summary>
    public enum ExitStyle
    {
        Retreat,  // 帰投（自陣側へ去っていく）
        Sink,     // 沈没（泡を出して沈む）
        Eaten     // 食べられた（中華。湯気とともにふわっと消える）
    }

    public void Init(PieceInstance piece)
    {
        pieceInstance = piece;
        pieceRenderer = GetComponent<PieceRenderer>();
        homePos = transform.position;
        bobPhase = Random.value * Mathf.PI * 2f;
        StartCoroutine(SpawnRoutine());
    }

    public PieceInstance GetPiece() { return pieceInstance; }
    public PieceRenderer GetRenderer() { return pieceRenderer; }

    void LateUpdate()
    {
        if (isDying || pieceRenderer == null) return;

        lift = Mathf.MoveTowards(lift, liftTarget, Time.deltaTime * 8f);
        float up = Mathf.Max(lift, hop);

        transform.position = homePos + shakeOffset + lungeOffset;

        float s = pieceRenderer.BaseScale * spawnScale * (1f + 0.08f * up);
        pieceRenderer.Visual.localScale = new Vector3(s * flipScale, s * nodY, 1f);
        // 海の駒（艦娘・深海）は波に揺られる
        float bob = pieceRenderer.IsFloating ? Mathf.Sin(Time.time * 2.1f + bobPhase) * 0.03f : 0f;
        pieceRenderer.Visual.localPosition = new Vector3(0f, 0.04f * up + bob, 0f);
        if (pieceRenderer.IsFloating)
            pieceRenderer.Visual.localRotation = Quaternion.Euler(0f, 0f, (pieceInstance.team == Team.Enemy ? 180f : 0f) + Mathf.Sin(Time.time * 1.7f + bobPhase) * 2.5f);
        pieceRenderer.ShadowHolder.localScale = new Vector3(s * flipScale, s, 1f);
        pieceRenderer.ShadowHolder.localPosition = ShadowOffset * (1f + 1.6f * up);
        pieceRenderer.Stats.localScale = Vector3.one * spawnScale;

        if (Time.time < trailUntil && BattleEffects.Instance != null && (transform.position - lastTrailPos).sqrMagnitude > 0.12f * 0.12f)
        {
            lastTrailPos = transform.position;
            BattleEffects.Instance.PlayAfterimage(pieceRenderer.Body, trailTint);
        }
    }

    /// <summary>しばらくのあいだ、動いた跡に残像を残す</summary>
    public void SetTrail(Color tint, float seconds)
    {
        trailTint = tint;
        trailUntil = Time.time + seconds;
        lastTrailPos = transform.position;
    }

    /// <summary>うんうんと頷く（僕）</summary>
    public void Nod()
    {
        if (!isDying) StartCoroutine(NodRoutine());
    }

    private IEnumerator NodRoutine()
    {
        const float duration = 0.55f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            nodY = 1f - 0.1f * Mathf.Abs(Mathf.Sin(elapsed / duration * Mathf.PI * 2f));
            yield return null;
        }
        nodY = 1f;
    }

    /// <summary>origin から今の位置へ放物線を描いて飛んでくる（ヲツが作った中華）</summary>
    public void FlyFrom(Vector3 origin, float duration)
    {
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(FlyRoutine(origin, homePos, duration));
    }

    private IEnumerator FlyRoutine(Vector3 from, Vector3 target, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            homePos = Vector3.Lerp(from, target, t) + new Vector3(0f, Ease.Arc(t) * 0.8f, 0f);
            hop = Ease.Arc(t);
            yield return null;
        }
        homePos = target;
        hop = 0f;
        moveRoutine = null;
    }

    // ------------------------------------------------------------
    // 移動
    // ------------------------------------------------------------

    /// <summary>盤上の位置を更新し、そこまで滑らかに移動する</summary>
    public void MoveTo(Vector2Int newPos)
    {
        pieceInstance.boardPosition = newPos;
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(MoveRoutine(new Vector3(newPos.x, newPos.y, 0f), MoveDuration));
    }

    /// <summary>アニメーションなしで位置を合わせる</summary>
    public void SnapTo(Vector2Int newPos)
    {
        pieceInstance.boardPosition = newPos;
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        homePos = new Vector3(newPos.x, newPos.y, 0f);
        hop = 0f;
    }

    /// <summary>見た目だけ指定位置まで滑らせる（盤面データは呼び出し側で更新する）</summary>
    public IEnumerator SlideToCoroutine(Vector2Int newPos, float duration)
    {
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        yield return MoveRoutine(new Vector3(newPos.x, newPos.y, 0f), duration);
    }

    private IEnumerator MoveRoutine(Vector3 target, float duration)
    {
        Vector3 start = homePos;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            homePos = Vector3.LerpUnclamped(start, target, Ease.OutCubic(t));
            hop = Ease.Arc(t);
            yield return null;
        }
        homePos = target;
        hop = 0f;
        moveRoutine = null;
    }

    /// <summary>攻撃の突進（相手の方向へ踏み込んで戻る）</summary>
    public void Lunge(Vector2Int toward)
    {
        if (isDying) return;
        StartCoroutine(LungeRoutine(new Vector3(toward.x, toward.y, 0f)));
    }

    private IEnumerator LungeRoutine(Vector3 target)
    {
        Vector3 dir = (target - homePos);
        dir.z = 0f;
        if (dir.sqrMagnitude > 0.0001f) dir.Normalize();
        float elapsed = 0f;
        while (elapsed < LungeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / LungeDuration;
            // 素早く踏み込み、ゆっくり戻る
            float k = t < 0.4f ? Ease.OutCubic(t / 0.4f) : 1f - Ease.InOutCubic((t - 0.4f) / 0.6f);
            lungeOffset = dir * 0.32f * k;
            yield return null;
        }
        lungeOffset = Vector3.zero;
    }

    // ------------------------------------------------------------
    // 被弾・選択・成り
    // ------------------------------------------------------------

    public void UpdateHP()
    {
        if (pieceRenderer != null) pieceRenderer.UpdateHP();
    }

    public void Shake()
    {
        if (isDying) return;
        if (!isShaking) StartCoroutine(ShakeRoutine(0.3f, 0.07f));
    }

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        isShaking = true;
        SpriteRenderer body = pieceRenderer.Body;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float m = magnitude * (1f - t);
            shakeOffset = new Vector3(Random.Range(-m, m), Random.Range(-m, m), 0f);
            if (body != null) body.color = Color.Lerp(new Color(1f, 0.55f, 0.5f), Color.white, Ease.OutCubic(t));
            yield return null;
        }
        shakeOffset = Vector3.zero;
        if (body != null) body.color = Color.white;
        isShaking = false;
    }

    /// <summary>選択中は少し持ち上げる</summary>
    public void SetLifted(bool lifted)
    {
        liftTarget = lifted ? 1f : 0f;
    }

    /// <summary>成り（駒を裏返す）演出。見た目の更新は裏返った瞬間に行う</summary>
    public void PlayPromote()
    {
        if (isDying) return;
        StartCoroutine(FlipRoutine());
    }

    private IEnumerator FlipRoutine()
    {
        const float half = 0.12f;
        float elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            flipScale = 1f - Ease.InCubic(elapsed / half);
            yield return null;
        }
        if (pieceRenderer != null) pieceRenderer.UpdateAllStats();
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            flipScale = Ease.OutBack(elapsed / half);
            yield return null;
        }
        flipScale = 1f;
    }

    // ------------------------------------------------------------
    // ワープ（提督の潜航と浮上）
    // ------------------------------------------------------------

    public IEnumerator WarpOutRoutine()
    {
        const float duration = 0.28f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            spawnScale = 1f - Ease.InCubic(elapsed / duration);
            yield return null;
        }
        spawnScale = 0f;
    }

    public IEnumerator WarpInRoutine()
    {
        const float duration = 0.32f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            spawnScale = Mathf.LerpUnclamped(0f, 1f, Ease.OutBack(elapsed / duration));
            yield return null;
        }
        spawnScale = 1f;
    }

    // ------------------------------------------------------------
    // 登場・退場
    // ------------------------------------------------------------

    /// <summary>撃破以外で盤を去る（帰投・沈没）。演出のあと破棄する</summary>
    public void PlayExit(ExitStyle style)
    {
        pieceInstance.isAlive = false;
        if (isDying) return;
        isDying = true;
        StopAllCoroutines();
        if (!gameObject.activeInHierarchy) { Destroy(gameObject); return; }
        StartCoroutine(ExitRoutine(style));
    }

    private IEnumerator ExitRoutine(ExitStyle style)
    {
        if (pieceRenderer != null && pieceRenderer.Stats != null)
            pieceRenderer.Stats.gameObject.SetActive(false);

        var renderers = GetComponentsInChildren<SpriteRenderer>();
        var texts = GetComponentsInChildren<TMPro.TextMeshPro>();
        var baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].color;

        Vector3 start = transform.position;
        float duration = style == ExitStyle.Retreat ? 0.9f : (style == ExitStyle.Eaten ? 0.5f : 0.8f);
        float elapsed = 0f;
        float wakeTimer = 0f;
        while (elapsed < duration)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            wakeTimer += dt;
            float t = elapsed / duration;
            float alpha = 1f - Ease.InCubic(t);

            if (style == ExitStyle.Retreat)
            {
                // 自陣の方へ滑るように去り、航跡を残す
                transform.position = start + new Vector3(0f, -0.9f * Ease.InCubic(t), 0f);
                if (wakeTimer > 0.06f && BattleEffects.Instance != null)
                {
                    wakeTimer = 0f;
                    BattleEffects.Instance.PlayWakePuff(transform.position + new Vector3(0f, 0.25f, 0f));
                }
            }
            else if (style == ExitStyle.Eaten)
            {
                // ふわっと浮いて小さくなる
                transform.position = start + new Vector3(0f, 0.25f * Ease.OutCubic(t), 0f);
                if (pieceRenderer != null) pieceRenderer.Visual.localScale = Vector3.one * pieceRenderer.BaseScale * (1f - 0.5f * t);
            }
            else
            {
                // 傾きながら沈み、泡が上がる
                transform.position = start + new Vector3(0f, -0.15f * t, 0f);
                transform.rotation = Quaternion.Euler(0f, 0f, 18f * t);
                if (pieceRenderer != null) pieceRenderer.Visual.localScale = Vector3.one * pieceRenderer.BaseScale * (1f - 0.35f * t);
                if (wakeTimer > 0.08f && BattleEffects.Instance != null)
                {
                    wakeTimer = 0f;
                    BattleEffects.Instance.PlayBubble(transform.position + new Vector3(Random.Range(-0.25f, 0.25f), 0.1f, 0f));
                }
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color c = baseColors[i];
                c.a *= alpha;
                renderers[i].color = c;
            }
            foreach (var tx in texts) if (tx != null) tx.alpha = alpha;
            yield return null;
        }
        Destroy(gameObject);
    }

    private IEnumerator SpawnRoutine()
    {
        const float duration = 0.22f;
        float elapsed = 0f;
        spawnScale = 0.4f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            spawnScale = Mathf.LerpUnclamped(0.4f, 1f, Ease.OutBack(elapsed / duration));
            yield return null;
        }
        spawnScale = 1f;
    }

    /// <summary>盤から取り除く（animate=trueなら縮んで消える演出のあと破棄）</summary>
    public void DestroyPiece(bool animate = true)
    {
        pieceInstance.isAlive = false;
        if (isDying) return;
        isDying = true;
        StopAllCoroutines();
        if (!animate || !gameObject.activeInHierarchy) { Destroy(gameObject); return; }
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        if (pieceRenderer != null && pieceRenderer.Stats != null)
            pieceRenderer.Stats.gameObject.SetActive(false);

        Vector3 start = transform.position;
        Transform visual = pieceRenderer != null ? pieceRenderer.Visual : null;
        Transform shadow = pieceRenderer != null ? pieceRenderer.ShadowHolder : null;
        Vector3 visualScale = visual != null ? visual.localScale : Vector3.one;
        Quaternion visualRot = visual != null ? visual.localRotation : Quaternion.identity;
        float spin = Random.Range(-40f, 40f);

        const float duration = 0.28f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float k = 1f - Ease.InCubic(t);
            transform.position = start + new Vector3(0f, -0.08f * t, 0f);
            if (visual != null)
            {
                visual.localScale = visualScale * k;
                visual.localRotation = visualRot * Quaternion.Euler(0, 0, spin * t);
            }
            if (shadow != null) shadow.localScale = visualScale * k;
            yield return null;
        }
        Destroy(gameObject);
    }
}
