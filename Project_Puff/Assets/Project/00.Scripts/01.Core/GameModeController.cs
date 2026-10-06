using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 게임 모드(타이틀/플랫포밍/미니게임/보스전/결과) 상태머신.
/// 모드가 바뀔 때 입력 맵(Action Map)을 통째로 교체하고, 일시정지도 여기서 관리함.
/// 씬에 하나만 두면 됨.
/// </summary>
public class GameModeController : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private GameMode startMode = GameMode.Platforming;

    private readonly StateMachine machine = new StateMachine();
    private readonly Dictionary<GameMode, GameModeState> states = new Dictionary<GameMode, GameModeState>();
    private GameModeState currentState;

    private InputAction pauseAction;
    private InputAction cancelAction;

    // 입력 콜백 안에서 바로 맵을 바꾸지 않고, 다음 Update에서 처리하기 위한 플래그
    private bool pauseToggleRequested;

    public GameMode CurrentMode { get; private set; } = GameMode.None;
    public bool IsPaused { get; private set; }
    public IMiniGame CurrentMiniGame { get; private set; }

    // (이전 모드, 새 모드). 카메라/HUD/BGM 등이 구독해서 반응
    public event Action<GameMode, GameMode> ModeChanged;
    public event Action<bool> PauseChanged;

    private bool PauseAllowed =>
        CurrentMode == GameMode.Platforming ||
        CurrentMode == GameMode.BossFight ||
        CurrentMode == GameMode.MiniGame;

    private void Awake()
    {
        states[GameMode.Title] = new TitleState(this);
        states[GameMode.Platforming] = new PlatformingState(this);
        states[GameMode.MiniGame] = new MiniGameState(this);
        states[GameMode.BossFight] = new BossFightState(this);
        states[GameMode.Result] = new ResultState(this);

        if (inputActions == null)
        {
            Debug.LogError("[GameModeController] Input Actions 에셋이 연결되지 않았습니다.", this);
            return;
        }

        // 액션이 없어도 에러로 멈추지 않게 throwIfNotFound = false
        pauseAction = inputActions.FindAction($"{InputMaps.Platformer}/Pause", false);
        cancelAction = inputActions.FindAction($"{InputMaps.UI}/Cancel", false);
    }

    private void OnEnable()
    {
        if (pauseAction != null) pauseAction.performed += OnPausePerformed;
        if (cancelAction != null) cancelAction.performed += OnCancelPerformed;
    }

    private void OnDisable()
    {
        if (pauseAction != null) pauseAction.performed -= OnPausePerformed;
        if (cancelAction != null) cancelAction.performed -= OnCancelPerformed;

        // 일시정지 상태로 씬이 바뀌어도 시간이 멈춘 채로 남지 않게
        Time.timeScale = 1f;

        if (inputActions != null) inputActions.Disable();
    }

    private void Start()
    {
        SetMode(startMode);
    }

    private void Update()
    {
        if (pauseToggleRequested)
        {
            pauseToggleRequested = false;
            TogglePause();
        }

        machine.Tick();
    }

    private void OnPausePerformed(InputAction.CallbackContext context)
    {
        pauseToggleRequested = true;
    }

    private void OnCancelPerformed(InputAction.CallbackContext context)
    {
        // UI 맵의 Cancel(Esc)은 일시정지 중일 때만 해제로 사용
        if (IsPaused) pauseToggleRequested = true;
    }

    // ---------------- 모드 전환 ----------------

    public void SetMode(GameMode mode)
    {
        if (!states.TryGetValue(mode, out GameModeState next) || next == currentState)
        {
            return;
        }

        // 일시정지 중에 모드가 바뀌면 일시정지부터 풀고 진행
        if (IsPaused) Resume();

        GameMode previous = CurrentMode;

        CurrentMode = mode;
        currentState = next;
        machine.ChangeState(next);

        ModeChanged?.Invoke(previous, mode);
    }

    /// <summary>
    /// 미니게임 시작. 어드레서블로 미니게임을 로드한 뒤 이걸 호출하는 흐름을 가정.
    /// </summary>
    public void StartMiniGame(IMiniGame game)
    {
        if (game == null) return;

        if (CurrentMode == GameMode.MiniGame)
        {
            Debug.LogWarning("[GameModeController] 이미 미니게임 진행 중입니다. EndMiniGame()을 먼저 호출하세요.", this);
            return;
        }

        CurrentMiniGame = game;
        SetMode(GameMode.MiniGame);
    }

    public void EndMiniGame(GameMode returnMode = GameMode.Platforming)
    {
        if (CurrentMode != GameMode.MiniGame) return;

        SetMode(returnMode);
    }

    // MiniGameState.Exit()에서 호출
    public void ClearMiniGame()
    {
        CurrentMiniGame = null;
    }

    // ---------------- 일시정지 (모드가 아닌 오버레이) ----------------

    public void TogglePause()
    {
        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (IsPaused || !PauseAllowed) return;

        IsPaused = true;
        Time.timeScale = 0f;
        EnableInputMap(InputMaps.UI);

        PauseChanged?.Invoke(true);
    }

    public void Resume()
    {
        if (!IsPaused) return;

        IsPaused = false;
        Time.timeScale = 1f;

        // 일시정지 전의 모드가 쓰던 입력 맵으로 되돌림
        if (currentState != null) EnableInputMap(currentState.InputMapName);

        PauseChanged?.Invoke(false);
    }

    // ---------------- 입력 맵 ----------------

    /// <summary>
    /// 지정한 Action Map만 켜고 나머지는 전부 끔.
    /// </summary>
    public void EnableInputMap(string mapName)
    {
        if (inputActions == null) return;

        InputActionMap map = inputActions.FindActionMap(mapName, false);

        if (map == null)
        {
            Debug.LogWarning($"[GameModeController] Action Map '{mapName}'을(를) 찾을 수 없습니다. Input Actions 에셋을 확인하세요.", this);
            return;
        }

        inputActions.Disable();
        map.Enable();
    }

    // ---------------- 테스트용 (컴포넌트 우측 상단 ⋮ 메뉴 → 실행) ----------------

    [ContextMenu("Debug/Mode - Title")] private void DebugTitle() => SetMode(GameMode.Title);
    [ContextMenu("Debug/Mode - Platforming")] private void DebugPlatforming() => SetMode(GameMode.Platforming);
    [ContextMenu("Debug/Mode - BossFight")] private void DebugBossFight() => SetMode(GameMode.BossFight);
    [ContextMenu("Debug/Mode - Result")] private void DebugResult() => SetMode(GameMode.Result);
}