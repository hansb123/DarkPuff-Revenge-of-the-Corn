using System;
using UnityEngine;

/// <summary>
/// "뻥이요" 산탄 한 알. Rigidbody 없이 직접 이동하면서, 매 프레임 이동 구간을 CircleCast로 검사한다
/// (빠른 알갱이가 얇은 벽이나 적을 통과하는 문제 방지).
/// 맞았을 때 데미지/이펙트/풀 반환은 발사한 쪽(PlayerSpecialAttack)이 처리한다.
/// 프리팹 구성: SpriteRenderer + 이 컴포넌트 (콜라이더/리지드바디 불필요).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpecialPellet : MonoBehaviour
{
    [SerializeField] private Sprite[] variants;          // pellet_1~3 중 랜덤
    [SerializeField] private float radius = 0.15f;       // 충돌 반경

    private SpriteRenderer sr;
    private Vector2 direction;
    private float speed;
    private float remaining;
    private LayerMask hitMask;
    private bool active;

    /// <summary>(알갱이, 맞은 위치, 맞은 콜라이더)</summary>
    public Action<SpecialPellet, Vector2, Collider2D> Impact;

    /// <summary>사거리 끝에서 사라질 때 (알갱이, 위치)</summary>
    public Action<SpecialPellet, Vector2> Expired;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    public void Launch(Vector2 position, Vector2 dir, float speed, float range, LayerMask hitMask)
    {
        transform.position = position;
        direction = dir.normalized;
        this.speed = speed;
        remaining = range;
        this.hitMask = hitMask;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);   // 스프라이트는 오른쪽이 앞

        if (variants != null && variants.Length > 0)
            sr.sprite = variants[UnityEngine.Random.Range(0, variants.Length)];

        active = true;
    }

    private void Update()
    {
        if (!active) return;

        float step = Mathf.Min(speed * Time.deltaTime, remaining);
        Vector2 pos = transform.position;

        RaycastHit2D hit = Physics2D.CircleCast(pos, radius, direction, step, hitMask);
        if (hit.collider != null)
        {
            active = false;
            Impact?.Invoke(this, hit.point, hit.collider);
            return;
        }

        pos += direction * step;
        transform.position = pos;
        remaining -= step;

        if (remaining <= 0f)
        {
            active = false;
            Expired?.Invoke(this, pos);
        }
    }
}
