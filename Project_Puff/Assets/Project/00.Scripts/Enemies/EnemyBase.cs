using UnityEngine;

/// <summary>
/// 일반 적 공통 기능. 순찰용 벽/낭떠러지 체크, 플레이어 감지, 피격, 사망, 애니메이션 재생.
/// 행동 로직은 상태머신(StateMachine/IState, 플레이어와 같은 것)으로 돌린다.
/// 상태 클래스는 각 적 스크립트(MeleeEnemy, RangedEnemy) 안에 중첩 클래스로 둔다.
/// 스프라이트는 기본적으로 오른쪽을 보는 기준(flipX로 반전).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public abstract class EnemyBase : MonoBehaviour, IDamageable
{
    [Header("Data")]
    [SerializeField] protected EnemyData data;        // 적 종류별 밸런스 값 (ScriptableObject)

    [Header("Detection / Patrol")]
    [SerializeField] protected LayerMask playerLayer;
    [SerializeField] protected LayerMask groundLayer;
    [Tooltip("시작 위치에서 좌우로 이만큼 벗어나면 돌아섬. 0이면 제한 없음(벽/낭떠러지에서만 돌아섬). 배치마다 다르게 쓰려고 데이터가 아닌 여기에 둠")]
    [SerializeField] protected float patrolDistance = 3f;

    [Header("Visual")]
    [SerializeField] protected SpriteRenderer spriteRenderer;
    [SerializeField] protected Animator animator;     // 선택. 클립 이름: Idle, Walk, Attack, Hit, Dead

    [Header("Debug")]
    [SerializeField] protected bool logStateChanges;  // 켜면 상태가 바뀔 때마다 Console에 출력

    protected readonly StateMachine fsm = new StateMachine();
    protected Rigidbody2D rb;
    protected Collider2D bodyCol;
    protected Transform player;
    protected int hp;
    protected float spawnX;         // 순찰 기준점 (시작 위치)
    protected int facing = 1;       // 1 = 오른쪽, -1 = 왼쪽
    protected bool isDead;

    // 데이터 값을 짧은 이름으로 (상태 클래스에서 e.moveSpeed처럼 쓰기 위함)
    protected int maxHp => data.MaxHp;
    protected float moveSpeed => data.MoveSpeed;
    protected int contactDamage => data.ContactDamage;
    protected float detectRange => data.DetectRange;
    protected float detectHeight => data.DetectHeight;
    protected float wallCheckDistance => data.WallCheckDistance;
    protected float ledgeCheckForward => data.LedgeCheckForward;
    protected float ledgeCheckDown => data.LedgeCheckDown;
    protected float knockbackSpeed => data.KnockbackSpeed;
    protected float hitStunTime => data.HitStunTime;
    protected float deadDestroyDelay => data.DeadDestroyDelay;

    protected virtual void Awake()
    {
        if (data == null)
        {
            Debug.LogError($"[{name}] Enemy Data가 연결되지 않았습니다. Create → Darkpuff → Enemy 에서 만든 에셋을 연결하세요.", this);
            enabled = false;
            return;
        }

        rb = GetComponent<Rigidbody2D>();
        bodyCol = GetComponent<Collider2D>();
        rb.freezeRotation = true;
        hp = maxHp;
        spawnX = transform.position.x;

        if (logStateChanges)
        {
            fsm.StateChanged += (prev, next) =>
                Debug.Log($"[{name}] {prev?.GetType().Name ?? "-"} → {next.GetType().Name}", this);
        }
    }

    protected virtual void Start()
    {
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;
        else Debug.LogWarning($"[{name}] 씬에서 PlayerController를 찾지 못했습니다.", this);

        fsm.ChangeState(GetInitialState());
    }

    /// <summary>시작 상태 (보통 Patrol). Start에서 한 번 호출</summary>
    protected abstract IState GetInitialState();

    protected virtual void Update()
    {
        if (isDead) return;
        fsm.Tick();
    }

    // ---------------- 이동 / 방향 ----------------

    protected void SetVelocityX(float vx)
    {
        rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
    }

    protected void Face(int dir)
    {
        if (dir == 0) return;
        facing = dir > 0 ? 1 : -1;
        if (spriteRenderer != null) spriteRenderer.flipX = facing < 0;
    }

    protected void FacePlayer()
    {
        if (player != null) Face(player.position.x >= transform.position.x ? 1 : -1);
    }

    /// <summary>몸통 앞쪽 끝 바로 앞에 벽이 있으면 true (콜라이더 크기 기준이라 몸집이 달라도 동작)</summary>
    protected bool WallAhead()
    {
        Bounds b = bodyCol.bounds;
        Vector2 origin = new Vector2(b.center.x, b.center.y);
        float dist = b.extents.x + wallCheckDistance;
        return Physics2D.Raycast(origin, Vector2.right * facing, dist, groundLayer);
    }

    /// <summary>순찰 반경 끝에 도달했고 아직 바깥쪽을 보고 있으면 true</summary>
    protected bool PatrolRangeReached()
    {
        if (patrolDistance <= 0f) return false;
        return (transform.position.x - spawnX) * facing >= patrolDistance;
    }

    /// <summary>순찰 중 돌아서야 하는지: 벽, 낭떠러지, 순찰 반경 끝 중 하나</summary>
    protected bool ShouldTurnAround()
    {
        return WallAhead() || LedgeAhead() || PatrolRangeReached();
    }

    /// <summary>앞쪽 발밑에 바닥이 없으면 true (낭떠러지)</summary>
    protected bool LedgeAhead()
    {
        Bounds b = bodyCol.bounds;
        Vector2 origin = new Vector2(b.center.x + facing * (b.extents.x + ledgeCheckForward), b.min.y + 0.1f);
        return !Physics2D.Raycast(origin, Vector2.down, ledgeCheckDown + 0.1f, groundLayer);
    }

    // ---------------- 플레이어 감지 ----------------

    protected float HorizontalDistanceToPlayer()
    {
        return player != null ? Mathf.Abs(player.position.x - transform.position.x) : float.MaxValue;
    }

    protected bool PlayerInSight()
    {
        if (player == null) return false;
        Vector2 d = player.position - transform.position;
        return Mathf.Abs(d.x) <= detectRange && Mathf.Abs(d.y) <= detectHeight;
    }

    // ---------------- 애니메이션 ----------------

    private string lastClip;

    /// <summary>같은 클립을 매 프레임 불러도 처음부터 다시 재생하지 않음. 강제로 다시 재생하려면 restart=true</summary>
    protected void Play(string clip, bool restart = false)
    {
        if (animator == null) return;
        if (!restart && clip == lastClip) return;

        lastClip = clip;
        animator.Play(clip, 0, 0f);
    }

    // ---------------- 피격 / 사망 ----------------

    public void TakeDamage(int amount, Vector2 hitFrom)
    {
        if (isDead) return;

        hp -= amount;

        if (hp <= 0)
        {
            Die();
            return;
        }

        float dir = transform.position.x >= hitFrom.x ? 1f : -1f;
        OnHit(dir);
    }

    /// <summary>살아남아 피격당했을 때. 하위 클래스에서 Hit 상태로 전환(이미 Hit 중이면 갱신)</summary>
    protected abstract void OnHit(float knockbackDir);

    /// <summary>하위 클래스는 먼저 Dead 상태로 전환한 뒤 base.Die()를 호출</summary>
    protected virtual void Die()
    {
        isDead = true;
        SetVelocityX(0f);
        foreach (var col in GetComponents<Collider2D>()) col.enabled = false;
        Destroy(gameObject, deadDestroyDelay);   // 풀링을 쓰면 여기서 반환
    }

    protected void ApplyKnockback(float dir)
    {
        rb.linearVelocity = new Vector2(dir * knockbackSpeed, 2f);
    }

    // ---------------- 몸통 접촉 데미지 ----------------

    private void OnCollisionStay2D(Collision2D other)
    {
        if (isDead || contactDamage <= 0) return;
        if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

        var target = other.gameObject.GetComponentInParent<IDamageable>();
        if (target != null) target.TakeDamage(contactDamage, transform.position);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (data == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(detectRange * 2f, detectHeight * 2f, 0f));

        // 순찰 반경 (초록)
        if (patrolDistance > 0f)
        {
            float cx = Application.isPlaying ? spawnX : transform.position.x;
            Gizmos.color = Color.green;
            Gizmos.DrawLine(new Vector3(cx - patrolDistance, transform.position.y, 0f),
                            new Vector3(cx + patrolDistance, transform.position.y, 0f));
        }

        var col = bodyCol != null ? bodyCol : GetComponent<Collider2D>();
        if (col == null) return;

        // 벽/낭떠러지 검사선 (빨강 = 벽, 청록 = 발밑)
        Bounds b = col.bounds;
        int dir = Application.isPlaying ? facing : 1;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(b.center, b.center + Vector3.right * dir * (b.extents.x + wallCheckDistance));
        Gizmos.color = Color.cyan;
        Vector3 o = new Vector3(b.center.x + dir * (b.extents.x + ledgeCheckForward), b.min.y + 0.1f, 0f);
        Gizmos.DrawLine(o, o + Vector3.down * (ledgeCheckDown + 0.1f));
    }
}
