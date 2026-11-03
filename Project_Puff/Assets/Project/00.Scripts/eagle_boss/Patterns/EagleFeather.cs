using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 독수리 깃털 한 발 (패링 불가, 피하기만). 정해진 방향으로 직선으로 날아가 플레이어에 닿으면 데미지.
/// 프리팹 구성: SpriteRenderer(eagle normal_feather 스프라이트) + 이 컴포넌트. 콜라이더는 필요 없다.
/// 스프라이트가 오른쪽을 향한 그림이면 Angle Offset = 0, 위를 향하면 -90, 아래를 향하면 90.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class EagleFeather : MonoBehaviour, IPatternHazard
{
    [SerializeField] private float hitRadius = 0.35f;
    [SerializeField] private int damage = 1;
    [SerializeField] private LayerMask playerLayer;
    [Tooltip("스프라이트가 기본으로 향한 방향 보정 (도)")]
    [SerializeField] private float angleOffset = 0f;
    [SerializeField] private float maxDistance = 30f;

    private Action<EagleFeather> release;
    private PatternRunner runner;
    private Coroutine routine;

    public void SetReleaser(Action<EagleFeather> r) => release = r;

    public void Launch(PatternRunner owner, Vector2 start, Vector2 dir, float speed)
    {
        runner = owner;
        runner?.Register(this);
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Fly(start, dir.normalized, speed));
    }

    private IEnumerator Fly(Vector2 start, Vector2 dir, float speed)
    {
        transform.position = start;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + angleOffset);

        Vector2 pos = start;
        float travelled = 0f;
        while (travelled < maxDistance)
        {
            float step = speed * Time.deltaTime;
            pos += dir * step;
            travelled += step;
            transform.position = pos;

            var hit = Physics2D.OverlapCircle(pos, hitRadius, playerLayer);
            if (hit != null)
            {
                var d = hit.GetComponentInParent<IDamageable>();
                if (d != null) d.TakeDamage(damage, pos);
                break;
            }
            yield return null;
        }
        Finish();
    }

    private void Finish()
    {
        runner?.Unregister(this);
        runner = null;
        routine = null;
        if (release != null) release(this); else Destroy(gameObject);
    }

    public void Cancel()
    {
        runner = null;
        if (routine != null) { StopCoroutine(routine); routine = null; }
        if (release != null) release(this); else Destroy(gameObject);
    }
}
