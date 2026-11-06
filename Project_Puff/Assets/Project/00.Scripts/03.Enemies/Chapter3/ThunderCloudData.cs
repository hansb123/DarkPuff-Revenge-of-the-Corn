using UnityEngine;

/// <summary>번개구름(엘리트 원거리) 데이터. Assets → Create → Darkpuff → Enemy → Thunder Cloud Data</summary>
[CreateAssetMenu(fileName = "ThunderCloudData", menuName = "Darkpuff/Enemy/Thunder Cloud Data")]
public class ThunderCloudData : EnemyData
{
    [field: Header("Hover (떠다니기)")]
    [field: SerializeField, Tooltip("플레이어 머리 위로 이만큼 높이를 유지하며 따라감 (바닥 기준이 아니라 플레이어 기준)")]
    public float HoverHeight { get; private set; } = 3.5f;
    [field: SerializeField, Tooltip("추적 속도 배율 (MoveSpeed × 이 값)")]
    public float ChaseSpeedMultiplier { get; private set; } = 1.4f;
    [field: SerializeField, Tooltip("스폰 위치에서 좌우로 이 거리 이상 못 벗어남 (구역 이탈 방지)")]
    public float LeashDistance { get; private set; } = 14f;
    [field: SerializeField, Tooltip("위/아래 이동 보정 속도")]
    public float VerticalSpeed { get; private set; } = 3f;

    [field: Header("Lightning (낙뢰)")]
    [field: SerializeField, Tooltip("플레이어와 가로 거리가 이 안으로 들어오면 낙뢰 준비")]
    public float AttackRange { get; private set; } = 2.5f;
    [field: SerializeField, Tooltip("경고(바닥 표시)가 뜨고 번개가 떨어지기까지 시간 = 플레이어가 피할 시간")]
    public float WarnTime { get; private set; } = 1.0f;
    [field: SerializeField, Tooltip("번개가 화면에 남는 시간")]
    public float StrikeTime { get; private set; } = 0.25f;
    [field: SerializeField] public int StrikeDamage { get; private set; } = 2;
    [field: SerializeField, Tooltip("번개 기둥의 가로 폭(피격 범위)")]
    public float StrikeWidth { get; private set; } = 1.6f;
    [field: SerializeField, Tooltip("한 번에 떨어지는 번개 수. 1=플레이어 위치, 3=플레이어+좌우로 벌려서")]
    public int StrikesPerCast { get; private set; } = 1;
    [field: SerializeField, Tooltip("번개가 여러 개일 때 간격")]
    public float StrikeSpacing { get; private set; } = 3f;
    [field: SerializeField, Tooltip("공격 후 빈틈 동안 플레이어 머리 위 이 높이까지 내려옴. 플레이어 근접 공격이 닿는 높이(점프 포함)로 맞출 것")]
    public float RecoverHeight { get; private set; } = 1.2f;
    [field: SerializeField, Tooltip("빈틈 동안 내려오는 속도")]
    public float RecoverDescendSpeed { get; private set; } = 4f;
    [field: SerializeField, Tooltip("공격 후 쉬는 시간 (구름 본체를 때릴 기회)")]
    public float RecoverTime { get; private set; } = 1.6f;

    [field: Header("Rod (피뢰침)")]
    [field: SerializeField, Tooltip("이 거리 안에 피뢰침(LightningRod)이 있으면 번개가 플레이어 대신 피뢰침에 떨어짐")]
    public float RodAttractRange { get; private set; } = 6f;
}