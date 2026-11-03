using UnityEngine;

/// <summary>
/// 3페이즈 하늘 그로기 카메라 연출. 평소에는 카메라를 전혀 건드리지 않는다 (고정 카메라 그대로).
///
///  1) 솟구침/바람(Rising, Summoning) : 카메라가 하늘로 올라가는 독수리를 따라간다
///  2) 어지러움(Dazed)                : 카메라가 아래로 내려가며 생겨난 플랫폼들을 훑고 플레이어에게 도착
///  3) 플랫포밍(Playable)             : 플레이어를 따라간다
///  4) 끝나면                          : 원래 고정 위치로 부드럽게 복귀
///
/// 아무 오브젝트(보스 등)에 붙이고, Camera 칸은 비우면 메인 카메라를 쓴다.
/// 연출 중(Rising~Dazed) 플레이어 조작을 막고 싶으면 아래 'Lock Behaviours' 에 플레이어의 이동/공격 스크립트를 넣는다 (선택).
/// </summary>
public class EagleSkyCamera : MonoBehaviour
{
    [SerializeField] private EagleBoss boss;
    [SerializeField] private Camera cam;
    [Tooltip("비우면 Player 태그를 찾음")]
    [SerializeField] private Transform player;

    [Header("독수리 따라 올라가기")]
    [Tooltip("독수리보다 이만큼 아래를 화면 중심으로 (양수 = 독수리가 화면 위쪽에 보임)")]
    [SerializeField] private float bossViewOffset = 0.5f;
    [Tooltip("올라갈 때 카메라가 따라가는 부드러움(초). 작을수록 독수리에 딱 붙음")]
    [SerializeField] private float bossFollowSmooth = 0.3f;

    [Header("플레이어 추적 (플랫포밍)")]
    [Tooltip("플레이어보다 이만큼 위를 비춤(유닛). 올라갈 곳이 더 보이게")]
    [SerializeField] private float lookAhead = 2.5f;
    [Tooltip("카메라가 따라가는 부드러움(초). 작을수록 빠름")]
    [SerializeField] private float followSmooth = 0.25f;
    [Tooltip("좌우도 따라갈지 (끄면 원래 X 에 고정)")]
    [SerializeField] private bool followX = false;
    [Tooltip("좌우를 따라갈 때 원래 X 에서 벗어날 수 있는 최대 거리")]
    [SerializeField] private float maxXOffset = 4f;
    [Tooltip("연출/추적 동안 화면 크기(Orthographic Size) 배율. 1 = 그대로, 1.2 = 더 넓게")]
    [SerializeField] private float zoomOut = 1.15f;

    [Header("복귀")]
    [SerializeField] private float returnTime = 0.9f;

    [Header("연출 중 플레이어 조작 잠금 (선택)")]
    [Tooltip("올라가는 연출(Rising~Dazed) 동안 꺼 둘 플레이어 스크립트들. 비워두면 조작 가능")]
    [SerializeField] private Behaviour[] lockBehaviours;

    private enum Mode { Off, FollowBoss, PanDown, FollowPlayer, Returning }
    private Mode mode = Mode.Off;

    private Vector3 origPos;
    private float origSize;
    private Vector3 vel;
    private float sizeVel;

    private float timer;
    private Vector3 fromPos;
    private float fromSize;
    private bool locked;
    private Rigidbody2D playerBody;

    private void Awake()
    {
        if (boss == null) boss = GetComponent<EagleBoss>();
        if (boss == null) boss = FindFirstObjectByType<EagleBoss>();
        if (cam == null) cam = Camera.main;
        if (player == null)
        {
            var go = GameObject.FindWithTag("Player");
            if (go != null) player = go.transform;
        }
        if (player != null) playerBody = player.GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        if (boss == null) return;
        boss.SkyStageChanged += OnStage;
        boss.SkyGroggyEnded += OnEnd;
    }

    private void OnDisable()
    {
        if (boss != null) { boss.SkyStageChanged -= OnStage; boss.SkyGroggyEnded -= OnEnd; }
        SetLock(false);
        if (mode != Mode.Off) Restore();
    }

    private void OnStage(EagleBoss.SkyStage stage, int level)
    {
        if (cam == null) return;
        switch (stage)
        {
            case EagleBoss.SkyStage.Rising:
                if (mode == Mode.Off || mode == Mode.Returning)
                {
                    if (mode == Mode.Off) { origPos = cam.transform.position; origSize = Size(); }
                }
                mode = Mode.FollowBoss;
                vel = Vector3.zero; sizeVel = 0f;
                SetLock(true);
                break;
            case EagleBoss.SkyStage.Dazed:
                mode = Mode.PanDown;
                timer = 0f;
                fromPos = cam.transform.position;
                fromSize = Size();
                break;
            case EagleBoss.SkyStage.Playable:
                mode = Mode.FollowPlayer;
                vel = Vector3.zero; sizeVel = 0f;
                SetLock(false);
                break;
        }
    }

    private void OnEnd()
    {
        SetLock(false);
        if (mode == Mode.Off || cam == null) return;
        mode = Mode.Returning;
        timer = 0f;
        fromPos = cam.transform.position;
        fromSize = Size();
    }

    private void SetLock(bool on)
    {
        if (locked == on) return;
        locked = on;
        if (lockBehaviours != null)
            foreach (var b in lockBehaviours) if (b != null) b.enabled = !on;
        if (on && playerBody != null) playerBody.linearVelocity = new Vector2(0f, playerBody.linearVelocity.y);
    }

    private float Size() => cam.orthographic ? cam.orthographicSize : cam.fieldOfView;
    private void SetSize(float v) { if (cam.orthographic) cam.orthographicSize = v; else cam.fieldOfView = v; }

    private void Restore()
    {
        if (cam == null) return;
        cam.transform.position = origPos;
        SetSize(origSize);
        mode = Mode.Off;
    }

    private Vector3 BossTarget()
    {
        float y = Mathf.Max(origPos.y, boss.transform.position.y - bossViewOffset);
        return new Vector3(origPos.x, y, origPos.z);
    }

    private Vector3 PlayerTarget()
    {
        if (player == null) return origPos;
        float tx = followX ? Mathf.Clamp(player.position.x, origPos.x - maxXOffset, origPos.x + maxXOffset) : origPos.x;
        float ty = Mathf.Max(origPos.y, player.position.y + lookAhead);   // 아래로는 원래 위치보다 내려가지 않음
        return new Vector3(tx, ty, origPos.z);
    }

    private void LateUpdate()
    {
        if (cam == null || mode == Mode.Off) return;
        float wantSize = origSize * zoomOut;

        switch (mode)
        {
            case Mode.FollowBoss:
                cam.transform.position = Vector3.SmoothDamp(cam.transform.position, BossTarget(), ref vel, bossFollowSmooth);
                if (cam.orthographic) SetSize(Mathf.SmoothDamp(Size(), wantSize, ref sizeVel, 0.6f));
                break;

            case Mode.PanDown:
                {
                    timer += Time.deltaTime;
                    float dur = Mathf.Max(0.05f, boss.SkyPanDownTime);
                    // 어지러운 포즈로 멈춘 잠깐(Daze Time) 은 그 자리를 보여준 뒤, 그 다음부터 내려간다
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((timer - boss.SkyDazeTime) / dur));
                    cam.transform.position = Vector3.Lerp(fromPos, PlayerTarget(), k);
                    SetSize(Mathf.Lerp(fromSize, wantSize, k));
                    break;
                }

            case Mode.FollowPlayer:
                cam.transform.position = Vector3.SmoothDamp(cam.transform.position, PlayerTarget(), ref vel, followSmooth);
                if (cam.orthographic) SetSize(Mathf.SmoothDamp(Size(), wantSize, ref sizeVel, followSmooth));
                break;

            case Mode.Returning:
                {
                    timer += Time.deltaTime;
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / Mathf.Max(0.05f, returnTime)));
                    cam.transform.position = Vector3.Lerp(fromPos, origPos, k);
                    SetSize(Mathf.Lerp(fromSize, origSize, k));
                    if (k >= 1f) mode = Mode.Off;
                    break;
                }
        }
    }
}