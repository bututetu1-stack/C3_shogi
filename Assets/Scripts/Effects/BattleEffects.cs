using UnityEngine;
using System.Collections;

public class BattleEffects : MonoBehaviour
{
    public static BattleEffects Instance { get; private set; }

    private AudioSource audioSource;

    private AudioClip moveClip;
    private AudioClip hitClip;
    private AudioClip defeatClip;
    private AudioClip battleStartClip;
    private AudioClip airRaidClip;
    private AudioClip torpedoClip;
    private AudioClip bombardmentClip;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

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
    }

    // 駒移動SE
    public void PlayMoveEffect()
    {
        if (moveClip != null)
            audioSource.PlayOneShot(moveClip);
    }

    // 対局開始SE
    public void PlayBattleStartEffect()
    {
        if (battleStartClip != null)
            audioSource.PlayOneShot(battleStartClip);
    }

    // ヒットエフェクト (非撃破)
    public void PlayHitEffect(Vector2Int pos)
    {
        if (hitClip != null)
            audioSource.PlayOneShot(hitClip);
        StartCoroutine(ShowHitMark(new Vector3(pos.x, pos.y, -1f)));
    }

    // 撃破エフェクト
    public void PlayDefeatEffect(Vector2Int pos)
    {
        if (defeatClip != null)
            audioSource.PlayOneShot(defeatClip);
        StartCoroutine(ShowDefeatEffect(new Vector3(pos.x, pos.y, -1f)));
    }

    private IEnumerator ShowHitMark(Vector3 worldPos)
    {
        GameObject hitMark = CreateEffectSprite(worldPos, Color.white, 0.4f);
        float duration = 0.3f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float scale = 0.4f * (1f - t);
            hitMark.transform.localScale = new Vector3(scale, scale, 1f);
            SpriteRenderer sr = hitMark.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = new Color(1f, 1f, 1f, 1f - t);
            yield return null;
        }
        Destroy(hitMark);
    }

    private IEnumerator ShowDefeatEffect(Vector3 worldPos)
    {
        int particleCount = 8;
        GameObject[] particles = new GameObject[particleCount];
        Vector2[] velocities = new Vector2[particleCount];

        for (int i = 0; i < particleCount; i++)
        {
            float angle = (360f / particleCount) * i + Random.Range(-15f, 15f);
            float rad = angle * Mathf.Deg2Rad;
            velocities[i] = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * Random.Range(1.5f, 3f);

            Color pColor = new Color(
                Random.Range(0.8f, 1f),
                Random.Range(0.3f, 0.7f),
                Random.Range(0.1f, 0.3f)
            );
            particles[i] = CreateEffectSprite(worldPos, pColor, 0.15f);
        }

        // 画面フラッシュ
        GameObject flash = new GameObject("Flash");
        SpriteRenderer flashSR = flash.AddComponent<SpriteRenderer>();
        Texture2D whiteTex = new Texture2D(4, 4);
        Color[] whitePixels = new Color[16];
        for (int i = 0; i < 16; i++) whitePixels[i] = Color.white;
        whiteTex.SetPixels(whitePixels);
        whiteTex.Apply();
        flashSR.sprite = Sprite.Create(whiteTex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
        flash.transform.position = new Vector3(worldPos.x, worldPos.y, -2f);
        flash.transform.localScale = new Vector3(2f, 2f, 1f);
        flashSR.sortingOrder = 100;

        float duration = 0.4f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            for (int i = 0; i < particleCount; i++)
            {
                if (particles[i] != null)
                {
                    particles[i].transform.position += new Vector3(velocities[i].x * Time.deltaTime, velocities[i].y * Time.deltaTime, 0);
                    float scale = 0.15f * (1f - t);
                    particles[i].transform.localScale = new Vector3(scale, scale, 1f);
                    SpriteRenderer sr = particles[i].GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        Color c = sr.color;
                        sr.color = new Color(c.r, c.g, c.b, 1f - t);
                    }
                }
            }

            if (t < 0.25f)
                flashSR.color = new Color(1f, 1f, 1f, 0.5f * (1f - t / 0.25f));
            else
                flashSR.color = new Color(1f, 1f, 1f, 0f);

            yield return null;
        }

        for (int i = 0; i < particleCount; i++)
            if (particles[i] != null) Destroy(particles[i]);
        Destroy(flash);
    }

    // 空爆エフェクト
    public void PlayAirRaidEffect(Vector2Int pos)
    {
        if (airRaidClip != null)
            audioSource.PlayOneShot(airRaidClip);
        else if (defeatClip != null)
            audioSource.PlayOneShot(defeatClip);
        StartCoroutine(ShowAirRaidEffect(new Vector3(pos.x, pos.y, -1f)));
    }

    private IEnumerator ShowAirRaidEffect(Vector3 worldPos)
    {
        int particleCount = 15;
        GameObject[] particles = new GameObject[particleCount];
        float[] startOffsetX = new float[particleCount];
        float[] endOffsetX = new float[particleCount];

        for (int i = 0; i < particleCount; i++)
        {
            startOffsetX[i] = Random.Range(-1.5f, 1.5f);
            endOffsetX[i] = Random.Range(-0.5f, 0.5f);
            Vector3 startPos = new Vector3(worldPos.x + startOffsetX[i], worldPos.y + 4f, -2f);
            Color c = new Color(1f, Random.Range(0.1f, 0.4f), 0.1f);
            particles[i] = CreateEffectSprite(startPos, c, 0.15f);
        }

        float duration = 1.0f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            for (int i = 0; i < particleCount; i++)
            {
                if (particles[i] == null) continue;
                float startY = worldPos.y + 3f;
                float x = Mathf.Lerp(worldPos.x + startOffsetX[i], worldPos.x + endOffsetX[i], t);
                float y = Mathf.Lerp(startY, worldPos.y, t);
                particles[i].transform.position = new Vector3(x, y, -2f);
                float scale = 0.12f * (1f - t * 0.5f);
                particles[i].transform.localScale = new Vector3(scale, scale, 1f);
                SpriteRenderer sr = particles[i].GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f - t);
            }
            yield return null;
        }

        for (int i = 0; i < particleCount; i++)
            if (particles[i] != null) Destroy(particles[i]);
    }

    // 雷撃エフェクト
    public void PlayTorpedoEffect(Vector2Int from, Vector2Int to)
    {
        if (torpedoClip != null)
            audioSource.PlayOneShot(torpedoClip);
        else if (hitClip != null)
            audioSource.PlayOneShot(hitClip);
        Vector3 fromPos = new Vector3(from.x, from.y, -2f);
        Vector3 toPos = new Vector3(to.x, to.y, -2f);
        StartCoroutine(ShowTorpedoEffect(fromPos, toPos));
    }

    private IEnumerator ShowTorpedoEffect(Vector3 fromPos, Vector3 toPos)
    {
        Color torpedoColor = new Color(0.8f, 0.9f, 0.2f);
        GameObject projectile = CreateEffectSprite(fromPos, torpedoColor, 0.22f);

        // 軌跡
        int trailCount = 6;
        GameObject[] trail = new GameObject[trailCount];
        for (int i = 0; i < trailCount; i++)
        {
            Color trailColor = new Color(0.6f, 0.7f, 0.15f, 0.5f);
            trail[i] = CreateEffectSprite(fromPos, trailColor, 0.1f);
        }

        float duration = 0.8f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            projectile.transform.position = Vector3.Lerp(fromPos, toPos, t);

            for (int i = 0; i < trailCount; i++)
            {
                float trailT = Mathf.Max(0f, t - (i + 1) * 0.06f);
                trail[i].transform.position = Vector3.Lerp(fromPos, toPos, trailT);
                SpriteRenderer sr = trail[i].GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(0.6f, 0.7f, 0.15f, 0.5f * (1f - t));
            }
            yield return null;
        }

        // ヒット時フラッシュ
        SpriteRenderer psr = projectile.GetComponent<SpriteRenderer>();
        if (psr != null) psr.color = Color.white;
        projectile.transform.localScale = new Vector3(0.4f, 0.4f, 1f);

        // 着弾爆発パーティクル
        int burstCount = 6;
        GameObject[] burst = new GameObject[burstCount];
        Vector2[] burstVel = new Vector2[burstCount];
        for (int i = 0; i < burstCount; i++)
        {
            float angle = (360f / burstCount) * i + Random.Range(-20f, 20f);
            float rad = angle * Mathf.Deg2Rad;
            burstVel[i] = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * Random.Range(1.5f, 3f);
            Color bColor = new Color(0.9f, 0.8f, 0.2f);
            burst[i] = CreateEffectSprite(toPos, bColor, 0.12f);
        }

        float burstDur = 0.3f;
        float burstElapsed = 0f;
        while (burstElapsed < burstDur)
        {
            burstElapsed += Time.deltaTime;
            float bt = burstElapsed / burstDur;
            for (int i = 0; i < burstCount; i++)
            {
                if (burst[i] != null)
                {
                    burst[i].transform.position += new Vector3(burstVel[i].x * Time.deltaTime, burstVel[i].y * Time.deltaTime, 0);
                    float s = 0.12f * (1f - bt);
                    burst[i].transform.localScale = new Vector3(s, s, 1f);
                    SpriteRenderer bsr = burst[i].GetComponent<SpriteRenderer>();
                    if (bsr != null) bsr.color = new Color(0.9f, 0.8f, 0.2f, 1f - bt);
                }
            }
            yield return null;
        }

        Destroy(projectile);
        for (int i = 0; i < trailCount; i++)
            if (trail[i] != null) Destroy(trail[i]);
        for (int i = 0; i < burstCount; i++)
            if (burst[i] != null) Destroy(burst[i]);
    }

    // 砲撃エフェクト
    public void PlayBombardmentEffect(Vector2Int pos)
    {
        if (bombardmentClip != null)
            audioSource.PlayOneShot(bombardmentClip);
        else if (defeatClip != null)
            audioSource.PlayOneShot(defeatClip);
        StartCoroutine(ShowBombardmentEffect(new Vector3(pos.x, pos.y, -1f)));
    }

    private IEnumerator ShowBombardmentEffect(Vector3 worldPos)
    {
        // 大きなオレンジ爆発
        int particleCount = 16;
        GameObject[] particles = new GameObject[particleCount];
        Vector2[] velocities = new Vector2[particleCount];

        for (int i = 0; i < particleCount; i++)
        {
            float angle = (360f / particleCount) * i + Random.Range(-20f, 20f);
            float rad = angle * Mathf.Deg2Rad;
            velocities[i] = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * Random.Range(2f, 5f);
            Color pColor = new Color(1f, Random.Range(0.4f, 0.7f), Random.Range(0f, 0.2f));
            particles[i] = CreateEffectSprite(worldPos, pColor, 0.25f);
        }

        // 画面フラッシュ
        GameObject flash = new GameObject("BombFlash");
        SpriteRenderer flashSR = flash.AddComponent<SpriteRenderer>();
        Texture2D whiteTex = new Texture2D(4, 4);
        Color[] whitePixels = new Color[16];
        for (int i = 0; i < 16; i++) whitePixels[i] = Color.white;
        whiteTex.SetPixels(whitePixels);
        whiteTex.Apply();
        flashSR.sprite = Sprite.Create(whiteTex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
        flash.transform.position = new Vector3(worldPos.x, worldPos.y, -3f);
        flash.transform.localScale = new Vector3(3f, 3f, 1f);
        flashSR.sortingOrder = 100;
        flashSR.color = new Color(1f, 0.6f, 0f, 0.8f);

        float duration = 1.2f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            for (int i = 0; i < particleCount; i++)
            {
                if (particles[i] != null)
                {
                    particles[i].transform.position += new Vector3(
                        velocities[i].x * Time.deltaTime,
                        velocities[i].y * Time.deltaTime, 0);
                    float scale = 0.2f * (1f - t);
                    particles[i].transform.localScale = new Vector3(scale, scale, 1f);
                    SpriteRenderer sr = particles[i].GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        Color c = sr.color;
                        sr.color = new Color(c.r, c.g, c.b, 1f - t);
                    }
                }
            }

            if (t < 0.3f)
                flashSR.color = new Color(1f, 0.6f, 0f, 0.8f * (1f - t / 0.3f));
            else
                flashSR.color = new Color(1f, 0.6f, 0f, 0f);

            yield return null;
        }

        for (int i = 0; i < particleCount; i++)
            if (particles[i] != null) Destroy(particles[i]);
        Destroy(flash);

        // 画面揺れ
        if (Camera.main != null)
            StartCoroutine(CameraShake(0.3f, 0.15f));
    }

    private IEnumerator CameraShake(float duration, float magnitude)
    {
        Transform camTransform = Camera.main.transform;
        Vector3 originalPos = camTransform.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float x = originalPos.x + Random.Range(-magnitude, magnitude);
            float y = originalPos.y + Random.Range(-magnitude, magnitude);
            camTransform.position = new Vector3(x, y, originalPos.z);
            yield return null;
        }
        camTransform.position = originalPos;
    }

    private GameObject CreateEffectSprite(Vector3 pos, Color color, float size)
    {
        GameObject obj = new GameObject("Effect");
        obj.transform.position = pos;
        obj.transform.localScale = new Vector3(size, size, 1f);

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();

        Texture2D tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[256];
        float center = 8f;
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist < 6f)
                    pixels[y * 16 + x] = color;
                else
                    pixels[y * 16 + x] = new Color(0, 0, 0, 0);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();

        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
        sr.sortingOrder = 50;
        sr.color = color;

        return obj;
    }
}
