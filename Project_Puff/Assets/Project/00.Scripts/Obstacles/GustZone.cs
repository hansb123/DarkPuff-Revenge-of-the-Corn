using UnityEngine;

/// <summary>
/// 돌풍 구역(3챕터 함정). 트리거 영역 안의 플레이어를 좌우로 밀어낸다.
/// 바람은 "잠잠함 → 서서히 강해짐(예고) → 돌풍 → 서서히 약해짐"을 반복한다. 예고 구간에 바람 줄기가 먼저 나타난다.
/// 단단해지기(C) 중에는 거의 안 밀린다(Harden Resist). 굳혔다가 풀고 이동하는 리듬이 된다.
/// 플레이어 이동이 매 프레임 속도를 덮어쓰기 때문에 속도가 아니라 위치를 직접 조금씩 민다(벽/바닥 옆은 막힘).
/// 구성: 이 오브젝트 = BoxCollider2D(Is Trigger) + 바람 줄기 SpriteRenderer(Draw Mode = Tiled, 자동 설정)
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class GustZone : MonoBehaviour
{
    [Header("영역 (유닛)")]
    [SerializeField] private Vector2 size = new Vector2(10f, 5f);

    [Header("바람")]
    [Tooltip("1 = 오른쪽으로 분다, -1 = 왼쪽으로 분다")]
    [SerializeField] private int direction = 1;
    [Tooltip("돌풍이 가장 셀 때 미는 속도 (유닛/초). 플레이어 이동속도보다 작으면 맞서 걸어갈 수 있고, 크면 밀려남")]
    [SerializeField] private float pushSpeed = 5f;
    [Tooltip("공중에서 받는 힘 배율. 1보다 크면 점프 중에 더 멀리 날아감")]
    [SerializeField] private float airMultiplier = 1.2f;
    [Tooltip("단단해지기 중 받는 힘 배율. 0 = 전혀 안 밀림")]
    [SerializeField, Range(0f, 1f)] private float hardenResist = 0f;

    [Header("주기")]
    [Tooltip("항상 불면 켜기 (주기 무시, 늘 최대 세기)")]
    [SerializeField] private bool constant = false;
    [SerializeField] private float calmTime = 2f;
    [Tooltip("약함 → 강함으로 올라가는 시간. 이 구간이 '예고'")]
    [SerializeField] private float rampTime = 0.8f;
    [SerializeField] private float gustTime = 2.2f;
    [Tooltip("켜면 돌풍이 한 번 끝날 때마다 바람 방향이 반대로 바뀜")]
    [SerializeField] private bool alternateDirection = false;
    [Tooltip("여러 구역의 주기를 어긋나게 하고 싶을 때 (초)")]
    [SerializeField] private float timeOffset = 0f;

    [Header("벽 체크 (밀려서 벽을 뚫지 않게)")]
    [SerializeField] private LayerMask wallLayer;

    [Header("바람 줄기 표시")]
    [Tooltip("Draw Mode = Tiled 인 SpriteRenderer. 보통 이 오브젝트의 것")]
    [SerializeField] private SpriteRenderer streamRenderer;
    [Tooltip("wind_1 ~ 3")]
    [SerializeField] private Sprite[] streamFrames;
    [SerializeField] private float streamFps = 8f;
    [Tooltip("바람이 셀 때 줄기의 최대 불투명도")]
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.85f;
    [Tooltip("바람이 없을 때도 이만큼은 희미하게 보임 (0이면 완전히 사라짐)")]
    [SerializeField, Range(0f, 0.5f)] private float minAlpha = 0f;

    [Header("바람 소리 (선택)")]
    [Tooltip("Loop 가 켜진 AudioSource")]
    [SerializeField] private AudioSource windLoop;
    [SerializeField] private float windVolume = 0.6f;

    private BoxCollider2D box;
    private ContactFilter2D wallFilter = new ContactFilter2D();
    private readonly RaycastHit2D[] hits = new RaycastHit2D[4];

    /// <summary>지금 바람 세기 0~1</summary>
    public float Strength { get; private set; }
    /// <summary>지금 부는 방향 (1 오른쪽, -1 왼쪽)</summary>
    public int CurrentDirection { get; private set; } = 1;

    private void Reset()
    {
        box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;
        streamRenderer = GetComponent<SpriteRenderer>();
        SyncSize();
    }

    private void Awake()
    {
        box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;
        wallFilter.useLayerMask = true;
        wallFilter.layerMask = wallLayer;
        wallFilter.useTriggers = false;
        SyncSize();
        if (windLoop != null) { windLoop.loop = true; windLoop.volume = 0f; }
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            if (box == null) box = GetComponent<BoxCollider2D>();
            SyncSize();
        };
#endif
    }

    private void SyncSize()
    {
        if (box == null) return;
        box.offset = Vector2.zero;
        box.size = size;
        if (streamRenderer != null)
        {
            streamRenderer.drawMode = SpriteDrawMode.Tiled;
            streamRenderer.tileMode = SpriteTileMode.Continuous;
            streamRenderer.size = size;
        }
    }

    // ---------------- 바람 세기 계산 ----------------

    private void ComputeWind()
    {
        if (constant)
        {
            Strength = 1f;
            CurrentDirection = direction >= 0 ? 1 : -1;
            return;
        }

        float cycle = calmTime + rampTime + gustTime + rampTime;
        if (cycle < 0.01f) { Strength = 0f; return; }

        float t = Time.time + timeOffset;
        int index = Mathf.FloorToInt(t / cycle);
        float p = t - index * cycle;

        if (p < calmTime) Strength = 0f;
        else if (p < calmTime + rampTime) Strength = Mathf.SmoothStep(0f, 1f, (p - calmTime) / Mathf.Max(0.01f, rampTime));
        else if (p < calmTime + rampTime + gustTime) Strength = 1f;
        else Strength = Mathf.SmoothStep(1f, 0f, (p - calmTime - rampTime - gustTime) / Mathf.Max(0.01f, rampTime));

        int baseDir = direction >= 0 ? 1 : -1;
        CurrentDirection = (alternateDirection && index % 2 == 1) ? -baseDir : baseDir;
    }

    private void Update()
    {
        ComputeWind();

        if (streamRenderer != null)
        {
            if (streamFrames != null && streamFrames.Length > 0)
                streamRenderer.sprite = streamFrames[(int)(Time.time * streamFps) % streamFrames.Length];
            streamRenderer.flipX = CurrentDirection < 0;
            var c = streamRenderer.color;
            c.a = Mathf.Lerp(minAlpha, maxAlpha, Strength);
            streamRenderer.color = c;
        }

        if (windLoop != null)
        {
            float target = windVolume * Strength;
            windLoop.volume = Mathf.MoveTowards(windLoop.volume, target * GameAudioSettings.SfxVolume, Time.deltaTime * 2f);
            if (target > 0.01f && !windLoop.isPlaying) windLoop.Play();
            else if (target <= 0.01f && windLoop.isPlaying && windLoop.volume <= 0.001f) windLoop.Stop();
        }
    }

    // ---------------- 밀기 ----------------

    private int lastStep = -1;

    private void OnTriggerStay2D(Collider2D other)
    {
        if (Strength <= 0.001f) return;

        var rb = other.attachedRigidbody;
        if (rb == null) return;
        var player = rb.GetComponent<PlayerController>();
        if (player == null || player.IsDead) return;

        int step = Mathf.RoundToInt(Time.fixedTime / Time.fixedDeltaTime);
        if (step == lastStep) return;           // 콜라이더가 여러 개여도 한 스텝에 한 번
        lastStep = step;

        float mul = 1f;
        var harden = rb.GetComponent<HardenAbility>();
        if (harden != null && harden.IsActive) mul = hardenResist;
        if (!player.IsGrounded) mul *= airMultiplier;
        if (mul <= 0f) return;

        float dist = pushSpeed * Strength * mul * Time.fixedDeltaTime;
        Vector2 dir = Vector2.right * CurrentDirection;

        // 벽이 바로 앞이면 그만큼만 (벽을 뚫고 가지 않음)
        if (wallLayer.value != 0)
        {
            int n = rb.Cast(dir, wallFilter, hits, dist + 0.02f);
            for (int i = 0; i < n; i++) dist = Mathf.Min(dist, Mathf.Max(0f, hits[i].distance - 0.01f));
        }
        if (dist <= 0f) return;

        rb.position += dir * dist;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.9f);
        Gizmos.DrawWireCube(transform.position, size);
        Vector3 c = transform.position;
        float d = direction >= 0 ? 1f : -1f;
        for (int i = -1; i <= 1; i++)
        {
            Vector3 a = c + new Vector3(-d * size.x * 0.25f, i * size.y * 0.2f);
            Vector3 b = c + new Vector3(d * size.x * 0.25f, i * size.y * 0.2f);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, b + new Vector3(-d * 0.4f, 0.25f));
            Gizmos.DrawLine(b, b + new Vector3(-d * 0.4f, -0.25f));
        }
    }
}
