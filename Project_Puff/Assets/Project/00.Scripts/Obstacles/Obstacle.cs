using UnityEngine;

/// <summary>
/// 닿으면 플레이어가 다치는 모든 장애물(가시, 톱날, 떨어지는 블록 등)의 기본 컴포넌트.
/// - 일반 데미지(damage) 또는 즉사(instantKill)
/// - 콜라이더가 Is Trigger이면 겹친 동안, 일반(solid) 콜라이더이면 부딪힌 동안 데미지를 준다.
///   (연속 피격은 PlayerHealth의 무적 시간이 막아 준다)
/// - 함정 스크립트(FallingObstacle, PopUpObstacle 등)가 DamageEnabled로 "지금 위험한가"를 켜고 끈다.
/// 플레이어는 PlayerHealth 컴포넌트로 알아보므로 레이어 설정이 필요 없다. 적은 다치지 않는다.
/// </summary>
public class Obstacle : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private int damage = 1;
    [Tooltip("켜면 하트 수와 상관없이 즉사 (낙사 같은 함정, 고양이마리오식 가시)")]
    [SerializeField] private bool instantKill;
    [Tooltip("꺼 두면 위험하지 않은 상태. 함정 스크립트가 시작할 때 이 값을 덮어쓸 수 있음")]
    [SerializeField] private bool damageEnabled = true;

    private Collider2D[] cols;
    private readonly Collider2D[] overlapBuffer = new Collider2D[8];
    private ContactFilter2D filter;

    /// <summary>지금 닿으면 다치는가</summary>
    public bool DamageEnabled
    {
        get => damageEnabled;
        set => damageEnabled = value;
    }

    private void Awake()
    {
        cols = GetComponents<Collider2D>();
        filter = new ContactFilter2D().NoFilter();

        if (cols.Length == 0)
            Debug.LogWarning($"[Obstacle] '{name}'에 Collider2D가 없습니다. 닿는 판정이 생기지 않습니다.", this);
    }

    // Trigger 콜라이더: OnTriggerStay2D는 플레이어 Rigidbody가 잠들면(가만히 서 있으면) 호출되지 않으므로
    // 직접 겹침을 검사한다.
    private void FixedUpdate()
    {
        if (!damageEnabled) return;

        foreach (var col in cols)
        {
            if (col == null || !col.enabled || !col.isTrigger) continue;

            int count = col.Overlap(filter, overlapBuffer);
            for (int i = 0; i < count; i++) Touch(overlapBuffer[i]);
        }
    }

    // Solid 콜라이더
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!damageEnabled) return;
        Touch(collision.collider);
    }

    private void Touch(Collider2D other)
    {
        var health = other.GetComponentInParent<PlayerHealth>();
        if (health == null || health.IsDead) return;

        if (instantKill) health.Kill();
        else health.TakeDamage(damage, transform.position);
    }
}
