using UnityEngine;

/// <summary>바위 골렘(엘리트 근접) 데이터. Assets → Create → Darkpuff → Enemy → Rock Golem Data</summary>
[CreateAssetMenu(fileName = "RockGolemData", menuName = "Darkpuff/Enemy/Rock Golem Data")]
public class RockGolemData : EnemyData
{
    [field: Header("Armor (단단함)")]
    [field: SerializeField, Tooltip("앞에서 맞을 때 받는 피해에서 이만큼을 깎음. 플레이어 기본 피해가 1이면 1 → 앞 공격은 완전히 튕김")]
    public int FrontArmor { get; private set; } = 1;
    [field: SerializeField, Tooltip("이 피해 이상(뻥이요 등)이면 앞에서도 갑옷을 뚫음 (갑옷 깎임 없이 그대로 들어감)")]
    public int ArmorBreakDamage { get; private set; } = 3;
    [field: SerializeField, Tooltip("등 뒤(등 수정)를 때릴 때 피해 배율")]
    public int BackDamageMultiplier { get; private set; } = 3;
    [field: SerializeField, Tooltip("공격 후 빈틈(Recover) 동안은 갑옷이 풀려 앞에서도 정상 피해")]
    public bool ArmorDownWhileRecover { get; private set; } = true;

    [field: Header("Move")]
    [field: SerializeField] public float ChaseSpeedMultiplier { get; private set; } = 1.2f;

    [field: Header("Slam (내려찍기)")]
    [field: SerializeField] public float AttackRange { get; private set; } = 2.4f;
    [field: SerializeField, Tooltip("팔을 드는 예고 시간")]
    public float WindupTime { get; private set; } = 0.9f;
    [field: SerializeField, Tooltip("내려찍은 직후 경직(충격파가 퍼지는 동안)")]
    public float SlamHoldTime { get; private set; } = 0.25f;
    [field: SerializeField, Tooltip("공격 후 빈틈 = 때릴 기회")]
    public float RecoverTime { get; private set; } = 1.3f;
    [field: SerializeField] public int SlamDamage { get; private set; } = 2;
    [field: SerializeField] public Vector2 SlamHitBoxSize { get; private set; } = new Vector2(2.2f, 1.6f);
    [field: SerializeField] public float SlamHitBoxForward { get; private set; } = 1.4f;

    [field: Header("Shockwave (충격파)")]
    [field: SerializeField] public bool ShockwaveBothSides { get; private set; } = true;
    [field: SerializeField] public int ShockwaveDamage { get; private set; } = 1;
    [field: SerializeField] public float ShockwaveSpeed { get; private set; } = 7f;
    [field: SerializeField] public float ShockwaveLife { get; private set; } = 1.2f;
}
