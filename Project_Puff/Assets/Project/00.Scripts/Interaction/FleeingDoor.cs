using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 도망가는 문. 플레이어가 가까이 가면 놀라서 정해둔 위치(Waypoints)로 달아난다.
/// 도착한 자리에서 플레이어가 또 다가오면 다음 위치로 도망... 마지막 위치에서는 지쳐서 더 못 도망가고,
/// 같은 오브젝트의 GoalDoor가 해금되어 평소 클리어 문이 된다.
///
/// 구성: 문 오브젝트 하나에 GoalDoor(Start Unlocked 끄기, Is Trigger 콜라이더) + 이 스크립트.
///   ├ (자기 SpriteRenderer = GoalDoor의 문 그림. 도망치는 동안엔 이 스크립트가 꺼둠)
///   └ Runner (자식 SpriteRenderer, flee_* 그림)  ← Runner Visual 에 연결
/// Waypoints는 문이 서 있을 바닥 위 지점들(스프라이트 피벗 = 발바닥). 문의 시작 위치가 0번째 자리이다.
/// 이동은 직선 + 통통 뛰기이므로, 벽/구멍을 넘는 길이면 지점을 그 앞뒤로 나눠 찍는다.
/// </summary>
[RequireComponent(typeof(GoalDoor))]
public class FleeingDoor : MonoBehaviour
{
    [Header("도망갈 위치 (순서대로)")]
    [Tooltip("문이 차례로 도망갈 자리. 빈 오브젝트를 바닥에 놓고 끌어다 넣는다. 마지막이 최종 자리(여기서 클리어 문이 됨)")]
    [SerializeField] private Transform[] waypoints;

    [Header("발동")]
    [Tooltip("플레이어가 이 거리 안으로 들어오면 도망 (유닛)")]
    [SerializeField] private float triggerDistance = 3.5f;
    [Tooltip("놀라는 시간 (느낌표 포즈)")]
    [SerializeField] private float alertTime = 0.4f;
    [Tooltip("도착 후 이 시간 동안은 다시 놀라지 않음")]
    [SerializeField] private float rearmDelay = 0.6f;

    [Header("달리기")]
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private float stepInterval = 0.1f;
    [SerializeField] private float hopHeight = 0.18f;
    [SerializeField] private float hopFrequency = 12f;

    [Header("마지막 자리")]
    [Tooltip("마지막 자리에 도착해 지친 포즈로 있는 시간. 이 뒤에 클리어 문으로 바뀜")]
    [SerializeField] private float tiredTime = 1.2f;
    [Tooltip("켜면 도망 중 플레이어가 죽어 리스폰할 때 첫 자리로 되돌아감 (마지막까지 간 뒤에는 유지)")]
    [SerializeField] private bool resetOnRespawn = true;

    [Header("그림")]
    [SerializeField] private SpriteRenderer runnerVisual;      // 자식 Runner의 SpriteRenderer
    [SerializeField] private Sprite idleSprite;                // flee_idle
    [SerializeField] private Sprite alertSprite;               // flee_alert
    [SerializeField] private Sprite[] runSprites;              // flee_run_1, flee_run_2
    [SerializeField] private Sprite tiredSprite;               // flee_tired
    [Tooltip("스프라이트는 오른쪽을 보는 기준. 달리는 방향에 따라 좌우 반전")]
    [SerializeField] private bool spriteFacesRight = true;

    [Header("이벤트 (효과음 연결용)")]
    public UnityEvent OnAlert;
    public UnityEvent OnStartRun;
    public UnityEvent OnArrive;
    public UnityEvent OnFinalReached;

    private enum Phase { Waiting, Alert, Running, Final, Done }

    private GoalDoor goalDoor;
    private SpriteRenderer ownRenderer;
    private Transform player;
    private Vector3 startPos;
    private int nextIndex;            // 다음에 도망갈 waypoint 번호
    private Phase phase = Phase.Waiting;
    private float armedAt;
    private RespawnManager manager;

    private void Awake()
    {
        goalDoor = GetComponent<GoalDoor>();
        ownRenderer = GetComponent<SpriteRenderer>();
        startPos = transform.position;
    }

    private void Start()
    {
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;
        else Debug.LogWarning("[FleeingDoor] 씬에서 PlayerController를 찾지 못했습니다.", this);

        if (waypoints == null || waypoints.Length == 0)
            Debug.LogWarning("[FleeingDoor] Waypoints가 비어 있습니다. 도망갈 자리를 지정하세요.", this);

        manager = RespawnManager.Instance;
        if (manager != null) manager.RespawnApplied += OnRespawn;

        ShowRunner(true);
        SetSprite(idleSprite);
        armedAt = Time.time;
    }

    private void OnDestroy()
    {
        if (manager != null) manager.RespawnApplied -= OnRespawn;
    }

    private void OnRespawn()
    {
        if (!resetOnRespawn || phase == Phase.Final || phase == Phase.Done) return;
        StopAllCoroutines();
        transform.position = startPos;
        nextIndex = 0;
        phase = Phase.Waiting;
        SetSprite(idleSprite);
        if (runnerVisual != null) runnerVisual.transform.localPosition = Vector3.zero;
        armedAt = Time.time;
    }

    private void Update()
    {
        if (phase != Phase.Waiting || player == null) return;
        if (waypoints == null || nextIndex >= waypoints.Length) return;
        if (Time.time - armedAt < rearmDelay) return;

        Vector2 d = player.position - transform.position;
        if (d.magnitude <= triggerDistance) StartCoroutine(FleeRoutine());
    }

    // ---------------- 도망 ----------------

    private IEnumerator FleeRoutine()
    {
        // 1) 놀람
        phase = Phase.Alert;
        FaceAwayFromPlayer();
        SetSprite(alertSprite);
        OnAlert?.Invoke();
        yield return new WaitForSeconds(alertTime);

        // 2) 달리기
        phase = Phase.Running;
        OnStartRun?.Invoke();
        Vector3 target = waypoints[nextIndex].position;
        Face(target.x - transform.position.x);

        float step = 0f; int frame = 0; float t = 0f;
        while (Vector2.Distance(transform.position, target) > 0.02f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, runSpeed * Time.deltaTime);
            t += Time.deltaTime; step += Time.deltaTime;
            if (step >= stepInterval)
            {
                step = 0f;
                if (runSprites != null && runSprites.Length > 0) { frame = (frame + 1) % runSprites.Length; SetSprite(runSprites[frame]); }
            }
            if (runnerVisual != null)
                runnerVisual.transform.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(t * hopFrequency)) * hopHeight, 0f);
            yield return null;
        }
        transform.position = target;
        if (runnerVisual != null) runnerVisual.transform.localPosition = Vector3.zero;

        nextIndex++;
        OnArrive?.Invoke();

        // 3) 도착: 마지막이면 지침 → 클리어 문, 아니면 다시 대기
        if (nextIndex >= waypoints.Length)
        {
            yield return FinalRoutine();
        }
        else
        {
            SetSprite(idleSprite);
            phase = Phase.Waiting;
            armedAt = Time.time;
        }
    }

    private IEnumerator FinalRoutine()
    {
        phase = Phase.Final;
        SetSprite(tiredSprite);
        FaceTowardPlayer();
        OnFinalReached?.Invoke();
        yield return new WaitForSeconds(tiredTime);

        // 도망치던 그림을 숨기고 원래 클리어 문 그림으로 교체 후 해금
        ShowRunner(false);
        if (ownRenderer != null) ownRenderer.enabled = true;
        if (ownRenderer != null) ownRenderer.flipX = false;
        goalDoor.Unlock();
        phase = Phase.Done;
    }

    // ---------------- 보조 ----------------

    private void ShowRunner(bool on)
    {
        if (runnerVisual != null) runnerVisual.enabled = on;
        if (ownRenderer != null && on) ownRenderer.enabled = false;   // 도망 중엔 문 그림을 숨김
    }

    private void SetSprite(Sprite s)
    {
        if (runnerVisual != null && s != null) runnerVisual.sprite = s;
    }

    private void Face(float dx)
    {
        if (runnerVisual == null || Mathf.Abs(dx) < 0.01f) return;
        bool right = dx > 0f;
        runnerVisual.flipX = spriteFacesRight ? !right : right;
    }

    private void FaceAwayFromPlayer()
    {
        if (player == null) return;
        Face(transform.position.x - player.position.x);
    }

    private void FaceTowardPlayer()
    {
        if (player == null) return;
        Face(player.position.x - transform.position.x);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, triggerDistance);

        if (waypoints == null) return;
        Vector3 prev = Application.isPlaying ? startPos : transform.position;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            Gizmos.color = i == waypoints.Length - 1 ? Color.green : Color.yellow;
            Gizmos.DrawLine(prev, waypoints[i].position);
            Gizmos.DrawWireSphere(waypoints[i].position, 0.3f);
            prev = waypoints[i].position;
        }
    }
}