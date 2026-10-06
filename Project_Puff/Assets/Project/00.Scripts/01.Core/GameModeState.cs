// 게임 모드 상태들. 각 상태는 "어떤 입력 맵을 켤지"만 정함.
public abstract class GameModeState : IState
{
    protected readonly GameModeController controller;
    public abstract GameMode Mode { get; }
    public virtual string InputMapName => InputMaps.Platformer;

    protected GameModeState(GameModeController controller)
    {
        this.controller = controller;
    }

    public virtual void Enter() => controller.EnableInputMap(InputMapName);
    public virtual void Tick() { }
    public virtual void Exit() { }
}

public sealed class TitleState : GameModeState
{
    public TitleState(GameModeController controller) : base(controller) { }
    public override GameMode Mode => GameMode.Title;
    public override string InputMapName => InputMaps.UI;
}

public sealed class PlatformingState : GameModeState
{
    public PlatformingState(GameModeController controller) : base(controller) { }
    public override GameMode Mode => GameMode.Platforming;
}

// 입력은 플랫포밍과 같지만, 카메라/보스 HUD/BGM 등이 ModeChanged 이벤트로 구분
public sealed class BossFightState : GameModeState
{
    public BossFightState(GameModeController controller) : base(controller) { }

    public override GameMode Mode => GameMode.BossFight;
}

public sealed class ResultState : GameModeState
{
    public ResultState(GameModeController controller) : base(controller) { }

    public override GameMode Mode => GameMode.Result;
    public override string InputMapName => InputMaps.UI;
}

public sealed class MiniGameState : GameModeState
{
    public MiniGameState(GameModeController controller) : base(controller) { }
    public override GameMode Mode => GameMode.MiniGame;
    // 미니게임이 자기 입력 맵을 지정함. 없으면 플랫포머 맵 사용
    public override string InputMapName =>
        controller.CurrentMiniGame != null ? controller.CurrentMiniGame.ActionMapName : InputMaps.Platformer;
    public override void Enter()
    {
        base.Enter();
        controller.CurrentMiniGame?.Begin();
    }

    public override void Exit()
    {
        controller.CurrentMiniGame?.End();
        controller.ClearMiniGame();
    }
}