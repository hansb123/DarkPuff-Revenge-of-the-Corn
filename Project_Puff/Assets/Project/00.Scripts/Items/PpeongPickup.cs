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

    [Header("재생성")]
    [Tooltip("먹은 뒤 이 시간(초)이 지나면 다시 나타남. 0이면 다시 나타나지 않음")]
    [SerializeField] private float respawnDelay = 10f;
    [Tooltip("기다리는 동안 흐릿한 아이템 + 둘레에 차오르는 원 게이지를 보여줌. 끄면 그냥 사라졌다가 나타남")]
    [SerializeField] private bool showRespawnGauge = true;
    [Range(0f, 1f)][SerializeField] private float ghostAlpha = 0.25f;
    [SerializeField] private float gaugeRadius = 0.5f;
    [SerializeField] private float gaugeWidth = 0.07f;
    [SerializeField] private Color gaugeColor = new Color(0.08f, 0.08f, 0.08f, 1f);
    [Tooltip("비워 두면 기본 Sprites/Default 사용")]
    [SerializeField] private Material gaugeMaterial;

    private SpriteRenderer sr;
    private Vector3 basePosition;
    private float phase;
    private bool collected;
    private float nextTryTime;
    private const float retryInterval = 0.6f;

    private Collider2D col;

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
        basePosition = transform.position;
        phase = Random.value * Mathf.PI * 2f;   // 여러 개가 똑같이 움직이지 않게

        // 미니게임처럼 "처음부터 다시" 시작할 때(체크포인트를 안 쓰는 모드)만 먹은 아이템이 다시 나타남
        if (RespawnManager.Instance != null) RespawnManager.Instance.RespawnApplied += OnRespawnApplied;
    }

    private void OnDestroy()
    {
        if (RespawnManager.Instance != null) RespawnManager.Instance.RespawnApplied -= OnRespawnApplied;
    }

    private void OnRespawnApplied()
    {
        if (RespawnManager.Instance != null && !RespawnManager.Instance.UseCheckpoints) Restore();
    }

    private Coroutine respawnRoutine;
    private LineRenderer gauge;
    private const int GaugeSegments = 40;

    private System.Collections.IEnumerator RespawnAfterDelay()
    {
        // 일시정지(timeScale 0) 중에는 시간이 가지 않음
        for (float t = 0f; t < respawnDelay; t += Time.deltaTime)
        {
            SetGauge(t / respawnDelay);
            yield return null;
        }
        respawnRoutine = null;
        Restore();
    }

    private void BuildGauge()
    {
        var go = new GameObject("RespawnGauge");
        go.transform.SetParent(transform, false);

        gauge = go.AddComponent<LineRenderer>();
        gauge.useWorldSpace = false;
        gauge.material = gaugeMaterial != null ? gaugeMaterial : new Material(Shader.Find("Sprites/Default"));
        gauge.startColor = gauge.endColor = gaugeColor;
        gauge.widthMultiplier = gaugeWidth;
        gauge.numCapVertices = 4;
        gauge.sortingLayerID = sr.sortingLayerID;
        gauge.sortingOrder = sr.sortingOrder + 1;
        gauge.enabled = false;
    }

    /// <summary>progress 0~1. 12시 방향에서 시계 방향으로 원이 차오름</summary>
    private void SetGauge(float progress)
    {
        if (gauge == null) return;

        progress = Mathf.Clamp01(progress);
        int count = Mathf.Max(2, Mathf.CeilToInt(progress * GaugeSegments) + 1);
        gauge.positionCount = count;
        for (int i = 0; i < count; i++)
        {
            float a = (90f - 360f * progress * i / (count - 1)) * Mathf.Deg2Rad;
            gauge.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * gaugeRadius);
        }
        gauge.enabled = progress > 0.001f;
    }

    private void Restore()
    {
        if (!collected) return;
        if (respawnRoutine != null) { StopCoroutine(respawnRoutine); respawnRoutine = null; }
        collected = false;
        nextTryTime = 0f;
        if (gauge != null) gauge.enabled = false;
        if (sr != null) { sr.enabled = true; sr.color = Color.white; }
        if (col != null) col.enabled = true;
        transform.position = basePosition;
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

        // 파괴하지 않고 숨김: 일정 시간 뒤 / 미니게임이 처음부터 다시 시작할 때 되살리기 위해
        if (col != null) col.enabled = false;

        bool waiting = respawnDelay > 0f;
        if (sr != null)
        {
            if (waiting && showRespawnGauge)
            {
                // 흐릿한 아이템을 남겨 두고 둘레 게이지가 차오름
                sr.enabled = true;
                var c = sr.color; c.a = ghostAlpha; sr.color = c;
                if (gauge == null) BuildGauge();
                SetGauge(0f);
            }
            else sr.enabled = false;
        }

        if (waiting) respawnRoutine = StartCoroutine(RespawnAfterDelay());
    }
}