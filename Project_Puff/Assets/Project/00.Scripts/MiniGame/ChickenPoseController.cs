using System.Collections;
using UnityEngine;

/// <summary>
/// 정면 닭의 몸짓. 평소에는 숨 쉬듯 살짝 부풀고, 쪼기 패턴이 시작되면
///   ① 날개를 들고 머리를 뒤로 젖힌 자세(windup)로 경고 기둥 쪽으로 몸이 기울어지며 따라가다가
///   ② 기둥이 멈추면 부들부들 떨고(lock)
///   ③ 찍는 순간 머리가 크게 내리꽂히는 자세(peckHit)로 몸을 낮췄다가
///   ④ 다시 일어난다.
/// 정면 닭이라 머리가 카메라 쪽으로 '확대'되는 것으로 내려찍는 느낌을 낸다 (머리가 커지고 몸이 눌림).
/// 닭 SpriteRenderer의 피벗은 Bottom Center(발 밑)로 두는 것을 권장 (눌릴 때 발이 바닥에 붙어 있도록).
/// 구성: 닭 오브젝트에 이 컴포넌트를 붙이고 Body에 닭 SpriteRenderer를 연결. PeckStrike.PhaseChanged 를 자동으로 구독한다.
/// </summary>
public class ChickenPoseController : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private SpriteRenderer body;

    [Header("포즈 (chicken_front_*.png)")]
    [SerializeField] private Sprite idle;          // chicken_front_idle
    [SerializeField] private Sprite windup;        // chicken_front_peck_windup
    [SerializeField] private Sprite peckMid;       // chicken_front_attack (내리꽂는 중간)
    [SerializeField] private Sprite peckHit;       // chicken_front_peck_hit

    [Header("날개짓 (바람 패턴)")]
    [Tooltip("날개를 든 자세 (chicken_front_peck_windup)")]
    [SerializeField] private Sprite flapUp;
    [Tooltip("날개를 활짝 편 자세 (chicken_front_roar)")]
    [SerializeField] private Sprite flapOut;
    [SerializeField] private float flapFps = 7f;
    [Tooltip("퍼덕일 때 몸이 위아래로 들썩이는 폭 (월드 단위)")]
    [SerializeField] private float flapBounce = 0.12f;
    [Tooltip("바람이 부는 쪽으로 기울어지는 각도")]
    [SerializeField] private float flapLean = 4f;

    [Header("숨쉬기")]
    [SerializeField] private float breathSpeed = 2.2f;
    [SerializeField] private float breathAmount = 0.012f;

    [Header("조준 (경고 기둥을 따라 몸이 기울어짐)")]
    [Tooltip("기둥이 닭에서 1유닛 떨어질 때마다 몸이 옆으로 움직이는 거리")]
    [SerializeField] private float swayPerUnit = 0.1f;
    [SerializeField] private float maxSway = 1.2f;
    [Tooltip("최대로 기울어질 때의 각도")]
    [SerializeField] private float leanDegrees = 7f;
    [SerializeField] private float follow = 8f;
    [Tooltip("고정(lock) 구간에서 부들부들 떠는 세기")]
    [SerializeField] private float lockShake = 0.05f;

    [Header("찍기")]
    [Tooltip("peckMid → peckHit 로 넘어가기 전 시간")]
    [SerializeField] private float midTime = 0.05f;
    [Tooltip("몸이 눌리는 정도 (세로 비율). 0.08 = 8% 납작해짐")]
    [SerializeField] private float squash = 0.1f;
    [SerializeField] private float hitHold = 0.18f;
    [SerializeField] private float recoverTime = 0.3f;
    [Tooltip("찍을 때 화면 흔들림")]
    [SerializeField] private float hitShakeTime = 0.18f;
    [SerializeField] private float hitShakeDistance = 0.12f;

    private enum State { Idle, Aim, Lock, Hit, Flap }

    private State state = State.Idle;
    private Vector3 basePos, baseScale;
    private float aimX;
    private float offsetX;
    private Coroutine hitRoutine;
    private int gustCount;      // 지금 진행 중인 돌풍 수
    private int gustDir = 1;

    private void Awake()
    {
        basePos = transform.position;
        baseScale = transform.localScale;
        if (body == null) body = GetComponentInChildren<SpriteRenderer>();
        if (body != null && idle != null) body.sprite = idle;
    }

    private void OnEnable()
    {
        PeckStrike.PhaseChanged += OnPeckPhase;
        GustWave.PhaseChanged += OnGustPhase;
    }

    private void OnDisable()
    {
        PeckStrike.PhaseChanged -= OnPeckPhase;
        GustWave.PhaseChanged -= OnGustPhase;
    }

    private void OnGustPhase(GustWave.Phase phase, int dir)
    {
        if (body == null) return;
        if (phase == GustWave.Phase.Start) { gustCount++; gustDir = dir; }
        else if (phase == GustWave.Phase.End) gustCount = Mathf.Max(0, gustCount - 1);
    }

    /// <summary>닭이 서 있는 기준 위치를 다시 잡는다 (등장 연출이 끝난 뒤 호출하면 안전)</summary>
    public void RebaseHere()
    {
        basePos = transform.position;
        baseScale = transform.localScale;
    }

    private void OnPeckPhase(PeckStrike.Phase phase, float x, bool isMain)
    {
        if (!isMain || body == null) return;

        switch (phase)
        {
            case PeckStrike.Phase.Start:
                CancelHit();
                state = State.Aim;
                aimX = x;
                Set(windup);
                break;
            case PeckStrike.Phase.Track:
                if (state == State.Aim) aimX = x;
                break;
            case PeckStrike.Phase.Lock:
                if (state == State.Aim) state = State.Lock;
                break;
            case PeckStrike.Phase.End:
                // 패턴이 중간에 취소됐을 때(죽어서 재시작 등) 조준 자세에서 멈춰 있지 않도록
                if (state == State.Aim || state == State.Lock) { state = State.Idle; Set(idle); }
                break;
            case PeckStrike.Phase.Strike:
                CancelHit();
                hitRoutine = StartCoroutine(HitRoutine());
                break;
        }
    }

    private void Update()
    {
        if (state == State.Hit) return;   // 코루틴이 직접 움직임

        // 바람 패턴: 날개를 번갈아 퍼덕임 (조준/찍기 중이 아닐 때만)
        if (gustCount > 0 && (state == State.Idle || state == State.Flap))
        {
            state = State.Flap;
            bool up = Mathf.FloorToInt(Time.time * flapFps) % 2 == 0;
            Set(up ? flapUp : flapOut);
            float bounce = up ? flapBounce : 0f;
            transform.position = new Vector3(basePos.x, basePos.y + bounce, basePos.z);
            transform.localScale = baseScale;
            transform.rotation = Quaternion.Euler(0f, 0f, -gustDir * flapLean);
            return;
        }
        if (state == State.Flap)   // 바람이 끝나면 원래대로
        {
            state = State.Idle;
            Set(idle);
            transform.position = basePos;
            transform.rotation = Quaternion.identity;
        }

        float targetOffset = 0f;
        if (state == State.Aim || state == State.Lock)
            targetOffset = Mathf.Clamp((aimX - basePos.x) * swayPerUnit, -maxSway, maxSway);

        offsetX = Mathf.Lerp(offsetX, targetOffset, 1f - Mathf.Exp(-follow * Time.deltaTime));

        float shake = state == State.Lock ? Mathf.Sin(Time.time * 70f) * lockShake : 0f;
        float breath = state == State.Idle ? 1f + Mathf.Sin(Time.time * breathSpeed) * breathAmount : 1f;

        transform.position = new Vector3(basePos.x + offsetX + shake, basePos.y, basePos.z);
        transform.localScale = new Vector3(baseScale.x, baseScale.y * breath, baseScale.z);
        float lean = maxSway > 0f ? -offsetX / maxSway * leanDegrees : 0f;
        transform.rotation = Quaternion.Euler(0f, 0f, lean);
    }

    private IEnumerator HitRoutine()
    {
        state = State.Hit;
        Set(peckMid);
        yield return new WaitForSeconds(midTime);

        // 내리꽂기: 머리가 확대되며 몸이 눌림
        Set(peckHit);
        transform.localScale = new Vector3(baseScale.x, baseScale.y * (1f - squash), baseScale.z);
        StartCoroutine(Shake());
        yield return new WaitForSeconds(hitHold);

        // 일어나기
        Set(idle);
        Vector3 fromPos = transform.position;
        Quaternion fromRot = transform.rotation;
        Vector3 fromScale = transform.localScale;
        Vector3 toPos = new Vector3(basePos.x, basePos.y, basePos.z);
        for (float t = 0f; t < recoverTime; t += Time.deltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / recoverTime, 3f);
            transform.position = Vector3.Lerp(fromPos, toPos, k);
            transform.rotation = Quaternion.Slerp(fromRot, Quaternion.identity, k);
            transform.localScale = Vector3.Lerp(fromScale, baseScale, k);
            yield return null;
        }
        transform.position = toPos;
        transform.rotation = Quaternion.identity;
        transform.localScale = baseScale;
        offsetX = 0f;
        state = State.Idle;
        hitRoutine = null;
    }

    private void CancelHit()
    {
        if (hitRoutine != null) { StopCoroutine(hitRoutine); hitRoutine = null; }
    }

    private IEnumerator Shake()
    {
        var cam = Camera.main != null ? Camera.main.transform : null;
        if (cam == null || hitShakeTime <= 0f) yield break;

        Vector3 basePosCam = cam.position;
        for (float t = 0f; t < hitShakeTime; t += Time.unscaledDeltaTime)
        {
            float fade = 1f - t / hitShakeTime;
            cam.position = basePosCam + new Vector3(Mathf.Sin(t * 90f), Mathf.Cos(t * 77f), 0f) * hitShakeDistance * fade;
            yield return null;
        }
        cam.position = basePosCam;
    }

    private void Set(Sprite s)
    {
        if (body != null && s != null) body.sprite = s;
    }
}
