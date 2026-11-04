using UnityEngine;

/// <summary>
/// 독수리 포즈 표시. 정면샷(1페이즈)과 측면샷(2페이즈~)을 모두 담당한다.
///  - Side From Phase 이상의 페이즈에서는 측면 포즈를 쓰고, 플레이어 쪽을 바라보도록 좌우로 뒤집는다 (측면 그림은 왼쪽을 향함)
///  - 평소엔 날갯짓, EaglePose 신호(예고/공격/포효/급강하/그로기/피격)가 오면 해당 포즈
/// 보스 오브젝트(SpriteRenderer 가 있는 곳)에 붙인다. Boss 를 연결하면 페이즈 진입 때 시점이 바뀌고 포효한다.
/// 임포트: 정면/측면 모든 포즈의 Pivot 을 Custom (0.5, 0.5) 로 통일해야 시점이 바뀔 때 위치가 튀지 않는다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class EaglePoseController : MonoBehaviour
{
    [Header("정면샷 스프라이트 (eagle_front_*)")]
    [SerializeField] private Sprite idle1;
    [SerializeField] private Sprite idle2;
    [SerializeField] private Sprite windup;
    [SerializeField] private Sprite strike;
    [SerializeField] private Sprite roar;
    [Tooltip("eagle_front_groggy. 비우면 idle_2 사용")]
    [SerializeField] private Sprite groggy;

    [Header("측면샷 스프라이트 (eagle_*, 왼쪽을 향한 그림)")]
    [SerializeField] private Sprite sideIdle;
    [SerializeField] private Sprite sideFlapUp;
    [SerializeField] private Sprite sideFlapDown;
    [Tooltip("날개 편 포즈 (포효에 사용)")]
    [SerializeField] private Sprite sideSpread;
    [Tooltip("eagle_roar. 머리를 젖히고 부리를 크게 벌린 포효 포즈. 비우면 spread 사용")]
    [SerializeField] private Sprite sideRoar;
    [SerializeField] private Sprite sideWindup;
    [Tooltip("날개 휘두르는 포즈 (일반 공격에 사용)")]
    [SerializeField] private Sprite sideSwing;
    [SerializeField] private Sprite sideDive;
    [Tooltip("eagle_stand. 땅에 내려앉은 포즈 (날개 후려치기에 사용). 비우면 idle")]
    [SerializeField] private Sprite sideStand;
    [SerializeField] private Sprite sideGroggy;
    [Tooltip("하늘 그로기(3페이즈)에서 공중에 떠서 어지러워하는 포즈. 비우면 groggy 사용")]
    [SerializeField] private Sprite sideSkyGroggy;
    [Tooltip("그로기 중 맞았을 때 번쩍 보이는 포즈")]
    [SerializeField] private Sprite sideHurt;

    [Header("3페이즈 (화난) 측면 스프라이트  — 비워 두면 기본 스프라이트를 씀")]
    [Tooltip("이 페이즈 번호 이상이면 화난 스프라이트를 씀 (2 = 3페이즈)")]
    [SerializeField] private int rageFromPhase = 2;
    [SerializeField] private Sprite rageIdle;
    [SerializeField] private Sprite rageFlapUp;
    [SerializeField] private Sprite rageFlapDown;
    [SerializeField] private Sprite rageSpread;
    [SerializeField] private Sprite rageRoar;
    [SerializeField] private Sprite rageWindup;
    [SerializeField] private Sprite rageSwing;
    [SerializeField] private Sprite rageDive;
    [SerializeField] private Sprite rageStand;
    [SerializeField] private Sprite rageGroggy;
    [SerializeField] private Sprite rageHurt;
    [SerializeField] private Sprite rageSkyGroggy;
    private bool rage;

    [Header("시점")]
    [Tooltip("이 페이즈 번호 이상이면 측면샷 사용 (0 = 1페이즈, 1 = 2페이즈)")]
    [SerializeField] private int sideFromPhase = 1;
    [Tooltip("전투가 시작되기 전(등장 연출 등)에는 측면샷으로 시작. 끄면 정면샷")]
    [SerializeField] private bool sideBeforeFight = true;
    [Tooltip("측면일 때 플레이어 쪽을 바라보게 좌우 반전")]
    [SerializeField] private bool faceToPlayer = true;
    [Tooltip("보스와 플레이어 X 차이가 이 값보다 작으면 방향을 바꾸지 않음 (떨림 방지)")]
    [SerializeField] private float faceDeadZone = 0.4f;

    [Header("타이밍")]
    [Tooltip("날갯짓 속도 (초당 프레임 전환 수)")]
    [SerializeField] private float flapFps = 2.5f;
    [SerializeField] private float strikeHold = 0.35f;
    [SerializeField] private float roarHold = 1.0f;
    [SerializeField] private float hurtHold = 0.12f;
    [Tooltip("그로기가 아닐 때(돌진을 패링당했을 때 등) 맞은 포즈를 보여주는 시간")]
    [SerializeField] private float hurtHoldNormal = 0.6f;
    [Tooltip("예고 포즈가 이 시간 안에 다음 신호가 없으면 자동으로 날갯짓으로 복귀 (패턴이 끊겨도 멈춰 있지 않게)")]
    [SerializeField] private float windupTimeout = 2.0f;

    [Header("패링 성공 연출")]
    [Tooltip("패링에 성공하면 독수리가 이 시간 동안 깜빡임")]
    [SerializeField] private float flickerTime = 0.6f;
    [SerializeField] private float flickerInterval = 0.06f;
    [Tooltip("깜빡일 때 가장 흐려지는 투명도 (0~1)")]
    [Range(0f, 1f)][SerializeField] private float flickerAlpha = 0.2f;

    [Header("그로기 중 피격 연출")]
    [Tooltip("그로기 중 맞으면 깜빡이는 횟수")]
    [SerializeField] private int hitBlinkCount = 3;
    [Tooltip("한 번 흐려졌다 돌아오는 데 걸리는 시간(초)")]
    [SerializeField] private float hitBlinkCycle = 0.1f;
    [Range(0f, 1f)][SerializeField] private float hitBlinkAlpha = 0.25f;

    [Header("좌우 반전 시 같이 뒤집을 자식 (피격 콜라이더 등)")]
    [Tooltip("독수리가 오른쪽을 볼 때 이 오브젝트들의 Local X 위치를 좌우 반대로 옮긴다. 왼쪽을 본 상태(기본)에서 위치를 잡아 두면 된다")]
    [SerializeField] private Transform[] mirrorOnFlip;

    [Header("얼굴 피격 콜라이더 (포즈마다 머리를 따라감)")]
    [Tooltip("보스의 자식에 만든 얼굴 박스 콜라이더 오브젝트. 포즈가 바뀔 때마다 그 포즈의 눈 위치(EagleFaceData)를 기준으로 자동 이동. 이 오브젝트는 Mirror On Flip 에 넣지 말 것")]
    [SerializeField] private Transform faceHit;
    [Tooltip("눈 위치에서 얼굴 박스 중심까지의 거리 (그림 속 단위, 왼쪽을 본 기준: X 음수 = 부리 쪽). Scene 창의 노란 박스를 보며 맞춤")]
    [SerializeField] private Vector2 faceOffset = new Vector2(-0.3f, -0.3f);

    [Header("연결")]
    [SerializeField] private EagleBoss boss;

    private SpriteRenderer sr;
    private Transform player;
    private bool sideView;
    private Sprite SIdle => rage && rageIdle != null ? rageIdle : sideIdle;
    private Sprite SFlapUp => rage && rageFlapUp != null ? rageFlapUp : sideFlapUp;
    private Sprite SFlapDown => rage && rageFlapDown != null ? rageFlapDown : sideFlapDown;
    private Sprite SSpread => rage && rageSpread != null ? rageSpread : sideSpread;
    private Sprite SRoar => rage && rageRoar != null ? rageRoar : sideRoar;
    private Sprite SWindup => rage && rageWindup != null ? rageWindup : sideWindup;
    private Sprite SSwing => rage && rageSwing != null ? rageSwing : sideSwing;
    private Sprite SDive => rage && rageDive != null ? rageDive : sideDive;
    private Sprite SStand => rage && rageStand != null ? rageStand : sideStand;
    private Sprite SGroggy => rage && rageGroggy != null ? rageGroggy : sideGroggy;
    private Sprite SSkyGroggy => rage && rageSkyGroggy != null ? rageSkyGroggy : (sideSkyGroggy != null ? sideSkyGroggy : SGroggy);
    private Sprite SHurt => rage && rageHurt != null ? rageHurt : sideHurt;

    private Sprite overrideSprite;
    private float overrideUntil;
    private float hurtUntil;
    private bool groggyHeld;

    /// <summary>현재 포즈에서 눈의 위치 (보스 기준 로컬 좌표, 좌우 반전 반영). 눈 번쩍임 등이 사용</summary>
    public Vector2 EyeLocal { get; private set; }
    public bool HasEye { get; private set; }

    private float[] mirrorBaseX;
    private bool lastFlip;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (mirrorOnFlip != null)
        {
            mirrorBaseX = new float[mirrorOnFlip.Length];
            for (int i = 0; i < mirrorOnFlip.Length; i++)
                if (mirrorOnFlip[i] != null) mirrorBaseX[i] = mirrorOnFlip[i].localPosition.x;
        }
    }

    private void ApplyMirror()
    {
        if (mirrorOnFlip == null || sr.flipX == lastFlip) return;
        lastFlip = sr.flipX;
        for (int i = 0; i < mirrorOnFlip.Length; i++)
        {
            var t = mirrorOnFlip[i];
            if (t == null) continue;
            var lp = t.localPosition;
            lp.x = lastFlip ? -mirrorBaseX[i] : mirrorBaseX[i];
            t.localPosition = lp;
        }
    }

    private void Start()
    {
        var health = FindFirstObjectByType<PlayerHealth>();
        if (health != null) player = health.transform;
        if (boss != null) sideView = boss.PhaseIndex >= 0 ? boss.PhaseIndex >= sideFromPhase : sideBeforeFight;
        else sideView = sideBeforeFight;
        rage = boss != null && boss.PhaseIndex >= rageFromPhase;
    }

    private Coroutine flickerRoutine;

    private void OnEnable()
    {
        ParryEvents.Succeeded += OnParried;
        EaglePose.Changed += OnPose;
        if (boss != null) boss.PhaseEntered += OnPhase;
    }

    private void OnDisable()
    {
        EaglePose.FacingLocked = false;
        ParryEvents.Succeeded -= OnParried;
        if (flickerRoutine != null) { StopCoroutine(flickerRoutine); flickerRoutine = null; }
        if (sr != null) sr.color = Color.white;
        EaglePose.Changed -= OnPose;
        if (boss != null) boss.PhaseEntered -= OnPhase;
    }

    /// <summary>패링 성공: 독수리가 껌뻑이듯 깜빡인다</summary>
    private void OnParried()
    {
        if (flickerRoutine != null) StopCoroutine(flickerRoutine);
        flickerRoutine = StartCoroutine(Flicker());
    }

    private void HitBlink()
    {
        if (sr == null || !isActiveAndEnabled) return;
        if (flickerRoutine != null) StopCoroutine(flickerRoutine);
        flickerRoutine = StartCoroutine(Blink());
    }

    private System.Collections.IEnumerator Blink()
    {
        var wait = new WaitForSeconds(hitBlinkCycle * 0.5f);
        for (int i = 0; i < hitBlinkCount; i++)
        {
            sr.color = new Color(1f, 1f, 1f, hitBlinkAlpha);
            yield return wait;
            sr.color = Color.white;
            yield return wait;
        }
        sr.color = Color.white;
        flickerRoutine = null;
    }

    private System.Collections.IEnumerator Flicker()
    {
        bool dim = true;
        for (float t = 0f; t < flickerTime; t += flickerInterval)
        {
            sr.color = new Color(1f, 1f, 1f, dim ? flickerAlpha : 1f);
            dim = !dim;
            yield return new WaitForSeconds(flickerInterval);
        }
        sr.color = Color.white;
        flickerRoutine = null;
    }

    private void OnPhase(int index)
    {
        sideView = index >= sideFromPhase;
        rage = index >= rageFromPhase;
        groggyHeld = false;
        OnPose(EaglePose.Id.Roar);
    }

    private void OnPose(EaglePose.Id id)
    {
        switch (id)
        {
            case EaglePose.Id.Idle:
                groggyHeld = false;
                overrideSprite = null;
                break;
            case EaglePose.Id.Windup: Hold(sideView ? SWindup : windup, windupTimeout); break;
            case EaglePose.Id.Strike: Hold(sideView ? SSwing : strike, strikeHold); break;
            case EaglePose.Id.Roar: Hold(sideView ? (SRoar != null ? SRoar : SSpread) : roar, roarHold); break;
            case EaglePose.Id.Land: Hold(sideView && SStand != null ? SStand : (sideView ? SIdle : idle2), 9999f); break;   // Idle 신호까지 유지
            case EaglePose.Id.Dive: Hold(sideView ? SDive : strike, 5f); break;   // 급강하는 Idle 신호(복귀)까지 유지
            case EaglePose.Id.Groggy:
                groggyHeld = true;
                Hold(sideView ? SGroggy : (groggy != null ? groggy : idle2), 9999f);   // Idle 신호가 올 때까지 유지
                break;
            case EaglePose.Id.SkyGroggy:
                groggyHeld = true;
                Hold(sideView ? SSkyGroggy : (groggy != null ? groggy : idle2), 9999f);   // Idle 신호가 올 때까지 유지
                break;
            case EaglePose.Id.Hurt:
                if (groggyHeld) { HitBlink(); break; }                       // 그로기 중 맞음: 포즈 유지 + 투명하게 깜빡
                if (SHurt != null && sideView) Hold(SHurt, hurtHoldNormal);   // 패링당함 등: 잠깐 맞은 포즈
                break;
        }
    }

    private void Hold(Sprite s, float time)
    {
        if (s == null) return;
        overrideSprite = s;
        overrideUntil = Time.time + time;
    }

    private void Update()
    {
        UpdateFacing();
        ApplyMirror();
        UpdateFace();

        if (overrideSprite != null && Time.time >= overrideUntil) overrideSprite = null;

        if (Time.time < hurtUntil && SHurt != null) { sr.sprite = SHurt; return; }
        if (overrideSprite != null) { sr.sprite = overrideSprite; return; }

        sr.sprite = FlapSprite();
    }

    /// <summary>
    /// 그로기(쓰러진) 포즈에서 스프라이트 맨 아래 끝의 로컬 Y (보스 피벗 기준, 스케일 적용 전).
    /// 데이터가 없으면 false.
    /// </summary>
    public bool TryGetGroggyBottomLocalY(out float localY)
    {
        localY = 0f;
        Sprite sp = sideView ? SGroggy : (groggy != null ? groggy : idle2);
        if (sp == null || !EagleFaceData.TryGetBottom(sp.name, out float px)) return false;
        float v = 1f - px / EagleFaceData.Height;
        localY = (v * sp.rect.height - sp.pivot.y) / sp.pixelsPerUnit;
        return true;
    }

    /// <summary>땅에 내려앉은(stand) 포즈에서 스프라이트 맨 아래(발끝)의 로컬 Y. 데이터가 없으면 false</summary>
    public bool TryGetStandBottomLocalY(out float localY)
    {
        localY = 0f;
        Sprite sp = SStand;
        if (sp == null || !EagleFaceData.TryGetBottom(sp.name, out float px)) return false;
        float v = 1f - px / EagleFaceData.Height;
        localY = (v * sp.rect.height - sp.pivot.y) / sp.pixelsPerUnit;
        return true;
    }

    /// <summary>현재 스프라이트의 눈 위치로 얼굴 콜라이더를 옮긴다</summary>
    private void UpdateFace()
    {
        var sp = sr.sprite;
        HasEye = false;
        if (sp == null || !EagleFaceData.TryGetEye(sp.name, out Vector2 px)) return;

        float u = px.x / EagleFaceData.Width;
        float v = 1f - px.y / EagleFaceData.Height;
        // 스프라이트 사각형 안의 비율 → 피벗 기준 로컬 좌표 (텍스처가 축소 임포트돼도 맞음)
        Vector2 eye = new Vector2((u * sp.rect.width - sp.pivot.x) / sp.pixelsPerUnit,
                                  (v * sp.rect.height - sp.pivot.y) / sp.pixelsPerUnit);
        if (sr.flipX) eye.x = -eye.x;   // 좌우 반전은 피벗 기준 거울 대칭
        EyeLocal = eye;
        HasEye = true;

        if (faceHit != null)
        {
            Vector2 off = faceOffset;
            if (sr.flipX) off.x = -off.x;
            faceHit.localPosition = new Vector3(eye.x + off.x, eye.y + off.y, faceHit.localPosition.z);
        }
    }

    private Sprite FlapSprite()
    {
        if (!sideView)
        {
            bool a = Mathf.FloorToInt(Time.time * flapFps * EaglePose.FlapSpeed) % 2 == 0;
            return (a ? idle1 : idle2) != null ? (a ? idle1 : idle2) : sr.sprite;
        }
        // 측면: 위 → 중간 → 아래 → 중간 순서로 부드럽게
        int step = Mathf.FloorToInt(Time.time * flapFps * EaglePose.FlapSpeed * 2f) % 4;
        Sprite s = step == 0 ? SFlapUp : step == 2 ? SFlapDown : SIdle;
        return s != null ? s : sr.sprite;
    }

    /// <summary>분신 등이 쓸 포즈 스프라이트 (측면 기준, 분노 단계 반영). 없으면 null</summary>
    public Sprite PoseSprite(EaglePose.Id id)
    {
        switch (id)
        {
            case EaglePose.Id.Windup: return sideView ? SWindup : windup;
            case EaglePose.Id.Dive: return sideView ? SDive : strike;
            default: return sideView ? SIdle : idle2;
        }
    }

    private void UpdateFacing()
    {
        if (EaglePose.FacingLocked) return;
        if (!sideView || !faceToPlayer || player == null) { sr.flipX = false; return; }
        float dx = player.position.x - transform.position.x;
        if (Mathf.Abs(dx) < faceDeadZone) return;
        sr.flipX = dx > 0f;   // 측면 그림은 왼쪽을 향하므로, 플레이어가 오른쪽이면 뒤집는다
    }
}