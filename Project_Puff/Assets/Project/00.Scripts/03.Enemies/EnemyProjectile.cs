using UnityEngine;

/// <summary>
/// 마늘맨이 던지는 마늘쪽. 직선으로 날아가다 플레이어에 닿으면 데미지, 벽에 닿으면 사라진다.
/// Is Trigger 콜라이더와 Rigidbody2D(Kinematic 또는 Gravity Scale 0)가 필요하다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 6f;
    [SerializeField] private int damage = 1;
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float spinSpeed = 540f;     // 빙글빙글 도는 연출 (도/초)
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
    }

    public void Launch(Vector2 direction)
    {
        rb.linearVelocity = direction.normalized * speed;
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        transform.Rotate(0f, 0f, -spinSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        int bit = 1 << other.gameObject.layer;

        if ((playerLayer.value & bit) != 0)
        {
            var target = other.GetComponentInParent<IDamageable>();
            if (target != null) target.TakeDamage(damage, transform.position);
            GameSfx.PlayAt(GameSfxId.EnemyProjectileHit, transform.position);
            Destroy(gameObject);
        }
        else if ((groundLayer.value & bit) != 0)
        {
            GameSfx.PlayAt(GameSfxId.EnemyProjectileHit, transform.position);
            Destroy(gameObject);
        }
    }
}
