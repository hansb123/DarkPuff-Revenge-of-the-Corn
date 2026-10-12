using UnityEngine;

/// <summary>
/// 데미지를 받을 수 있는 모든 대상(적, 보스, 플레이어)이 구현.
/// hitFrom: 공격이 온 위치 (넉백 방향 계산용)
/// </summary>
public interface IDamageable
{
    void TakeDamage(int amount, Vector2 hitFrom);
}
