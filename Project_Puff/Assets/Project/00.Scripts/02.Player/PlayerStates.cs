using UnityEngine;

/// <summary>
/// 플레이어 상태들. 지금은 Idle / Run / Jump 세 개만.
/// Jump는 "공중에 있는 동안" 전체를 맡음 (낙하도 포함). 나중에 Fall을 따로 빼도 됨.
/// Attack / Hit / Dead는 기능을 만들 때 같은 방식으로 클래스를 추가하면 됨.
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

        // 점프 직후에는 아직 바닥에 닿아 있다고 판정되므로, 내려오는 중일 때만 착지로 인정
        if (player.IsGrounded && player.Rb.linearVelocity.y <= 0.01f)
        {
            player.ChangeState(Mathf.Abs(inputX) > InputDeadZone ? player.RunState : player.IdleState);
        }
    }
}