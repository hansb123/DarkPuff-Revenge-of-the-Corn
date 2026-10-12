using UnityEngine;

/// <summary>
/// 플레이어 상태들. Idle / Run / Jump / Attack / Hit / Dead.
/// Jump는 "공중에 있는 동안" 전체를 맡음 (낙하도 포함). 나중에 Fall을 따로 빼도 됨.
/// </summary>
public abstract class PlayerState : IState
{
    protected const float InputDeadZone = 0.01f;

    protected readonly PlayerController player;

    protected PlayerState(PlayerController player)
    {
        this.player = player;
    }

    public virtual void Enter() { }
    public virtual void Tick() { }
    public virtual void Exit() { }
}

public sealed class PlayerIdleState : PlayerState
{
    public PlayerIdleState(PlayerController player) : base(player) { }

    public override void Enter()
    {
        player.SetHorizontalVelocity(0f);
        player.PlayAnimation("Idle");
    }

    public override void Tick()
    {
        // 발판 끝에서 떨어지는 경우
        if (!player.IsGrounded)
        {
            player.ChangeState(player.JumpState);
            return;
        }

        if (player.ConsumeJump())
        {
            player.Jump();
            player.ChangeState(player.JumpState);
            return;
        }

        if (player.ConsumeAttack())
        {
            player.ChangeState(player.AttackState);
            return;
        }

        if (Mathf.Abs(player.MoveInput) > InputDeadZone)
        {
            player.ChangeState(player.RunState);
        }
    }
}

public sealed class PlayerRunState : PlayerState
{
    public PlayerRunState(PlayerController player) : base(player) { }

    public override void Enter()
    {
        player.PlayAnimation("Run");
    }

    public override void Tick()
    {
        if (!player.IsGrounded)
        {
            player.ChangeState(player.JumpState);
            return;
        }

        if (player.ConsumeJump())
        {
            player.Jump();
            player.ChangeState(player.JumpState);
            return;
        }

        if (player.ConsumeAttack())
        {
            player.ChangeState(player.AttackState);
            return;
        }

        float inputX = player.MoveInput;

        if (Mathf.Abs(inputX) <= InputDeadZone)
        {
            player.ChangeState(player.IdleState);
            return;
        }

        player.SetHorizontalVelocity(inputX * player.MoveSpeed);
        player.FaceDirection(inputX);
    }
}

public sealed class PlayerJumpState : PlayerState
{
    public PlayerJumpState(PlayerController player) : base(player) { }

    public override void Enter()
    {
        player.PlayAnimation("Jump");
    }

    public override void Tick()
    {
        float inputX = player.MoveInput;

        // 공중에서도 좌우 조작 가능
        player.SetHorizontalVelocity(inputX * player.MoveSpeed);
        player.FaceDirection(inputX);

        // 코요테 타임: 발판에서 막 벗어난 직후에도 점프 가능
        if (player.CanCoyoteJump && player.ConsumeJump())
        {
            player.Jump();
            return;
        }

        // 공중 공격
        if (player.ConsumeAttack())
        {
            player.ChangeState(player.AttackState);
            return;
        }

        // 가변 점프: 상승 중 버튼을 떼면 낮게 뜀
        player.CutJumpIfReleased();

        // 점프 직후에는 아직 바닥에 닿아 있다고 판정되므로, 내려오는 중일 때만 착지로 인정
        if (player.IsGrounded && player.Rb.linearVelocity.y <= 0.01f)
        {
            player.ChangeState(Mathf.Abs(inputX) > InputDeadZone ? player.RunState : player.IdleState);
        }
    }
}

/// <summary>
/// 피격. 튕겨 나가며 잠깐 조작 불가. 끝나면 땅이면 Idle, 공중이면 Jump(낙하)로 복귀.
/// 무적/깜빡임은 PlayerHealth가 따로 처리하므로 여기서는 움직임만 다룸.
/// </summary>
public sealed class PlayerHitState : PlayerState
{
    private float timer;

    public PlayerHitState(PlayerController player) : base(player) { }

    public override void Enter()
    {
        timer = player.HitStunTime;
        player.PlayAnimation("Hit");
        player.ApplyHitKnockback();
    }

    public override void Tick()
    {
        // 조작 입력은 읽지 않음 (점프 버퍼도 소비하지 않으므로, 끝나기 직전에 누른 점프는 살아남음)
        timer -= Time.deltaTime;
        if (timer > 0f) return;

        player.ChangeState(player.IsGrounded ? player.IdleState : player.JumpState);
    }
}

/// <summary>사망. 입력을 받지 않음. 부활/결과 화면은 PlayerHealth.Died를 구독하는 쪽이 처리.</summary>
public sealed class PlayerDeadState : PlayerState
{
    public PlayerDeadState(PlayerController player) : base(player) { }

    public override void Enter()
    {
        player.SetHorizontalVelocity(0f);
        player.PlayAnimation("Dead");
    }
}

/// <summary>
/// 일반 공격(근접). 준비 → 판정 → 빈틈 순서. 땅에서는 제자리에서 휘두르고, 공중에서는 공중 조작을 유지한다.
/// 빈틈 단계에서 점프 입력이 있으면 공격을 끊고 점프할 수 있다 (조작감용).
/// </summary>
public sealed class PlayerAttackState : PlayerState
{
    private enum Phase { Startup, Active, Recover }

    private Phase phase;
    private float timer;

    public PlayerAttackState(PlayerController player) : base(player) { }

    private PlayerAttack Attack => player.Attack;

    public override void Enter()
    {
        phase = Phase.Startup;
        timer = Attack.StartupTime;

        Attack.BeginSwing();
        player.PlayAnimation("Attack");

        // 공격 방향 = 지금 누르고 있는 방향 (없으면 바라보던 방향)
        player.FaceDirection(player.MoveInput);
    }

    public override void Tick()
    {
        timer -= Time.deltaTime;

        if (player.IsGrounded)
        {
            player.SetHorizontalVelocity(0f);
        }
        else
        {
            // 공중에서는 좌우 조작 유지 (방향은 바꾸지 않음)
            player.SetHorizontalVelocity(player.MoveInput * player.MoveSpeed);
        }

        switch (phase)
        {
            case Phase.Startup:
                if (timer <= 0f)
                {
                    phase = Phase.Active;
                    timer = Attack.ActiveTime;
                    Attack.PlaySwingEffect(player.FacingDir);   // 판정이 켜지는 순간 이펙트도 같이
                }
                break;

            case Phase.Active:
                Attack.TryHit(player.FacingDir);

                if (timer <= 0f)
                {
                    phase = Phase.Recover;
                    timer = Attack.RecoverTime;
                }
                break;

            case Phase.Recover:
                // 점프로 공격 취소
                if (player.IsGrounded && player.ConsumeJump())
                {
                    player.Jump();
                    player.ChangeState(player.JumpState);
                    return;
                }

                if (timer <= 0f) Finish();
                break;
        }
    }

    private void Finish()
    {
        if (!player.IsGrounded)
        {
            player.ChangeState(player.JumpState);
        }
        else
        {
            player.ChangeState(Mathf.Abs(player.MoveInput) > InputDeadZone ? player.RunState : player.IdleState);
        }
    }
}
