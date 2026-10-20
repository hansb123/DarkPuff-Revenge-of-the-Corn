using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 닭의 날개짓으로 생긴 돌풍 한 줄기. 무대 한쪽 끝에 방향 화살표 경고가 깜빡이다가(예고),
/// 바닥 가까이를 따라 반대쪽 끝까지 휩쓸고 간다. 바람 높이(Hit Height)보다 높이 점프하면 피할 수 있다 (한 번만 맞음).
/// 프리팹 구성: 빈 루트 + 이 컴포넌트, 자식으로 Warn / Gust 각각 SpriteRenderer (콜라이더 필요 없음).
/// </summary>
public class GustWave : MonoBehaviour, IPatternHazard
{
    [Header("스프라이트 (자식 SpriteRenderer)")]
    [SerializeField] private SpriteRenderer warn;
    [SerializeField] private SpriteRenderer gust;
    [Tooltip("wind_warn_1~2 (오른쪽을 향함. 왼쪽으로 불 때는 자동으로 뒤집힘)")]
    [SerializeField] private Sprite[] warnFrames;
    [Tooltip("wind_gust_1~3 (오른쪽을 향함)")]
    [SerializeField] private Sprite[] gustFrames;
    [SerializeField] private float gustFps = 12f;

    [Header("크기 (월드 단위, 가로 기준)")]
    [SerializeField] private float warnWidth = 2.4f;
    [SerializeField] private float gustWidth = 5f;
    [Tooltip("경고가 무대 가장자리에서 안쪽으로 얼마나 들어와 있을지")]
    [SerializeField] private float warnInset = 1.6f;
    [Tooltip("경고/바람을 바닥에서 얼마나 띄울지")]
    [SerializeField] private float liftY = 0.1f;

    [Header("충돌")]
    [SerializeField] private float hitWidth = 3.2f;
    [Tooltip("이 높이보다 위로 점프하면 피함. 너무 낮으면 못 피하고, 너무 높으면 점프해도 맞음")]
    [SerializeField] private float hitHeight = 1.7f;
    [SerializeField] private int damage = 1;
    [SerializeField] private LayerMask playerLayer;

    /// <summary>바람의 진행 단계. 닭 모션 등이 구독한다 (Phase, 방향 +1 오른쪽/-1 왼쪽)</summary>
    public enum Phase { Start, Blow, End }
    public static event Action<Phase, int> PhaseChanged;

    private Action<GustWave> release;
    private PatternRunner runner;
    private Coroutine routine;
    private int dir;

    public void SetReleaser(Action<GustWave> r) => release = r;

    /// <summary>
    /// dir: +1 이면 왼쪽 끝에서 오른쪽으로, -1 이면 오른쪽 끝에서 왼쪽으로. warnTime: 예고 시간. speed: 바람 속도(유닛/초)
    /// </summary>
    public void Launch(PatternRunner owner, int direction, float leftX, float rightX, float groundY, float warnTime, float speed)
    {
        runner = owner;
        dir = direction >= 0 ? 1 : -1;
        runner?.Register(this);

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Blow(leftX, rightX, groundY, Mathf.Max(0.2f, warnTime), Mathf.Max(1f, speed)));
    }

    private IEnumerator Blow(float leftX, float rightX, float groundY, float warnTime, float speed)
    {
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        float startX = dir > 0 ? leftX : rightX;
        float endX = dir > 0 ? rightX : leftX;
        float y = groundY + liftY;

        if (gust != null) gust.enabled = false;
        Show(warn, warnFrames, warnWidth, dir < 0);
        PlaceBottom(warn, startX + dir * warnInset, y);

        PhaseChanged?.Invoke(Phase.Start, dir);
        GameSfx.Play(GameSfxId.ChickenWingFlap);

        // 1) 예고: 점점 빠르게 깜빡임
        for (float t = 0f; t < warnTime; t += Time.deltaTime)
        {
            float k = t / warnTime;
            if (warnFrames != null && warnFrames.Length > 1)
                warn.sprite = warnFrames[Mathf.FloorToInt(t * Mathf.Lerp(5f, 14f, k)) % warnFrames.Length];
            yield return null;
        }
        warn.enabled = false;

        // 2) 휩쓸기
        PhaseChanged?.Invoke(Phase.Blow, dir);
        GameSfx.Play(GameSfxId.ChickenWindGust);

        float x = startX - dir * gustWidth * 0.5f;   // 화면 밖에서 들어옴
        float stopX = endX + dir * gustWidth * 0.5f;
        bool hitDone = false;
        float elapsed = 0f;

        Show(gust, gustFrames, gustWidth, dir < 0);

        while ((stopX - x) * dir > 0f)
        {
            x += dir * speed * Time.deltaTime;
            elapsed += Time.deltaTime;

            PlaceBottom(gust, x, y);
            if (gustFrames != null && gustFrames.Length > 1)
                gust.sprite = gustFrames[Mathf.FloorToInt(elapsed * gustFps) % gustFrames.Length];

            if (!hitDone)
            {
                var hit = Physics2D.OverlapBox(new Vector2(x, groundY + hitHeight * 0.5f), new Vector2(hitWidth, hitHeight), 0f, playerLayer);
                if (hit != null)
                {
                    var target = hit.GetComponentInParent<IDamageable>();
                    if (target != null) target.TakeDamage(damage, new Vector2(x - dir * 1f, groundY));
                    hitDone = true;
                }
            }
            yield return null;
        }

        Finish();
    }

    // ───────────── 도우미 ─────────────

    private static void Show(SpriteRenderer sr, Sprite[] frames, float worldWidth, bool flipX)
    {
        if (sr == null) return;
        if (frames != null && frames.Length > 0) sr.sprite = frames[0];
        sr.enabled = true;
        sr.flipX = flipX;
        sr.color = Color.white;
        if (sr.sprite != null)
        {
            float w = sr.sprite.bounds.size.x;
            float s = w > 0f ? worldWidth / w : 1f;
            sr.transform.localScale = new Vector3(s, s, 1f);
        }
    }

    // 스프라이트 아래쪽 끝이 y 에 오도록 (피벗과 상관없이)
    private static void PlaceBottom(SpriteRenderer sr, float x, float y)
    {
        if (sr == null || sr.sprite == null) return;
        float bottomOffset = sr.sprite.bounds.min.y * sr.transform.localScale.y;
        sr.transform.position = new Vector3(x, y - bottomOffset, 0f);
    }

    // ───────────── 정리 ─────────────

    public void Cancel()
    {
        PhaseChanged?.Invoke(Phase.End, dir);
        runner = null;
        if (routine != null) { StopCoroutine(routine); routine = null; }
        ReleaseSelf();
    }

    private void Finish()
    {
        PhaseChanged?.Invoke(Phase.End, dir);
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
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.6f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * hitHeight * 0.5f, new Vector3(hitWidth, hitHeight, 0f));
    }
}
