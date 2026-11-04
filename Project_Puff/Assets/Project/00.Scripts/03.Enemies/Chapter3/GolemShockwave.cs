using UnityEngine;

/// <summary>
/// 골렘 충격파. 바닥을 따라 한 방향으로 달리며 닿은 플레이어에게 피해 (점프로 넘는다).
/// 프리팹: SpriteRenderer(fx_shock) + BoxCollider2D(Is Trigger). Rigidbody 불필요.
/// 스프라이트는 오른쪽 진행 기준, 왼쪽이면 flipX.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class GolemShockwave : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private Sprite[] frames;          // fx_shock_1, 2
    [SerializeField] private float frameInterval = 0.1f;
    [SerializeField] private LayerMask wallLayer;      // 벽에 닿으면 사라짐 (비우면 무시)

    private LayerMask playerLayer;
    private int damage;
    private float speed, life, frameTimer;
    private int dir = 1, frame;
    private bool hit;

    public void Launch(int direction, float spd, float lifetime, int dmg, LayerMask players)
    {
        dir = direction >= 0 ? 1 : -1;
        speed = spd; life = lifetime; damage = dmg; playerLayer = players;
        if (sr != null) sr.flipX = dir < 0;
        var col = GetComponent<Collider2D>(); col.isTrigger = true;
    }

    private void Update()
    {
        float step = speed * Time.deltaTime;
        if (wallLayer.value != 0)
        {
            var hitInfo = Physics2D.Raycast(transform.position + Vector3.up * 0.3f, Vector2.right * dir, step + 0.3f, wallLayer);
            if (hitInfo.collider != null) { Destroy(gameObject); return; }
        }
        transform.position += Vector3.right * (dir * step);

        if (sr != null && frames != null && frames.Length > 1)
        {
            frameTimer -= Time.deltaTime;
            if (frameTimer <= 0f) { frameTimer = frameInterval; frame = (frame + 1) % frames.Length; sr.sprite = frames[frame]; }
        }

        life -= Time.deltaTime;
        if (life <= 0f) Destroy(gameObject);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (hit) return;
        if (((1 << other.gameObject.layer) & playerLayer) == 0) return;
        var target = other.GetComponentInParent<IDamageable>();
        if (target == null) return;
        hit = true;
        target.TakeDamage(damage, transform.position);
    }
}
