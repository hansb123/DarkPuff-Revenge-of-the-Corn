using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 스테이지 안 미니게임 (문 앞에서 시작).
/// 흐름: 잠긴 문 앞에서 ↑ → Enter() → 스테이지를 처음부터, 어둡고 시야가 좁은 상태로 다시 시작
///       → 미니게임 중에만 나타나는 맵 변형(함정/벽/빛 뻥튀기 등)을 헤치고 진행
///       → 정해진 자리의 빛 뻥튀기에 닿으면 시야가 잠깐 넓어짐
///       → 하트 0 / 낙사 = 처음부터 다시 → 문에 다시 도착해 ↑ = Clear() → 문 해금
/// 씬에 하나만 두고, GoalDoor의 Mini Game 칸에 연결한다.
/// </summary>
public class StageMiniGame : MonoBehaviour, IMiniGame
{
    [Header("연결")]
    [SerializeField] private GameModeController gameMode;
    [SerializeField] private PlayerHealth health;

    [Header("어둠 / 시야")]
    [Tooltip("Global Light 2D. 비우면 씬에서 자동으로 찾음")]
    [SerializeField] private Light2D globalLight;
    [SerializeField, Range(0f, 1f)] private float darkIntensity = 0.04f;
    [Tooltip("플레이어 자식에 둔 Point Light 2D (평소에는 꺼 둠). 좁은 시야 역할")]
    [SerializeField] private Light2D playerLight;
    [SerializeField] private float baseRadius = 2.2f;
    [Header("빛 뻥튀기 (먹을수록 영구적으로 밝아짐)")]
    [Tooltip("하나 먹을 때마다 늘어나는 시야 반경")]
    [SerializeField] private float radiusPerLight = 1.0f;
    [SerializeField] private float maxRadius = 9f;
    [Tooltip("하나 먹을 때마다 전체 밝기(Global Light)도 이만큼 올라감. 0이면 시야만 넓어짐")]
    [SerializeField, Range(0f, 0.3f)] private float ambientPerLight = 0.03f;
    [SerializeField, Range(0f, 1f)] private float maxAmbient = 0.4f;
    [SerializeField] private float lightFadeTime = 0.6f;

    [Header("맵 변경")]
    [Tooltip("미니게임 중에만 켜지는 오브젝트 (추가 함정, 막는 벽 등)")]
    [SerializeField] private GameObject[] onlyInMiniGame;
    [Tooltip("미니게임 중에는 꺼지는 오브젝트 (쉬운 길, 평소 발판 등)")]
    [SerializeField] private GameObject[] hiddenInMiniGame;

    private PlayerSpecialAttack special;
    private float originalGlobal = 1f;
    private Coroutine lightRoutine;
    private int lightLevel;
    private bool running;

    public bool Cleared { get; private set; }
    public bool IsRunning => running;
    public int LightLevel => lightLevel;

    private float CurrentRadius => Mathf.Min(maxRadius, baseRadius + lightLevel * radiusPerLight);
    private float CurrentAmbient => Mathf.Min(maxAmbient, darkIntensity + lightLevel * ambientPerLight);

    public string ActionMapName => InputMaps.Platformer;   // 이동/공격 입력 그대로

    private void Awake()
    {
        if (gameMode == null) gameMode = FindFirstObjectByType<GameModeController>();
        if (health == null) health = FindFirstObjectByType<PlayerHealth>();
        special = FindFirstObjectByType<PlayerSpecialAttack>();
        if (globalLight == null)
            foreach (var l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
                if (l.lightType == Light2D.LightType.Global) { globalLight = l; break; }
        if (globalLight != null) originalGlobal = globalLight.intensity;
        if (playerLight != null) playerLight.enabled = false;
        SetMapChanges(false);
    }

    // ---------------- 문(GoalDoor)이 호출 ----------------

    public void Enter()
    {
        if (Cleared || running || gameMode == null) return;
        gameMode.StartMiniGame(this);   // → MiniGameState.Enter → Begin()
    }

    public void Clear()
    {
        if (!running) return;
        Cleared = true;
        gameMode.EndMiniGame(GameMode.Platforming);   // → End()
    }

    // ---------------- IMiniGame ----------------

    public void Begin()
    {
        running = true;
        lightLevel = 0;

        if (RespawnManager.Instance != null)
        {
            RespawnManager.Instance.UseCheckpoints = false;   // 죽으면 항상 처음부터
            RespawnManager.Instance.RespawnApplied += OnRestarted;
            RespawnManager.Instance.RestartFromStart();
        }

        if (special != null) special.ResetCharges();   // 뻥! 충전도 처음부터
        SetMapChanges(true);
        FadeLight(CurrentAmbient, CurrentRadius);
    }

    public void End()
    {
        running = false;

        if (RespawnManager.Instance != null)
        {
            RespawnManager.Instance.UseCheckpoints = true;
            RespawnManager.Instance.RespawnApplied -= OnRestarted;
        }

        lightLevel = 0;
        SetMapChanges(false);
        FadeLight(originalGlobal, baseRadius, turnOffPlayerLight: true);
    }

    private void OnRestarted()
    {
        // 죽었거나 처음부터 다시 시작할 때: 시야를 기본으로 (빛 뻥튀기는 스스로 다시 나타남)
        if (special != null) special.ResetCharges();
        // 이미 먹은 빛은 유지 (이 스테이지에서 영구). 시야는 지금 단계 그대로
        FadeLight(CurrentAmbient, CurrentRadius);
    }

    // ---------------- 빛 ----------------

    /// <summary>빛 뻥튀기를 먹었을 때 (LightCornPickup이 호출). 시야/밝기가 한 단계 영구적으로 올라감</summary>
    public void AddLight()
    {
        if (!running) return;
        lightLevel++;
        FadeLight(CurrentAmbient, CurrentRadius);
    }

    private void FadeLight(float globalTarget, float radiusTarget, bool turnOffPlayerLight = false)
    {
        if (lightRoutine != null) StopCoroutine(lightRoutine);
        lightRoutine = StartCoroutine(LightRoutine(globalTarget, radiusTarget, turnOffPlayerLight));
    }

    private IEnumerator LightRoutine(float globalTarget, float radiusTarget, bool turnOff)
    {
        if (playerLight != null && !turnOff) playerLight.enabled = true;

        float g0 = globalLight != null ? globalLight.intensity : 0f;
        float r0 = playerLight != null ? playerLight.pointLightOuterRadius : 0f;
        for (float t = 0f; t < lightFadeTime; t += Time.unscaledDeltaTime)
        {
            float k = t / lightFadeTime;
            if (globalLight != null) globalLight.intensity = Mathf.Lerp(g0, globalTarget, k);
            if (playerLight != null) playerLight.pointLightOuterRadius = Mathf.Lerp(r0, radiusTarget, k);
            yield return null;
        }
        if (globalLight != null) globalLight.intensity = globalTarget;
        if (playerLight != null)
        {
            playerLight.pointLightOuterRadius = radiusTarget;
            if (turnOff) playerLight.enabled = false;
        }
        lightRoutine = null;
    }

    // ---------------- 맵 변경 ----------------

    private void SetMapChanges(bool miniGame)
    {
        if (onlyInMiniGame != null)
            foreach (var o in onlyInMiniGame) if (o != null) o.SetActive(miniGame);
        if (hiddenInMiniGame != null)
            foreach (var o in hiddenInMiniGame) if (o != null) o.SetActive(!miniGame);
    }

    // 테스트용 (⋮ 메뉴)
    [ContextMenu("Debug/Enter")] private void DebugEnter() => Enter();
    [ContextMenu("Debug/Clear")] private void DebugClear() => Clear();
}
