/// <summary>
/// 게임 모드. 
/// 일시정지는 모드로 따로 두지 않음. (모든 씬에서 동작하니까.)
/// </summary>
public enum GameMode
{
    None,
    Title,
    Platforming,
    MiniGame,
    BossFight,
    Result
}

public static class InputMaps
{
    public const string Platformer = "Platformer";   // Move, Jump, Pause
    public const string UI = "UI";                   // Cancel
}