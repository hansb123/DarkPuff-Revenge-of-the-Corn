using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 맵에 놓인 "뻥!" 아이템. 닿으면 특수공격 충전 +1.
/// 이미 가득 찼으면 먹어지지 않고 그대로 남는다 (비워지면 그때 다시 먹을 수 있음).
/// 프리팹 구성: SpriteRenderer + Trigger Collider2D(CircleCollider2D 추천) + 이 컴포넌트.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class PpeongPickup : MonoBehaviour
{
    [Header("표시")]
    [Tooltip("item_ppeong_1~2. 번갈아 재생되어 깜빡임")]
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float fps = 4f;

    [Header("둥둥 떠다니기")]
    [SerializeField] private float bobHeight = 0.12f;
    [SerializeField] private float bobSpeed = 2.2f;

    [Header("획득")]
    [Tooltip("먹었을 때 나오는 이펙트 프리팹 (OneShotEffect, pickup_1~4)")]
    [SerializeField] private OneShotEffect pickupEffectPrefab;
    [SerializeField] private float pickupEffectWidth = 1.2f;
    [Tooltip("효과음 등을 연결하는 용도")]
    [SerializeField] private UnityEvent onCollected;

    private SpriteRenderer sr;
    private Vector3 basePosition;
    private float phase;
    private bool collected;
    private float nextTryTime;
    private const float retryInterval = 0.6f;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Start()
    {
        basePosition = transform.position;
        phase = Random.value * Mathf.PI * 2f;   // 여러 개가 똑같이 움직이지 않게
    }

    private void Update()
    {
        if (collected) return;

        transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight);

        if (frames != null && frames.Length > 0)
            sr.sprite = frames[Mathf.FloorToInt(Time.time * fps + phase) % frames.Length];
    }

    // Stay를 쓰는 이유: 가득 찬 상태로 아이템 위에 서 있다가 충전을 쓰면 바로 먹히게
    private void OnTriggerStay2D(Collider2D other)
    {
        if (collected || Time.time < nextTryTime) return;

        var special = other.GetComponentInParent<PlayerSpecialAttack>();
        if (special == null) return;

        // 가득 찼으면 먹지 못함. 위에 서 있는 동안 HUD가 계속 흔들리지 않게 잠깐 쉬고 다시 시도
        if (!special.TryAddCharge())
        {
            nextTryTime = Time.time + retryInterval;
            return;
        }

        Collect();
    }

    private void Collect()
    {
        collected = true;

        if (pickupEffectPrefab != null)
            Instantiate(pickupEffectPrefab).Play(transform.position, false, pickupEffectWidth);

        GameSfx.Play(GameSfxId.ItemPpeongGet);
        onCollected?.Invoke();
        Destroy(gameObject);
    }
}
