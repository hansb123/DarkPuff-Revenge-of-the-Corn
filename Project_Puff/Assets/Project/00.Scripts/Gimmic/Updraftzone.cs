using UnityEngine;

/// <summary>
/// 상승 기류. 트리거 영역 안에 들어온 플레이어를 위로 띄운다.
/// - 영역 위쪽에 가까워질수록 힘이 약해져서, 꼭대기 근처에서 둥실 떠 있게 된다 (Hover Fade Height).
/// - '활공 중일 때만' 옵션을 켜면 활공 버튼을 누르고 있을 때만 떠오른다 (활공 튜토리얼 구간용).
/// 구성: 이 스크립트가 붙은 오브젝트 = BoxCollider2D(Is Trigger) + 기류 SpriteRenderer(Draw Mode = Tiled).
///       통풍구 그림은 자식 오브젝트(선택)로 두고 Vent Renderer 에 연결.
/// 오브젝트의 중심이 기류 영역의 중심이다. 통풍구 바로 위에 영역의 아래쪽이 오도록 놓는다.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class UpdraftZone : MonoBehaviour
{
    [Header("영역 (유닛)")]
    [Tooltip("가로 x 세로. 콜라이더와 기류 스프라이트 크기가 이 값으로 자동 맞춰진다")]
    [SerializeField] private Vector2 size = new Vector2(1f, 6f);

    [Header("힘")]
    [Tooltip("최대 상승 속도")]
    [SerializeField] private float liftSpeed = 6f;
    [Tooltip("초당 가속량. 중력보다 커야 떠오른다 (기본 중력이면 40~50)")]
    [SerializeField] private float liftAcceleration = 45f;
    [Tooltip("영역 위쪽에서 이 높이 안으로 들어오면 힘이 서서히 약해져서 그 부근에 뜬다. 0 = 약해지지 않음(밖으로 튀어나감)")]
    [SerializeField] private float hoverFadeHeight = 1.5f;
    [Tooltip("켜면 활공 중(점프 버튼을 누르고 있을 때)에만 떠오른다")]
    [SerializeField] private bool onlyWhileGliding = false;

    [Header("기류 표시")]
    [Tooltip("Draw Mode = Tiled 인 SpriteRenderer. 보통 이 오브젝트의 것")]
    [SerializeField] private SpriteRenderer streamRenderer;
    [Tooltip("updraft_1 ~ 4")]
    [SerializeField] private Sprite[] streamFrames;
    [SerializeField] private float streamFps = 10f;

    [Header("통풍구 표시 (선택)")]
    [SerializeField] private SpriteRenderer ventRenderer;
    [Tooltip("vent_1 ~ 4")]
    [SerializeField] private Sprite[] ventFrames;
    [SerializeField] private float ventFps = 12f;

    [Header("바람 소리 (선택)")]
    [Tooltip("Loop 가 켜진 AudioSource. 안에 있는 동안만 들린다")]
    [SerializeField] private AudioSource windLoop;
    [SerializeField] private float windVolume = 0.5f;

    private BoxCollider2D box;
    private float lastLiftTime = -10f;
    private int lastStep = -1;

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
        SyncSize();
        if (windLoop != null) { windLoop.loop = true; windLoop.volume = 0f; }
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        // OnValidate 안에서 SpriteRenderer 를 건드리면 SendMessage 경고가 나므로 한 박자 늦춰서 적용
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

    private void OnTriggerStay2D(Collider2D other)
    {
        var rb = other.attachedRigidbody;
        if (rb == null) return;
        var player = rb.GetComponent<PlayerController>();
        if (player == null || player.IsDead) return;

        // 플레이어 콜라이더가 여러 개여도 물리 한 스텝에 한 번만 적용
        int step = Mathf.RoundToInt(Time.fixedTime / Time.fixedDeltaTime);
        if (step == lastStep) return;
        lastStep = step;

        if (onlyWhileGliding)
        {
            var glide = rb.GetComponent<GlideAbility>();
            if (glide == null || !glide.IsGliding) return;
        }

        float top = box.bounds.max.y;
        float y = other.bounds.center.y;
        float target = liftSpeed;
        if (hoverFadeHeight > 0.01f) target *= Mathf.Clamp01((top - y) / hoverFadeHeight);

        var v = rb.linearVelocity;
        if (v.y < target)
        {
            v.y = Mathf.Min(target, v.y + liftAcceleration * Time.fixedDeltaTime);
            rb.linearVelocity = v;
        }
        lastLiftTime = Time.time;
    }

    private void Update()
    {
        if (streamRenderer != null && streamFrames != null && streamFrames.Length > 0)
            streamRenderer.sprite = streamFrames[(int)(Time.time * streamFps) % streamFrames.Length];
        if (ventRenderer != null && ventFrames != null && ventFrames.Length > 0)
            ventRenderer.sprite = ventFrames[(int)(Time.time * ventFps) % ventFrames.Length];

        if (windLoop != null)
        {
            bool inside = Time.time - lastLiftTime < 0.15f;
            windLoop.volume = Mathf.MoveTowards(windLoop.volume, inside ? windVolume * GameAudioSettings.SfxVolume : 0f, Time.deltaTime * 3f);
            if (inside && !windLoop.isPlaying) windLoop.Play();
            else if (!inside && windLoop.isPlaying && windLoop.volume <= 0.001f) windLoop.Stop();
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.9f);
        Gizmos.DrawWireCube(transform.position, size);
        Vector3 c = transform.position;
        Gizmos.DrawLine(c + Vector3.down * size.y * 0.35f, c + Vector3.up * size.y * 0.35f);
        Gizmos.DrawLine(c + Vector3.up * size.y * 0.35f, c + new Vector3(-0.25f, size.y * 0.25f));
        Gizmos.DrawLine(c + Vector3.up * size.y * 0.35f, c + new Vector3(0.25f, size.y * 0.25f));
    }
}
