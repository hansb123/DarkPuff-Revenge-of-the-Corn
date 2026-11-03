using UnityEngine;

/// <summary>
/// 상시 패턴: 보스 몸에서 패링 깃털을 일정 주기로 "한 발씩" 플레이어 쪽으로 천천히 쏜다.
/// 다른 패턴과 상관없이 동시에 나온다 (보스 트리와 독립). 발사 순간의 플레이어 위치로만 조준하고, 끝까지 쫓아가지는 않는다.
///  - 화면에 깃털이 이미 있으면 새로 쏘지 않는다 (항상 최대 1발)
///  - 그로기/페이즈 전환/사망 때는 보스가 기존 깃털을 치우고, 이 컴포넌트는 쉬었다가 다시 시작한다
/// 사용법: EagleBoss 오브젝트에 붙이고 Projectile Prefab 에 패링 깃털 프리팹(ParryProjectile)을 넣는다.
/// </summary>
public class EagleAmbientFeather : MonoBehaviour
{
    [SerializeField] private EagleBoss boss;
    [SerializeField] private PatternRunner runner;
    [Tooltip("비우면 'Player' 태그를 찾음")]
    [SerializeField] private Transform player;
    [SerializeField] private ParryProjectile projectilePrefab;

    [Header("쏘는 시점")]
    [Tooltip("이 페이즈부터 쏜다 (0=1페이즈, 1=2페이즈). 시간제 페이즈(1페이즈)에서는 쏘지 않음")]
    [SerializeField] private int fromPhase = 1;
    [Tooltip("몇 초에 하나씩 쏠지")]
    [SerializeField] private float interval = 4f;
    [Tooltip("페이즈 시작/그로기 끝난 뒤 첫 발까지 기다리는 시간")]
    [SerializeField] private float firstDelay = 2f;
    [Tooltip("그로기 중에도 쏠지")]
    [SerializeField] private bool fireWhileGroggy = false;

    [Header("깃털")]
    [Tooltip("날아가는 속도 (유닛/초). 작을수록 느림")]
    [SerializeField] private float speed = 3.5f;
    [Tooltip("발사 전 깜빡이는 예고 시간(초)")]
    [SerializeField] private float telegraph = 0.7f;
    [Tooltip("이 거리를 날아가도 아무도 안 맞으면 사라짐")]
    [SerializeField] private float maxDistance = 20f;
    [Tooltip("플레이어 공격(뻥/기본 공격)에 맞으면 깃털이 사라지게 할지. 꺼두면 맞아도 안 사라짐 (둘 다 패링 카운트는 안 올라감)")]
    [SerializeField] private bool destroyableByHit = true;
    [Tooltip("독수리 중심에서 나오는 위치 (X 는 플레이어 쪽이 +)")]
    [SerializeField] private Vector2 muzzleOffset = new Vector2(0.8f, -0.3f);

    private PrefabPool<ParryProjectile> pool;
    private float nextAt;
    private bool alive;          // 깃털이 화면에 있음
    private int lastPhase = -2;
    private bool wasBlocked = true;

    private void Awake()
    {
        if (boss == null) boss = GetComponent<EagleBoss>();
        if (runner == null) runner = FindFirstObjectByType<PatternRunner>();
        if (player == null)
        {
            var go = GameObject.FindWithTag("Player");
            if (go != null) player = go.transform;
        }
    }

    private void Update()
    {
        if (boss == null || runner == null || projectilePrefab == null) return;

        bool blocked = !boss.IsFighting || boss.PhaseIndex < fromPhase || boss.IsTimedPhase
                       || (boss.IsGroggy && !fireWhileGroggy);

        // 막혀 있다가 풀렸거나 페이즈가 바뀌었으면 처음부터 다시 기다림. 보스가 정리한 깃털은 더 이상 없다.
        if (blocked) { wasBlocked = true; alive = false; return; }
        if (wasBlocked || boss.PhaseIndex != lastPhase)
        {
            wasBlocked = false;
            lastPhase = boss.PhaseIndex;
            nextAt = Time.time + firstDelay;
        }

        if (alive || Time.time < nextAt || player == null) return;
        Fire();
    }

    private void Fire()
    {
        if (pool == null) pool = runner.GetPool(projectilePrefab, (p, pl) => p.SetReleaser(x => pl.Release(x)));

        float side = player.position.x < transform.position.x ? -1f : 1f;
        Vector2 start = (Vector2)transform.position + new Vector2(muzzleOffset.x * side, muzzleOffset.y);

        alive = true;
        nextAt = Time.time + interval;
        GameSfx.Play(GameSfxId.EagleWingFlap);

        var p = pool.Get();
        p.SetPlain(destroyableByHit);   // 패링 카운트와 무관한 그냥 공격
        p.Launch(runner, start, player.position, telegraph, speed, maxDistance, () => alive = false);
    }
}