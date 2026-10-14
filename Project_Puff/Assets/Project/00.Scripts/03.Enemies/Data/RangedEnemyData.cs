using UnityEngine;

/// <summary>원거리 적(마늘맨) 데이터. Assets → Create → Darkpuff → Enemy → Ranged Enemy Data</summary>
[CreateAssetMenu(fileName = "RangedEnemyData", menuName = "Darkpuff/Enemy/Ranged Enemy Data")]
public class RangedEnemyData : EnemyData
{
    [field: Header("Ranged")]
    [field: SerializeField] public EnemyProjectile ProjectilePrefab { get; private set; }
    [field: SerializeField, Tooltip("이 거리 근처를 유지")]
    public float PreferredRange { get; private set; } = 5f;
    [field: SerializeField, Tooltip("이보다 가까우면 물러남")]
    public float TooCloseRange { get; private set; } = 2.5f;
    [field: SerializeField] public float RetreatSpeedMultiplier { get; private set; } = 1.2f;
    [field: SerializeField] public float AttackCooldown { get; private set; } = 2f;
    [field: SerializeField] public float WindupTime { get; private set; } = 0.4f;
    [field: SerializeField] public float RecoverTime { get; private set; } = 0.5f;
}
