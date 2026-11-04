using UnityEngine;

/// <summary>
/// 바위 골렘(엘리트 근접). 느리고 단단하다.
///  - 앞에서 때리면 거의 안 들어감(FrontArmor), 등 수정을 때리거나 뻥이요(ArmorBreakDamage 이상)로 갑옷을 뚫어야 함
///  - 내려찍기 후 빈틈(Recover) 동안은 갑옷이 풀려 앞에서도 정상 피해 → 패턴을 보고 때리는 보스급 소형 적
/// FSM: Patrol → Chase → Windup(팔 들기) → Slam(찍기+충격파) → Recover(빈틈) → Chase / (Hit) → Dead
/// 팔 드는 중/찍는 중에는 슈퍼아머(맞아도 경직 없음).
/// 스프라이트는 오른쪽을 보는 기준(등 수정은 왼쪽). Face()가 flipX로 반전하므로 등 위치도 함께 뒤집힘.
/// 애니메이터 클립: Idle, Walk, Windup, Slam, Hit, Dead
/// </summary>
public class RockGolem : EnemyBase
{
    [Header("Slam")]
    [SerializeField] private GolemShockwave shockwavePrefab;
    [SerializeField, Tooltip("충격파가 나오는 위치(발밑 앞). 비우면 몸 아래쪽")]
    private Transform shockwavePoint;
    [SerializeField] private bool shakeCameraOnSlam = true;

    [Header("Feedback")]
    [SerializeField, Tooltip("앞에서 막혔을 때 잠깐 이 색으로 반짝")]
    private Color blockedFlash = new Color(0.7f, 0.8f, 1f);

    private RockGolemData d;

    private PatrolState patrol;
    private ChaseState chase;
    private WindupState windup;
    private SlamState slam;
    private RecoverState recover;
    private HitState hit;
    private DeadState dead;

    private float flashTimer;

    protected override void Awake()
    {
        base.Awake();
        if (data == null) return;

        d = data as RockGolemData;
        if (d == null)
        {
            Debug.LogError($"[{name}] RockGolem에는 RockGolemData가 필요합니다.", this);
            enabled = false;
            return;
        }

        patrol = new PatrolState(this);
        chase = new ChaseState(this);
        windup = new WindupState(this);
        slam = new SlamState(this);
        recover = new RecoverState(this);
        hit = new HitState(this);
        dead = new DeadState(this);
    }

    protected override IState GetInitialState() => patrol;

    protected override void Update()
    {
        base.Update();
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (spriteRenderer != null) spriteRenderer.color = flashTimer > 0f ? blockedFlash : Color.white;
        }
    }

    // ---------------- 갑옷 ----------------

    /// <summary>공격이 골렘의 앞쪽에서 왔는지 (바라보는 방향 기준)</summary>
    private bool IsFront(Vector2 hitFrom) => (hitFrom.x - transform.position.x) * facing > 0f;

    protected override int ModifyDamage(int amount, Vector2 hitFrom)
    {
        if (!IsFront(hitFrom))
            return amount * Mathf.Max(1, d.BackDamageMultiplier);            // 등 수정

        if (amount >= d.ArmorBreakDamage) return amount;                      // 뻥이요 등 강공격은 관통
        if (d.ArmorDownWhileRecover && fsm.Current == recover) return amount; // 빈틈엔 갑옷 풀림

        return amount - d.FrontArmor;                                         // 보통 0 → 막힘
    }

    protected override void OnDamageBlocked(Vector2 hitFrom)
    {
        Sfx(GameSfxId.EnemyProjectileHit);   // 땅! 하고 튕기는 소리 대용 (전용 효과음이 있으면 교체)
        flashTimer = 0.08f;
    }

    protected override void OnHit(float knockbackDir)
    {
        // 슈퍼아머: 팔 들기/내려찍기 중엔 안 끊김
        if (fsm.Current == windup || fsm.Current == slam) return;

        hit.KnockbackDir = knockbackDir;
        if (fsm.Current == hit) hit.Restart();
        else fsm.ChangeState(hit);
    }

    protected override void Die()
    {
        fsm.ChangeState(dead);
        base.Die();
    }

    // ---------------- 공격 ----------------

    private void DoSlam()
    {
        Sfx(GameSfxId.EnemyMeleeAttack);
        if (shakeCameraOnSlam) EagleCamShake.Add(0.18f, 0.3f);

        // 앞쪽 직접 타격
        Vector2 c = (Vector2)transform.position + Vector2.right * (facing * d.SlamHitBoxForward);
        var col = Physics2D.OverlapBox(c, d.SlamHitBoxSize, 0f, playerLayer);
        if (col != null)
        {
            var target = col.GetComponentInParent<IDamageable>();
            if (target != null) target.TakeDamage(d.SlamDamage, transform.position);
        }

        // 충격파
        if (shockwavePrefab == null)
        {
            Debug.LogWarning("[RockGolem] Shockwave Prefab이 연결되지 않았습니다.", this);
            return;
        }
        Vector3 origin = shockwavePoint != null ? shockwavePoint.position
                       : new Vector3(transform.position.x, bodyCol.bounds.min.y + 0.3f, 0f);
        SpawnWave(origin, facing);
        if (d.ShockwaveBothSides) SpawnWave(origin, -facing);
    }

    private void SpawnWave(Vector3 pos, int dir)
    {
        var w = Instantiate(shockwavePrefab, pos + Vector3.right * (dir * 0.8f), Quaternion.identity);
        w.Launch(dir, d.ShockwaveSpeed, d.ShockwaveLife, d.ShockwaveDamage, playerLayer);
    }

    // ---------------- 상태들 ----------------

    private abstract class GolemState : IState
    {
        protected readonly RockGolem e;
        protected GolemState(RockGolem enemy) { e = enemy; }
        public virtual void Enter() { }
        public virtual void Tick() { }
        public virtual void Exit() { }
    }

    private sealed class PatrolState : GolemState
    {
        private float turnCooldown;
        public PatrolState(RockGolem enemy) : base(enemy) { }

        public override void Enter() { turnCooldown = 0f; e.Play("Walk"); }

        public override void Tick()
        {
            if (e.PlayerInSight())
            {
                e.Sfx(GameSfxId.EnemyAlert);
                e.fsm.ChangeState(e.chase);
                return;
            }

            turnCooldown -= Time.deltaTime;
            if (turnCooldown <= 0f && e.ShouldTurnAround())
            {
                e.Face(-e.facing);
                turnCooldown = 0.4f;
            }
            e.SetVelocityX(e.facing * e.moveSpeed);
        }
    }

    private sealed class ChaseState : GolemState
    {
        public ChaseState(RockGolem enemy) : base(enemy) { }

        public override void Tick()
        {
            if (!e.PlayerInSight()) { e.fsm.ChangeState(e.patrol); return; }

            e.FacePlayer();

            if (e.HorizontalDistanceToPlayer() <= e.d.AttackRange)
            {
                e.fsm.ChangeState(e.windup);
                return;
            }

            if (e.WallAhead() || e.LedgeAhead())
            {
                e.SetVelocityX(0f);
                e.Play("Idle");
                return;
            }

            e.Play("Walk");
            e.SetVelocityX(e.facing * e.moveSpeed * e.d.ChaseSpeedMultiplier);
        }
    }

    private sealed class WindupState : GolemState
    {
        private float timer;
        public WindupState(RockGolem enemy) : base(enemy) { }

        public override void Enter()
        {
            e.SetVelocityX(0f);
            e.Play("Windup", true);
            timer = e.d.WindupTime;
        }

        public override void Tick()
        {
            timer -= Time.deltaTime;
            if (timer <= 0f) e.fsm.ChangeState(e.slam);   // 팔을 든 뒤엔 방향 고정 (돌아 피하면 됨)
        }
    }

    private sealed class SlamState : GolemState
    {
        private float timer;
        public SlamState(RockGolem enemy) : base(enemy) { }

        public override void Enter()
        {
            e.Play("Slam", true);
            timer = e.d.SlamHoldTime;
            e.DoSlam();
        }

        public override void Tick()
        {
            timer -= Time.deltaTime;
            if (timer <= 0f) e.fsm.ChangeState(e.recover);
        }
    }

    private sealed class RecoverState : GolemState
    {
        private float timer;
        public RecoverState(RockGolem enemy) : base(enemy) { }

        public override void Enter()
        {
            e.SetVelocityX(0f);
            e.Play("Idle");
            timer = e.d.RecoverTime;
        }

        public override void Tick()
        {
            timer -= Time.deltaTime;
            if (timer <= 0f) e.fsm.ChangeState(e.chase);
        }
    }

    private sealed class HitState : GolemState
    {
        private float timer;
        public float KnockbackDir { get; set; }
        public HitState(RockGolem enemy) : base(enemy) { }

        public override void Enter() => Restart();

        public void Restart()
        {
            timer = e.hitStunTime;
            e.Play("Hit", true);
            e.ApplyKnockback(KnockbackDir);
        }

        public override void Tick()
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                e.SetVelocityX(0f);
                e.fsm.ChangeState(e.chase);
            }
        }
    }

    private sealed class DeadState : GolemState
    {
        public DeadState(RockGolem enemy) : base(enemy) { }
        public override void Enter() { e.SetVelocityX(0f); e.Play("Dead"); }
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        var gd = data as RockGolemData;
        if (gd == null) return;

        int dir = Application.isPlaying ? facing : 1;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position + Vector3.right * dir * gd.SlamHitBoxForward, gd.SlamHitBoxSize);
        Gizmos.color = new Color(1f, 0.6f, 0f);
        Gizmos.DrawLine(transform.position + Vector3.left * gd.AttackRange, transform.position + Vector3.right * gd.AttackRange);
    }
}
