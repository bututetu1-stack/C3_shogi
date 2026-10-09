using UnityEngine;
using System.Collections;

/// <summary>戦闘の演出（効果音・光・衝撃波・パーティクル・画面揺れ）</summary>
public class BattleEffects : MonoBehaviour
{
    public static BattleEffects Instance { get; private set; }

    private const int OrderGlow = 50;
    private const int OrderParticle = 55;

    private AudioSource audioSource;

    private AudioClip moveClip;
    private AudioClip hitClip;
    private AudioClip defeatClip;
    private AudioClip battleStartClip;
    private AudioClip airRaidClip;
    private AudioClip torpedoClip;
    private AudioClip bombardmentClip;
    private AudioClip promoteClip;

    private Transform effectsRoot;
    private Coroutine cameraShakeRoutine;

    /// <summary>演出用オブジェクトの親（シーン直下に散らからないように）</summary>
    public Transform EffectsRoot
    {
        get
        {
            if (effectsRoot == null) effectsRoot = new GameObject("Effects").transform;
            return effectsRoot;
        }
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.volume = 0.5f;

        moveClip = Resources.Load<AudioClip>("Audio/Move");
        hitClip = Resources.Load<AudioClip>("Audio/Hit");
        defeatClip = Resources.Load<AudioClip>("Audio/Defeat");
        battleStartClip = Resources.Load<AudioClip>("Audio/BattleStart");
        airRaidClip = Resources.Load<AudioClip>("Audio/AirRaid");
        torpedoClip = Resources.Load<AudioClip>("Audio/Torpedo");
        bombardmentClip = Resources.Load<AudioClip>("Audio/Bombardment");
        promoteClip = Resources.Load<AudioClip>("Audio/Promote");
    }

    // 同じ効果音が同時に大量に鳴らないようにする（髑髏の連鎖爆発など）
    private readonly System.Collections.Generic.Dictionary<AudioClip, float> lastPlayed = new System.Collections.Generic.Dictionary<AudioClip, float>();
    private const float MinReplayInterval = 0.06f;

    private void Play(AudioClip clip, AudioClip fallback = null)
    {
        AudioClip c = clip != null ? clip : fallback;
        if (c == null) return;
        float now = Time.unscaledTime;
        float last;
        if (lastPlayed.TryGetValue(c, out last) && now - last < MinReplayInterval) return;
        lastPlayed[c] = now;
        audioSource.PlayOneShot(c);
    }

    private static Vector3 World(Vector2Int pos) { return new Vector3(pos.x, pos.y, 0f); }

    // ============================================================
    // 公開API
    // ============================================================

    public void PlayMoveEffect() { Play(moveClip); }

    public void PlayBattleStartEffect() { Play(battleStartClip); }

    /// <summary>被弾（撃破なし）</summary>
    public void PlayHitEffect(Vector2Int pos)
    {
        Play(hitClip);
        Vector3 p = World(pos);
        StartCoroutine(Glow(p, new Color(1f, 0.95f, 0.85f, 0.9f), 0.5f, 1.1f, 0.2f));
        StartCoroutine(RingWave(p, new Color(1f, 0.9f, 0.7f, 0.9f), 0.35f, 1.0f, 0.25f));
        StartCoroutine(Burst(p, 7, new Color(1f, 0.95f, 0.7f), new Color(1f, 0.7f, 0.3f), 2f, 4.5f, 0.10f, 0.28f, 0f));
    }

    /// <summary>撃破</summary>
    public void PlayDefeatEffect(Vector2Int pos)
    {
        Play(defeatClip);
        Vector3 p = World(pos);
        StartCoroutine(Glow(p, new Color(1f, 0.8f, 0.45f, 0.85f), 0.6f, 1.9f, 0.32f));
        StartCoroutine(RingWave(p, Palette.GoldLight, 0.4f, 1.6f, 0.35f));
        StartCoroutine(Burst(p, 12, new Color(1f, 0.75f, 0.3f), new Color(0.95f, 0.35f, 0.15f), 1.5f, 4.2f, 0.13f, 0.5f, -5f));
        StartCoroutine(Burst(p, 6, Palette.BoardWoodDark, Palette.BoardFrame, 1.2f, 3f, 0.09f, 0.55f, -7f, SpriteFactory.Pixel));
        ShakeCamera(0.12f, 0.04f);
    }

    /// <summary>髑髏の爆発</summary>
    public void PlayExplosionEffect(Vector2Int pos)
    {
        Play(bombardmentClip, defeatClip);
        Vector3 p = World(pos);
        StartCoroutine(Glow(p, new Color(1f, 0.55f, 0.15f, 0.95f), 0.8f, 3.2f, 0.4f));
        StartCoroutine(RingWave(p, new Color(1f, 0.6f, 0.2f, 1f), 0.5f, 3.2f, 0.4f));
        StartCoroutine(Burst(p, 22, new Color(1f, 0.85f, 0.3f), new Color(0.85f, 0.2f, 0.1f), 2.5f, 6.5f, 0.16f, 0.55f, -3f));
        ShakeCamera(0.25f, 0.12f);
    }

    /// <summary>成り（金色の光）</summary>
    public void PlayPromoteEffect(Vector2Int pos)
    {
        Play(promoteClip);
        Vector3 p = World(pos);
        StartCoroutine(Glow(p, new Color(1f, 0.85f, 0.4f, 0.8f), 0.6f, 1.6f, 0.45f));
        StartCoroutine(RingWave(p, Palette.GoldLight, 0.5f, 1.5f, 0.45f));
        StartCoroutine(Burst(p, 12, Palette.GoldLight, Palette.Gold, 0.8f, 2.4f, 0.09f, 0.7f, 2.5f));
    }

    /// <summary>召喚（ぽんと現れる）</summary>
    public void PlaySpawnEffect(Vector2Int pos)
    {
        Vector3 p = World(pos);
        StartCoroutine(Glow(p, new Color(1f, 1f, 1f, 0.6f), 0.4f, 1.2f, 0.25f));
    }

    // --- 艦娘の攻撃 ---

    public void PlayAirRaidEffect(Vector2Int pos)
    {
        Play(airRaidClip, defeatClip);
        StartCoroutine(AirRaidRoutine(World(pos)));
    }

    public void PlayTorpedoEffect(Vector2Int from, Vector2Int to)
    {
        Play(torpedoClip, hitClip);
        StartCoroutine(TorpedoRoutine(World(from), World(to)));
    }

    public void PlayBombardmentEffect(Vector2Int pos)
    {
        Play(bombardmentClip, defeatClip);
        StartCoroutine(BombardmentRoutine(World(pos)));
    }

    private IEnumerator AirRaidRoutine(Vector3 target)
    {
        // 上空から爆弾が降ってくる
        const int count = 12;
        var bombs = new SpriteRenderer[count];
        var offsets = new Vector3[count];
        var delays = new float[count];
        for (int i = 0; i < count; i++)
        {
            offsets[i] = new Vector3(Random.Range(-1.2f, 1.2f), 0f, 0f);
            delays[i] = Random.Range(0f, 0.35f);
            bombs[i] = CreateSprite(SpriteFactory.SoftCircle, target, new Color(1f, 0.5f, 0.2f, 0f), 0.22f, OrderParticle);
        }
        const float fall = 0.45f;
        float elapsed = 0f;
        while (elapsed < 0.8f + fall)
        {
            elapsed += Time.deltaTime;
            for (int i = 0; i < count; i++)
            {
                if (bombs[i] == null) continue;
                float t = (elapsed - delays[i]) / fall;
                if (t < 0f) continue;
                if (t >= 1f)
                {
                    Vector3 hit = target + offsets[i] * 0.6f;
                    StartCoroutine(Glow(hit, new Color(1f, 0.6f, 0.2f, 0.8f), 0.3f, 0.9f, 0.22f));
                    Destroy(bombs[i].gameObject);
                    bombs[i] = null;
                    continue;
                }
                bombs[i].transform.position = Vector3.Lerp(target + offsets[i] + new Vector3(0.4f, 3.2f, 0f), target + offsets[i] * 0.6f, Ease.InCubic(t));
                bombs[i].color = new Color(1f, 0.55f, 0.2f, Mathf.Clamp01(t * 3f));
            }
            yield return null;
        }
        for (int i = 0; i < count; i++) if (bombs[i] != null) Destroy(bombs[i].gameObject);
    }

    private IEnumerator TorpedoRoutine(Vector3 from, Vector3 to)
    {
        var head = CreateSprite(SpriteFactory.SoftCircle, from, new Color(0.7f, 0.95f, 1f, 1f), 0.4f, OrderParticle + 1);
        const float duration = 0.8f;
        float elapsed = 0f;
        float trailTimer = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            trailTimer += Time.deltaTime;
            Vector3 p = Vector3.Lerp(from, to, Ease.InOutCubic(elapsed / duration));
            head.transform.position = p;
            if (trailTimer > 0.03f)
            {
                trailTimer = 0f;
                StartCoroutine(Glow(p, new Color(0.5f, 0.85f, 1f, 0.5f), 0.25f, 0.1f, 0.35f));
            }
            yield return null;
        }
        Destroy(head.gameObject);
        StartCoroutine(Glow(to, new Color(0.7f, 0.95f, 1f, 0.9f), 0.6f, 2f, 0.3f));
        StartCoroutine(RingWave(to, new Color(0.6f, 0.9f, 1f, 1f), 0.4f, 1.8f, 0.35f));
        StartCoroutine(Burst(to, 12, new Color(0.8f, 0.97f, 1f), new Color(0.3f, 0.6f, 0.95f), 2f, 5f, 0.12f, 0.45f, -2f));
    }

    private IEnumerator BombardmentRoutine(Vector3 target)
    {
        // 着弾までの予告（赤い照準）
        var marker = CreateSprite(SpriteFactory.Ring, target, new Color(1f, 0.3f, 0.2f, 0f), 2.2f, OrderGlow);
        float elapsed = 0f;
        while (elapsed < 0.9f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.9f;
            marker.transform.localScale = Vector3.one * Mathf.Lerp(2.2f, 1.1f, Ease.OutCubic(t));
            marker.color = new Color(1f, 0.3f, 0.2f, 0.9f * Mathf.Clamp01(t * 2f));
            yield return null;
        }
        Destroy(marker.gameObject);

        StartCoroutine(Glow(target, new Color(1f, 0.6f, 0.15f, 1f), 1f, 3.6f, 0.45f));
        StartCoroutine(RingWave(target, new Color(1f, 0.65f, 0.25f, 1f), 0.6f, 3.4f, 0.45f));
        StartCoroutine(Burst(target, 26, new Color(1f, 0.85f, 0.3f), new Color(0.8f, 0.2f, 0.1f), 2.5f, 7f, 0.18f, 0.6f, -3f));
        ShakeCamera(0.3f, 0.15f);
    }

    // ============================================================
    // 画面揺れ
    // ============================================================

    public void ShakeCamera(float duration, float magnitude)
    {
        if (Camera.main == null || BoardManager.Instance == null) return;
        if (cameraShakeRoutine != null) StopCoroutine(cameraShakeRoutine);
        cameraShakeRoutine = StartCoroutine(CameraShake(duration, magnitude));
    }

    private IEnumerator CameraShake(float duration, float magnitude)
    {
        // 基準位置は毎フレーム取得する（揺れの最中に盤サイズが変わっても正しい位置に戻る）
        float elapsed = 0f;
        while (elapsed < duration && Camera.main != null)
        {
            elapsed += Time.deltaTime;
            float m = magnitude * (1f - elapsed / duration);
            Camera.main.transform.position = BoardManager.Instance.CameraHomePosition
                + new Vector3(Random.Range(-m, m), Random.Range(-m, m), 0f);
            yield return null;
        }
        if (Camera.main != null)
            Camera.main.transform.position = BoardManager.Instance.CameraHomePosition;
        cameraShakeRoutine = null;
    }

    // ============================================================
    // 演出の部品
    // ============================================================

    private SpriteRenderer CreateSprite(Sprite sprite, Vector3 pos, Color color, float size, int order)
    {
        var obj = new GameObject("Fx");
        obj.transform.SetParent(EffectsRoot, false);
        obj.transform.position = pos;
        obj.transform.localScale = new Vector3(size, size, 1f);
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    /// <summary>ふわっと広がって消える光</summary>
    private IEnumerator Glow(Vector3 pos, Color color, float startSize, float endSize, float life)
    {
        var sr = CreateSprite(SpriteFactory.SoftCircle, pos, color, startSize, OrderGlow);
        float elapsed = 0f;
        while (elapsed < life)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / life;
            sr.transform.localScale = Vector3.one * Mathf.Lerp(startSize, endSize, Ease.OutCubic(t));
            Color c = color;
            c.a = color.a * (1f - t);
            sr.color = c;
            yield return null;
        }
        Destroy(sr.gameObject);
    }

    /// <summary>衝撃波のリング</summary>
    private IEnumerator RingWave(Vector3 pos, Color color, float startSize, float endSize, float life)
    {
        var sr = CreateSprite(SpriteFactory.Ring, pos, color, startSize, OrderGlow + 1);
        float elapsed = 0f;
        while (elapsed < life)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / life;
            sr.transform.localScale = Vector3.one * Mathf.Lerp(startSize, endSize, Ease.OutCubic(t));
            Color c = color;
            c.a = color.a * (1f - t) * (1f - t);
            sr.color = c;
            yield return null;
        }
        Destroy(sr.gameObject);
    }

    /// <summary>放射状に飛び散るパーティクル（gravity: 正で上昇、負で落下）</summary>
    private IEnumerator Burst(Vector3 pos, int count, Color colorA, Color colorB, float speedMin, float speedMax,
        float size, float life, float gravity, Sprite sprite = null)
    {
        if (sprite == null) sprite = SpriteFactory.SoftCircle;
        var parts = new SpriteRenderer[count];
        var vel = new Vector3[count];
        var spin = new float[count];
        var baseColor = new Color[count];
        for (int i = 0; i < count; i++)
        {
            float ang = (360f / count) * i + Random.Range(-20f, 20f);
            float rad = ang * Mathf.Deg2Rad;
            vel[i] = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * Random.Range(speedMin, speedMax);
            spin[i] = Random.Range(-360f, 360f);
            baseColor[i] = Color.Lerp(colorA, colorB, Random.value);
            parts[i] = CreateSprite(sprite, pos, baseColor[i], size * Random.Range(0.7f, 1.3f), OrderParticle);
        }

        float elapsed = 0f;
        while (elapsed < life)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            float t = elapsed / life;
            for (int i = 0; i < count; i++)
            {
                vel[i] *= 1f - 3.5f * dt;          // 空気抵抗
                vel[i].y += gravity * dt;
                Transform tr = parts[i].transform;
                tr.position += vel[i] * dt;
                tr.Rotate(0f, 0f, spin[i] * dt);
                Color c = baseColor[i];
                c.a = 1f - t * t;
                parts[i].color = c;
            }
            yield return null;
        }
        for (int i = 0; i < count; i++) Destroy(parts[i].gameObject);
    }
}
