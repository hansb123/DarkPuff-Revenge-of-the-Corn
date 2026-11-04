using System.Collections;
using UnityEngine;

/// <summary>
/// 독수리 분신 (그림자 돌진 패턴용). 코드로 만들어지므로 프리팹이 필요 없다 (EagleShadowClone.Spawn).
/// 흐름: 보스 위치에서 갈라져 나옴(번쩍) → 대기 위치로 날아가 자세를 잡음 → 조준이 고정되면 붉은 예고선 → 돌진 → 사라짐.
/// 분신의 돌진은 진짜처럼 데미지가 있지만 패링은 되지 않는다. 패링에 성공하면 분신은 모두 연기처럼 사라진다.
/// PatternRunner 에 등록되어, 패턴이 끊기거나 보스가 멈추면 함께 정리된다.
/// </summary>
public class EagleShadowClone : MonoBehaviour, IPatternHazard
{
    public struct Plan
    {
        public Vector2 from;          // 갈라져 나오는 위치 (보스 위치)
        public Vector2 start;         // 자세를 잡고 기다릴 위치
        public float arriveTime;      // 대기 위치까지 날아가는 시간
        public float lockAt;          // 조준 고정 + 예고선이 뜨는 시각 (분열 시작 기준 초)
        public float launchAt;        // 돌진 시각
        public float speed, overshoot, hitRadius;
        public int damage;
        public LayerMask playerLayer;
        public float groundY;
        public Transform player;
        public Sprite windupSprite, diveSprite;
        public Color tint;
        public Color lineColor;
    }

    private SpriteRenderer sr, line;
    private PatternRunner runner;
    private SpriteRenderer templateSr;
    private bool finished;
    public bool IsFinished => finished;

    public static EagleShadowClone Spawn(PatternRunner owner, SpriteRenderer boss, Plan plan)
    {
        var go = new GameObject("EagleShadowClone");
        go.transform.localScale = boss.transform.lossyScale;
        var c = go.AddComponent<EagleShadowClone>();
        c.sr = go.AddComponent<SpriteRenderer>();
        c.sr.sortingLayerID = boss.sortingLayerID;
        c.sr.sortingOrder = boss.sortingOrder - 1;
        c.templateSr = boss;
        c.runner = owner;
        owner?.Register(c);
        c.StartCoroutine(c.Play(plan));
        return c;
    }

    private void OnEnable() => ParryEvents.Succeeded += OnParry;
    private void OnDisable() => ParryEvents.Succeeded -= OnParry;

    private void OnDestroy()
    {
        runner?.Unregister(this);
        if (line != null) Destroy(line.gameObject);
        finished = true;
    }

    private void OnParry()
    {
        if (finished) return;
        StopAllCoroutines();
        if (line != null) line.enabled = false;
        StartCoroutine(Poof());
    }

    /// <summary>PatternRunner 가 부름: 즉시 제거</summary>
    public void Cancel()
    {
        finished = true;
        runner?.Unregister(this);
        runner = null;
        Destroy(gameObject);
    }

    private IEnumerator Poof()
    {
        Burst(transform.position, 0.35f);
        Color c = sr.color;
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            c.a = Mathf.Lerp(c.a, 0f, t / 0.25f);
            sr.color = c;
            yield return null;
        }
        finished = true;
        Destroy(gameObject);
    }

    private IEnumerator Play(Plan p)
    {
        sr.sprite = p.windupSprite;
        Vector2 pos = p.from;
        transform.position = pos;
        Color col = p.tint; col.a = 0f; sr.color = col;
        Burst(pos, 0.45);

        bool locked = false;
        Vector2 dir = Vector2.right, end = p.start;
        float t = 0f;
        while (t < p.launchAt)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.05f, p.arriveTime));
            pos = Vector2.Lerp(p.from, p.start, Mathf.SmoothStep(0f, 1f, k));
            if (k >= 1f) pos.y += Mathf.Sin(t * 9f) * 0.08f;   // 대기 중 살짝 둥실
            transform.position = pos;
            col.a = p.tint.a * Mathf.Clamp01(t / 0.25f);
            sr.color = col;
            if (p.player != null) sr.flipX = p.player.position.x > pos.x;   // 플레이어 쪽을 봄

            if (!locked && t >= p.lockAt)
            {
                locked = true;
                Vector2 aim = p.player != null ? (Vector2)p.player.position : pos + Vector2.right;
                dir = (aim - pos).normalized;
                if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;
                end = aim + dir * p.overshoot;
                line = MakeLine(p.lineColor);
            }
            if (locked) UpdateLine(pos, end, dir, p, t);
            yield return null;
        }

        // 돌진
        if (line != null) line.enabled = false;
        sr.sprite = p.diveSprite != null ? p.diveSprite : p.windupSprite;
        sr.flipX = dir.x > 0f;
        float tilt = Mathf.Atan2(dir.y, Mathf.Abs(dir.x)) * Mathf.Rad2Deg * (dir.x > 0f ? 1f : -1f) * 0.5f;
        transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        bool hitDone = false;
        float ghostT = 0f;
        while (Vector2.Dot(end - pos, dir) > 0f && pos.y > p.groundY + 0.2f)
        {
            pos += dir * (p.speed * Time.deltaTime);
            transform.position = pos;

            ghostT += Time.deltaTime;
            if (ghostT >= 0.035f) { ghostT = 0f; StartCoroutine(Ghost(p.tint)); }

            if (!hitDone)
            {
                var hit = Physics2D.OverlapCircle(pos, p.hitRadius, p.playerLayer);
                if (hit != null)
                {
                    var d = hit.GetComponentInParent<IDamageable>();
                    if (d != null) d.TakeDamage(p.damage, pos);
                    hitDone = true;
                }
            }
            yield return null;
        }

        // 사라짐
        for (float f = 0f; f < 0.25f; f += Time.deltaTime)
        {
            col.a = p.tint.a * (1f - f / 0.25f);
            sr.color = col;
            yield return null;
        }
        finished = true;
        Destroy(gameObject);
    }

    // ───────────── 이펙트 ─────────────

    private SpriteRenderer MakeLine(Color c)
    {
        var go = new GameObject("CloneLine");
        var l = go.AddComponent<SpriteRenderer>();
        l.sprite = EagleVis.White;
        l.sortingOrder = 2;
        l.color = c;
        return l;
    }

    private void UpdateLine(Vector2 pos, Vector2 end, Vector2 dir, Plan p, float t)
    {
        if (line == null) return;
        Vector2 e = end;
        if (dir.y < -0.01f && e.y < p.groundY + 0.3f)   // 바닥에서 끊김
        {
            float s = (p.groundY + 0.3f - pos.y) / dir.y;
            e = pos + dir * Mathf.Max(0f, s);
        }
        Vector2 mid = (pos + e) * 0.5f;
        float len = Vector2.Distance(pos, e);
        float pulse = 0.55f + 0.45f * Mathf.Sin(t * 30f);
        Color c = p.lineColor; c.a *= pulse;
        line.color = c;
        line.transform.position = new Vector3(mid.x, mid.y, 0f);
        line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        line.transform.localScale = new Vector3(len, 0.22f, 1f);
    }

    private IEnumerator Ghost(Color tint)
    {
        var go = new GameObject("CloneGhost");
        go.transform.SetPositionAndRotation(transform.position, transform.rotation);
        go.transform.localScale = transform.lossyScale;
        var g = go.AddComponent<SpriteRenderer>();
        g.sprite = sr.sprite; g.flipX = sr.flipX;
        g.sortingLayerID = sr.sortingLayerID;
        g.sortingOrder = sr.sortingOrder - 1;
        Color c = tint;
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            c.a = tint.a * 0.5f * (1f - t / 0.25f);
            g.color = c;
            yield return null;
        }
        Destroy(go);
    }

    /// <summary>갈라질 때/사라질 때 사방으로 퍼지는 빛줄기</summary>
    private void Burst(Vector3 at, double life)
    {
        for (int i = 0; i < 10; i++)
            StartCoroutine(BurstStreak(at, i * 36f + Random.Range(-8f, 8f), (float)life));
    }

    private IEnumerator BurstStreak(Vector3 at, float angle, float life)
    {
        var go = new GameObject("CloneBurst");
        var b = go.AddComponent<SpriteRenderer>();
        b.sprite = EagleVis.White;
        b.sortingOrder = 4;
        Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        Color c = new Color(0.8f, 0.9f, 1f, 1f);
        float len = Random.Range(0.8f, 1.6f);
        for (float t = 0f; t < life; t += Time.deltaTime)
        {
            float k = t / life;
            float dist = Mathf.Lerp(0.3f, 2.6f, 1f - (1f - k) * (1f - k));
            go.transform.position = at + (Vector3)(dir * dist);
            go.transform.localScale = new Vector3(len * (1f - k * 0.6f), 0.07f, 1f);
            c.a = 0.9f * (1f - k);
            b.color = c;
            yield return null;
        }
        Destroy(go);
    }
}