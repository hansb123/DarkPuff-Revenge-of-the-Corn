using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 죽음 처리 + 부활. 씬에 하나만 둔다.
/// - 하트가 0이 되거나 낙사(Fall Y 아래로 떨어짐)하면 잠깐 기다린 뒤 마지막 체크포인트에서 부활
/// - UseCheckpoints = false 이면 항상 시작 지점에서 부활 (미니게임: "처음부터 다시")
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

    [Header("연출")]
    [SerializeField] private float deathDelay = 1.0f;       // 죽는 모션을 보여줄 시간
    [SerializeField] private float respawnInvincible = 1.2f;
    [Tooltip("선택: 검은색 Image + CanvasGroup (전체 화면). 죽고 나서 암전 → 부활 때 밝아짐")]
    [SerializeField] private CanvasGroup fade;
    [SerializeField] private float fadeTime = 0.25f;

    private Vector2 startPosition;
    private Checkpoint currentCheckpoint;
    private bool respawning;

    /// <summary>false면 체크포인트를 무시하고 항상 시작 지점으로 (미니게임)</summary>
    public bool UseCheckpoints { get; set; } = true;
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

        startPosition = startPoint != null ? (Vector2)startPoint.position : (Vector2)player.transform.position;
        if (fade != null) fade.alpha = 0f;
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
        currentCheckpoint = checkpoint;
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

        // 죽는 모션을 보여주고 암전
        yield return new WaitForSecondsRealtime(deathDelay);
        yield return Fade(1f);

        Vector2 pos = (UseCheckpoints && currentCheckpoint != null) ? currentCheckpoint.RespawnPosition : startPosition;
        player.transform.position = pos;
        health.ResetFull(respawnInvincible);
        player.Revive();
        RespawnApplied?.Invoke();

        yield return null;   // 한 프레임: 위치가 반영된 뒤 화면을 밝힘
        yield return Fade(0f);

        respawning = false;
        Respawned?.Invoke(DeathCount);
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
