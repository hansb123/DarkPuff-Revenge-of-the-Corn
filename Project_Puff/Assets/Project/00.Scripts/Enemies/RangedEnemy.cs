using UnityEngine;

/// <summary>
/// 마늘맨(원거리). 순찰하다가 플레이어를 발견하면 거리를 유지하며 마늘쪽을 던진다.
/// 너무 가까워지면 뒤로 물러난다.
/// FSM: Patrol → Aim → Attack(예고/발사/빈틈) → (Hit) → Dead
/// </summary>
public class RangedEnemy : EnemyBase
{
    [Header("Ranged")]
    [SerializeField] private Transform firePoint;               // 손 근처에 둔 빈 오브젝트

    private RangedEnemyData d;

    private PatrolState patrol;
    private AimState aim;
    private AttackState attack;
    private HitState hit;
    private DeadState dead;

    private float nextAttackTime;

    protected override void Awake()
    {
        base.Awake();
        if (data == null) return;

        d = data as RangedEnemyData;
        if (d == null)
        {
            Debug.LogError($"[{name}] RangedEnemy에는 RangedEnemyData가 필요합니다.", this);
            enabled = false;
            return;
        }

        patrol = new PatrolState(this);
        aim = new AimState(this);
        attack = new AttackState(this);
        hit = new HitState(this);
        dead = new DeadState(this);
    }

    protected override IState GetInitialState() => patrol;

    protected override void OnHit(float knockbackDir)
    {
        hit.KnockbackDir = knockbackDir;

        if (fsm.Current == hit) hit.Restart();
        else fsm.ChangeState(hit);
    }

    protected override void Die()
    {
        fsm.ChangeState(dead);
        base.Die();
    }

    private void Fire()
    {
        if (d.ProjectilePrefab == null)
        {
            Debug.LogWarning("[RangedEnemy] 투사체 프리팹이 연결되지 않았습니다.", this);
            return;
        }

        Vector3 pos = firePoint != null ? firePoint.position : transform.position + Vector3.right * facing * 0.6f;
        var proj = Instantiate(d.ProjectilePrefab, pos, Quaternion.identity);   // 풀링을 쓰면 여기를 교체
        proj.Launch(Vector2.right * facing);
    }

    /// <summary>플레이어 반대쪽(뒤)이 벽이거나 낭떠러지인지</summary>
    private bool BackBlocked()
    {
        Face(-facing);                      // 뒤쪽 체크를 위해 잠깐 반대 방향
        bool blocked = WallAhead() || LedgeAhead();
        Face(-facing);                      // 다시 플레이어 쪽
        return blocked;
    }

    // ---------------- 상태들 ----------------

    private abstract class RangedState : IState
    {
        protected readonly RangedEnemy e;
        protected RangedState(RangedEnemy enemy) { e = enemy; }

        public virtual void Enter() { }
        public virtual void Tick() { }
        public virtual void Exit() { }
    }

    private sealed class PatrolState : RangedState
    {
        private float turnCooldown;

        public PatrolState(RangedEnemy enemy) : base(enemy) { }

        public override void Enter()
        {
            turnCooldown = 0f;
            e.Play("Walk");
        }

        public override void Tick()
        {
            if (e.PlayerInSight())
            {
                e.fsm.ChangeState(e.aim);
                return;
            }

            turnCooldown -= Time.deltaTime;

            if (turnCooldown <= 0f && e.ShouldTurnAround())
            {
                e.Face(-e.facing);
                turnCooldown = 0.3f;
            }

            e.SetVelocityX(e.facing * e.moveSpeed * 0.7f);   // 원거리는 느리게 순찰
        }
    }

    private sealed class AimState : RangedState
    {
        public AimState(RangedEnemy enemy) : base(enemy) { }

        public override void Tick()
        {
            if (!e.PlayerInSight())
            {
                e.fsm.ChangeState(e.patrol);
                return;
            }

            e.FacePlayer();
            float dist = e.HorizontalDistanceToPlayer();

            // 너무 가까우면 뒤로 (뒤쪽이 벽/낭떠러지면 멈춤)
            if (dist < e.d.TooCloseRange && !e.BackBlocked())
            {
                e.Play("Walk");
                e.SetVelocityX(-e.facing * e.moveSpeed * e.d.RetreatSpeedMultiplier);
                return;
            }

            e.SetVelocityX(0f);
            e.Play("Idle");

            if (Time.time >= e.nextAttackTime && dist <= e.d.PreferredRange + 2f)
            {
                e.fsm.ChangeState(e.attack);
            }
        }
    }

    private sealed class AttackState : RangedState
    {
        private enum Phase { Windup, Recover }

        private Phase phase;
        private float timer;

        public AttackState(RangedEnemy enemy) : base(enemy) { }

        public override void Enter()
        {
            phase = Phase.Windup;
            timer = e.d.WindupTime;
            e.SetVelocityX(0f);
            e.Play("Attack");
        }

        public override void Tick()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;

            if (phase == Phase.Windup)
            {
                e.FacePlayer();
                e.Fire();
                e.nextAttackTime = Time.time + e.d.AttackCooldown;

                phase = Phase.Recover;
                timer = e.d.RecoverTime;
            }
            else
            {
                e.fsm.ChangeState(e.aim);
            }
        }
    }

    private sealed class HitState : RangedState
    {
        private float timer;

        public float KnockbackDir { get; set; }

        public HitState(RangedEnemy enemy) : base(enemy) { }

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
                e.fsm.ChangeState(e.aim);
            }
        }
    }

    private sealed class DeadState : RangedState
    {
        public DeadState(RangedEnemy enemy) : base(enemy) { }

        public override void Enter()
        {
            e.SetVelocityX(0f);
            e.Play("Dead");
        }
    }
}
