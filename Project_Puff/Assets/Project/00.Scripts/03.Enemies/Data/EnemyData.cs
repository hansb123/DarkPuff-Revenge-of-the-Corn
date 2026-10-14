using UnityEngine;

/// <summary>
/// 적 공통 데이터(밸런스 값). 적 종류마다 에셋 하나씩 만들어 프리팹에 연결한다.
/// 런타임에 값을 바꾸지 않는다 (같은 에셋을 쓰는 모든 적이 공유하므로). 현재 HP 같은 변하는 값은 적 컴포넌트가 따로 가진다.
/// 순찰 거리처럼 "배치할 때마다 달라지는 값"은 데이터가 아니라 씬의 적 컴포넌트에 둔다.
/// </summary>
public abstract class EnemyData : ScriptableObject
{
    [field: Header("Stats")]
    [field: SerializeField] public int MaxHp { get; private set; } = 3;
    [field: SerializeField] public float MoveSpeed { get; private set; } = 2f;
    [field: SerializeField] public int ContactDamage { get; private set; } = 1;

    [field: Header("Detection")]
    [field: SerializeField] public float DetectRange { get; private set; } = 7f;
    [field: SerializeField, Tooltip("위아래로 이 높이 차 안에 있어야 플레이어를 발견")]
    public float DetectHeight { get; private set; } = 2f;

    [field: Header("Patrol Checks")]
    [field: SerializeField, Tooltip("몸통 콜라이더 앞쪽 끝에서 이만큼 더 앞에 벽이 있으면 벽으로 판정 (0.1~0.2 권장)")]
    public float WallCheckDistance { get; private set; } = 0.15f;
    [field: SerializeField, Tooltip("몸통 앞쪽 끝에서 이만큼 더 앞쪽 발밑을 검사 (0.1~0.2 권장)")]
    public float LedgeCheckForward { get; private set; } = 0.15f;
    [field: SerializeField, Tooltip("발밑에서 아래로 이 거리 안에 바닥이 없으면 낭떠러지")]
    public float LedgeCheckDown { get; private set; } = 0.6f;

    [field: Header("Hit")]
    [field: SerializeField] public float KnockbackSpeed { get; private set; } = 4f;
    [field: SerializeField] public float HitStunTime { get; private set; } = 0.25f;
    [field: SerializeField] public float DeadDestroyDelay { get; private set; } = 1f;
}
