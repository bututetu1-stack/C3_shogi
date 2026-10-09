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
    private AudioClip clickClip;
    private AudioClip stageClearClip;
    private AudioSource bgmSource;
    private string currentBgm;

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
        clickClip = Resources.Load<AudioClip>("Audio/Click");
        stageClearClip = Resources.Load<AudioClip>("Audio/StageClear");

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
        bgmSource.volume = 0.35f;
    }

    /// <summary>BGMを流す（Resources/Audio/BGM_名前 があれば）。同じ曲なら何もしない</summary>
    public void PlayBGM(string name)
    {
        if (currentBgm == name) return;
        currentBgm = name;
        AudioClip clip = Resources.Load<AudioClip>("Audio/BGM_" + name);
        if (clip == null) { bgmSource.Stop(); return; }
        bgmSource.clip = clip;
        bgmSource.Play();
    }

    public void PlayClick() { Play(clickClip); }

    /// <summary>ステージクリア（効果音がなければ対局開始の音で代用）</summary>
    public void PlayStageClearEffect() { Play(stageClearClip, battleStartClip); }

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

    // ============================================================
    // 提督の艦隊
    // ============================================================

    public const float AirRaidImpactTime = 1.0f;
    public const float TorpedoImpactTime = 0.85f;
    public const float BombardmentImpactTime = 0.8f;

    private static readonly Color Sea = new Color(0.62f, 0.86f, 1f);
    private static readonly Color SeaDeep = new Color(0.22f, 0.5f, 0.85f);
    private static readonly Color AbyssDark = new Color(0.16f, 0.08f, 0.24f);
    private static readonly Color AbyssTeal = new Color(0.25f, 0.85f, 0.78f);

    private AudioClip Clip(string name)
    {
        return Resources.Load<AudioClip>("Audio/" + name);
    }

    public void PlayTeitokuFanfare() { Play(Clip("Teitoku"), battleStartClip); }
    public void PlayAbyssRumble() { Play(Clip("AbyssRise")); }
    public void PlayRetreatHorn() { Play(Clip("Retreat"), battleStartClip); }

    /// <summary>潜航（青い波紋と泡）</summary>
    public void PlayDiveEffect(Vector2Int pos)
    {
        Play(Clip("Splash"), moveClip);
        Vector3 p = World(pos);
        StartCoroutine(RingWave(p, Sea, 0.3f, 1.4f, 0.45f));
        StartCoroutine(RingWave(p, SeaDeep, 0.2f, 1.0f, 0.55f));
        StartCoroutine(Burst(p, 10, Color.white, Sea, 0.6f, 1.6f, 0.08f, 0.6f, 2.5f, SpriteFactory.Ring));
    }

    /// <summary>浮上（水しぶき）</summary>
    public void PlaySurfaceEffect(Vector2Int pos)
    {
        Play(Clip("Splash"), moveClip);
        Vector3 p = World(pos);
        StartCoroutine(Glow(p, new Color(Sea.r, Sea.g, Sea.b, 0.8f), 0.4f, 1.6f, 0.35f));
        StartCoroutine(RingWave(p, Color.white, 0.3f, 1.5f, 0.4f));
        StartCoroutine(WaterColumn(p, 16, 1f));
    }

    /// <summary>深海の浮上（暗い渦が集まる）</summary>
    public void PlayAbyssRiseEffect(Vector2Int pos)
    {
        Vector3 p = World(pos);
        StartCoroutine(Glow(p, new Color(AbyssDark.r, AbyssDark.g, AbyssDark.b, 0.95f), 1.6f, 0.6f, 0.45f));
        StartCoroutine(RingWave(p, AbyssTeal, 1.6f, 0.2f, 0.45f));
        StartCoroutine(Implode(p, 12, AbyssTeal, AbyssDark, 1.1f, 0.4f));
        FloatingText.Spawn(pos, "浮上", AbyssTeal, 3.2f, 0.3f);
    }

    /// <summary>艦娘の出撃（白い水しぶきと航跡）</summary>
    public void PlaySortieEffect(Vector2Int pos)
    {
        Play(Clip("Sortie"), battleStartClip);
        Vector3 p = World(pos);
        StartCoroutine(Glow(p, new Color(1f, 1f, 1f, 0.8f), 0.4f, 1.5f, 0.3f));
        StartCoroutine(RingWave(p, Sea, 0.3f, 1.3f, 0.4f));
        StartCoroutine(WaterColumn(p, 12, 0.8f));
    }

    /// <summary>深海の攻撃（暗い触手が伸びて引きずり込む）</summary>
    public void PlayAbyssStrike(Vector2Int from, Vector2Int to)
    {
        Play(Clip("AbyssStrike"), hitClip);
        StartCoroutine(AbyssStrikeRoutine(World(from), World(to)));
    }

    /// <summary>帰投する駒の航跡</summary>
    public void PlayWakePuff(Vector3 worldPos)
    {
        StartCoroutine(Glow(worldPos, new Color(1f, 1f, 1f, 0.5f), 0.25f, 0.6f, 0.5f));
    }

    /// <summary>沈む駒の泡</summary>
    public void PlayBubble(Vector3 worldPos)
    {
        StartCoroutine(Bubble(worldPos));
    }

    // --- 艦娘の攻撃 ---

    public void PlayAirRaidEffect(Vector2Int from, Vector2Int to)
    {
        Play(Clip("Plane"), airRaidClip);
        StartCoroutine(AirRaidRoutine(World(from), World(to)));
    }

    public void PlayTorpedoEffect(Vector2Int from, Vector2Int to)
    {
        Play(torpedoClip, hitClip);
        StartCoroutine(TorpedoRoutine(World(from), World(to)));
    }

    public void PlayBombardmentEffect(Vector2Int from, Vector2Int to)
    {
        Play(bombardmentClip, defeatClip);
        StartCoroutine(BombardmentRoutine(World(from), World(to)));
    }

    /// <summary>空爆: 艦載機3機が編隊で飛来し、目標の上で爆弾を落とす</summary>
    private IEnumerator AirRaidRoutine(Vector3 from, Vector3 target)
    {
        Vector3 dir = target - from;
        if (dir.sqrMagnitude < 0.01f) dir = Vector3.up;
        dir.Normalize();
        Vector3 side = new Vector3(-dir.y, dir.x, 0f);
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;

        Vector3[] offsets = { Vector3.zero, -dir * 0.45f + side * 0.45f, -dir * 0.45f - side * 0.45f };
        var planes = new SpriteRenderer[3];
        var shadows = new SpriteRenderer[3];
        for (int i = 0; i < 3; i++)
        {
            shadows[i] = CreateSprite(SpriteFactory.Plane, from, new Color(0f, 0f, 0f, 0.3f), 0.42f, OrderGlow);
            planes[i] = CreateSprite(SpriteFactory.Plane, from, new Color(0.9f, 0.95f, 1f, 1f), 0.42f, OrderParticle + 2);
            planes[i].transform.rotation = Quaternion.Euler(0, 0, angle);
            shadows[i].transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        Vector3 start = from - dir * 1.2f;
        Vector3 end = target + dir * 3.2f;
        const float flight = 1.4f;
        float dropAt = 0.55f;          // 目標の真上に来る割合
        bool dropped = false;
        float elapsed = 0f;
        while (elapsed < flight)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flight;
            Vector3 lead = Vector3.Lerp(start, end, t);
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = lead + offsets[i];
                planes[i].transform.position = p + new Vector3(0f, 0.35f, 0f);   // 高度
                shadows[i].transform.position = p + new Vector3(0.25f, -0.2f, 0f);
                float a = t < 0.1f ? t / 0.1f : (t > 0.85f ? (1f - t) / 0.15f : 1f);
                planes[i].color = new Color(0.9f, 0.95f, 1f, a);
                shadows[i].color = new Color(0f, 0f, 0f, 0.3f * a);
            }
            if (!dropped && t >= dropAt * 0.75f)
            {
                dropped = true;
                for (int i = 0; i < 4; i++)
                {
                    Vector3 hit = target + new Vector3(Random.Range(-0.45f, 0.45f), Random.Range(-0.45f, 0.45f), 0f);
                    StartCoroutine(FallingBomb(lead + new Vector3(0f, 0.35f, 0f), hit, 0.3f + i * 0.05f));
                }
            }
            yield return null;
        }
        for (int i = 0; i < 3; i++) { Destroy(planes[i].gameObject); Destroy(shadows[i].gameObject); }
    }

    private IEnumerator FallingBomb(Vector3 from, Vector3 hit, float duration)
    {
        var bomb = CreateSprite(SpriteFactory.Circle, from, new Color(0.2f, 0.2f, 0.25f, 1f), 0.16f, OrderParticle + 1);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Ease.InCubic(elapsed / duration);
            bomb.transform.position = Vector3.Lerp(from, hit, t);
            bomb.transform.localScale = Vector3.one * Mathf.Lerp(0.16f, 0.07f, t);
            yield return null;
        }
        Destroy(bomb.gameObject);
        StartCoroutine(Glow(hit, new Color(1f, 0.65f, 0.25f, 0.95f), 0.3f, 1.1f, 0.28f));
        StartCoroutine(Burst(hit, 8, new Color(1f, 0.85f, 0.4f), new Color(0.9f, 0.3f, 0.1f), 1.5f, 3.5f, 0.1f, 0.35f, -2f));
        ShakeCamera(0.08f, 0.04f);
    }

    /// <summary>雷撃: 白い航跡を引いて魚雷が走り、水柱が上がる</summary>
    private IEnumerator TorpedoRoutine(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        var head = CreateSprite(SpriteFactory.SoftCircle, from, new Color(0.85f, 1f, 1f, 1f), 0.3f, OrderParticle + 1);
        head.transform.rotation = Quaternion.Euler(0, 0, angle);
        head.transform.localScale = new Vector3(0.5f, 0.2f, 1f);

        const float duration = 0.8f;
        float elapsed = 0f;
        float trailTimer = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            trailTimer += Time.deltaTime;
            Vector3 p = Vector3.Lerp(from, to, Ease.InOutCubic(elapsed / duration));
            head.transform.position = p;
            if (trailTimer > 0.025f)
            {
                trailTimer = 0f;
                StartCoroutine(WakeStreak(p, angle));
            }
            yield return null;
        }
        Destroy(head.gameObject);
        StartCoroutine(Glow(to, new Color(Sea.r, Sea.g, Sea.b, 0.9f), 0.6f, 2f, 0.3f));
        StartCoroutine(RingWave(to, Color.white, 0.4f, 1.8f, 0.35f));
        StartCoroutine(WaterColumn(to, 22, 1.4f));
        ShakeCamera(0.15f, 0.07f);
    }

    private IEnumerator WakeStreak(Vector3 pos, float angle)
    {
        var sr = CreateSprite(SpriteFactory.SoftCircle, pos, new Color(1f, 1f, 1f, 0.6f), 0.3f, OrderGlow);
        sr.transform.rotation = Quaternion.Euler(0, 0, angle);
        const float life = 0.6f;
        float elapsed = 0f;
        while (elapsed < life)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / life;
            sr.transform.localScale = new Vector3(0.45f + 0.3f * t, 0.16f + 0.2f * t, 1f);
            sr.color = new Color(0.85f, 0.97f, 1f, 0.55f * (1f - t));
            yield return null;
        }
        Destroy(sr.gameObject);
    }

    /// <summary>砲撃: 発砲の閃光から砲弾が弧を描き、着弾で大爆発</summary>
    private IEnumerator BombardmentRoutine(Vector3 from, Vector3 target)
    {
        // 着弾予告
        var marker = CreateSprite(SpriteFactory.Ring, target, new Color(1f, 0.3f, 0.2f, 0f), 1.8f, OrderGlow);

        // 発砲
        StartCoroutine(Glow(from, new Color(1f, 0.8f, 0.4f, 1f), 0.4f, 1.3f, 0.22f));
        StartCoroutine(Burst(from, 6, new Color(0.6f, 0.6f, 0.62f), new Color(0.35f, 0.35f, 0.4f), 0.8f, 1.6f, 0.22f, 0.7f, 0.8f));

        var shell = CreateSprite(SpriteFactory.SoftCircle, from, new Color(1f, 0.9f, 0.6f, 1f), 0.22f, OrderParticle + 2);
        const float flight = 0.7f;
        float elapsed = 0f;
        float trailTimer = 0f;
        while (elapsed < flight)
        {
            elapsed += Time.deltaTime;
            trailTimer += Time.deltaTime;
            float t = elapsed / flight;
            Vector3 p = Vector3.Lerp(from, target, t) + new Vector3(0f, Mathf.Sin(t * Mathf.PI) * 1.4f, 0f);
            shell.transform.position = p;
            shell.transform.localScale = Vector3.one * (0.2f + Mathf.Sin(t * Mathf.PI) * 0.12f);
            marker.transform.localScale = Vector3.one * Mathf.Lerp(1.8f, 1.0f, t);
            marker.color = new Color(1f, 0.3f, 0.2f, 0.9f * Mathf.Clamp01(t * 2f));
            if (trailTimer > 0.03f)
            {
                trailTimer = 0f;
                StartCoroutine(Glow(p, new Color(1f, 0.75f, 0.4f, 0.5f), 0.14f, 0.05f, 0.3f));
            }
            yield return null;
        }
        Destroy(shell.gameObject);
        Destroy(marker.gameObject);

        StartCoroutine(Glow(target, new Color(1f, 0.6f, 0.15f, 1f), 1f, 3.6f, 0.45f));
        StartCoroutine(RingWave(target, new Color(1f, 0.65f, 0.25f, 1f), 0.6f, 3.4f, 0.45f));
        StartCoroutine(Burst(target, 26, new Color(1f, 0.85f, 0.3f), new Color(0.8f, 0.2f, 0.1f), 2.5f, 7f, 0.18f, 0.6f, -3f));
        StartCoroutine(WaterColumn(target, 14, 1.1f));
        ShakeCamera(0.3f, 0.15f);
    }

    private IEnumerator AbyssStrikeRoutine(Vector3 from, Vector3 to)
    {
        // 暗い触手がうねりながら伸びる
        Vector3 dir = to - from;
        Vector3 side = new Vector3(-dir.y, dir.x, 0f).normalized;
        const int segments = 9;
        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector3 p = Vector3.Lerp(from, to, t) + side * Mathf.Sin(t * Mathf.PI * 2f) * 0.18f;
            StartCoroutine(Glow(p, new Color(AbyssDark.r, AbyssDark.g, AbyssDark.b, 0.9f), 0.32f, 0.12f, 0.5f));
            StartCoroutine(Glow(p, new Color(AbyssTeal.r, AbyssTeal.g, AbyssTeal.b, 0.4f), 0.16f, 0.05f, 0.4f));
            yield return new WaitForSeconds(0.025f);
        }
        StartCoroutine(Glow(to, new Color(AbyssDark.r, AbyssDark.g, AbyssDark.b, 0.95f), 0.5f, 1.5f, 0.4f));
        StartCoroutine(RingWave(to, AbyssTeal, 0.3f, 1.2f, 0.4f));
        StartCoroutine(Burst(to, 10, AbyssTeal, AbyssDark, 1f, 2.5f, 0.12f, 0.45f, -2f));
    }

    /// <summary>水柱（上に吹き上がって落ちる水しぶき）</summary>
    private IEnumerator WaterColumn(Vector3 pos, int count, float height)
    {
        var parts = new SpriteRenderer[count];
        var vel = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            vel[i] = new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(2.5f, 5f) * height, 0f);
            Color c = Color.Lerp(Color.white, Sea, Random.value);
            parts[i] = CreateSprite(SpriteFactory.SoftCircle, pos, c, Random.Range(0.12f, 0.24f), OrderParticle);
        }
        const float life = 0.75f;
        float elapsed = 0f;
        while (elapsed < life)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            float t = elapsed / life;
            for (int i = 0; i < count; i++)
            {
                vel[i].y -= 11f * dt;
                parts[i].transform.position += vel[i] * dt;
                Color c = parts[i].color;
                c.a = 1f - t * t;
                parts[i].color = c;
            }
            yield return null;
        }
        for (int i = 0; i < count; i++) Destroy(parts[i].gameObject);
    }

    /// <summary>周りから中心へ吸い込まれる粒子（深海の浮上）</summary>
    private IEnumerator Implode(Vector3 pos, int count, Color a, Color b, float radius, float life)
    {
        var parts = new SpriteRenderer[count];
        var starts = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            float ang = (360f / count) * i * Mathf.Deg2Rad;
            starts[i] = pos + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * radius;
            parts[i] = CreateSprite(SpriteFactory.SoftCircle, starts[i], Color.Lerp(a, b, Random.value), 0.16f, OrderParticle);
        }
        float elapsed = 0f;
        while (elapsed < life)
        {
            elapsed += Time.deltaTime;
            float t = Ease.InCubic(elapsed / life);
            for (int i = 0; i < count; i++)
            {
                // 渦を巻きながら中心へ
                Vector3 off = starts[i] - pos;
                float rot = t * 2.2f;
                Vector3 r = new Vector3(off.x * Mathf.Cos(rot) - off.y * Mathf.Sin(rot), off.x * Mathf.Sin(rot) + off.y * Mathf.Cos(rot), 0f);
                parts[i].transform.position = pos + r * (1f - t);
            }
            yield return null;
        }
        for (int i = 0; i < count; i++) Destroy(parts[i].gameObject);
    }

    private IEnumerator Bubble(Vector3 pos)
    {
        var sr = CreateSprite(SpriteFactory.Ring, pos, new Color(0.8f, 0.95f, 1f, 0.8f), 0.1f, OrderParticle);
        const float life = 0.7f;
        float elapsed = 0f;
        while (elapsed < life)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / life;
            sr.transform.position = pos + new Vector3(Mathf.Sin(t * 9f) * 0.04f, t * 0.5f, 0f);
            sr.transform.localScale = Vector3.one * Mathf.Lerp(0.08f, 0.16f, t);
            sr.color = new Color(0.8f, 0.95f, 1f, 0.8f * (1f - t));
            yield return null;
        }
        Destroy(sr.gameObject);
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
