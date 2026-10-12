using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 일반 공격(근접). 공격 판정(히트박스)과 수치를 담당한다.
/// "언제 휘두르는가"(입력, 상태 전환, 애니메이션)는 PlayerController의 Attack 상태가 맡고,
/// 이 컴포넌트는 BeginSwing()으로 한 번의 휘두름을 시작하고 TryHit()으로 판정만 한다.
/// 한 번의 휘두름에서 같은 적은 한 번만 맞는다.
/// </summary>
public class PlayerAttack : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private int damage = 1;
    [SerializeField] private LayerMask targetLayer;      // Enemy 레이어 (나중에 보스도 같은 레이어)

    [Header("Timing (초)")]
    [Tooltip("휘두르기 전 준비 시간 (이 동안은 판정 없음)")]
    [SerializeField] private float startupTime = 0.08f;
    [Tooltip("판정이 켜져 있는 시간")]
    [SerializeField] private float activeTime = 0.12f;
    [Tooltip("휘두른 뒤 빈틈")]
    [SerializeField] private float recoverTime = 0.15f;
    [Tooltip("다음 공격을 시작할 수 있을 때까지 (공격 시작 기준). 위 세 시간의 합 이상 권장")]
    [SerializeField] private float cooldown = 0.4f;

    [Header("Hit Box")]
    [SerializeField] private Vector2 hitBoxSize = new Vector2(1.3f, 1f);
    [Tooltip("플레이어 중심에서 바라보는 방향으로 이만큼 앞 (x), 위 (y)")]
    [SerializeField] private Vector2 hitBoxOffset = new Vector2(0.9f, 0f);

    [Header("Effects (선택)")]
    [Tooltip("휘두를 때 나오는 이펙트 프리팹 (OneShotEffect). 판정 범위를 보여주는 용도")]
    [SerializeField] private OneShotEffect swingEffectPrefab;
    [Tooltip("이펙트 가로 크기 = 히트박스 가로 x 이 값. 1이면 판정 범위와 같고, 조금 크게 보이게 하려면 1.1")]
    [SerializeField] private float swingEffectScale = 1.1f;
    [Tooltip("적을 맞혔을 때 맞은 자리에 나오는 이펙트 프리팹")]
    [SerializeField] private OneShotEffect hitEffectPrefab;
    [SerializeField] private float hitEffectWidth = 1f;

    private readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();
    private float nextAttackTime;

    public float StartupTime => startupTime;
    public float ActiveTime => activeTime;
    public float RecoverTime => recoverTime;
    public bool CanStart => Time.time >= nextAttackTime;

    /// <summary>적중했을 때 (이번 판정에서 맞은 수). 나중에 특수공격 게이지, 히트스톱, 효과음이 구독</summary>
    public event Action<int> HitLanded;

    /// <summary>새 휘두름 시작. 이미 맞은 적 목록을 비우고 쿨타임을 시작</summary>
    public void BeginSwing()
    {
        hitThisSwing.Clear();
        nextAttackTime = Time.time + cooldown;
    }

    /// <summary>판정이 켜지는 순간 한 번 호출. 히트박스 위치/크기에 맞춰 휘두르기 이펙트를 띄운다</summary>
    public void PlaySwingEffect(int facing)
    {
        if (swingEffectPrefab == null) return;

        Vector2 center = (Vector2)transform.position + new Vector2(hitBoxOffset.x * facing, hitBoxOffset.y);
        var fx = Instantiate(swingEffectPrefab);
        fx.Play(center, facing < 0, hitBoxSize.x * swingEffectScale);
    }

    /// <summary>판정 시간 동안 매 프레임 호출. 처음 맞는 적에게만 데미지를 준다</summary>
    public void TryHit(int facing)
    {
        Vector2 center = (Vector2)transform.position + new Vector2(hitBoxOffset.x * facing, hitBoxOffset.y);
        var cols = Physics2D.OverlapBoxAll(center, hitBoxSize, 0f, targetLayer);

        int newHits = 0;

        foreach (var col in cols)
        {
            var target = col.GetComponentInParent<IDamageable>();
            if (target == null || hitThisSwing.Contains(target)) continue;

            hitThisSwing.Add(target);
            target.TakeDamage(damage, transform.position);
            newHits++;

            if (hitEffectPrefab != null)
            {
                Vector2 point = col.ClosestPoint(center);   // 적 몸에서 히트박스 중심에 가장 가까운 점
                Instantiate(hitEffectPrefab).Play(point, false, hitEffectWidth);
            }
        }

        if (newHits > 0) HitLanded?.Invoke(newHits);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector2 center = (Vector2)transform.position + hitBoxOffset;   // 에디터에서는 오른쪽 기준으로 표시
        Gizmos.DrawWireCube(center, hitBoxSize);
    }
}
