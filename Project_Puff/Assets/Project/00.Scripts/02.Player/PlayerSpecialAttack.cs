using System;
using UnityEngine;

/// <summary>
/// 특수공격 "뻥이요": 앞쪽 부채꼴로 산탄 여러 알을 발사한다. (X키)
/// 쿨타임 대신 "뻥!" 충전(최대 3개)을 쓴다. 맵의 뻥! 아이템을 먹으면 +1, 쏠 때마다 -1.
/// 가까이서 맞을수록 여러 알이 겹쳐 맞아서 강하고, 멀수록 흩어져서 약하다.
/// "언제 쏘는가"(입력, 상태)는 PlayerSpecialState가 맡고, 이 컴포넌트는 발사/풀/쿨타임/수치만 맡는다.
/// 알갱이와 이펙트는 풀링한다. Use Pooling을 끄면 Instantiate/Destroy로 동작 (Profiler 비교용).
/// </summary>
public class PlayerSpecialAttack : MonoBehaviour
{
    [Header("충전")]
    [SerializeField] private int maxCharges = 3;
    [Tooltip("스테이지 시작 때 가지고 있는 충전 수")]
    [SerializeField] private int startCharges = 0;

    [Header("발사")]
    [SerializeField] private int pelletCount = 5;
    [Tooltip("부채꼴 전체 각도 (도). 5발이면 알갱이 사이 간격은 이 값 / 4")]
    [SerializeField] private float spreadAngle = 44f;
    [Tooltip("각 알의 각도에 더하는 무작위 흔들림 (±도)")]
    [SerializeField] private float angleJitter = 2f;
    [SerializeField] private float pelletSpeed = 16f;
    [Tooltip("속도 무작위 비율 (±)")]
    [Range(0f, 0.3f)] [SerializeField] private float speedJitter = 0.08f;
    [Tooltip("사거리 (유닛). 이만큼 날아가면 사라짐")]
    [SerializeField] private float range = 6f;
    [Tooltip("플레이어 중심에서 바라보는 방향으로 이만큼 앞 (x), 위 (y)")]
    [SerializeField] private Vector2 muzzleOffset = new Vector2(1.1f, 0.1f);

    [Header("데미지")]
    [SerializeField] private int damagePerPellet = 1;
    [SerializeField] private LayerMask targetLayer;      // Enemy
    [SerializeField] private LayerMask blockLayer;       // Ground (벽/바닥에 막힘)

    [Header("Timing (초)")]
    [SerializeField] private float startupTime = 0.12f;
    [SerializeField] private float recoverTime = 0.3f;
    [Tooltip("발사 반동: 바라보는 반대 방향으로 밀려나는 속도 (땅에서만)")]
    [SerializeField] private float recoilSpeed = 3.5f;

    [Header("로켓점프 (아래 방향키 + 특수공격)")]
    [SerializeField] private bool rocketJumpEnabled = true;
    [Tooltip("로켓점프 때 발사 위치 (플레이어 중심 기준). 발밑에서 아래로 터진다")]
    [SerializeField] private Vector2 rocketMuzzleOffset = new Vector2(0f, -0.4f);
    [Tooltip("일반 점프 대비 높이 배율. 2면 두 배 높이")]
    [SerializeField] private float rocketJumpHeightMultiplier = 2f;
    [Tooltip("아래 입력이 이 값 이상 눌려 있어야 로켓점프 (-1~0 기준 절댓값)")]
    [Range(0.1f, 1f)] [SerializeField] private float rocketJumpDownThreshold = 0.5f;
    [Tooltip("공중에서도 로켓점프 가능 (쿨타임이 있어서 무한 비행은 안 됨)")]
    [SerializeField] private bool rocketJumpInAir = true;

    [Header("Animation")]
    [Tooltip("재생할 Animator 상태 이름. 특수공격 전용 상태가 없으면 Attack 그대로 사용")]
    [SerializeField] private string animationName = "Attack";

    [Header("프리팹")]
    [SerializeField] private SpecialPellet pelletPrefab;
    [SerializeField] private OneShotEffect blastEffectPrefab;     // blast_1~5
    [SerializeField] private OneShotEffect hitEffectPrefab;       // pellet_hit_1~4
    [SerializeField] private OneShotEffect popEffectPrefab;       // pellet_pop_1~3
    [SerializeField] private float blastWidth = 3.2f;
    [Tooltip("발사 이펙트를 총구에서 앞으로 더 밀어내는 거리 (유닛). 이펙트가 몸에 가려지면 늘리세요")]
    [SerializeField] private float blastForwardOffset = 0.3f;
    [SerializeField] private float hitEffectWidth = 0.75f;
    [SerializeField] private float popEffectWidth = 0.5f;

    [Header("Pooling")]
    [Tooltip("끄면 Instantiate/Destroy. Profiler에서 GC Alloc을 비교할 때 켜고 끄며 측정")]
    [SerializeField] private bool usePooling = true;

    private PrefabPool<SpecialPellet> pelletPool;
    private PrefabPool<OneShotEffect> blastPool;
    private PrefabPool<OneShotEffect> hitPool;
    private PrefabPool<OneShotEffect> popPool;
    private Transform poolRoot;
    private int charges;

    public float StartupTime => startupTime;
    public float RecoverTime => recoverTime;
    public float RecoilSpeed => recoilSpeed;
    public string AnimationName => animationName;
    public int Charges => charges;
    public int MaxCharges => maxCharges;
    public bool IsFull => charges >= maxCharges;
    public bool CanStart => charges > 0;
    public float RocketJumpHeightMultiplier => rocketJumpHeightMultiplier;

    /// <summary>지금 입력/상태로 로켓점프를 할 수 있는가 (아래 입력 + 땅이거나 공중 허용)</summary>
    public bool WantsRocketJump(float inputY, bool grounded)
    {
        if (!rocketJumpEnabled) return false;
        if (!grounded && !rocketJumpInAir) return false;
        return inputY <= -rocketJumpDownThreshold;
    }

    /// <summary>충전 수가 바뀔 때 (현재, 최대). HUD가 구독</summary>
    public event Action<int, int> ChargesChanged;

    /// <summary>가득 찬 상태에서 아이템을 먹으려 했을 때 (HUD 흔들림 등 피드백용)</summary>
    public event Action ChargeRejected;

    /// <summary>발사 순간</summary>
    public event Action Fired;

    /// <summary>알갱이가 적을 맞힐 때마다 (적 수가 아니라 알갱이 수 기준)</summary>
    public event Action PelletHit;

    private void Awake()
    {
        charges = Mathf.Clamp(startCharges, 0, maxCharges);

        // 풀 오브젝트가 플레이어를 따라다니지 않도록 별도 루트
        poolRoot = new GameObject("[SpecialAttack Pool]").transform;

        if (pelletPrefab != null)
            pelletPool = new PrefabPool<SpecialPellet>(pelletPrefab, poolRoot, usePooling, 16, 48, OnPelletCreated);

        blastPool = MakeEffectPool(blastEffectPrefab, 2, 4);
        hitPool = MakeEffectPool(hitEffectPrefab, 8, 24);
        popPool = MakeEffectPool(popEffectPrefab, 8, 24);
    }

    private void OnDestroy()
    {
        if (poolRoot != null) Destroy(poolRoot.gameObject);
    }

    private PrefabPool<OneShotEffect> MakeEffectPool(OneShotEffect prefab, int capacity, int max)
    {
        if (prefab == null) return null;
        return new PrefabPool<OneShotEffect>(prefab, poolRoot, usePooling, capacity, max,
            (fx, pool) => fx.SetReleaser(pool.Release));
    }

    private void OnPelletCreated(SpecialPellet pellet, PrefabPool<SpecialPellet> pool)
    {
        pellet.Impact = OnPelletImpact;
        pellet.Expired = OnPelletExpired;
    }

    private void Start()
    {
        ChargesChanged?.Invoke(charges, maxCharges);   // HUD가 처음 상태를 그리도록
    }

    /// <summary>충전 +1. 가득 차 있으면 false (아이템은 먹어지지 않음)</summary>
    public bool TryAddCharge()
    {
        if (IsFull)
        {
            ChargeRejected?.Invoke();
            return false;
        }

        charges++;
        ChargesChanged?.Invoke(charges, maxCharges);
        return true;
    }

    /// <summary>충전을 모두 비움 (필요할 때만 사용)</summary>
    public void ResetCharges()
    {
        charges = 0;
        ChargesChanged?.Invoke(charges, maxCharges);
    }

    /// <summary>
    /// 발사. 부채꼴로 알갱이를 한꺼번에 날리고 발사 이펙트를 띄운다.
    /// downward = true 이면 (로켓점프) 발밑에서 아래쪽 부채꼴로 발사한다.
    /// </summary>
    public void Fire(int facing, bool downward = false)
    {
        if (charges <= 0) return;   // 충전 없이는 발사하지 않음

        charges--;
        ChargesChanged?.Invoke(charges, maxCharges);

        GameSfx.Play(GameSfxId.SpecialShot);
        if (downward) GameSfx.Play(GameSfxId.SpecialRocketJump);

        if (pelletPool == null)
        {
            Debug.LogWarning("[PlayerSpecialAttack] Pellet Prefab이 연결되지 않았습니다.", this);
            return;
        }

        Vector2 origin = downward
            ? (Vector2)transform.position + rocketMuzzleOffset
            : (Vector2)transform.position + new Vector2(muzzleOffset.x * facing, muzzleOffset.y);
        float baseAngle = downward ? -90f : (facing > 0 ? 0f : 180f);
        LayerMask mask = targetLayer | blockLayer;

        for (int i = 0; i < pelletCount; i++)
        {
            float t = pelletCount > 1 ? (float)i / (pelletCount - 1) : 0.5f;
            float angle = baseAngle + Mathf.Lerp(-spreadAngle * 0.5f, spreadAngle * 0.5f, t)
                          + UnityEngine.Random.Range(-angleJitter, angleJitter);
            float rad = angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            float speed = pelletSpeed * (1f + UnityEngine.Random.Range(-speedJitter, speedJitter));

            var pellet = pelletPool.Get();
            pellet.Launch(origin, dir, speed, range, mask);
        }

        if (blastPool != null)
        {
            var fx = blastPool.Get();
            // blast 이미지의 원점(왼쪽 가운데)이 총구에 오도록: 가로 크기의 절반만큼 앞으로
            float reach = blastWidth * 0.5f - 0.2f + blastForwardOffset;

            if (downward)
            {
                // 이미지가 오른쪽을 향하므로 -90도 돌려서 아래로
                fx.Play(origin + new Vector2(0f, -reach), false, blastWidth, -90f);
            }
            else
            {
                fx.Play(origin + new Vector2(reach * facing, 0f), facing < 0, blastWidth);
            }
        }

        Fired?.Invoke();
    }

    private void OnPelletImpact(SpecialPellet pellet, Vector2 point, Collider2D col)
    {
        bool isTarget = ((1 << col.gameObject.layer) & targetLayer) != 0;

        if (isTarget)
        {
            var target = col.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                target.TakeDamage(damagePerPellet, transform.position);
                GameSfx.PlayAt(GameSfxId.PelletHit, point);
                PelletHit?.Invoke();
            }

            if (hitPool != null) hitPool.Get().Play(point, false, hitEffectWidth);
        }
        else
        {
            GameSfx.PlayAt(GameSfxId.PelletPop, point);
            if (popPool != null) popPool.Get().Play(point, false, popEffectWidth);
        }

        pelletPool.Release(pellet);
    }

    private void OnPelletExpired(SpecialPellet pellet, Vector2 point)
    {
        if (popPool != null) popPool.Get().Play(point, false, popEffectWidth);
        pelletPool.Release(pellet);
    }

    private void OnDrawGizmosSelected()
    {
        // 에디터에서는 오른쪽 기준 부채꼴 표시
        Vector3 origin = transform.position + (Vector3)muzzleOffset;
        Gizmos.color = Color.yellow;
        for (int i = 0; i < Mathf.Max(2, pelletCount); i++)
        {
            float t = (float)i / (Mathf.Max(2, pelletCount) - 1);
            float rad = Mathf.Lerp(-spreadAngle * 0.5f, spreadAngle * 0.5f, t) * Mathf.Deg2Rad;
            Gizmos.DrawLine(origin, origin + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * range);
        }
    }
}
