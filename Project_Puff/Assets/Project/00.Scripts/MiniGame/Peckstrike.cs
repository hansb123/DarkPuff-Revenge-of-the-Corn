using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 닭이 부리로 내리찍는 공격 한 번. 바닥에 세로 경고 기둥이 나타나 플레이어를 따라다니다가(추적),
/// 멈추고 깜빡인 뒤(고정) 그 자리를 찍는다. 찍는 순간 기둥 영역 안에 있으면 데미지.
/// "따라오다 멈춘다" 덕분에 움직이면서 피하는 맛이 생기고, 멈춘 뒤에는 반드시 피할 여유가 있다.
/// 프리팹 구성: 빈 루트 + 이 컴포넌트, 자식으로 Warn / Impact 각각 SpriteRenderer (콜라이더 필요 없음).
/// </summary>
public class PeckStrike : MonoBehaviour, IPatternHazard
{
    [Header("스프라이트 (자식 SpriteRenderer)")]
    [SerializeField] private SpriteRenderer warn;
    [SerializeField] private SpriteRenderer impact;
    [Tooltip("peck_warn_1~2. 번갈아 재생")]
    [SerializeField] private Sprite[] warnFrames;
    [Tooltip("peck_impact_1~4. 순서대로 재생")]
    [SerializeField] private Sprite[] impactFrames;
    [SerializeField] private float impactFps = 14f;

    [Header("크기 (월드 단위, 가로 기준)")]
    [SerializeField] private float warnWidth = 1.6f;
    [SerializeField] private float impactWidth = 3.4f;

    [Header("충돌 (경고 기둥 영역)")]
    [Tooltip("맞는 영역의 가로/세로. 보통 경고 기둥보다 살짝 좁게")]
    [SerializeField] private float hitWidth = 1.3f;
    [SerializeField] private float hitHeight = 4.5f;
    [SerializeField] private int damage = 1;
    [SerializeField] private LayerMask playerLayer;
    [Tooltip("찍은 뒤 이 시간 동안 판정이 유지됨 (한 번만 맞음)")]
    [SerializeField] private float activeTime = 0.12f;

    /// <summary>찍기의 진행 단계. 닭 모션 등 다른 스크립트가 구독한다 (Phase, 현재 X 좌표, 플레이어를 따라가는 '주' 찍기인지)</summary>
    public enum Phase { Start, Track, Lock, Strike, End }
    public static event Action<Phase, float, bool> PhaseChanged;

    private Action<PeckStrike> release;
    private bool isMain;   // 플레이어를 따라다니는 찍기만 닭의 몸짓을 움직인다
    private PatternRunner runner;
    private Coroutine routine;

    public void SetReleaser(Action<PeckStrike> r) => release = r;

    /// <summary>
    /// target: 따라갈 대상 (null 이면 추적 없이 startX 에 고정).
    /// trackTime: 따라다니는 시간. lockTime: 멈춰서 깜빡이는 시간(= 마지막으로 피할 여유).
    /// trackSpeed: 따라가는 속도(유닛/초). 느릴수록 쉽다.
    /// </summary>
    public void Launch(PatternRunner owner, Transform target, float startX, float groundY,
                       float trackTime, float lockTime, float trackSpeed, float minX, float maxX)
    {
        runner = owner;
        isMain = target != null;
        runner?.Register(this);

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Strike(target, startX, groundY, trackTime, lockTime, trackSpeed, minX, maxX));
    }

    private IEnumerator Strike(Transform target, float x, float groundY,
                               float trackTime, float lockTime, float trackSpeed, float minX, float maxX)
    {
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        if (impact != null) impact.enabled = false;
        Show(warn, warnFrames, 0, warnWidth, 0.55f);
        PlaceBottom(warn, x, groundY);
        PhaseChanged?.Invoke(Phase.Start, x, isMain);

        // 1) 추적: 천천히 플레이어 쪽으로 따라간다
        for (float t = 0f; t < trackTime; t += Time.deltaTime)
        {
            if (target != null) x = Mathf.MoveTowards(x, Mathf.Clamp(target.position.x, minX, maxX), trackSpeed * Time.deltaTime);
            PlaceBottom(warn, x, groundY);
            Animate(warn, warnFrames, t, 4f);
            PhaseChanged?.Invoke(Phase.Track, x, isMain);
            yield return null;
        }
        PhaseChanged?.Invoke(Phase.Lock, x, isMain);

        // 2) 고정: 그 자리에서 빠르게 깜빡임 (곧 찍는다는 신호)
        for (float t = 0f; t < lockTime; t += Time.deltaTime)
        {
            Animate(warn, warnFrames, t, 12f);
            var c = warn.color; c.a = 1f; warn.color = c;
            yield return null;
        }

        // 3) 찍기
        warn.enabled = false;
        PhaseChanged?.Invoke(Phase.Strike, x, isMain);
        GameSfx.PlayAt(GameSfxId.ChickenPeckHit, new Vector2(x, groundY));

        Vector2 center = new Vector2(x, groundY + hitHeight * 0.5f);
        bool hitDone = false;
        float elapsed = 0f;
        float impactDuration = impactFrames != null && impactFrames.Length > 0 ? impactFrames.Length / impactFps : 0.3f;

        if (impact != null && impactFrames != null && impactFrames.Length > 0)
        {
            Show(impact, impactFrames, 0, impactWidth, 1f);
            PlaceBottom(impact, x, groundY);
        }

        while (elapsed < impactDuration)
        {
            if (!hitDone && elapsed < activeTime)
            {
                var hit = Physics2D.OverlapBox(center, new Vector2(hitWidth, hitHeight), 0f, playerLayer);
                if (hit != null)
                {
                    var target2 = hit.GetComponentInParent<IDamageable>();
                    if (target2 != null) target2.TakeDamage(damage, new Vector2(x, groundY));
                    hitDone = true;
                }
            }

            if (impact != null && impactFrames != null && impactFrames.Length > 0)
                impact.sprite = impactFrames[Mathf.Min(impactFrames.Length - 1, Mathf.FloorToInt(elapsed * impactFps))];

            elapsed += Time.deltaTime;
            yield return null;
        }

        Finish();
    }

    // ───────────── 스프라이트 도우미 ─────────────

    private static void Show(SpriteRenderer sr, Sprite[] frames, int index, float worldWidth, float alpha)
    {
        if (sr == null) return;
        if (frames != null && frames.Length > 0) sr.sprite = frames[index];
        sr.enabled = true;
        var c = Color.white; c.a = alpha; sr.color = c;

        if (sr.sprite != null)
        {
            float w = sr.sprite.bounds.size.x;
            float s = w > 0f ? worldWidth / w : 1f;
            sr.transform.localScale = new Vector3(s, s, 1f);
        }
    }

    private static void Animate(SpriteRenderer sr, Sprite[] frames, float t, float fps)
    {
        if (sr == null || frames == null || frames.Length < 2) return;
        sr.sprite = frames[Mathf.FloorToInt(t * fps) % frames.Length];
    }

    // 스프라이트의 아래쪽 끝이 바닥선에 닿도록 놓는다 (피벗이 어디든 상관없이)
    private static void PlaceBottom(SpriteRenderer sr, float x, float groundY)
    {
        if (sr == null || sr.sprite == null) return;
        float s = sr.transform.localScale.y;
        float bottomOffset = sr.sprite.bounds.min.y * s;   // 피벗 기준 아래쪽 끝(보통 음수)
        sr.transform.position = new Vector3(x, groundY - bottomOffset, 0f);
    }

    // ───────────── 정리 ─────────────

    public void Cancel()
    {
        PhaseChanged?.Invoke(Phase.End, transform.position.x, isMain);   // 닭이 조준 자세에서 멈춰 있지 않게
        runner = null;
        if (routine != null) { StopCoroutine(routine); routine = null; }
        ReleaseSelf();
    }

    private void Finish()
    {
        PhaseChanged?.Invoke(Phase.End, transform.position.x, isMain);
        runner?.Unregister(this);
        runner = null;
        routine = null;
        ReleaseSelf();
    }

    private void ReleaseSelf()
    {
        if (release != null) release(this);
        else Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.6f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * hitHeight * 0.5f, new Vector3(hitWidth, hitHeight, 0f));
    }
}