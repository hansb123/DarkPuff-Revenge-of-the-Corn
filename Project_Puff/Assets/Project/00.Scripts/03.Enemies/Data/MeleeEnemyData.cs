using UnityEngine;

/// <summary>근거리 적(성난 호떡) 데이터. Assets → Create → Darkpuff → Enemy → Melee Enemy Data</summary>
[CreateAssetMenu(fileName = "MeleeEnemyData", menuName = "Darkpuff/Enemy/Melee Enemy Data")]
public class MeleeEnemyData : EnemyData
{
    [field: Header("Melee")]
    [field: SerializeField] public float ChaseSpeedMultiplier { get; private set; } = 1.6f;
    [field: SerializeField] public float AttackRange { get; private set; } = 1.6f;
    [field: SerializeField, Tooltip("예고 시간 (플레이어가 피할 시간)")]
    public float WindupTime { get; private set; } = 0.35f;
    [field: SerializeField] public float LungeSpeed { get; private set; } = 7f;
    [field: SerializeField] public float LungeTime { get; private set; } = 0.2f;
    [field: SerializeField, Tooltip("공격 후 빈틈")]
    public float RecoverTime { get; private set; } = 0.5f;
    [field: SerializeField] public int AttackDamage { get; private set; } = 1;
    [field: SerializeField] public Vector2 HitBoxSize { get; private set; } = new Vector2(1.4f, 1f);
    [field: SerializeField] public float HitBoxForward { get; private set; } = 0.9f;
}
