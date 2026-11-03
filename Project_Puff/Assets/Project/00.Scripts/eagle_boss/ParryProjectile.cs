using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 패링할 수 있는 공격 한 발. 플레이어를 향해 날아온다.
///  - 뻥 알갱이에 맞으면(IDamageable.TakeDamage) → 패링 성공
///  - 플레이어에게 닿으면 → 데미지 + 패링 실패
///  - 아무에게도 안 맞고 지나가 사라지면 → 회피 (이득도 손해도 없음)
/// 프리팹 구성: SpriteRenderer + 이 컴포넌트 + Enemy 레이어의 콜라이더(CircleCollider2D). 알갱이가 이 콜라이더를 맞힌다.
/// (PlayerSpecialAttack 의 Target Layer 에 Enemy 가 있어야 함. 기존 코드는 수정 필요 없음)
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ParryProjectile : MonoBehaviour, IDamageable, IPatternHazard
{
    [Header("충돌")]
    [SerializeField] private float hitRadius = 0.45f;
    [SerializeField] private int damage = 1;
    [SerializeField] private LayerMask playerLayer;

    [Header("예고")]
    [Tooltip("발사 전에 깜빡이는 색 (패링 가능 공격은 일반 공격과 색이 달라야 구분됨)")]
    [SerializeField] private Color warnColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private float blinkInterval = 0.07f;

    private SpriteRenderer sr;
    private Action<ParryProjectile> release;
    private Action onResolved;
    private PatternRunner runner;
    private Coroutine routine;
    private bool resolved;
    private bool canParry = true;   // 곡선 모드: 올라가는 동안은 패링 불가, 꼭대기에서 멈춘 뒤부터 가능

    private bool countsAsParry = true;   // false = 그냥 공격: 패링/피격/회피 이벤트를 보내지 않음 (그로기 카운트에 안 들어감)
    private bool destroyable = true;     // 그냥 공격일 때 플레이어 공격에 맞으면 사라질지

    public void SetReleaser(Action<ParryProjectile> r) => release = r;

    /// <summary>Launch 직전에 호출. countsAsParry=false 면 그냥 공격으로 동작한다 (끝나면 자동으로 원래대로 돌아감)</summary>
    public void SetPlain(bool destroyableByHit)
    {
        countsAsParry = false;
        destroyable = destroyableByHit;
    }

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    /// <summary>start 에서 예고(telegraph) 후, target 방향으로 speed 로 날아가 maxDistance 를 넘으면 사라짐</summary>
    public void Launch(PatternRunner owner, Vector2 start, Vector2 target, float telegraph, float speed,
                       float maxDistance, Action onResolved)
    {
        runner = owner;
        runner?.Register(this);
        this.onResolved = onResolved;
        resolved = false;
        canParry = true;

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Fly(start, target, telegraph, speed, maxDistance));
    }

    /// <summary>
    /// 곡선 모드: start 에서 위로 솟아올라(riseY, 플레이어 쪽으로 riseX 만큼 휘며) → 꼭대기에서 잠깐 멈춰 플레이어를 조준하고 → 그 위치로 내리꽂는다.
    /// 올라가는 동안은 패링할 수 없고, 꼭대기에서 멈춘 순간부터 패링할 수 있다 (멈춘 시간 = 반응할 시간).
    /// 내리꽂는 방향은 멈추는 동안 따라가다가 마지막 순간에 고정된다.
    /// </summary>
    public void LaunchCurved(PatternRunner owner, Vector2 start, Transform player, float riseX, float riseY,
                             float riseTime, float apexPause, float diveSpeed, float maxDistance, Action onResolved)
    {
        runner = owner;
        runner?.Register(this);
        this.onResolved = onResolved;
        resolved = false;

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(FlyCurved(start, player, riseX, riseY, Mathf.Max(0.05f, riseTime), Mathf.Max(0f, apexPause), diveSpeed, maxDistance));
    }

    private IEnumerator FlyCurved(Vector2 start, Transform player, float riseX, float riseY,
                                  float riseTime, float apexPause, float diveSpeed, float maxDistance)
    {
        canParry = false;
        sr.color = normalColor;
        transform.position = start;

        float side = player != null && player.position.x < start.x ? -1f : 1f;
        Vector2 apex = start + new Vector2(side * riseX, riseY);
        Vector2 ctrl = new Vector2(start.x, apex.y);   // 처음엔 곧게 솟다가 옆으로 휘는 곡선

        // 1) 솟아오르기 (패링 불가)
        Vector2 prev = start;
        for (float t = 0f; t < riseTime; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / riseTime);
            Vector2 pos = Bezier(start, ctrl, apex, k);
            Vector2 d = pos - prev;
            if (d.sqrMagnitude > 0.00001f) Face(d);
            transform.position = pos;
            prev = pos;
            yield return null;
        }
        transform.position = apex;

        // 2) 꼭대기에서 잠깐 멈춤: 플레이어를 조준 (패링 가능, 깜빡임)
        canParry = true;
        Vector2 aim = player != null ? (Vector2)player.position : apex + Vector2.down;
        bool on = false;
        float blinkT = 0f;
        for (float t = 0f; t < apexPause; t += Time.deltaTime)
        {
            if (player != null && t < apexPause * 0.7f) aim = player.position;   // 마지막 30%는 조준 고정 (피할 틈)
            Face(aim - apex);
            blinkT += Time.deltaTime;
            if (blinkT >= blinkInterval) { blinkT = 0f; on = !on; sr.color = on ? warnColor : normalColor; }
            yield return null;
        }
        sr.color = warnColor;

        // 3) 내리꽂기
        Vector2 dir = (aim - apex).sqrMagnitude > 0.0001f ? (aim - apex).normalized : Vector2.down;
        Face(dir);
        float travelled = 0f;
        Vector2 p = apex;
        while (travelled < maxDistance)
        {
            float step = diveSpeed * Time.deltaTime;
            p += dir * step;
            travelled += step;
            transform.position = p;

            var hit = Physics2D.OverlapCircle(p, hitRadius, playerLayer);
            if (hit != null)
            {
                var victim = hit.GetComponentInParent<IDamageable>();
                victim?.TakeDamage(damage, p);
                Resolve(ParryEvents.RaiseFailed);
                yield break;
            }
            yield return null;
        }

        Resolve(ParryEvents.RaiseDodged);
    }

    private void Face(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    private IEnumerator Fly(Vector2 start, Vector2 target, float telegraph, float speed, float maxDistance)
    {
        transform.position = start;
        Vector2 dir = (target - start).sqrMagnitude > 0.0001f ? (target - start).normalized : Vector2.down;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        // 예고: 깜빡임 (이 동안은 맞지도 않고 패링도 안 됨)
        bool on = false;
        for (float t = 0f; t < telegraph; t += blinkInterval)
        {
            on = !on;
            sr.color = on ? warnColor : normalColor;
            yield return new WaitForSeconds(blinkInterval);
        }
        sr.color = warnColor;

        float travelled = 0f;
        Vector2 pos = start;
        while (travelled < maxDistance)
        {
            float step = speed * Time.deltaTime;
            pos += dir * step;
            travelled += step;
            transform.position = pos;

            var hit = Physics2D.OverlapCircle(pos, hitRadius, playerLayer);
            if (hit != null)
            {
                var target2 = hit.GetComponentInParent<IDamageable>();
                target2?.TakeDamage(damage, pos);
                Resolve(ParryEvents.RaiseFailed);
                yield break;
            }
            yield return null;
        }

        Resolve(ParryEvents.RaiseDodged);
    }

    /// <summary>뻥 알갱이가 맞음 = 패링 성공</summary>
    public void TakeDamage(int amount, Vector2 hitFrom)
    {
        if (resolved || !canParry) return;
        if (!countsAsParry) { if (destroyable) Resolve(null); return; }   // 그냥 공격: 맞으면 사라질 뿐, 패링 아님
        // 예고 중(색이 깜빡이는 동안)에는 패링 불가로 하고 싶으면 여기서 return 하면 된다.
        Resolve(ParryEvents.RaiseSucceeded);
    }

    private void Resolve(Action raise)
    {
        if (resolved) return;
        resolved = true;
        runner?.Unregister(this);
        if (countsAsParry) raise?.Invoke();
        var done = onResolved;
        onResolved = null;
        Finish();
        done?.Invoke();
    }

    private void Finish()
    {
        if (routine != null) { StopCoroutine(routine); routine = null; }
        sr.color = normalColor;
        countsAsParry = true; destroyable = true;   // 풀에서 재사용될 때를 위해 초기화
        if (release != null) release(this); else gameObject.SetActive(false);
    }

    /// <summary>플레이어 사망/페이즈 전환/그로기로 강제 정리될 때 (결과 이벤트 없음)</summary>
    public void Cancel()
    {
        if (resolved) return;
        resolved = true;
        onResolved = null;
        Finish();
    }
}