using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 컨베이어 벨트. 위에 서 있는 플레이어를 한 방향으로 밀어 옮긴다.
/// 구성: 이 오브젝트 = BoxCollider2D(Is Trigger 끔, Layer = Ground → 바닥 역할) + SpriteRenderer(conveyor_1, Draw Mode = Tiled).
/// - Speed 가 양수면 오른쪽, 음수면 왼쪽. 플레이어 입력 이동에 더해져서 같이 움직인다.
/// - Reversing 을 켜면 일정 시간마다 멈췄다가 반대로 돈다 (멈추는 동안이 예고).
/// - 이동은 Rigidbody 위치를 직접 옮기므로 플레이어 스크립트를 고칠 필요가 없다. 벽에 막히면 거기서 멈춘다.
/// 스프라이트는 conveyor_1~4 (오른쪽으로 갈 때 1→2→3→4). 끝 마개 conveyor_cap_L/R 은 선택.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class ConveyorBelt : MonoBehaviour
{
    [Header("크기 (유닛)")]
    [Tooltip("가로 칸 수. 콜라이더와 스프라이트 크기가 자동으로 맞춰진다")]
    [SerializeField] private float width = 4f;
    [Tooltip("스프라이트 윗면까지의 여백 (conveyor 그림은 8px / 64 = 0.125)")]
    [SerializeField] private float topInset = 0.125f;
    [Tooltip("콜라이더 높이 (그림 윗면부터 아래 하우징 끝까지)")]
    [SerializeField] private float colliderHeight = 0.5625f;

    [Header("움직임")]
    [Tooltip("초당 이동 거리. +오른쪽 / -왼쪽")]
    [SerializeField] private float speed = 3f;
    [Tooltip("켜면 주기적으로 멈췄다가 방향이 바뀐다")]
    [SerializeField] private bool reversing;
    [SerializeField] private float reverseInterval = 3f;
    [Tooltip("방향을 바꾸기 전 멈추는 시간 (초)")]
    [SerializeField] private float pauseTime = 0.6f;
    [Tooltip("플레이어가 벽에 밀리는지 검사할 레이어 (Ground)")]
    [SerializeField] private LayerMask obstacleLayer;

    [Header("표시")]
    [SerializeField] private SpriteRenderer beltRenderer;
    [Tooltip("conveyor_1 ~ 4")]
    [SerializeField] private Sprite[] frames;
    [SerializeField] private SpriteRenderer capLeft;
    [SerializeField] private SpriteRenderer capRight;
    [Tooltip("움직이는 동안 반복 재생할 소리 (선택)")]
    [SerializeField] private AudioSource loopSound;
    [SerializeField] private float loopVolume = 0.35f;

    private const float FrameStep = 0.0625f;   // 프레임 하나가 그림을 밀어내는 거리 (4px / 64)
    private BoxCollider2D box;
    private float animPos;
    private float phaseTimer;
    private int dirSign = 1;
    private float currentSpeed;
    private ContactFilter2D blockRight, blockLeft;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[4];
    private readonly Collider2D[] overlaps = new Collider2D[8];

    public float CurrentSpeed => currentSpeed;

    private void Reset()
    {
        box = GetComponent<BoxCollider2D>();
        box.isTrigger = false;
        beltRenderer = GetComponent<SpriteRenderer>();
        Sync();
    }

    private void Awake()
    {
        box = GetComponent<BoxCollider2D>();
        Sync();

        blockRight = new ContactFilter2D { useLayerMask = true, layerMask = obstacleLayer, useTriggers = false };
        blockRight.SetNormalAngle(135f, 225f);   // 오른쪽으로 갈 때 막는 건 왼쪽을 향한 면
        blockLeft = new ContactFilter2D { useLayerMask = true, layerMask = obstacleLayer, useTriggers = false };
        blockLeft.SetNormalAngle(-45f, 45f);

        if (loopSound != null) { loopSound.loop = true; loopSound.volume = 0f; }
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        // OnValidate 안에서 SpriteRenderer 를 건드리면 SendMessage 경고가 나므로 한 박자 늦춰서 적용
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            if (box == null) box = GetComponent<BoxCollider2D>();
            Sync();
        };
#endif
    }

    private void Sync()
    {
        if (box == null) return;
        box.size = new Vector2(width, colliderHeight);
        box.offset = new Vector2(0f, 0.5f - topInset - colliderHeight * 0.5f);
        if (beltRenderer != null)
        {
            beltRenderer.drawMode = SpriteDrawMode.Tiled;
            beltRenderer.tileMode = SpriteTileMode.Continuous;
            beltRenderer.size = new Vector2(width, 1f);
        }
        if (capLeft != null) capLeft.transform.localPosition = new Vector3(-width * 0.5f + 0.5f, 0f, 0f);
        if (capRight != null) capRight.transform.localPosition = new Vector3(width * 0.5f - 0.5f, 0f, 0f);
    }

    private void FixedUpdate()
    {
        UpdateSpeed(Time.fixedDeltaTime);
        if (Mathf.Abs(currentSpeed) < 0.001f) return;

        Vector2 center = (Vector2)transform.position + new Vector2(0f, 0.5f - topInset);
        var size = new Vector2(width * transform.lossyScale.x - 0.1f, 0.14f);
        int count = Physics2D.OverlapBoxNonAlloc(center + Vector2.up * 0.07f, size, 0f, overlaps);
        Rigidbody2D carried = null;
        for (int i = 0; i < count; i++)
        {
            var col = overlaps[i];
            if (col == null || col.isTrigger) continue;
            var player = col.GetComponentInParent<PlayerController>();
            if (player == null || player.IsDead || !player.IsGrounded) continue;
            var rb = player.Rb;
            if (rb.linearVelocity.y > 0.5f) continue;
            carried = rb;
            break;
        }
        if (carried == null) return;

        float dx = currentSpeed * Time.fixedDeltaTime;
        var filter = dx > 0f ? blockRight : blockLeft;
        float dist = Mathf.Abs(dx);
        int n = carried.Cast(new Vector2(Mathf.Sign(dx), 0f), filter, hits, dist + 0.02f);
        if (n > 0)
        {
            float free = Mathf.Max(0f, hits[0].distance - 0.02f);
            dist = Mathf.Min(dist, free);
        }
        carried.position += new Vector2(Mathf.Sign(dx) * dist, 0f);
    }

    private void UpdateSpeed(float dt)
    {
        if (!reversing) { currentSpeed = speed; return; }

        phaseTimer += dt;
        float cycle = reverseInterval + pauseTime;
        if (phaseTimer >= cycle) { phaseTimer -= cycle; dirSign = -dirSign; }
        currentSpeed = phaseTimer < reverseInterval ? speed * dirSign : 0f;
    }

    private void Update()
    {
        animPos += currentSpeed * Time.deltaTime / FrameStep;
        if (beltRenderer != null && frames != null && frames.Length > 0)
        {
            int f = Mathf.FloorToInt(animPos) % frames.Length;
            if (f < 0) f += frames.Length;
            beltRenderer.sprite = frames[f];
        }
        if (loopSound != null)
        {
            bool running = Mathf.Abs(currentSpeed) > 0.01f;
            loopSound.volume = Mathf.MoveTowards(loopSound.volume, running ? loopVolume : 0f, Time.deltaTime * 2f);
            if (running && !loopSound.isPlaying) loopSound.Play();
            else if (!running && loopSound.isPlaying && loopSound.volume <= 0.001f) loopSound.Stop();
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        Vector3 c = transform.position + new Vector3(0f, 0.5f - topInset + 0.3f, 0f);
        float dir = Application.isPlaying ? Mathf.Sign(currentSpeed == 0f ? 1f : currentSpeed) : Mathf.Sign(speed == 0f ? 1f : speed);
        Gizmos.DrawLine(c - Vector3.right * width * 0.4f * dir, c + Vector3.right * width * 0.4f * dir);
        Gizmos.DrawLine(c + Vector3.right * width * 0.4f * dir, c + new Vector3(width * 0.4f * dir - 0.25f * dir, 0.2f, 0f));
        Gizmos.DrawLine(c + Vector3.right * width * 0.4f * dir, c + new Vector3(width * 0.4f * dir - 0.25f * dir, -0.2f, 0f));
    }
}