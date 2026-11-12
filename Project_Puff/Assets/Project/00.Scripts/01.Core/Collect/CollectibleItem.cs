using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 맵에 놓는 수집품. 프리팹 구성: SpriteRenderer + Trigger Collider2D + 이 컴포넌트.
///  - 처음 먹는 수집품: 사라지고 '임시 보유'가 됨 (스테이지를 클리어해야 도감에 영구 저장)
///  - 이미 도감에 있는 수집품: 흐리게 보이고, 닿아도 아무 일도 일어나지 않음
///  - 죽어서 체크포인트로 돌아가도 이번 판에 먹은 것은 유지됨. 씬이 다시 불리면 임시 보유분은 사라지고 다시 나타남.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class CollectibleItem : MonoBehaviour
{
    [SerializeField] private CollectibleData data;

    [Header("둥둥 떠다니기")]
    [SerializeField] private float bobHeight = 0.12f;
    [SerializeField] private float bobSpeed = 2f;

    [Header("이미 가진 수집품")]
    [Range(0f, 1f)] [SerializeField] private float ownedAlpha = 0.35f;

    [Header("획득")]
    [Tooltip("먹었을 때 이펙트 프리팹 (선택)")]
    [SerializeField] private OneShotEffect pickupEffectPrefab;
    [SerializeField] private float pickupEffectWidth = 1.4f;
    [Tooltip("효과음 등을 연결하는 용도")]
    [SerializeField] private UnityEvent onCollected;

    private SpriteRenderer sr;
    private Collider2D col;
    private Vector3 basePos;
    private float phase;
    private bool owned;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
    }

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Start()
    {
        basePos = transform.position;
        phase = Random.value * Mathf.PI * 2f;

        if (data == null) { Debug.LogWarning("[CollectibleItem] Data가 비어 있습니다.", this); return; }
        if (data.sprite != null) sr.sprite = data.sprite;

        owned = CollectibleProgress.IsOwned(data.id);
        if (owned)
        {
            var c = sr.color; c.a = ownedAlpha; sr.color = c;   // 이미 가진 것: 흐릿하게
        }
        else if (CollectibleProgress.IsPending(data.id))
        {
            Hide();                                              // 이번 판에 이미 먹음
        }
    }

    private void Update()
    {
        if (!sr.enabled) return;
        transform.position = basePos + Vector3.up * (Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (data == null || owned || !sr.enabled) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;

        // 이미 먹은 것이면 아무 일도 없음
        if (!CollectibleProgress.TryPickUp(data.id)) return;

        if (pickupEffectPrefab != null)
            Instantiate(pickupEffectPrefab).Play(transform.position, false, pickupEffectWidth);
        onCollected?.Invoke();
        Hide();
    }

    private void Hide()
    {
        if (sr != null) sr.enabled = false;
        if (col != null) col.enabled = false;
    }
}
