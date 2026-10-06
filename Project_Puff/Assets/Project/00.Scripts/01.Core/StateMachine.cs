using System;
/// <summary>
/// 상태 하나만 활성화하고, 전환 시 Exit → Enter 순서를 보장하는 최소치? 라고 보면됨
/// 게임 모드용, 플레이어용 나눔
/// </summary>
public class StateMachine
{
    public IState Current { get; private set; }

    // (이전 상태, 다음 상태). 처음 진입 시 이전 상태는 null
    public event Action<IState, IState> StateChanged;

    public void ChangeState(IState next)
    {
        if (next == null || next == Current)
        {
            return;
        }

        IState previous = Current;

        previous?.Exit();
        Current = next;
        Current.Enter();

        StateChanged?.Invoke(previous, next);
    }

    public void Tick() //Tick => 업데이트라고 생각하면 편함.
    {
        Current?.Tick();
    }
}
