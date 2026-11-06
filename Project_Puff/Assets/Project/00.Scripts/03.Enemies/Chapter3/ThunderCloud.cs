using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 번개구름(엘리트 원거리). 하늘을 떠다니다 플레이어를 발견하면 머리 위로 따라붙고,
/// 가로로 가까워지면 "바닥 경고 → 낙뢰"를 떨어뜨린다. 가까이 붙어서 때려야 하는 높은 HP의 공중 몬스터.
/// FSM: Hover → Chase → Charge(경고) → Strike(번개) → Recover(빈틈) → Chase / (Hit) → Dead
/// 중요 설계: 경고 시점에 플레이어 위치를 고정 → 움직여서 피하면 됨. Charge/Strike 중에는 슈퍼아머(피격 경직 없음).
/// 몸체 Rigidbody2D: Dynamic, Gravity Scale 0 (Awake에서 자동으로 0 설정). 콜라이더는 비트리거.
/// 애니메이터 클립: Idle, Charge, Attack, Hit, Dead  (Walk 없음 — 떠다닐 때 Idle 사용)
/// </summary>
public class ThunderCloud : EnemyBase
{
    [Header("Lightning")]
    [SerializeField] private CloudLightningStrike strikePrefab;
    [SerializeField, Tooltip("번개가 닿을 바닥 레이어 (Ground). 비워두면 플레이어 발밑 추정")]
    private LayerMask strikeGroundLayer;

    private ThunderCloudData d;

    private HoverState hover;
    private ChaseState chase;
    private ChargeState charge;
    private StrikeState strike;
    private RecoverState recover;
    private HitState hit;
    private DeadState dead;

    private readonly List<CloudLightningStrike> activeStrikes = new List<CloudLightningStrike>();
    private float spawnY;

    protected override void Awake()
    {
        base.Awake();
        if (data == null) return;

        d = data as ThunderCloudData;
        if (d == null)
        {
            Debug.LogError($"[{name}] ThunderCloud에는 ThunderCloudData가 필요합니다.", this);
            enabled = false;
            return;
        }

        rb.gravityScale = 0f;
        spawnY = transform.position.y;

        hover = new HoverState(this);
        chase = new ChaseState(this);
        charge = new ChargeState(this);
        strike = new StrikeState(this);
        recover = new RecoverState(this);
        hit = new HitState(this);
        dead = new DeadState(this);
    }

    protected override IState GetInitialState() => hover;

    protected override void OnHit(float knockbackDir)
    {
        // 슈퍼아머: 번개를 준비/발사 중에는 맞아도 끊기지 않음 (피해는 이미 적용됨)
        if (fsm.Current == charge || fsm.Current == strike) return;

        hit.KnockbackDir = knockbackDir;
        if (fsm.Current == hit) hit.Restart();
        else fsm.ChangeState(hit);
    }

    protected override void Die()
    {
        foreach (var s in activeStrikes) if (s != null) s.Cancel();
        activeStrikes.Clear();
        fsm.ChangeState(dead);
        base.Die();
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;
    }

    // ---------------- 이동 ----------------

    private void FlyToward(float targetX, float targetY, float speedX)
    {
        float vx = Mathf.Clamp((targetX - transform.position.x) * 3f, -speedX, speedX);
        float vy = Mathf.Clamp((targetY - transform.position.y) * 3f, -d.VerticalSpeed, d.VerticalSpeed);

        // 구역 이탈 방지
        float fromSpawn = transform.position.x - spawnX;
        if (d.LeashDistance > 0f && Mathf.Abs(fromSpawn) >= d.LeashDistance && Mathf.Sign(vx) == Mathf.Sign(fromSpawn)) vx = 0f;

        rb.linearVelocity = new Vector2(vx, vy);
        if (Mathf.Abs(vx) > 0.1f) Face(vx > 0 ? 1 : -1);
    }

    private void StopFly() => rb.linearVelocity = Vector2.zero;

    // ---------------- 낙뢰 ----------------

    private void CastStrikes()
    {
        if (strikePrefab == null)
        {
            Debug.LogWarning("[ThunderCloud] Strike Prefab이 연결되지 않았습니다.", this);
            return;
        }
        if (player == null) return;

        int n = Mathf.Max(1, d.StrikesPerCast);
        for (int i = 0; i < n; i++)
        {
            // 0, +1, -1, +2, -2 ... 순으로 벌림 (1발이면 플레이어 위치)
            int k = (i + 1) / 2 * (i % 2 == 1 ? 1 : -1);
            float x = player.position.x + k * d.StrikeSpacing;
            SpawnStrike(x);
        }
    }

    private void SpawnStrike(float x)
    {
        LightningRod rod = LightningRod.FindNear(x, d.RodAttractRange);
        Vector3 pos;
        if (rod != null)
        {
            pos = rod.transform.position;
        }
        else
        {
            float y = GroundYAt(x);
            pos = new Vector3(x, y, 0f);
        }

        var s = Instantiate(strikePrefab, pos, Quaternion.identity);
        s.Init(d.StrikeDamage, d.StrikeWidth, d.WarnTime, d.StrikeTime, playerLayer, rod);
        activeStrikes.Add(s);
    }

    /// <summary>x 위치의 바닥 높이. 위에서 아래로 쏴서 찾고, 없으면 플레이어 발밑</summary>
    private float GroundYAt(float x)
    {
        float fromY = (player != null ? player.position.y : transform.position.y) + 2f;
        if (strikeGroundLayer.value != 0)
        {
            var hitInfo = Physics2D.Raycast(new Vector2(x, fromY), Vector2.down, 30f, strikeGroundLayer);
            if (hitInfo.collider != null) return hitInfo.point.y;
        }
        return (player != null ? player.position.y : transform.position.y) - 0.5f;
    }

    // ---------------- 상태들 ----------------

    private abstract class CloudState : IState
    {
        protected readonly ThunderCloud e;
        protected CloudState(ThunderCloud enemy) { e = enemy; }
        public virtual void Enter() { }
        public virtual void Tick() { }
        public virtual void Exit() { }
    }

    private sealed class HoverState : CloudState
    {
        private float turnCooldown;
        public HoverState(ThunderCloud enemy) : base(enemy) { }

        public override void Enter() { e.Play("Idle"); turnCooldown = 0f; }

        public override void Tick()
        {
            if (e.PlayerInSight())
            {
                e.AlertSfx();
                e.fsm.ChangeState(e.chase);
                return;
            }

            turnCooldown -= Time.deltaTime;
            if (turnCooldown <= 0f && e.PatrolRangeReached())
            {
                e.Face(-e.facing);
                turnCooldown = 0.3f;
            }

            // 원래 높이를 유지하며 천천히 좌우로
            e.FlyToward(e.transform.position.x + e.facing, e.spawnY, e.moveSpeed);
        }
    }

    private sealed class ChaseState : CloudState
    {
        public ChaseState(ThunderCloud enemy) : base(enemy) { }

        public override void Enter() { e.Play("Idle"); }

        public override void Tick()
        {
            if (!e.PlayerInSight())
            {
                e.fsm.ChangeState(e.hover);
                return;
            }

            float targetY = e.player.position.y + e.d.HoverHeight;
            e.FlyToward(e.player.position.x, targetY, e.moveSpeed * e.d.ChaseSpeedMultiplier);

            if (e.HorizontalDistanceToPlayer() <= e.d.AttackRange)
                e.fsm.ChangeState(e.charge);
        }
    }

    private sealed class ChargeState : CloudState
    {
        private float timer;
        public ChargeState(ThunderCloud enemy) : base(enemy) { }

        public override void Enter()
        {
            e.StopFly();
            e.FacePlayer();
            e.Play("Charge", true);
            e.AlertSfx();
            timer = e.d.WarnTime;
            e.CastStrikes();     // 이 순간 플레이어 위치가 고정되고 바닥 경고가 뜸
        }

        public override void Tick()
        {
            e.StopFly();
            timer -= Time.deltaTime;
            if (timer <= 0f) e.fsm.ChangeState(e.strike);
        }
    }

    private sealed class StrikeState : CloudState
    {
        private float timer;
        public StrikeState(ThunderCloud enemy) : base(enemy) { }

        public override void Enter()
        {
            e.Play("Attack", true);
            timer = e.d.StrikeTime;
        }

        public override void Tick()
        {
            e.StopFly();
            timer -= Time.deltaTime;
            if (timer <= 0f) e.fsm.ChangeState(e.recover);
        }
    }

    private sealed class RecoverState : CloudState
    {
        private float timer;
        public RecoverState(ThunderCloud enemy) : base(enemy) { }

        public override void Enter()
        {
            e.Play("Idle");
            timer = e.d.RecoverTime;
            e.activeStrikes.RemoveAll(s => s == null);
        }

        public override void Tick()
        {
            // 빈틈: 느리게 높이만 맞춤 (공격할 기회)
            // 빈틈: 때릴 수 있는 높이(RecoverHeight)까지 내려와 가만히 있음
            float ty = e.player != null ? e.player.position.y + e.d.RecoverHeight : e.spawnY;
            float vy = Mathf.Clamp((ty - e.transform.position.y) * 3f, -e.d.RecoverDescendSpeed, e.d.RecoverDescendSpeed);
            e.rb.linearVelocity = new Vector2(0f, vy);
            timer -= Time.deltaTime;
            if (timer <= 0f) e.fsm.ChangeState(e.chase);
        }
    }

    private sealed class HitState : CloudState
    {
        private float timer;
        public float KnockbackDir { get; set; }
        public HitState(ThunderCloud enemy) : base(enemy) { }

        public override void Enter() => Restart();

        public void Restart()
        {
            timer = e.hitStunTime;
            e.Play("Hit", true);
            e.rb.linearVelocity = new Vector2(KnockbackDir * e.knockbackSpeed, 0f);   // 구름은 위로 안 튐
        }

        public override void Tick()
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                e.StopFly();
                e.fsm.ChangeState(e.chase);
            }
        }
    }

    private sealed class DeadState : CloudState
    {
        public DeadState(ThunderCloud enemy) : base(enemy) { }
        public override void Enter() { e.StopFly(); e.Play("Dead"); }
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        var td = data as ThunderCloudData;
        if (td == null) return;

        Gizmos.color = new Color(1f, 0.9f, 0.2f);
        Vector3 p = transform.position;
        Gizmos.DrawLine(p + Vector3.left * td.AttackRange, p + Vector3.right * td.AttackRange);
        Gizmos.DrawWireSphere(p + Vector3.down * td.HoverHeight, 0.3f);
        Gizmos.color = new Color(0.6f, 0.8f, 1f);
        float cx = Application.isPlaying ? spawnX : p.x;
        Gizmos.DrawLine(new Vector3(cx - td.LeashDistance, p.y, 0f), new Vector3(cx + td.LeashDistance, p.y, 0f));
    }
}