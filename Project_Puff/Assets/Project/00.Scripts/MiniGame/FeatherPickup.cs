using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 닭 깃털. 팔랑팔랑 흔들리며 떨어지고, 바닥에 내려앉아 반짝이다가 플레이어가 닿으면 활공 능력이 해금된다.
/// 프리팹 구성: SpriteRenderer(feather_item_1) + Trigger Collider2D + 이 컴포넌트.
/// ChickenOutroSequence가 닭이 끌려갈 때 Launch()로 떨어뜨린다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class FeatherPickup : MonoBehaviour
{
    [Header("표시")]
    [Tooltip("feather_item_1~2 (번갈아 반짝)")]
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float fps = 3f;

    [Header("낙하")]
    [SerializeField] private float fallSpeed = 1.6f;
    [Tooltip("좌우로 흔들리는 폭 / 빠르기")]
    [SerializeField] private float swayAmplitude = 1.1f;
    [SerializeField] private float swayFrequency = 1.8f;
    [Tooltip("흔들릴 때 기울어지는 각도")]
    [SerializeField] private float tiltDegrees = 28f;
    [Tooltip("스프라이트 중심이 바닥에서 떨어져 있는 높이. 작을수록 더 아래에 내려앉음")]
    [SerializeField] private float heightAboveGround = 0.25f;
    [Tooltip("지정하면 떨어지는 위치에서 아래로 쏴서 실제 땅(타일) 높이에 내려앉음. 비우면 Launch에 넘어온 바닥 높이를 그대로 사용")]
    [SerializeField] private LayerMask groundLayer;

    [Header("바닥에서")]
    [SerializeField] private float bobHeight = 0.12f;
    [SerializeField] private float bobSpeed = 2.4f;

    [Header("획득")]
    [SerializeField] private OneShotEffect pickupEffectPrefab;
    [SerializeField] private float pickupEffectWidth = 2f;
    [Tooltip("먹으면 '활공 해금!' 배너를 보여줄 팝업. 비워두면 씬에서 자동으로 찾음")]
    [SerializeField] private AbilityUnlockPopup popup;
    [SerializeField] private UnityEvent onCollected;

    /// <summary>깃털을 먹었을 때 (씬의 다른 오브젝트가 구독: 문 해금 등)</summary>
    public static event System.Action Collected;

    private SpriteRenderer sr;
    private float groundY;
    private float x0, restY;
    private float t0;
    private bool landed, collected;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        GetComponent<Collider2D>().isTrigger = true;
        if (frames != null && frames.Length > 0) sr.sprite = frames[0];
    }

    public void Launch(float x, float startY, float ground)
    {
        x0 = x;
        groundY = ground;
        if (groundLayer.value != 0)
        {
            // 닭이 서 있던 높이가 아니라 이 자리 아래의 실제 땅을 찾는다
            var hit = Physics2D.Raycast(new Vector2(x, startY), Vector2.down, 60f, groundLayer);
            if (hit.collider != null) groundY = hit.point.y;
        }
        restY = groundY + heightAboveGround;
        t0 = Time.time;
        landed = false;
        transform.position = new Vector3(x, Mathf.Max(startY, restY), 0f);
        enabled = true;
    }

    private void Update()
    {
        float t = Time.time - t0;
        if (frames != null && frames.Length > 0) sr.sprite = frames[Mathf.FloorToInt(Time.time * fps) % frames.Length];

        if (!landed)
        {
            float y = transform.position.y - fallSpeed * Time.deltaTime;
            float sway = Mathf.Sin(t * swayFrequency);
            float xx = x0 + sway * swayAmplitude;
            if (y <= restY)
            {
                y = restY; landed = true;
                transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            }
            else
            {
                // 흔들리는 방향으로 기울어짐 (속도 방향을 따라)
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Cos(t * swayFrequency) * tiltDegrees);
            }
            transform.position = new Vector3(xx, y, 0f);
        }
        else
        {
            float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(transform.position.x, restY + bob, 0f);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;

        collected = true;
        AbilityProgress.UnlockGlide();
        GameSfx.Play(GameSfxId.FeatherGet);
        if (pickupEffectPrefab != null)
            Instantiate(pickupEffectPrefab).Play(transform.position, false, pickupEffectWidth);

        if (popup == null) popup = FindFirstObjectByType<AbilityUnlockPopup>(FindObjectsInactive.Include);
        if (popup != null) popup.Show();

        onCollected?.Invoke();
        Collected?.Invoke();
        Destroy(gameObject);
    }
}