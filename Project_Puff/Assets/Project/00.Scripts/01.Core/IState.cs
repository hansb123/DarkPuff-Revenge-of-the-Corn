

//해당 게임에서 기능할 상태머신의 상태
public interface IState
{
    void Enter();   // 이 상태로 들어올 때 1회
    void Tick();    // 유니티의 Update기능 이라고 생각하면 편함  
    void Exit();    // 이 상태를 떠날 때 1회
}