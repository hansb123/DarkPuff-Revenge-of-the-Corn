using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 메카 깃털 한 개. 날개에서 하늘로 솟구친 뒤, 바닥에 착탄 표시를 깔고, 잠시 뒤 비스듬히 내리꽂힌다.
/// stick = true 면 바닥에 박혀 일정 시간 장애물(솔리드 콜라이더)로 남았다가 사라진다.
/// 프리팹 구성: 루트(MechaFeather) + 자식 Body(SpriteRenderer, 깃털) / Mark(SpriteRenderer, feather_mark) / Impact(SpriteRenderer, 착탄 이펙트)
///   + 선택: 루트나 Body 쪽에 BoxCollider2D(Is Trigger 끔)를 붙여 'Solid' 칸에 연결 → 박힌 동안만 켜짐 (기본 꺼진 상태로 두기)
/// 깃털 스프라이트는 위로 솟을 땐 끝이 위, 떨어질 땐 끝이 아래를 보는 그림이라 가정 (mecha_feather_up / fall).
/// </summary>
public class MechaFeather : MonoBehaviour, IPatternHazard
{
    [Header("스프라이트")]
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private SpriteRenderer mark;
    [SerializeField] private SpriteRenderer impact;
    [SerializeField] private Sprite[] upFrames;      // mecha_feather_up_1~2
    [SerializeField] private Sprite[] fallFrames;    // mecha_feather_fall_1~2
    [SerializeField] private Sprite[] impactFrames;  // feather_impact_1~4
    [SerializeField] private float featherLength = 2.4f;
    [SerializeField] private float markWidth = 1.6f;
    [SerializeField] private float impactWidth = 2.2f;

    [Header("충돌")]
    [SerializeField] private int damage = 1;
    [SerializeField] private LayerMask playerLayer;
    [Tooltip("깃털 끝 주변 맞는 범위 (가로, 세로)")]
    [SerializeField] private Vector2 hitSize = new Vector2(0.9f, 1.4f);
    [Tooltip("박힌 동안 켜지는 솔리드 콜라이더 (선택, 기본 꺼짐)")]
    [SerializeField] private Collider2D solid;
    [Tooltip("박힌 깃털이 바닥에 파묻히는 깊이")]
    [SerializeField] private float sinkDepth = 0.35f;

    [Serializable]
    public struct Settings
    {
        public float groundY, topY;
        public float riseSpeed;      // 솟구치는 속도
        public float warnTime;       // 착탄 표시 후 떨어지기까지
        public float fallTime;       // 떨어지는 데 걸리는 시간
        public float slantX;         // 떨어지기 시작하는 X가 착탄 X에서 벗어난 거리 (부호 = 방향)
        public float stuckTime;      // 박혀 있는 시간 (0이면 바로 사라짐)
    }

    private Action<MechaFeather> release;
    private PatternRunner runner;
    private Coroutine routine;
    private bool ended;

    public void SetReleaser(Action<MechaFeather> r) => release = r;

    public void Launch(PatternRunner owner, Vector2 from, float targetX, bool stick, Settings s)
    {
        runner = owner; ended = false;
        runner?.Register(this);
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Run(from, targetX, stick, s));
    }

    /// <summary>플레이어를 노리는 경우: 솟구친 직후 그 시점의 플레이어 X 로 목표를 정한다</summary>
    public void Launch(PatternRunner owner, Vector2 from, Transform aimAt, float aimOffsetX, bool stick, Settings s)
    {
        runner = owner; ended = false;
        runner?.Register(this);
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(RunAimed(from, aimAt, aimOffsetX, stick, s));
    }

    private IEnumerator RunAimed(Vector2 from, Transform aimAt, float offX, bool stick, Settings s)
    {
        yield return Rise(from, s);
        float tx = (aimAt != null ? aimAt.position.x : from.x) + offX;
        yield return Fall(tx, stick, s);
    }

    private IEnumerator Run(Vector2 from, float targetX, bool stick, Settings s)
    {
        yield return Rise(from, s);
        yield return Fall(targetX, stick, s);
    }

    private IEnumerator Rise(Vector2 from, Settings s)
    {
        HideAll();
        if (solid != null) solid.enabled = false;
        transform.position = from;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        PlaceBody(upFrames);
        body.enabled = true;
        body.color = Color.white;
        body.transform.localRotation = Quaternion.identity;
        body.transform.localPosition = Vector3.zero;
        GameSfx.Play(GameSfxId.MechaFeatherLaunch);

        while (transform.position.y < s.topY)
        {
            transform.position += Vector3.up * (s.riseSpeed * Time.deltaTime);
            yield return null;
        }
        body.enabled = false;   // 화면 밖으로 사라짐
    }

    private IEnumerator Fall(float tx, bool stick, Settings s)
    {
        // 착탄 표시
        if (mark != null)
        {
            mark.enabled = true;
            SetWorldWidth(mark, markWidth);
            mark.transform.position = new Vector3(tx, s.groundY + 0.05f, 0f);
            mark.transform.rotation = Quaternion.identity;
        }
        for (float t = 0f; t < s.warnTime; t += Time.deltaTime)
        {
            if (mark != null)
            {
                var c = mark.color; c.a = Mathf.FloorToInt(t * 14f) % 2 == 0 ? 1f : 0.4f; mark.color = c;
            }
            yield return null;
        }
        if (mark != null) { var c = mark.color; c.a = 1f; mark.color = c; }

        // 낙하
        Vector2 start = new Vector2(tx + s.slantX, s.topY);
        Vector2 end = new Vector2(tx, s.groundY);
        Vector2 dir = (end - start).normalized;
        float rotZ = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f;   // 끝이 아래를 보는 그림을 진행 방향으로 돌림
        PlaceBody(fallFrames);
        body.enabled = true;
        body.transform.localPosition = Vector3.zero;
        transform.rotation = Quaternion.Euler(0f, 0f, rotZ);
        GameSfx.Play(GameSfxId.MechaFeatherFall);

        bool hit = false;
        float dur = Mathf.Max(0.05f, s.fallTime);
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float k = t / dur;
            // 점점 빨라지게
            Vector2 p = Vector2.Lerp(start, end, k * k);
            // 몸통 중심이 아닌 '끝'이 p 에 오도록: 길이의 절반만큼 위로
            transform.position = p - dir * (featherLength * 0.5f);
            if (!hit) hit = CheckHit(p);
            yield return null;
        }

        // 착탄
        Vector2 tip = end;
        if (mark != null) mark.enabled = false;
        GameSfx.Play(GameSfxId.MechaFeatherStick);
        if (!hit) hit = CheckHit(tip);
        StartCoroutine(PlayImpact(tip));

        if (stick && s.stuckTime > 0f)
        {
            transform.position = tip - dir * (featherLength * 0.5f - sinkDepth);
            if (solid != null) solid.enabled = true;
            yield return new WaitForSeconds(s.stuckTime - 0.4f);
            // 사라지기 전 깜빡임
            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                body.enabled = Mathf.FloorToInt(t * 20f) % 2 == 0;
                yield return null;
            }
            if (solid != null) solid.enabled = false;
        }
        else
        {
            body.enabled = false;
            yield return new WaitForSeconds(0.3f);   // 이펙트가 끝날 시간
        }

        HideAll();
        Finish();
    }

    private bool CheckHit(Vector2 tip)
    {
        var col = Physics2D.OverlapBox(tip, hitSize, 0f, playerLayer);
        if (col == null) return false;
        var dmg = col.GetComponentInParent<IDamageable>();
        if (dmg == null) return false;
        dmg.TakeDamage(damage, tip);
        return true;
    }

    private IEnumerator PlayImpact(Vector2 pos)
    {
        if (impact == null || impactFrames == null || impactFrames.Length == 0) yield break;
        impact.enabled = true;
        impact.transform.SetParent(null, true);   // 깃털 회전의 영향을 받지 않게 분리
        impact.transform.rotation = Quaternion.identity;
        impact.transform.localScale = Vector3.one;
        for (int i = 0; i < impactFrames.Length; i++)
        {
            impact.sprite = impactFrames[i];
            SetWorldWidth(impact, impactWidth);
            float h = impact.sprite.bounds.size.y * impact.transform.localScale.y;
            impact.transform.position = new Vector3(pos.x, pos.y + h * 0.5f, 0f);
            yield return new WaitForSeconds(0.06f);
        }
        impact.enabled = false;
        impact.transform.SetParent(transform, false);
        impact.transform.localPosition = Vector3.zero;
    }

    // ───────────── 표시 ─────────────

    private void PlaceBody(Sprite[] frames)
    {
        if (frames != null && frames.Length > 0) body.sprite = frames[UnityEngine.Random.Range(0, frames.Length)];
        float h = body.sprite != null ? body.sprite.bounds.size.y : 1f;
        float sc = h > 0f ? featherLength / h : 1f;
        body.transform.localScale = new Vector3(sc, sc, 1f);
    }

    private static void SetWorldWidth(SpriteRenderer sr, float worldWidth)
    {
        if (sr == null || sr.sprite == null) return;
        float w = sr.sprite.bounds.size.x;
        float s = w > 0f ? worldWidth / w : 1f;
        sr.transform.localScale = new Vector3(s, s, 1f);
    }

    private void HideAll()
    {
        if (body != null) body.enabled = false;
        if (mark != null) mark.enabled = false;
        if (impact != null) impact.enabled = false;
    }

    // ───────────── 정리 ─────────────

    public void Cancel()
    {
        if (routine != null) { StopCoroutine(routine); routine = null; }
        StopAllCoroutines();
        if (solid != null) solid.enabled = false;
        HideAll();
        runner = null;
        Finish();
    }

    private void Finish()
    {
        if (ended) return;
        ended = true;
        if (impact != null && impact.transform.parent != transform)
        {
            impact.transform.SetParent(transform, false);
            impact.transform.localPosition = Vector3.zero;
        }
        runner?.Unregister(this);
        runner = null;
        routine = null;
        if (release != null) release(this); else Destroy(gameObject);
    }
}
