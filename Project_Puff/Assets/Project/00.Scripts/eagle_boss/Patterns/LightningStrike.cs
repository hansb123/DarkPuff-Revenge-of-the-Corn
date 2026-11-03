using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 낙뢰 한 줄기. 바닥에 노란 경고 띠와 연한 기둥이 깜빡이다가(점점 빨라짐) 번개가 내리꽂힌다. 기둥 영역 안에 있으면 데미지(한 번만).
/// 스프라이트 없이 사각형으로 그려지므로 프리팹은 '빈 오브젝트 + 이 컴포넌트'면 된다.
/// 나중에 번개 아트가 생기면 Bolt Sprite 칸에 넣으면 기둥 대신 그 스프라이트가 늘려져 표시된다.
/// </summary>
public class LightningStrike : MonoBehaviour, IPatternHazard
{
    [Header("충돌")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private int damage = 1;
    [Tooltip("번개가 떨어진 뒤 판정이 유지되는 시간")]
    [SerializeField] private float activeTime = 0.12f;
    [SerializeField] private float fadeTime = 0.18f;
    [Tooltip("번개 폭 = 칸 폭 × 이 값 (맞는 영역은 칸 폭의 85%)")]
    [Range(0.2f, 1f)] [SerializeField] private float boltWidthRatio = 0.55f;

    [Header("모양")]
    [SerializeField] private Color warnColor = new Color(1f, 0.88f, 0.2f, 0.65f);
    [SerializeField] private Color columnColor = new Color(1f, 0.88f, 0.2f, 0.14f);
    [SerializeField] private Color boltColor = new Color(0.88f, 0.96f, 1f, 1f);
    [Header("스프라이트 (선택 — 비워두면 사각형으로 표시)")]
    [Tooltip("lightning_warn_1~2. 번갈아 깜빡임 (바닥 경고 띠)")]
    [SerializeField] private Sprite[] warnFrames;
    [Tooltip("lightning_bolt_1~3. 번개가 떨어지는 동안 번갈아 보임 (세로로 늘려 사용)")]
    [SerializeField] private Sprite[] boltFrames;
    [Tooltip("lightning_impact_1~4. 번개가 닿는 바닥의 불꽃 (순서대로 재생)")]
    [SerializeField] private Sprite[] impactFrames;
    [SerializeField] private float impactWidthRatio = 1.5f;
    [SerializeField] private int sortingOrder = 6;

    private SpriteRenderer strip, column, bolt, impact;
    private Action<LightningStrike> release;
    private PatternRunner runner;
    private Coroutine routine;

    public void SetReleaser(Action<LightningStrike> r) => release = r;

    private void Awake()
    {
        strip = EagleVis.MakeRect(transform, "Strip", sortingOrder);
        column = EagleVis.MakeRect(transform, "Column", sortingOrder - 1);
        bolt = EagleVis.MakeRect(transform, "Bolt", sortingOrder + 1);
        impact = EagleVis.MakeRect(transform, "Impact", sortingOrder + 2);
    }

    public void Launch(PatternRunner owner, float x, float groundY, float laneWidth, float warnTime, float topY)
    {
        runner = owner;
        runner?.Register(this);
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Strike(x, groundY, laneWidth, Mathf.Max(0.2f, warnTime), topY));
    }

    private IEnumerator Strike(float x, float groundY, float laneWidth, float warnTime, float topY)
    {
        float h = Mathf.Max(1f, topY - groundY);
        float cy = groundY + h * 0.5f;

        strip.enabled = true; column.enabled = true; bolt.enabled = false;
        bool hasWarnSprite = warnFrames != null && warnFrames.Length > 0;
        if (!hasWarnSprite) EagleVis.Place(strip, x, groundY + 0.12f, laneWidth * 0.95f, 0.24f);
        EagleVis.Place(column, x, cy, laneWidth * 0.85f, h);

        // 1) 예고: 깜빡임이 점점 빨라진다
        float t = 0f;
        bool on = true;
        float next = 0f;
        while (t < warnTime)
        {
            float k = t / warnTime;
            if (t >= next)
            {
                on = !on;
                next = t + Mathf.Lerp(0.16f, 0.05f, k);
            }
            if (hasWarnSprite)
            {
                strip.sprite = warnFrames[on ? 0 : Mathf.Min(1, warnFrames.Length - 1)];
                strip.color = Color.white;
                FitWidth(strip, laneWidth * 0.95f, x, groundY, 0f);
            }
            else strip.color = on ? warnColor : new Color(warnColor.r, warnColor.g, warnColor.b, warnColor.a * 0.25f);
            column.color = on ? columnColor : new Color(columnColor.r, columnColor.g, columnColor.b, 0.04f);
            t += Time.deltaTime;
            yield return null;
        }

        // 2) 낙뢰
        strip.enabled = false; column.enabled = false;
        bolt.enabled = true;
        bolt.color = boltColor;
        bool hasBolt = boltFrames != null && boltFrames.Length > 0;
        if (hasBolt) bolt.color = Color.white;
        else EagleVis.Place(bolt, x, cy, laneWidth * boltWidthRatio, h);
        ShowBolt(x, cy, laneWidth * boltWidthRatio, h, 0);
        StartImpact(x, groundY, laneWidth);

        GameSfx.PlayAt(GameSfxId.EagleStomp, new Vector2(x, groundY));

        bool hitDone = false;
        float flick = 0f;
        int frame = 0;
        for (float e = 0f; e < activeTime; e += Time.deltaTime)
        {
            flick += Time.deltaTime;
            if (hasBolt && flick > 0.04f) { flick = 0f; frame++; ShowBolt(x, cy, laneWidth * boltWidthRatio, h, frame); }
            AnimImpact(e);
            if (!hitDone)
            {
                var hit = Physics2D.OverlapBox(new Vector2(x, cy), new Vector2(laneWidth * 0.85f, h), 0f, playerLayer);
                if (hit != null)
                {
                    var d = hit.GetComponentInParent<IDamageable>();
                    if (d != null) d.TakeDamage(damage, new Vector2(x, groundY));
                    hitDone = true;
                }
            }
            yield return null;
        }

        // 3) 사라짐
        for (float e = 0f; e < fadeTime; e += Time.deltaTime)
        {
            var c = hasBolt ? Color.white : boltColor; c.a = 1f - e / fadeTime;
            bolt.color = c;
            AnimImpact(activeTime + e);
            yield return null;
        }

        Finish();
    }

    // ───────────── 스프라이트 도우미 ─────────────
    private void ShowBolt(float x, float cy, float width, float height, int frame)
    {
        if (boltFrames == null || boltFrames.Length == 0) return;
        var sp = boltFrames[frame % boltFrames.Length];
        bolt.sprite = sp;
        var b = sp.bounds.size;
        bolt.transform.position = new Vector3(x, cy, 0f);
        bolt.transform.localScale = new Vector3(width / Mathf.Max(0.01f, b.x), height / Mathf.Max(0.01f, b.y), 1f);
    }

    private void StartImpact(float x, float groundY, float laneWidth)
    {
        impactX = x; impactGround = groundY; impactLane = laneWidth;
        if (impactFrames == null || impactFrames.Length == 0) return;
        impact.enabled = true;
        impact.color = Color.white;
        AnimImpact(0f);
    }

    private float impactX, impactGround, impactLane;

    private void AnimImpact(float elapsed)
    {
        if (impactFrames == null || impactFrames.Length == 0) return;
        float total = activeTime + fadeTime;
        int i = Mathf.Min(impactFrames.Length - 1, Mathf.FloorToInt(elapsed / total * impactFrames.Length));
        impact.sprite = impactFrames[i];
        FitWidth(impact, impactLane * impactWidthRatio, impactX, impactGround, 0f);
    }

    /// <summary>가로폭에 맞추고 스프라이트 아래쪽이 바닥선에 닿도록 배치 (피벗과 무관)</summary>
    private static void FitWidth(SpriteRenderer sr, float worldWidth, float x, float groundY, float lift)
    {
        if (sr.sprite == null) return;
        sr.enabled = true;
        float w = sr.sprite.bounds.size.x;
        float sc = w > 0f ? worldWidth / w : 1f;
        sr.transform.localScale = new Vector3(sc, sc, 1f);
        sr.transform.position = new Vector3(x, groundY + lift - sr.sprite.bounds.min.y * sc, 0f);
    }

    private void Finish()
    {
        runner?.Unregister(this);
        runner = null;
        routine = null;
        Hide();
        if (release != null) release(this); else Destroy(gameObject);
    }

    private void Hide()
    {
        if (strip != null) strip.enabled = false;
        if (column != null) column.enabled = false;
        if (bolt != null) bolt.enabled = false;
        if (impact != null) impact.enabled = false;
    }

    public void Cancel()
    {
        runner = null;
        if (routine != null) { StopCoroutine(routine); routine = null; }
        Hide();
        if (release != null) release(this); else Destroy(gameObject);
    }
}
