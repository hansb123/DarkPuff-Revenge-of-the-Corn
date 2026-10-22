using System.Collections;
using UnityEngine;

/// <summary>
/// 하늘에서 떨어지는 회복템(하트 뻥튀기). HealDropSpawner가 만들어 준다.
/// 떨어지는 동안 바닥에 그림자가 점점 커지며 어디에 떨어질지 알려 주고, 착지하면 통통 튀고,
/// 일정 시간이 지나면 깜빡이다가 사라진다. 플레이어가 닿으면 체력 회복.
/// 프리팹 구성: SpriteRenderer(item_heal_1) + Trigger Collider2D + 이 컴포넌트, 자식으로 그림자 SpriteRenderer(heal_shadow).
/// 체력이 가득 찬 상태에서는 먹어지지 않고 그대로 남는다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class HealDrop : MonoBehaviour
{
    [Header("표시")]
    [Tooltip("item_heal_1~2 (번갈아 반짝)")]
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float fps = 4f;
    [Tooltip("바닥 그림자 (자식 SpriteRenderer). 월드에서 바닥에 놓임")]
    [SerializeField] private Transform shadow;
    [SerializeField] private float shadowMinScale = 0.4f;
    [SerializeField] private float shadowMaxScale = 1.2f;

    [Header("낙하")]
    [SerializeField] private float fallSpeed = 6f;
    [Tooltip("착지 때 튀어 오르는 높이")]
    [SerializeField] private float bounceHeight = 0.5f;
    [Tooltip("아이템 스프라이트의 중심이 바닥에서 떨어져 있는 높이 (피벗이 중앙이면 스프라이트 반 높이)")]
    [SerializeField] private float heightAboveGround = 0.6f;

    [Header("회복")]
    [SerializeField] private int healAmount = 1;
    [SerializeField] private OneShotEffect pickupEffectPrefab;
    [SerializeField] private float pickupEffectWidth = 1.4f;

    [Header("바닥에서 머무는 시간")]
    [SerializeField] private float groundLife = 7f;
    [Tooltip("사라지기 이 시간(초) 전부터 깜빡임")]
    [SerializeField] private float blinkTime = 2f;

    private SpriteRenderer sr;
    private float groundY;
    private bool landed, done;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        GetComponent<Collider2D>().isTrigger = true;
    }

    /// <summary>spawner가 호출. x,시작 높이,바닥 높이</summary>
    public void Launch(float x, float startY, float ground)
    {
        groundY = ground;
        done = false; landed = false;
        transform.position = new Vector3(x, startY, 0f);
        if (shadow != null) { shadow.SetParent(null, true); shadow.position = new Vector3(x, groundY, 0f); }
        StartCoroutine(Life(startY));
    }

    private IEnumerator Life(float startY)
    {
        GameSfx.Play(GameSfxId.HealDrop);
        float restY = groundY + heightAboveGround;
        float frameT = 0f;

        // 낙하
        while (transform.position.y > restY)
        {
            var p = transform.position;
            p.y = Mathf.Max(restY, p.y - fallSpeed * Time.deltaTime);
            transform.position = p;
            if (shadow != null)
            {
                float k = 1f - Mathf.InverseLerp(restY, startY, p.y);
                float s = Mathf.Lerp(shadowMinScale, shadowMaxScale, k);
                shadow.localScale = new Vector3(s, s, 1f);
            }
            frameT += Time.deltaTime; Animate(frameT);
            yield return null;
        }

        // 착지 + 통통
        landed = true;
        GameSfx.Play(GameSfxId.HealLand);
        if (shadow != null) shadow.localScale = Vector3.one * shadowMaxScale;
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            float k = t / 0.35f;
            var p = transform.position; p.y = restY + Mathf.Sin(k * Mathf.PI) * bounceHeight * (1f - k);
            transform.position = p;
            frameT += Time.deltaTime; Animate(frameT);
            yield return null;
        }
        transform.position = new Vector3(transform.position.x, restY, 0f);

        // 머물기 → 깜빡 → 사라짐
        float stay = Mathf.Max(0f, groundLife - blinkTime);
        for (float t = 0f; t < stay; t += Time.deltaTime) { frameT += Time.deltaTime; Animate(frameT); yield return null; }
        for (float t = 0f; t < blinkTime; t += Time.deltaTime)
        {
            frameT += Time.deltaTime; Animate(frameT);
            sr.enabled = Mathf.FloorToInt(t * 10f) % 2 == 0;
            yield return null;
        }
        Remove();
    }

    private void Animate(float t)
    {
        if (frames == null || frames.Length == 0) return;
        sr.sprite = frames[Mathf.FloorToInt(t * fps) % frames.Length];
        // 반짝이며 살짝 둥실
        if (landed) transform.localScale = Vector3.one * (1f + Mathf.Sin(t * 6f) * 0.03f);
    }

    private void OnTriggerStay2D(Collider2D other) => TryPickup(other);
    private void OnTriggerEnter2D(Collider2D other) => TryPickup(other);

    private void TryPickup(Collider2D other)
    {
        if (done) return;
        var hp = other.GetComponentInParent<PlayerHealth>();
        if (hp == null || hp.IsDead || hp.Current >= hp.Max) return;

        hp.Heal(healAmount);
        GameSfx.Play(GameSfxId.HealGet);
        if (pickupEffectPrefab != null)
            Instantiate(pickupEffectPrefab).Play(transform.position, false, pickupEffectWidth);

        Remove();
    }

    /// <summary>spawner가 한꺼번에 치울 때도 사용</summary>
    public void Remove()
    {
        if (done) return;
        done = true;
        StopAllCoroutines();
        if (shadow != null) Destroy(shadow.gameObject);
        Destroy(gameObject);
    }
}
