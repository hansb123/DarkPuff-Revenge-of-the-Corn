using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 메카 닭의 코어에서 튀어나오는 단단해지기 알. 포물선으로 튀어나와 바닥에서 통통 튀다 멈추고, 반짝이며 둥실거린다.
/// 플레이어가 닿으면 단단해지기가 해금된다 (AbilityProgress.UnlockHard).
/// 프리팹: SpriteRenderer(item_hard_egg_1) + Trigger Collider2D + 이 컴포넌트. 스프라이트 피벗은 Bottom Center 권장.
/// MechaOutroSequence 가 Launch()로 던진다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class HardEggPickup : MonoBehaviour
{
    [Header("표시")]
    [Tooltip("item_hard_egg_1~2 (코어가 깜빡)")]
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float fps = 4f;

    [Header("튀어나옴")]
    [SerializeField] private float gravity = 26f;
    [Tooltip("튀어나오는 처음 속도 (위, 옆). 옆 방향은 플레이어 쪽으로 자동")]
    [SerializeField] private Vector2 popVelocity = new Vector2(3.2f, 9f);
    [Tooltip("바닥에 닿을 때 튕기는 비율 (0 = 안 튐)")]
    [Range(0f, 0.8f)][SerializeField] private float bounciness = 0.45f;
    [SerializeField] private int maxBounces = 3;
    [Tooltip("튀는 동안 도는 속도(도/초)")]
    [SerializeField] private float spin = 220f;
    [Tooltip("지정하면 떨어지는 위치 아래의 실제 땅 높이에 내려앉음. 비우면 Launch 에 넘어온 바닥 높이 그대로")]
    [SerializeField] private LayerMask groundLayer;

    [Header("바닥에서")]
    [SerializeField] private float bobHeight = 0.1f;
    [SerializeField] private float bobSpeed = 2.4f;

    [Header("획득")]
    [SerializeField] private OneShotEffect pickupEffectPrefab;
    [SerializeField] private float pickupEffectWidth = 2.4f;
    [Tooltip("먹으면 '단단해지기 해금!' 배너를 보여줄 팝업. 프리팹에서는 비워 두면 된다 — MechaOutroSequence 의 'Unlock Popup' 이 알을 만들 때 넘겨 준다")]
    [SerializeField] private AbilityUnlockPopup popup;
    [SerializeField] private UnityEvent onCollected;

    /// <summary>알을 먹었을 때 (문 해금 등에 구독)</summary>
    public static event System.Action Collected;

    private SpriteRenderer sr;
    private Vector2 vel;
    private float groundY, restY, towardX;
    private int bounces;
    private bool landed, collected, launched;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        GetComponent<Collider2D>().isTrigger = true;
        if (frames != null && frames.Length > 0) sr.sprite = frames[0];
    }

    /// <summary>씬의 해금 팝업을 알에게 넘겨 준다 (프리팹은 씬 오브젝트를 직접 참조할 수 없어서)</summary>
    public void SetPopup(AbilityUnlockPopup p) { if (p != null) popup = p; }

    /// <param name="from">튀어나오는 위치 (코어)</param>
    /// <param name="ground">바닥 높이</param>
    /// <param name="towardPlayerX">알이 굴러갈 방향의 X (보통 플레이어 위치)</param>
    public void Launch(Vector2 from, float ground, float towardPlayerX)
    {
        groundY = ground;
        if (groundLayer.value != 0)
        {
            var hit = Physics2D.Raycast(from, Vector2.down, 60f, groundLayer);
            if (hit.collider != null) groundY = hit.point.y;
        }
        float dir = towardPlayerX >= from.x ? 1f : -1f;
        vel = new Vector2(popVelocity.x * dir, popVelocity.y);
        transform.position = from;
        transform.rotation = Quaternion.identity;
        bounces = 0; landed = false; launched = true;
        restY = groundY;
    }

    private void Update()
    {
        if (frames != null && frames.Length > 0) sr.sprite = frames[Mathf.FloorToInt(Time.time * fps) % frames.Length];
        if (!launched) return;

        if (!landed)
        {
            vel.y -= gravity * Time.deltaTime;
            Vector3 p = transform.position + (Vector3)(vel * Time.deltaTime);
            transform.Rotate(0f, 0f, -Mathf.Sign(vel.x == 0f ? 1f : vel.x) * spin * Time.deltaTime * Mathf.Clamp01(Mathf.Abs(vel.y) / 6f + 0.3f));
            if (p.y <= restY && vel.y < 0f)
            {
                p.y = restY;
                GameSfx.Play(GameSfxId.HardEggLand);
                if (bounces < maxBounces && Mathf.Abs(vel.y) > 2.5f)
                {
                    vel.y = -vel.y * bounciness; vel.x *= 0.6f; bounces++;
                }
                else
                {
                    landed = true; vel = Vector2.zero;
                    transform.rotation = Quaternion.identity;
                }
            }
            transform.position = p;
        }
        else
        {
            float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(transform.position.x, restY + bob, 0f);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected || !launched) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;

        collected = true;
        AbilityProgress.UnlockHard();
        GameSfx.Play(GameSfxId.HardGet);
        if (pickupEffectPrefab != null)
            Instantiate(pickupEffectPrefab).Play(transform.position, false, pickupEffectWidth);
        if (popup != null) popup.Show();

        onCollected?.Invoke();
        Collected?.Invoke();
        Destroy(gameObject);
    }
}