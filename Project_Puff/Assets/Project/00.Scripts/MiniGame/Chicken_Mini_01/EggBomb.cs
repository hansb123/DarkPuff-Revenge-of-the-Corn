using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 하늘에서 떨어지는 달걀 한 개. 바닥에 그림자가 먼저 나타나고(예고), 알이 떨어져 닿는 순간 그 자리 반경에 있는 플레이어가 데미지를 받는다.
/// 닿으면 터지는 이펙트를 내고 잠깐 웅덩이를 남긴 뒤 풀로 돌아간다.
/// 프리팹 구성: 빈 루트 + 이 컴포넌트, 자식으로 Shadow / Body / Puddle 각각 SpriteRenderer (콜라이더 필요 없음).
/// </summary>
public class EggBomb : MonoBehaviour, IPatternHazard
{
    [Header("스프라이트 (자식 SpriteRenderer)")]
    [SerializeField] private SpriteRenderer shadow;
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private SpriteRenderer puddle;
    [Tooltip("egg_fall_1~2. 떨어지는 동안 번갈아 재생")]
    [SerializeField] private Sprite[] bodyFrames;
    [Tooltip("egg_shadow_1~2")]
    [SerializeField] private Sprite[] shadowFrames;
    [Tooltip("egg_puddle_1~2. 둘 중 하나가 랜덤으로 남음")]
    [SerializeField] private Sprite[] puddleFrames;
    [SerializeField] private float bodyFps = 8f;

    [Header("크기 (월드 단위, 가로 기준)")]
    [SerializeField] private float shadowWidth = 1.6f;
    [SerializeField] private float puddleWidth = 2.2f;

    [Header("충돌")]
    [Tooltip("알이 닿는 순간 이 반경 안의 플레이어가 맞는다")]
    [SerializeField] private float hitRadius = 0.9f;
    [SerializeField] private int damage = 1;
    [SerializeField] private LayerMask playerLayer;
    [Tooltip("알 스프라이트의 중심이 바닥에서 얼마나 위에 있어야 '닿은' 모양인지")]
    [SerializeField] private float groundOffsetY = 0.35f;

    [Header("터짐")]
    [Tooltip("egg_splat_1~4 를 frames에 넣은 OneShotEffect 프리팹")]
    [SerializeField] private OneShotEffect splatEffectPrefab;
    [SerializeField] private float splatWidth = 2.4f;
    [SerializeField] private float puddleTime = 1.3f;

    private Action<EggBomb> release;
    private PatternRunner runner;
    private Coroutine routine;

    public void SetReleaser(Action<EggBomb> r) => release = r;

    /// <summary>
    /// groundPoint: 알이 떨어질 바닥 위치. spawnHeight: 시작 높이(바닥 기준, 화면 위쪽 밖이면 좋음).
    /// fallTime: 떨어지는 데 걸리는 시간 = 플레이어가 피할 수 있는 예고 시간.
    /// </summary>
    public void Launch(PatternRunner owner, Vector2 groundPoint, float spawnHeight, float fallTime)
    {
        runner = owner;
        runner?.Register(this);

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Fall(groundPoint, spawnHeight, Mathf.Max(0.1f, fallTime)));
    }

    private IEnumerator Fall(Vector2 ground, float height, float fallTime)
    {
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        Setup(shadow, shadowFrames, 0, shadowWidth);
        Setup(body, bodyFrames, 0, 0f);
        if (puddle != null) puddle.enabled = false;

        shadow.transform.position = ground;
        float restY = ground.y + groundOffsetY;
        float topY = ground.y + height;
        body.transform.position = new Vector3(ground.x, topY, 0f);

        GameSfx.PlayAt(GameSfxId.EggFall, ground);

        // 떨어지는 동안: 그림자가 점점 진해지며 커지고, 알이 가속하며 내려온다
        for (float t = 0f; t < fallTime; t += Time.deltaTime)
        {
            float k = t / fallTime;
            body.transform.position = new Vector3(ground.x, Mathf.Lerp(topY, restY, k * k), 0f);
            if (bodyFrames != null && bodyFrames.Length > 1)
                body.sprite = bodyFrames[Mathf.FloorToInt(t * bodyFps) % bodyFrames.Length];

            float s = Mathf.Lerp(0.45f, 1f, k);
            shadow.transform.localScale = new Vector3(s, s, 1f);
            var c = shadow.color; c.a = Mathf.Lerp(0.25f, 0.9f, k); shadow.color = c;
            yield return null;
        }

        // 닿음
        body.enabled = false;
        shadow.enabled = false;

        var hit = Physics2D.OverlapCircle(ground + Vector2.up * groundOffsetY, hitRadius, playerLayer);
        if (hit != null)
        {
            var target = hit.GetComponentInParent<IDamageable>();
            if (target != null) target.TakeDamage(damage, ground);
        }

        GameSfx.PlayAt(GameSfxId.EggSplat, ground);
        if (splatEffectPrefab != null) Instantiate(splatEffectPrefab).Play(ground + Vector2.up * 0.4f, false, splatWidth);

        if (puddle != null && puddleFrames != null && puddleFrames.Length > 0)
        {
            puddle.sprite = puddleFrames[UnityEngine.Random.Range(0, puddleFrames.Length)];
            puddle.transform.position = ground;
            Fit(puddle, puddleWidth);
            puddle.enabled = true;
        }

        yield return new WaitForSeconds(puddleTime);
        Finish();
    }

    private void Setup(SpriteRenderer sr, Sprite[] frames, int index, float width)
    {
        if (sr == null) return;
        if (frames != null && frames.Length > 0) sr.sprite = frames[index];
        sr.enabled = true;
        sr.color = Color.white;
        sr.transform.localScale = Vector3.one;
        if (width > 0f) Fit(sr, width);
    }

    // 스프라이트 가로가 worldWidth 유닛이 되도록 크기를 맞춘다 (이후 localScale 로 키우고 줄임)
    private static void Fit(SpriteRenderer sr, float worldWidth)
    {
        if (sr.sprite == null) return;
        float w = sr.sprite.bounds.size.x;
        float s = w > 0f ? worldWidth / w : 1f;
        sr.transform.localScale = new Vector3(s, s, 1f);
    }

    public void Cancel()
    {
        runner = null;   // 이미 Runner가 목록을 비웠으므로 다시 건드리지 않음
        if (routine != null) { StopCoroutine(routine); routine = null; }
        ReleaseSelf();
    }

    private void Finish()
    {
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
        Gizmos.DrawWireSphere(transform.position + Vector3.up * groundOffsetY, hitRadius);
    }
}
