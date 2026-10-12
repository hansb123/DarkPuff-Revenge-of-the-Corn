using UnityEngine;

/// <summary>
/// 성난 호떡(근거리). 순찰하다가 플레이어를 발견하면 추적하고, 가까워지면 돌진 공격.
/// FSM: Patrol → Chase → Attack(예고/돌진/빈틈) → (Hit) → Dead
/// 상태 클래스는 이 파일 안의 중첩 클래스. 전환은 상태가 직접 fsm.ChangeState로 한다.
/// </summary>
public class MeleeEnemy : EnemyBase
{
    private MeleeEnemyData d;

    private PatrolState patrol;
    private ChaseState chase;
    private AttackState attack;
    private HitState hit;
    private DeadState dead;

    protected override void Awake()
    {
        base.Awake();
        if (data == null) return;

        d = data as MeleeEnemyData;
        if (d == null)
        {
            Debug.LogError($"[{name}] MeleeEnemy에는 MeleeEnemyData가 필요합니다.", this);
            enabled = false;
            return;
        }

        patrol = new PatrolState(this);
        chase = new ChaseState(this);
        attack = new AttackState(this);
        hit = new HitState(this);
        dead = new DeadState(this);
    }

    protected override IState GetInitialState() => patrol;

    protected override void OnHit(float knockbackDir)
    {
        hit.KnockbackDir = knockbackDir;

        if (fsm.Current == hit) hit.Restart();   // 같은 상태로는 전환이 무시되므로 직접 갱신
        else fsm.ChangeState(hit);
    }

    protected override void Die()
    {
        fsm.ChangeState(dead);
        base.Die();
    }

    private bool TryHitPlayer()
    {
        Vector2 center = (Vector2)transform.position + Vector2.right * (facing * d.HitBoxForward);
        var col = Physics2D.OverlapBox(center, d.HitBoxSize, 0f, playerLayer);
        if (col == null) return false;

        var target = col.GetComponentInParent<IDamageable>();
        if (target == null) return false;

        target.TakeDamage(d.AttackDamage, transform.position);
        return true;
    }

    // ---------------- 상태들 ----------------

    private abstract class MeleeState : IState
    {
        protected readonly MeleeEnemy e;
        protected MeleeState(MeleeEnemy enemy) { e = enemy; }

        public virtual void Enter() { }
        public virtual void Tick() { }
        public virtual void Exit() { }
    }

    private sealed class PatrolState : MeleeState
    {
        private float turnCooldown;

        public PatrolState(MeleeEnemy enemy) : base(enemy) { }

        public override void Enter()
        {
            turnCooldown = 0f;
            e.Play("Walk");
        }

        public override void Tick()
        {
            if (e.PlayerInSight())
            {
                e.fsm.ChangeState(e.chase);
                return;
            }

            turnCooldown -= Time.deltaTime;

            if (turnCooldown <= 0f && e.ShouldTurnAround())
            {
                e.Face(-e.facing);
                turnCooldown = 0.3f;        // 모서리에서 떨리듯 계속 도는 것 방지
            }

            e.SetVelocityX(e.facing * e.moveSpeed);
        }
    }

    private sealed class ChaseState : MeleeState
    {
        public ChaseState(MeleeEnemy enemy) : base(enemy) { }

        public override void Tick()
        {
            if (!e.PlayerInSight())
            {
                e.fsm.ChangeState(e.patrol);
                return;
            }

            e.FacePlayer();

            if (e.HorizontalDistanceToPlayer() <= e.d.AttackRange)
            {
                e.fsm.ChangeState(e.attack);
                return;
            }

            // 낭떠러지/벽 앞에서는 멈춤 (스스로 떨어지지 않음)
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

    private sealed class AttackState : MeleeState
    {
        private enum Phase { Windup, Lunge, Recover }

        private Phase phase;
        private float timer;
        private bool hitOnce;

        public AttackState(MeleeEnemy enemy) : base(enemy) { }

        public override void Enter()
        {
            phase = Phase.Windup;
            timer = e.d.WindupTime;     // 예고(플레이어가 피할 시간)
            hitOnce = false;
            e.SetVelocityX(0f);
            e.Play("Attack");
        }

        public override void Tick()
        {
            timer -= Time.deltaTime;

            switch (phase)
            {
                case Phase.Windup:
                    if (timer <= 0f)
                    {
                        phase = Phase.Lunge;
                        timer = e.d.LungeTime;
                    }
                    break;

                case Phase.Lunge:
                    e.SetVelocityX(e.facing * e.d.LungeSpeed);
                    if (!hitOnce) hitOnce = e.TryHitPlayer();

                    if (timer <= 0f)
                    {
                        phase = Phase.Recover;
                        timer = e.d.RecoverTime;   // 공격 후 빈틈
                        e.SetVelocityX(0f);
                    }
                    break;

                case Phase.Recover:
                    if (timer <= 0f) e.fsm.ChangeState(e.chase);
                    break;
            }
        }

        public override void Exit()
        {
            e.SetVelocityX(0f);
        }
    }

    private sealed class HitState : MeleeState
    {
        private float timer;

        public float KnockbackDir { get; set; }

        public HitState(MeleeEnemy enemy) : base(enemy) { }

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

    private sealed class DeadState : MeleeState
    {
        public DeadState(MeleeEnemy enemy) : base(enemy) { }

        public override void Enter()
        {
            e.SetVelocityX(0f);
            e.Play("Dead");
        }
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        var md = data as MeleeEnemyData;   // 에디터에서는 Awake 전이라 d가 비어 있음
        if (md == null) return;

        Gizmos.color = Color.red;
        Vector3 c = transform.position + Vector3.right * (Application.isPlaying ? facing : 1) * md.HitBoxForward;
        Gizmos.DrawWireCube(c, md.HitBoxSize);
    }
}
