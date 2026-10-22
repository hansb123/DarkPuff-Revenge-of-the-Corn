/// <summary>미니게임 진입 연출 (닭, 메카 닭 등). MiniGameStartTrigger가 이 인터페이스로 연출을 시작한다.</summary>
public interface IMiniGameIntro
{
    bool IsPlaying { get; }
    void Play();
    void Skip();
}
