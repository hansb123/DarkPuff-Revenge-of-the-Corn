using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 죽음 처리 + 부활. 씬에 하나만 둔다.
/// - 하트가 0이 되거나 낙사(Fall Y 아래로 떨어짐)하면 잠깐 기다린 뒤 마지막 체크포인트에서 부활
/// - UseCheckpoints = false (미니게임) 이면 일반 체크포인트는 무시하고,
///   '미니게임 전용' 체크포인트를 밟았으면 거기서, 아니면 시작 지점에서 부활
/// - 부활할 때마다 Respawned 이벤트 (미니게임이 맵을 되돌리거나 사망 횟수 UI가 구독)
/// </summary>
public class RespawnManager : MonoBehaviour
{
    public static RespawnManager Instance { get; private set; }

    [Header("Player")]
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerHealth health;

    [Header("위치")]
    [Tooltip("스테이지 시작 위치 (비워 두면 씬 시작 때 플레이어 위치)")]
    [SerializeField] private Transform startPoint;
    [Tooltip("이 높이보다 아래로 떨어지면 낙사")]
    [SerializeField] private float fallY = -15f;
    /// <summary>이 높이 아래로 떨어지면 낙사 (적도 같은 기준으로 즉사)</summary>
    public float FallY => fallY;

    [Header("재시작 방식")]
    [Tooltip("켜면 죽었을 때 부활 대신 씬을 처음부터 다시 불러옴 (적/아이템/함정/게이지 전부 초기화)")]
    [SerializeField] private bool reloadSceneOnDeath = false;
    [Tooltip("켜면 미니게임 모드(UseCheckpoints=false)일 때만 씬을 다시 불러옴. 일반 구간은 기존대로 체크포인트 부활")]
    [SerializeField] private bool onlyInMiniGameMode = true;

    /// <summary>방금 죽어서 씬이 다시 로드된 것인지. 읽으면 false로 되돌아감 (연출 건너뛰기용)</summary>
    public static bool ConsumeReloadedByDeath()
    {
        bool v = reloadedByDeath; reloadedByDeath = false; return v;
    }
    private static bool reloadedByDeath;

    [Header("목숨 (노말 모드 전용. 이지 모드는 무한 부활)")]
    [Tooltip("노말 모드 시작 목숨. 씬(스테이지)마다 새로 채워진다")]
    [SerializeField] private int normalLives = 5;
    [Tooltip("미니게임에 처음 들어갈 때 이 스테이지에서 한 번 추가되는 목숨")]
    [SerializeField] private int miniGameBonusLives = 5;
    [Tooltip("목숨을 모두 잃었을 때 보여줄 패널 (선택). 이 시간 동안 보여준 뒤 스테이지를 처음부터 다시 불러옴")]
    [SerializeField] private GameObject outOfLivesPanel;
    [SerializeField] private float outOfLivesDelay = 2.0f;

    [Header("연출")]
    [SerializeField] private float deathDelay = 1.0f;       // 죽는 모션을 보여줄 시간
    [SerializeField] private float respawnInvincible = 1.2f;
    [Tooltip("선택: 검은색 Image + CanvasGroup (전체 화면). 죽고 나서 암전 → 부활 때 밝아짐")]
    [SerializeField] private CanvasGroup fade;
    [SerializeField] private float fadeTime = 0.25f;

    private Vector2 startPosition;
    private Checkpoint currentCheckpoint;       // 일반 체크포인트
    private Checkpoint currentMiniCheckpoint;   // 미니게임 전용 체크포인트
    private bool respawning;
    private bool useCheckpoints = true;
    private int lives;
    private int livesCapacity;          // HUD에 보여줄 칸 수 (시작 목숨 + 보너스)
    private bool bonusGranted;
    private bool bossFight;             // 보스전: 이지/노말 모두 목숨 없음 (사망해도 목숨이 깎이지 않음)

    /// <summary>
    /// false면 미니게임 모드: 일반 체크포인트는 무시하고 미니게임 전용 체크포인트만 사용.
    /// true로 돌아올 때(미니게임 종료) 미니게임 전용 체크포인트는 초기화됨
    /// </summary>
    public bool UseCheckpoints
    {
        get => useCheckpoints;
        set
        {
            if (value && !useCheckpoints) ClearMiniGameCheckpoints();
            // 미니게임에 처음 들어갈 때(체크포인트 사용 끔) 목숨 보너스 (스테이지당 한 번)
            if (!value && useCheckpoints && !bossFight) GrantMiniGameBonus();
            useCheckpoints = value;
        }
    }

    // ---------------- 목숨 ----------------

    /// <summary>노말 모드인가 (목숨이 있다). 이지 모드는 무한 부활</summary>
    public bool HasLives => GameDifficulty.IsNormal;
    public int Lives => lives;
    public int LivesCapacity => livesCapacity;

    /// <summary>보스전 중인가. 보스전에서는 목숨이 깎이지 않고 보너스도 없다</summary>
    public bool BossFight => bossFight;
    public event Action<bool> BossFightChanged;

    /// <summary>보스전 시작. 체크포인트를 끄고(죽으면 처음부터) 목숨 소모를 멈춘다. 닭/메카/독수리 보스가 호출</summary>
    public void BeginBossFight()
    {
        bool changed = !bossFight;
        bossFight = true;      // 먼저 켜야 미니게임 보너스(+5)가 붙지 않는다
        UseCheckpoints = false;
        if (changed) BossFightChanged?.Invoke(true);
    }

    /// <summary>보스전 종료. 체크포인트와 목숨 소모를 원래대로</summary>
    public void EndBossFight()
    {
        bool changed = bossFight;
        bossFight = false;
        UseCheckpoints = true;
        if (changed) BossFightChanged?.Invoke(false);
    }

    /// <summary>목숨이 바뀔 때 (남은 목숨, 표시 칸 수, 늘어난 것인지)</summary>
    public event Action<int, int, bool> LivesChanged;

    /// <summary>목숨 추가 (보너스). 이 씬에서만 유효</summary>
    public void AddLives(int amount)
    {
        if (!HasLives || amount <= 0) return;
        lives += amount;
        livesCapacity += amount;
        LivesChanged?.Invoke(lives, livesCapacity, true);
    }

    private void GrantMiniGameBonus()
    {
        if (bonusGranted) return;
        bonusGranted = true;
        AddLives(miniGameBonusLives);
    }

    /// <summary>미니게임 전용 체크포인트가 초기화될 때 (체크포인트가 표시를 원래대로 돌리는 용도)</summary>
    public event Action MiniGameCheckpointsCleared;
    public int DeathCount { get; private set; }

    /// <summary>부활이 끝난 직후. 인자는 누적 사망 횟수</summary>
    public event Action<int> Respawned;

    /// <summary>
    /// 플레이어를 되돌려 놓은 직후, 화면이 아직 까만 상태. 함정/장애물이 원래 자리로 돌아가는 타이밍
    /// (밝아진 뒤에 되돌아가면 눈에 보이므로 이쪽을 쓴다)
    /// </summary>
    public event Action RespawnApplied;

    private void Awake()
    {
        Instance = this;

        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (health == null && player != null) health = player.GetComponent<PlayerHealth>();

        if (player == null || health == null)
        {
            Debug.LogError("[RespawnManager] PlayerController / PlayerHealth를 찾지 못했습니다.", this);
            enabled = false;
            return;
        }

        lives = livesCapacity = normalLives;

        startPosition = startPoint != null ? (Vector2)startPoint.position : (Vector2)player.transform.position;
        if (fade != null) fade.alpha = 0f;
    }

    private void Start()
    {
        // HUD가 Awake 순서와 상관없이 처음 값을 받도록
        if (HasLives) LivesChanged?.Invoke(lives, livesCapacity, false);
        if (outOfLivesPanel != null) outOfLivesPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        if (health != null) health.Died += OnDied;
    }

    private void OnDisable()
    {
        if (health != null) health.Died -= OnDied;
    }

    private void Update()
    {
        // 낙사: 무적/하트와 상관없이 즉사
        if (!respawning && !health.IsDead && player.transform.position.y < fallY)
            health.Kill();
    }

    public void SetCheckpoint(Checkpoint checkpoint)
    {
        if (checkpoint.MiniGameOnly) currentMiniCheckpoint = checkpoint;
        else currentCheckpoint = checkpoint;
    }

    private void ClearMiniGameCheckpoints()
    {
        currentMiniCheckpoint = null;
        MiniGameCheckpointsCleared?.Invoke();
    }

    private Vector2 GetRespawnPosition()
    {
        if (UseCheckpoints)
            return currentCheckpoint != null ? currentCheckpoint.RespawnPosition : startPosition;

        return currentMiniCheckpoint != null ? currentMiniCheckpoint.RespawnPosition : startPosition;
    }

    private void OnDied()
    {
        if (respawning) return;
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        respawning = true;
        DeathCount++;

        // 노말 모드: 목숨 1개 소모
        bool outOfLives = false;
        if (HasLives && !bossFight)
        {
            lives = Mathf.Max(0, lives - 1);
            LivesChanged?.Invoke(lives, livesCapacity, false);
            outOfLives = lives <= 0;
        }

        // 죽는 모션을 보여주고 암전
        yield return new WaitForSecondsRealtime(deathDelay);

        if (outOfLives)
        {
            // 목숨을 모두 잃음 → 이 스테이지를 처음부터
            if (outOfLivesPanel != null) outOfLivesPanel.SetActive(true);
            yield return new WaitForSecondsRealtime(outOfLivesDelay);
            yield return Fade(1f);
            reloadedByDeath = true;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            yield break;
        }

        yield return Fade(1f);

        if (reloadSceneOnDeath && (!onlyInMiniGameMode || !UseCheckpoints))
        {
            reloadedByDeath = true;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            yield break;
        }

        player.transform.position = GetRespawnPosition();
        health.ResetFull(respawnInvincible);
        player.Revive();
        RespawnApplied?.Invoke();

        yield return null;   // 한 프레임: 위치가 반영된 뒤 화면을 밝힘
        yield return Fade(0f);

        respawning = false;
        Respawned?.Invoke(DeathCount);
    }

    /// <summary>
    /// 죽지 않았지만 처음 위치로 되돌릴 때 (미니게임 시작). 암전 → 시작 지점 이동 → 밝아짐.
    /// 함정도 RespawnApplied로 원래 자리로 돌아간다.
    /// </summary>
    public void RestartFromStart()
    {
        if (respawning) return;
        StartCoroutine(RestartRoutine());
    }

    private IEnumerator RestartRoutine()
    {
        respawning = true;
        yield return Fade(1f);

        player.transform.position = startPosition;
        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;
        health.ResetFull(respawnInvincible);
        ClearMiniGameCheckpoints();   // 미니게임을 새로 시작하므로 전용 체크포인트도 처음 상태로
        RespawnApplied?.Invoke();

        yield return null;
        yield return Fade(0f);
        respawning = false;
    }

    private IEnumerator Fade(float target)
    {
        if (fade == null) yield break;

        float from = fade.alpha, t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            fade.alpha = Mathf.Lerp(from, target, t / fadeTime);
            yield return null;
        }
        fade.alpha = target;
    }

    private void OnDrawGizmosSelected()
    {
        // 낙사 기준선
        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector3(-1000f, fallY, 0f), new Vector3(1000f, fallY, 0f));

        if (startPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(startPoint.position, 0.35f);
        }
    }
}