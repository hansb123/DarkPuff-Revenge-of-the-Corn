using UnityEngine;

/// <summary>
/// 갇힘 방지 안전망. 플레이어의 몸 중심이 지형(땅/벽 콜라이더) 안에 들어간 채로 일정 시간 지나면,
/// 마지막으로 안전하게 땅을 밟았던 위치로 되돌린다. 원인과 상관없이(콜라이더가 겹쳐서 켜짐, 빠른 낙하 관통 등)
/// 플레이어가 지형 안에 끼는 경우를 구해 준다.
/// 플레이어 오브젝트에 붙이고, Solid Layers에 땅/벽 레이어(플레이어가 서는 레이어)를 지정한다.
///  - 트리거 콜라이더와 한 방향 발판(Platform Effector)은 무시한다 (위로 뚫고 점프하는 경우 오작동 방지)
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerStuckGuard : MonoBehaviour
{
    [Tooltip("땅/벽 레이어. PlayerController의 Ground Layer와 같게 지정")]
    [SerializeField] private LayerMask solidLayers;
    [Tooltip("몸 중심이 지형 안에 이 시간(초) 이상 있으면 갇힌 것으로 판단")]
    [SerializeField] private float stuckTime = 0.3f;
    [Tooltip("안전 위치를 기록하는 간격 (초)")]
    [SerializeField] private float recordInterval = 0.25f;

    private Rigidbody2D rb;
    private Collider2D body;
    private PlayerController controller;
    private PlayerHealth health;
    private Vector2 lastSafe;
    private bool hasSafe;
    private float stuckTimer, recordTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        body = GetComponent<Collider2D>();
        controller = GetComponent<PlayerController>();
        health = GetComponent<PlayerHealth>();
        lastSafe = rb.position;
        if (solidLayers.value == 0)
            Debug.LogWarning("[PlayerStuckGuard] Solid Layers가 비어 있습니다. 땅/벽 레이어를 지정하세요.", this);
    }

    private void FixedUpdate()
    {
        if (health != null && health.IsDead) { stuckTimer = 0f; return; }
        if (solidLayers.value == 0) return;

        Vector2 center = body != null ? (Vector2)body.bounds.center : rb.position;
        bool inside = IsInsideSolid(center);

        if (inside)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer >= stuckTime) Rescue();
            return;
        }

        stuckTimer = 0f;

        // 땅에 서 있고 지형과 겹치지 않을 때만 안전 위치로 기록
        recordTimer += Time.fixedDeltaTime;
        if (recordTimer >= recordInterval && controller != null && controller.IsGrounded)
        {
            recordTimer = 0f;
            lastSafe = rb.position;
            hasSafe = true;
        }
    }

    private bool IsInsideSolid(Vector2 point)
    {
        var hits = Physics2D.OverlapPointAll(point, solidLayers);
        foreach (var h in hits)
        {
            if (h == null || h.isTrigger || h.usedByEffector) continue;   // 트리거/한 방향 발판은 제외
            if (h.attachedRigidbody == rb) continue;                      // 내 몸은 제외
            return true;
        }
        return false;
    }

    private void Rescue()
    {
        stuckTimer = 0f;
        Vector2 target = lastSafe;

        if (!hasSafe)
        {
            // 기록이 없으면 위쪽으로 올라가며 비어 있는 첫 자리를 찾는다
            target = rb.position;
            for (int i = 1; i <= 80; i++)
            {
                var p = rb.position + Vector2.up * (0.25f * i);
                if (!IsInsideSolid(p)) { target = p + Vector2.up * 0.3f; break; }
            }
        }

        rb.position = target;
        transform.position = target;
        rb.linearVelocity = Vector2.zero;
        Debug.Log($"[PlayerStuckGuard] 지형 안에 갇혀서 {target}로 되돌렸습니다.", this);
    }
}