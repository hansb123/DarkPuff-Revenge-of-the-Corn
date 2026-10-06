/// <summary>
/// 미니게임마다 조작방식이 다름. 때문에 다음과 같은 인터페이스를 만들었음. 
/// </summary>
public interface IMiniGame
{
    string ActionMapName { get; }   // 이 미니게임이 쓸 입력 맵 이름
    void Begin();                   // 시작 (씬/어드레서블 로드 이후)
    void End();                     // 종료 (정리)
}