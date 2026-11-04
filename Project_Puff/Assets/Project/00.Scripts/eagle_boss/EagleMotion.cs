using System.Collections;
using UnityEngine;

/// <summary>
/// 독수리의 하늘 이동과 급강하. 평소엔 하늘에서 부드럽게 떠다니고(둥실), 급강하 패턴이 요청할 때만 바닥까지 내려온다.
///  - 1페이즈(정면 고정): Front Locked 이면 좌우로 움직이지 않고 제자리에서 위아래로만 둥실
///  - 2·3페이즈: 플레이어 쪽으로 천천히 따라가며 하늘을 떠다님
/// 보스 오브젝트에 붙인다. (EagleBoss 의 Motion 칸에 연결)
/// </summary>
public class EagleMotion : MonoBehaviour
{
    [Header("무대")]
    [SerializeField] private Transform leftLimit;
    [SerializeField] private Transform rightLimit;
    [SerializeField] private Transform groundLine;

    [Header("하늘 이동")]
    [Tooltip("바닥에서 이 높이에 떠 있음")]
    [SerializeField] private float hoverHeight = 6.5f;
    [Tooltip("위아래로 둥실거리는 폭 / 속도")]
    [SerializeField] private float bobAmount = 0.35f;
    [SerializeField] private float bobSpeed = 1.6f;
    [Tooltip("좌우로 천천히 흔들리는 폭 (정면 고정일 땐 0)")]
    [SerializeField] private float swayAmount = 1.2f;
    [SerializeField] private float swaySpeed = 0.7f;
    [Tooltip("플레이어를 얼마나 따라갈지 (0 = 중앙 고정, 1 = 그대로 따라감)")]
    [Range(0f, 1f)][SerializeField] private float followPlayer = 0.45f;
    [Tooltip("따라가는 속도 (유닛/초)")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float edgeMargin = 2.5f;

    [Header("움직임 느낌 (관성 / 기울기)")]
    [Tooltip("하늘에서 목표 위치로 부드럽게 따라붙는 시간(초). 클수록 묵직하게 미끄러짐")]
    [SerializeField] private float smoothTime = 0.45f;
    [Tooltip("움직이는 방향으로 몸을 기울일지 (몸이 회전함. 직접 회전시키는 연출을 쓰면 끄세요)")]
    [SerializeField] private bool useTilt = true;
    [Tooltip("속도 1당 기울어지는 각도")]
    [SerializeField] private float tiltPerSpeed = 1.1f;
    [Tooltip("최대 기울기(도)")]
    [SerializeField] private float maxTilt = 14f;
    [Tooltip("기울기가 따라가는 부드러움(초)")]
    [SerializeField] private float tiltSmooth = 0.12f;

    [Header("결과 반응")]
    [Tooltip("돌진을 피하게 했을 때 하늘로 복귀하는 시간 배율. 작을수록 곧바로 다음 공격을 이어감")]
    [SerializeField] private float dodgedReturnScale = 0.6f;
    [Tooltip("돌진을 맞혔을 때 으스대며 포효하는 시간(초). 0이면 안 함")]
    [SerializeField] private float tauntTime = 0.5f;
    [Tooltip("패링당한 뒤 튕겨 나가는 시간(초)")]
    [SerializeField] private float recoilTime = 0.25f;
    [Tooltip("튕겨 나간 뒤 포효를 시작하기까지 멍하게 있는 시간(초). 0이면 바로 포효")]
    [SerializeField] private float roarDelay = 0f;
    [Tooltip("패링당했을 때 포효하며 몸을 떠는 세기")]
    [SerializeField] private float roarShake = 0.12f;

    [Header("그로기 (추락)")]
    [Tooltip("쓰러졌을 때 보스 중심이 바닥에서 떠 있는 높이. 포즈 데이터가 없을 때만 사용")]
    [SerializeField] private float groggyHeight = 1.3f;
    [Tooltip("착지 높이 미세조정. 스프라이트 맨 아래가 Ground Line에 정확히 닿는 게 0. +면 위로, -면 땅 속으로")]
    [SerializeField] private float landOffset = 0f;
    [Tooltip("떨어지는 가속도 (클수록 빨리 떨어짐)")]
    [SerializeField] private float fallGravity = 38f;
    [Tooltip("착지 때 살짝 튀어오르는 높이 (0이면 안 튐)")]
    [SerializeField] private float bounceHeight = 0.35f;
    [Tooltip("그로기가 끝난 뒤 하늘로 올라가는 시간(초)")]
    [SerializeField] private float recoverTime = 1.0f;

    [Header("공격 판정")]
    [Tooltip("돌진/급강하로 플레이어를 때리는 판정에 쓸 콜라이더 (얼굴 박스 콜라이더). 비우면 몸 중심의 Hit Radius 원을 씀")]
    [SerializeField] private Collider2D hitCollider;

    [Header("디버그")]
    [Tooltip("켜면 Scene 창(Game 창은 Gizmos 버튼을 켜면)에서 돌진/급강하의 공격 판정 원이 보임")]
    [SerializeField] private bool showHitGizmo = true;
    [Tooltip("돌진/급강하를 아직 한 번도 안 했을 때 미리 보여줄 반지름 (패턴 에셋의 Hit Radius 와 맞춰 보세요)")]
    [SerializeField] private float previewRadius = 1.1f;

    private float lastHitRadius;
    private Transform player;
    private bool frontLocked = true;
    private bool diving;
    private float homeX;
    private float curX;
    private float curY;
    private float velX, velY;      // SmoothDamp 속도
    private bool wasBusy = true;
    private Vector3 lastPos;
    private float tilt, tiltVel;

    [Header("하늘 그로기 연출")]
    [Tooltip("솟구쳐 오르는 동안 날갯짓 속도 배율")]
    [SerializeField] private float riseFlapSpeed = 2.2f;
    [Tooltip("바람을 일으키는 동안 날갯짓 속도 배율")]
    [SerializeField] private float summonFlapSpeed = 3.6f;
    /// <summary>하늘 그로기 중 날개짓으로 바람을 일으키는 중 (바람 이펙트가 구독)</summary>
    public bool IsSummoning { get; private set; }
    private bool skyDazed;
    private bool externalRotation;   // 사망 연출처럼 코드가 직접 회전을 줄 때 기울임 처리를 끈다
    private GameObject shadow;
    private bool grounded;       // 그로기로 쓰러져 있는 중
    private bool dashing;
    private bool sweeping;       // 날개 후려치기 중
    private bool dashResolved;
    private Coroutine fallRoutine;

    public bool IsDiving => diving;
    /// <summary>돌진/급강하로 전속력으로 날아가는 중 (바람 궤적 연출이 이걸 본다)</summary>
    public bool IsCharging { get; private set; }
    /// <summary>지금 돌진이 패링 가능한 돌진인지 (궤적 색을 다르게 하려고)</summary>
    public bool ChargeIsParry { get; private set; }
    public bool IsGrounded => grounded;

    /// <summary>돌진 한 번의 설정 (DashPattern 이 채워서 넘김)</summary>
    public struct DashSettings
    {
        public bool diagonal;        // false = 수평 돌진, true = 하늘에서 비스듬히 내리꽂는 돌진
        public bool parry;           // true = 패링할 수 있는 돌진 (몸을 맞히면 패링 성공)
        public bool eyeFlash;        // 돌진 직전 눈 번쩍임
        public float windup;         // 뒤로 물러나 자세 잡는 시간
        public float aimYOffset;     // 수평 돌진의 조준 높이 = 플레이어 높이 + 이 값. 0 이면 기본(0.4). 크게 주면 머리 위를 스쳐 지나가는 돌진 (가만히 서 있으면 안 맞음)
        public float flashAt;        // 눈이 번쩍이는 시점 (윈드업의 0~1 비율). 0 이면 기본(0.45)
        public float backOff;        // 물러나는 거리
        public float speed;          // 돌진 속도
        public float overshoot;      // 플레이어를 지나쳐 더 가는 거리
        public float minHeight;      // 수평 돌진의 최소 높이 (바닥 기준)
        public float hitRadius;      // 몸 판정 반경
        public LayerMask playerLayer;
        public int damage;
        public float recoil;         // 패링당했을 때 튕겨나는 거리
        public float stun;           // 패링당한 뒤 멍한 시간
        public float returnTime;     // 하늘로 복귀하는 시간
    }

    public enum DashResult { None, Hit, Parried, Dodged }
    public DashResult LastDashResult { get; private set; }

    /// <summary>패링 가능한 돌진 중에만 true</summary>
    public bool ParryWindow { get; private set; }

    /// <summary>패링 가능한 돌진 중 보스가 맞았을 때 EagleBoss 가 부름. 성공 처리했으면 true</summary>
    public bool NotifyParried()
    {
        if (!ParryWindow || dashResolved) return false;
        dashResolved = true;
        ParryWindow = false;
        LastDashResult = DashResult.Parried;
        ParryEvents.RaiseSucceeded();   // EagleBoss 가 세어서 그로기를 판단 (그로기가 되면 여기서 패턴이 끊긴다)
        return true;
    }

    private void Start()
    {
        var health = FindFirstObjectByType<PlayerHealth>();
        if (health != null) player = health.transform;
        homeX = transform.position.x;
        curX = homeX;
        curY = transform.position.y;
    }

    /// <summary>true = 정면 고정(좌우 이동 없음), false = 하늘을 떠다니며 플레이어를 따라감</summary>
    public void SetFrontLocked(bool locked) => frontLocked = locked;

    public float GroundY => groundLine != null ? groundLine.position.y : 0f;

    private void Update()
    {
        if (diving || grounded || dashing || sweeping) { wasBusy = true; return; }
        if (wasBusy)   // 패턴/그로기가 끝나고 호버로 돌아올 때: 현재 위치에서 속도 0으로 이어받음
        {
            wasBusy = false;
            velX = velY = 0f;
            curX = transform.position.x; curY = transform.position.y;
        }

        float targetX = homeX;
        if (!frontLocked)
        {
            if (player != null) targetX = Mathf.Lerp(homeX, player.position.x, followPlayer);
            targetX += Mathf.Sin(Time.time * swaySpeed) * swayAmount;
            if (leftLimit != null && rightLimit != null)
                targetX = Mathf.Clamp(targetX, leftLimit.position.x + edgeMargin, rightLimit.position.x - edgeMargin);
        }

        float targetY = GroundY + hoverHeight + Mathf.Sin(Time.time * bobSpeed) * bobAmount;

        // 관성: 목표에 부드럽게 가속/감속하며 따라붙음 (직선으로 뚝뚝 움직이지 않음)
        curX = Mathf.SmoothDamp(curX, targetX, ref velX, smoothTime, moveSpeed);
        curY = Mathf.SmoothDamp(curY, targetY, ref velY, smoothTime * 0.7f, moveSpeed * 2f);
        transform.position = new Vector3(curX, curY, transform.position.z);
    }

    /// <summary>움직이는 방향으로 몸을 기울인다. 모든 이동(호버/돌진/후려치기)의 실제 속도를 보고 판단</summary>
    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        Vector3 pos = transform.position;
        float vx = dt > 0f ? Mathf.Clamp((pos.x - lastPos.x) / dt, -25f, 25f) : 0f;
        lastPos = pos;
        if (!useTilt || externalRotation) return;

        float target = grounded ? 0f : Mathf.Clamp(-vx * tiltPerSpeed, -maxTilt, maxTilt);   // 오른쪽으로 가면 시계방향(앞으로 숙임)
        tilt = Mathf.SmoothDampAngle(tilt, target, ref tiltVel, tiltSmooth);
        transform.rotation = Quaternion.Euler(0f, 0f, tilt);
    }

    /// <summary>
    /// 급강하 한 번. 바닥에 그림자 예고 → 위로 살짝 솟았다가 → 내리꽂기 → 잠시 머묾 → 하늘로 복귀.
    /// 패턴 코루틴에서 yield return 으로 쓴다. 끊겨도 CancelAll() 로 정리된다.
    /// </summary>
    public IEnumerator DiveRoutine(float windup, float diveTime, float hold, float returnTime,
                                   float impactHeight, float hitRadius, LayerMask playerLayer, int damage,
                                   System.Action<float> onImpact = null, System.Action<float> onShock = null, float shockDelay = 0f, float lockHold = 0f)
    {
        diving = true;
        lastHitRadius = hitRadius;
        float groundY = GroundY;
        float minX = leftLimit != null ? leftLimit.position.x + 1f : -8f;
        float maxX = rightLimit != null ? rightLimit.position.x - 1f : 8f;

        Vector3 start = transform.position;
        float x = Mathf.Clamp(player != null ? player.position.x : start.x, minX, maxX);

        // 그림자 (예고)
        var sr = MakeShadow(groundY);
        EaglePose.Raise(EaglePose.Id.Windup);
        GameSfx.Play(GameSfxId.EagleDiveWarn);

        // 1) 예고: 앞 60%는 플레이어를 따라가고, 나머지는 위치 고정(피할 시간)
        float lockAt = windup * 0.6f;
        Vector3 riseTo = new Vector3(x, start.y + 1.0f, start.z);
        for (float t = 0f; t < windup; t += Time.deltaTime)
        {
            if (t < lockAt && player != null)
                x = Mathf.MoveTowards(x, Mathf.Clamp(player.position.x, minX, maxX), 9f * Time.deltaTime);

            float k = t / windup;
            float a = (Mathf.Sin(t * 28f) > 0f ? 0.6f : 0.3f) * Mathf.Lerp(0.5f, 1f, k);
            if (sr != null) { sr.color = new Color(0f, 0f, 0f, a); EagleVis.Place(sr, x, groundY + 0.08f, 2.6f, 0.3f); }

            float y = Mathf.Lerp(start.y, start.y + 1.0f, Mathf.SmoothStep(0f, 1f, k));
            transform.position = new Vector3(Mathf.Lerp(start.x, x, Mathf.SmoothStep(0f, 1f, k)), y, start.z);
            yield return null;
        }

        // 1-b) 위치 도착 후 대기: 그림자 위에 떠서 잠깐 멈춤 (플레이어가 피할 자리를 잡을 시간)
        for (float t = 0f; t < lockHold; t += Time.deltaTime)
        {
            float a = (Mathf.Sin(t * 34f) > 0f ? 0.75f : 0.4f);
            if (sr != null) { sr.color = new Color(0f, 0f, 0f, a); EagleVis.Place(sr, x, groundY + 0.08f, 2.6f, 0.3f); }
            transform.position = new Vector3(x, start.y + 1.0f + Mathf.Sin(t * 12f) * 0.06f, start.z);
            yield return null;
        }

        // 2) 내리꽂기
        EaglePose.Raise(EaglePose.Id.Dive);
        GameSfx.Play(GameSfxId.EagleDive);
        Vector3 from = transform.position;
        Vector3 to = new Vector3(x, groundY + impactHeight, start.z);
        bool hitDone = false;
        IsCharging = true; ChargeIsParry = false;
        for (float t = 0f; t < diveTime; t += Time.deltaTime)
        {
            float k = t / diveTime;
            transform.position = Vector3.Lerp(from, to, k * k);   // 점점 빨라짐
            TryHit(ref hitDone, hitRadius, playerLayer, damage);
            yield return null;
        }
        transform.position = to;
        IsCharging = false;
        TryHit(ref hitDone, hitRadius, playerLayer, damage);
        GameSfx.PlayAt(GameSfxId.EagleStomp, new Vector2(x, groundY));
        onImpact?.Invoke(x);   // 착지 순간 (충격파 생성 등)
        if (sr != null) sr.enabled = false;

        // 3) 잠시 머묾 (이 동안도 판정 유지 → 밑에 서 있으면 맞음)
        bool shocked = false;
        float holdTime = onShock != null ? Mathf.Max(hold, shockDelay + 0.1f) : hold;
        for (float t = 0f; t < holdTime; t += Time.deltaTime)
        {
            TryHit(ref hitDone, hitRadius, playerLayer, damage);
            if (!shocked && onShock != null && t >= shockDelay) { shocked = true; onShock(x); }   // 착지 후 잠깐 뒤 충격파 (피할 시간)
            yield return null;
        }
        if (!shocked && onShock != null) { shocked = true; onShock(x); }

        // 4) 하늘로 복귀
        float homeY = groundY + hoverHeight;
        Vector3 back = transform.position;
        EaglePose.Raise(EaglePose.Id.Idle);
        for (float t = 0f; t < returnTime; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / returnTime);
            transform.position = Vector3.Lerp(back, new Vector3(x, homeY, start.z), k);
            yield return null;
        }

        curX = transform.position.x;
        curY = transform.position.y;
        DestroyShadow();
        diving = false;
    }

    /// <summary>플레이어를 때리는 판정. 얼굴 콜라이더가 있으면 그 박스, 없으면 몸 중심 원</summary>
    private Collider2D HitTest(float radius, LayerMask mask)
    {
        if (hitCollider != null && hitCollider.enabled)
        {
            var box = hitCollider as BoxCollider2D;
            if (box != null)
            {
                // 보스(또는 부모)가 회전/크기 변경돼도 정확히: 박스의 실제 중심, 크기, 각도를 그대로 사용
                Transform t = box.transform;
                Vector2 center = t.TransformPoint(box.offset);
                Vector2 ls = t.lossyScale;
                Vector2 size = new Vector2(box.size.x * Mathf.Abs(ls.x), box.size.y * Mathf.Abs(ls.y));
                return Physics2D.OverlapBox(center, size, t.eulerAngles.z, mask);
            }
            Bounds b = hitCollider.bounds;   // 박스가 아닌 콜라이더는 바깥 박스로 근사
            return Physics2D.OverlapBox(b.center, b.size, 0f, mask);
        }
        return Physics2D.OverlapCircle(transform.position, radius, mask);
    }

    private void TryHit(ref bool done, float radius, LayerMask playerLayer, int damage)
    {
        if (done) return;
        var hit = HitTest(radius, playerLayer);
        if (hit == null) return;
        var d = hit.GetComponentInParent<IDamageable>();
        if (d != null) d.TakeDamage(damage, transform.position);
        done = true;
    }

    private SpriteRenderer MakeShadow(float groundY)
    {
        DestroyShadow();
        shadow = new GameObject("DiveShadow");
        var sr = shadow.AddComponent<SpriteRenderer>();
        sr.sprite = EagleVis.White;
        sr.sortingOrder = 4;
        sr.color = new Color(0f, 0f, 0f, 0.4f);
        return sr;
    }

    private void DestroyShadow()
    {
        if (shadow != null) { Destroy(shadow); shadow = null; }
    }

    // ───────────── 돌진 ─────────────

    /// <summary>
    /// 돌진 한 번. 뒤로 물러나 자세 잡기(윈드업, 눈 번쩍) → 플레이어 위치를 조준해 돌진 → 지나친 뒤 하늘로 복귀.
    /// 패링 가능한 돌진: 돌진 중 몸을 맞히면 패링 성공 → 튕겨나며 멍해짐. 몸에 닿으면 데미지+패링 실패, 그냥 지나가면 회피.
    /// 결과는 LastDashResult 에 남는다. 패턴 코루틴에서 yield return 으로 쓴다.
    /// </summary>
    public IEnumerator DashRoutine(DashSettings s)
    {
        dashing = true;
        lastHitRadius = s.hitRadius;
        dashResolved = false;
        ParryWindow = false;
        LastDashResult = DashResult.None;

        float groundY = GroundY;
        float minX = leftLimit != null ? leftLimit.position.x + 0.5f : -9f;
        float maxX = rightLimit != null ? rightLimit.position.x - 0.5f : 9f;

        Vector3 start = transform.position;
        float z = start.z;
        float dirX = player != null && player.position.x < start.x ? -1f : 1f;   // 플레이어 쪽
        EaglePose.FacingLocked = false;   // 먼저 플레이어 쪽을 보게 한 뒤
        yield return null;
        EaglePose.FacingLocked = true;    // 돌진이 끝날 때까지 방향 고정

        // 1) 윈드업: 뒤로 물러나며 자세 잡기. 마지막 순간에 조준(플레이어 위치) 고정
        EaglePose.Raise(EaglePose.Id.Windup);
        GameSfx.Play(GameSfxId.EagleDiveWarn);
        float playerY0 = player != null ? player.position.y : groundY;
        float yOff = s.aimYOffset != 0f ? s.aimYOffset : 0.4f;
        float levelY = Mathf.Max(playerY0 + yOff, groundY + s.minHeight);
        Vector3 backTo = s.diagonal
            ? new Vector3(Mathf.Clamp(start.x - dirX * s.backOff * 0.5f, minX, maxX), start.y + 1.5f, z)
            : new Vector3(Mathf.Clamp(start.x - dirX * s.backOff, minX, maxX), levelY, z);

        Vector2 aim = player != null ? (Vector2)player.position : (Vector2)start + Vector2.right * dirX;
        bool flashed = false;
        for (float t = 0f; t < s.windup; t += Time.deltaTime)
        {
            float k = t / s.windup;
            if (player != null && k < 0.75f) aim = player.position;   // 마지막 25%는 조준 고정
            if (!s.diagonal) backTo.y = Mathf.Lerp(backTo.y, Mathf.Max(aim.y + yOff, groundY + s.minHeight), 6f * Time.deltaTime);
            transform.position = Vector3.Lerp(start, backTo, Mathf.SmoothStep(0f, 1f, k));
            if (s.eyeFlash && !flashed && k > (s.flashAt > 0f ? s.flashAt : 0.45f)) { flashed = true; EaglePose.RaiseEyeFlash(true); GameSfx.Play(GameSfxId.EagleScreech); }
            yield return null;
        }

        // 2) 돌진
        Vector2 from = transform.position;
        Vector2 end = s.diagonal
            ? new Vector2(Mathf.Clamp(aim.x + dirX * s.overshoot, minX - 3f, maxX + 3f), groundY + 0.6f)
            : new Vector2(Mathf.Clamp(aim.x + dirX * s.overshoot, minX - 3f, maxX + 3f), from.y);
        if (s.diagonal)   // 하늘에서 플레이어 위치를 지나 바닥까지 내리꽂는 선
        {
            Vector2 d = (aim - from).normalized;
            if (d.y > -0.15f) d = new Vector2(dirX * 0.7f, -0.7f);
            float len = Mathf.Max(1f, (aim.y - (groundY + 0.6f)) / Mathf.Max(0.1f, -d.y));
            end = aim + d * len;
        }
        Vector2 dir = (end - from).normalized;
        EaglePose.Raise(EaglePose.Id.Dive);
        GameSfx.Play(GameSfxId.EagleDive);
        ParryWindow = s.parry;

        bool hitDone = false;
        Vector2 pos = from;
        IsCharging = true; ChargeIsParry = s.parry;
        float total = Vector2.Dot(end - from, dir);
        float brake = Mathf.Min(3f, total * 0.4f);   // 끝에서 감속 (뚝 멈추지 않고 미끄러지며 멈춤)
        while (Vector2.Dot(end - pos, dir) > 0f && !dashResolved)
        {
            float remain = Vector2.Dot(end - pos, dir);
            float mul = remain < brake ? Mathf.Lerp(0.3f, 1f, remain / brake) : 1f;
            pos += dir * (s.speed * mul * Time.deltaTime);
            transform.position = new Vector3(pos.x, pos.y, z);

            if (!hitDone)
            {
                var hit = HitTest(s.hitRadius, s.playerLayer);
                if (hit != null)
                {
                    var d = hit.GetComponentInParent<IDamageable>();
                    if (d != null) d.TakeDamage(s.damage, pos);
                    hitDone = true;
                    LastDashResult = DashResult.Hit;
                    if (s.parry) ParryEvents.RaiseFailed();
                }
            }
            yield return null;
        }
        IsCharging = false;
        ParryWindow = false;

        if (dashResolved)
        {
            // 3-a) 패링 당함: 반대로 튕겨나며 멍해짐
            EaglePose.RaiseEyeFlash(false);
            EaglePose.Raise(EaglePose.Id.Hurt);
            GameSfx.Play(GameSfxId.EagleHurt);
            Vector3 p0 = transform.position;
            Vector3 p1 = p0 - (Vector3)(dir * s.recoil);
            p1.y = Mathf.Max(p1.y, groundY + 1f);
            float rc = Mathf.Max(0.05f, recoilTime);
            for (float t = 0f; t < rc; t += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(p0, p1, 1f - Mathf.Pow(1f - t / rc, 2f));
                yield return null;
            }
            if (roarDelay > 0f) yield return new WaitForSeconds(roarDelay);   // 멍하게 있다가 포효
            // 분노의 포효: 몸을 떨며 크게 울부짖는다
            EaglePose.Raise(EaglePose.Id.Roar);
            GameSfx.Play(GameSfxId.EagleScreech);
            Vector3 roarBase = transform.position;
            for (float t = 0f; t < s.stun; t += Time.deltaTime)
            {
                float fade = 1f - t / Mathf.Max(0.01f, s.stun) * 0.5f;
                Vector2 jit = Random.insideUnitCircle * (roarShake * fade);
                transform.position = roarBase + new Vector3(jit.x, jit.y, 0f);
                yield return null;
            }
            transform.position = roarBase;
            EaglePose.Raise(EaglePose.Id.Idle);
        }
        else if (LastDashResult == DashResult.None)
        {
            LastDashResult = DashResult.Dodged;
            if (s.parry) ParryEvents.RaiseDodged();
        }

        // 4) 하늘로 복귀
        EaglePose.RaiseEyeFlash(false);
        if (LastDashResult == DashResult.Hit && tauntTime > 0f)   // 맞혔다: 잠깐 으스대며 포효
        {
            EaglePose.Raise(EaglePose.Id.Roar);
            GameSfx.Play(GameSfxId.EagleScreech);
            yield return new WaitForSeconds(tauntTime);
        }
        EaglePose.Raise(EaglePose.Id.Idle);
        Vector3 back0 = transform.position;
        float homeX = Mathf.Clamp(back0.x, minX + 1f, maxX - 1f);
        Vector3 back1 = new Vector3(homeX, groundY + hoverHeight, z);
        float retT = s.returnTime * (LastDashResult == DashResult.Dodged ? dodgedReturnScale : 1f);   // 피하게 했으면 서둘러 복귀
        for (float t = 0f; t < retT; t += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(back0, back1, Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.05f, retT)));
            yield return null;
        }
        curX = back1.x; curY = back1.y;
        EaglePose.FacingLocked = false;
        dashing = false;
    }


    // ───────────── 날개 후려치기 (땅에 내려앉아 후려친다) ─────────────

    [System.Serializable]
    public struct SweepSettings
    {
        public float edgeInset;      // 화면(레벨) 끝에서 안쪽으로 들어온 거리
        public float approachTime;   // 하늘에서 끝으로 날아와 내려앉는 시간
        public float settle;         // 내려앉은 뒤 숨 고르는 시간
        public float windup;         // 처음 날개를 젖히는 예고 시간
        public float hold;           // 마지막 후려친 뒤 머무는 시간
        public float returnTime;     // 하늘로 복귀
        public float landOffset;     // 착지 높이 미세조정
    }

    /// <summary>
    /// 날개 후려치기 1단계: 화면 끝으로 날아와 내려앉고 첫 날개 젖히기까지.
    /// dirX = 충격파가 가는 방향 (+1 이면 왼쪽 끝에 내려앉아 오른쪽으로 후려침).
    /// 이후 SweepReady()/SweepStrike() 로 박자에 맞춰 후려치고, SweepLeave() 로 마무리한다.
    /// </summary>
    public IEnumerator SweepLand(SweepSettings s, float dirX, bool eyeFlash = false)
    {
        sweeping = true;
        float groundY = GroundY;
        float minX = leftLimit != null ? leftLimit.position.x : -8f;
        float maxX = rightLimit != null ? rightLimit.position.x : 8f;
        float landX = dirX > 0f ? minX + s.edgeInset : maxX - s.edgeInset;

        float landY = groundY + groggyHeight;
        var pose = GetComponent<EaglePoseController>();
        if (pose != null && pose.TryGetStandBottomLocalY(out float bottomLocal))
            landY = groundY - bottomLocal * Mathf.Abs(transform.lossyScale.y) + s.landOffset;

        // 끝으로 날아가 내려앉기
        EaglePose.Raise(EaglePose.Id.Idle);
        GameSfx.Play(GameSfxId.EagleDiveWarn);
        Vector3 from = transform.position;
        Vector3 to = new Vector3(landX, landY, from.z);
        float apT = Mathf.Max(0.1f, s.approachTime);
        for (float t = 0f; t < apT; t += Time.deltaTime)
        {
            float k = t / apT;
            float x = Mathf.Lerp(from.x, to.x, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, k * 1.25f)));   // 가로는 먼저, 세로는 늦게(내려앉는 느낌)
            float y = Mathf.Lerp(from.y, to.y, k * k);
            transform.position = new Vector3(x, y, from.z);
            yield return null;
        }
        transform.position = to;
        EaglePose.Raise(EaglePose.Id.Land);
        GameSfx.PlayAt(GameSfxId.EagleStomp, new Vector2(landX, groundY));
        yield return new WaitForSeconds(s.settle);

        // 첫 날개 젖히기 (예고)
        EaglePose.Raise(EaglePose.Id.Windup);
        if (eyeFlash)   // 패링 공격 신호: 날개를 젖히는 도중 눈이 번쩍
        {
            yield return new WaitForSeconds(s.windup * 0.45f);
            EaglePose.RaiseEyeFlash(true);
            GameSfx.Play(GameSfxId.EagleScreech);
            yield return new WaitForSeconds(s.windup * 0.55f);
        }
        else
        {
            GameSfx.Play(GameSfxId.EagleScreech);
            yield return new WaitForSeconds(s.windup);
        }
    }

    /// <summary>
    /// 튕겨 나온 공격에 맞았을 때: 충격으로 휘청이며 밀려나고(pushDirX 방향) → 분노의 포효.
    /// (돌진 패링 때와 같은 연출. Recoil Time / Roar Delay / Roar Shake 설정을 같이 씀)
    /// </summary>
    public IEnumerator SweepStagger(float pushDirX, float recoil, float stunTime)
    {
        EaglePose.RaiseEyeFlash(false);
        EaglePose.Raise(EaglePose.Id.Hurt);
        GameSfx.Play(GameSfxId.EagleHurt);
        float minX = leftLimit != null ? leftLimit.position.x - 0.5f : -9f;
        float maxX = rightLimit != null ? rightLimit.position.x + 0.5f : 9f;
        Vector3 p0 = transform.position;
        Vector3 p1 = new Vector3(Mathf.Clamp(p0.x + pushDirX * recoil, minX, maxX), p0.y, p0.z);
        float rc = Mathf.Max(0.05f, recoilTime);
        for (float t = 0f; t < rc; t += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(p0, p1, 1f - Mathf.Pow(1f - t / rc, 2f));
            yield return null;
        }
        if (roarDelay > 0f) yield return new WaitForSeconds(roarDelay);

        EaglePose.Raise(EaglePose.Id.Roar);
        GameSfx.Play(GameSfxId.EagleScreech);
        Vector3 roarBase = transform.position;
        for (float t = 0f; t < stunTime; t += Time.deltaTime)
        {
            float fade = 1f - t / Mathf.Max(0.01f, stunTime) * 0.5f;
            Vector2 jit = Random.insideUnitCircle * (roarShake * fade);
            transform.position = roarBase + new Vector3(jit.x, jit.y, 0f);
            yield return null;
        }
        transform.position = roarBase;
        EaglePose.Raise(EaglePose.Id.Land);   // 다시 내려앉은 자세로
    }

    /// <summary>제자리 포효 (하늘에서 패링당했을 때 등). 호버가 멈춘 채 몸을 떨고, 끝나면 하늘 호버로 이어짐</summary>
    public IEnumerator RoarInPlace(float stunTime)
    {
        sweeping = true;   // 호버 이동을 잠시 멈춤
        EaglePose.RaiseEyeFlash(false);
        EaglePose.Raise(EaglePose.Id.Hurt);
        GameSfx.Play(GameSfxId.EagleHurt);
        yield return new WaitForSeconds(Mathf.Max(0.05f, recoilTime));
        EaglePose.Raise(EaglePose.Id.Roar);
        GameSfx.Play(GameSfxId.EagleScreech);
        Vector3 roarBase = transform.position;
        for (float t = 0f; t < stunTime; t += Time.deltaTime)
        {
            float fade = 1f - t / Mathf.Max(0.01f, stunTime) * 0.5f;
            Vector2 jit = Random.insideUnitCircle * (roarShake * fade);
            transform.position = roarBase + new Vector3(jit.x, jit.y, 0f);
            yield return null;
        }
        transform.position = roarBase;
        EaglePose.Raise(EaglePose.Id.Idle);
        curX = roarBase.x; curY = roarBase.y;
        sweeping = false;
    }

    /// <summary>
    /// 페이크 돌진: 돌진할 것처럼 자세를 잡고(눈 번쩍) 앞으로 살짝 덤비다가, 플레이어 머리 위를 넘어 반대편으로 휙 옮겨 간다.
    /// 끝나도 dashing 상태를 유지하므로 이어서 DashRoutine(진짜 돌진) 이나 FeintRoutine 을 바로 부르면 된다.
    /// </summary>
    public IEnumerator FeintRoutine(DashSettings s, float lungeDist, float lungeTime, float swapTime, bool flash)
    {
        dashing = true;
        float groundY = GroundY;
        float minX = leftLimit != null ? leftLimit.position.x + 0.5f : -9f;
        float maxX = rightLimit != null ? rightLimit.position.x - 0.5f : 9f;
        Vector3 start = transform.position;
        float z = start.z;
        float dirX = player != null && player.position.x < start.x ? -1f : 1f;
        EaglePose.FacingLocked = false;
        yield return null;
        EaglePose.FacingLocked = true;

        // 1) 돌진 자세 (진짜와 똑같이 보이도록 같은 윈드업)
        EaglePose.Raise(EaglePose.Id.Windup);
        GameSfx.Play(GameSfxId.EagleDiveWarn);
        float playerY0 = player != null ? player.position.y : groundY;
        Vector3 backTo = new Vector3(Mathf.Clamp(start.x - dirX * s.backOff, minX, maxX), Mathf.Max(playerY0 + 0.4f, groundY + s.minHeight), z);
        bool flashed = false;
        for (float t = 0f; t < s.windup; t += Time.deltaTime)
        {
            float k = t / s.windup;
            if (player != null) backTo.y = Mathf.Lerp(backTo.y, Mathf.Max(player.position.y + 0.4f, groundY + s.minHeight), 6f * Time.deltaTime);
            transform.position = Vector3.Lerp(start, backTo, Mathf.SmoothStep(0f, 1f, k));
            if (flash && !flashed && k > 0.45f) { flashed = true; EaglePose.RaiseEyeFlash(true); GameSfx.Play(GameSfxId.EagleScreech); }
            yield return null;
        }

        // 2) 덤비는 척 (판정 없음)
        EaglePose.Raise(EaglePose.Id.Dive);
        GameSfx.Play(GameSfxId.EagleDive);
        Vector3 l0 = transform.position;
        Vector3 l1 = new Vector3(Mathf.Clamp(l0.x + dirX * lungeDist, minX - 1f, maxX + 1f), l0.y, z);
        float lt = Mathf.Max(0.05f, lungeTime);
        for (float t = 0f; t < lt; t += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(l0, l1, 1f - Mathf.Pow(1f - t / lt, 2f));
            yield return null;
        }

        // 3) 방향을 바꿔 플레이어 머리 위로 넘어 반대편으로
        EaglePose.RaiseEyeFlash(false);
        EaglePose.Raise(EaglePose.Id.Windup);
        float px = player != null ? player.position.x : l1.x;
        float py = player != null ? player.position.y : groundY;
        float sideDist = s.backOff * 0.6f + 2f;
        float targetX = Mathf.Clamp(px + dirX * sideDist, minX, maxX);
        if (Mathf.Abs(targetX - px) < 2.5f) targetX = Mathf.Clamp(px - dirX * sideDist, minX, maxX);   // 벽에 막히면 반대쪽
        Vector3 w0 = transform.position;
        Vector3 w1 = new Vector3(targetX, Mathf.Max(py + 0.4f, groundY + s.minHeight), z);
        float st = Mathf.Max(0.05f, swapTime);
        for (float t = 0f; t < st; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / st);
            transform.position = Vector3.Lerp(w0, w1, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 2f);
            yield return null;
        }
        transform.position = w1;
        EaglePose.FacingLocked = false;   // 이어지는 돌진이 플레이어 쪽을 보게
    }

    /// <summary>날개를 젖힌 자세 (박자 사이: 다음 후려치기 준비)</summary>
    public void SweepReady() => EaglePose.Raise(EaglePose.Id.Windup);

    /// <summary>날개를 후려치는 순간 (포즈 + 소리). 충격파는 패턴이 만든다</summary>
    public void SweepStrike()
    {
        EaglePose.Raise(EaglePose.Id.Strike);
        GameSfx.Play(GameSfxId.EagleWingFlap);
    }

    /// <summary>날개 후려치기 마무리: 잠시 머문 뒤 하늘로 복귀</summary>
    public IEnumerator SweepLeave(SweepSettings s)
    {
        yield return new WaitForSeconds(s.hold);

        float groundY = GroundY;
        float minX = leftLimit != null ? leftLimit.position.x : -8f;
        float maxX = rightLimit != null ? rightLimit.position.x : 8f;
        EaglePose.Raise(EaglePose.Id.Idle);
        Vector3 back0 = transform.position;
        Vector3 back1 = new Vector3(Mathf.Clamp(back0.x, minX + 1f, maxX - 1f), groundY + hoverHeight, back0.z);
        for (float t = 0f; t < s.returnTime; t += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(back0, back1, Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.05f, s.returnTime)));
            yield return null;
        }
        curX = back1.x; curY = back1.y;
        sweeping = false;
    }

    // ───────────── 그로기: 추락 / 복귀 ─────────────

    /// <summary>하늘 그로기 1단계: 힘차게 날갯짓하며 target 까지 솟구친다. 도착하면 제자리에서 퍼덕이며 떠 있는다.
    /// 이후 SkySummon() → SkyDaze() 순서로 연출이 이어지고, Recover() 가 오면 끝난다.</summary>
    public void RiseGroggy(Vector3 target, float riseTime)
    {
        DestroyShadow();
        diving = false;
        dashing = false;
        sweeping = false;
        IsCharging = false;
        ParryWindow = false;
        EaglePose.FacingLocked = false;
        EaglePose.RaiseEyeFlash(false);
        grounded = true;   // 호버/패턴 이동을 멈춤 (그로기 상태 표시로 재사용)
        skyDazed = false;
        IsSummoning = false;
        EaglePose.FlapSpeed = riseFlapSpeed;
        EaglePose.Raise(EaglePose.Id.Idle);   // 날갯짓 스프라이트
        if (fallRoutine != null) StopCoroutine(fallRoutine);
        fallRoutine = StartCoroutine(RiseRoutine(target, Mathf.Max(0.1f, riseTime)));
    }

    /// <summary>하늘 그로기 2단계: 제자리에서 더 세게 퍼덕이며 바람을 일으킨다 (IsSummoning = true, 바람 이펙트/플랫폼 생성 신호)</summary>
    public void SkySummon()
    {
        IsSummoning = true;
        EaglePose.FlapSpeed = summonFlapSpeed;
    }

    /// <summary>하늘 그로기 3단계: 힘이 빠져 어지러운 포즈</summary>
    public void SkyDaze()
    {
        IsSummoning = false;
        skyDazed = true;
        EaglePose.FlapSpeed = 1f;
        EaglePose.Raise(EaglePose.Id.SkyGroggy);
        GameSfx.Play(GameSfxId.EagleGroggy);
    }

    private void ResetSky()
    {
        IsSummoning = false;
        skyDazed = false;
        EaglePose.FlapSpeed = 1f;
    }

    private IEnumerator RiseRoutine(Vector3 target, float riseTime)
    {
        Vector3 from = transform.position;
        float z = from.z;
        for (float t = 0f; t < riseTime; t += Time.deltaTime)
        {
            float k = 1f - (1f - t / riseTime) * (1f - t / riseTime);   // 빠르게 솟구쳐 천천히 멈춤
            transform.position = new Vector3(Mathf.Lerp(from.x, target.x, k), Mathf.Lerp(from.y, target.y, k), z);
            yield return null;
        }
        // 도착: 퍼덕이는 동안은 작게, 어지러워지면 둥실둥실 크게 (Recover 가 이 코루틴을 끊을 때까지)
        float t0 = Time.time;
        while (true)
        {
            float tt = Time.time - t0;
            float ax = skyDazed ? 0.15f : 0.05f;
            float ay = skyDazed ? 0.22f : 0.10f;
            transform.position = new Vector3(target.x + Mathf.Sin(tt * 1.3f) * ax, target.y + Mathf.Sin(tt * (skyDazed ? 2.1f : 5f)) * ay, z);
            yield return null;
        }
    }

    /// <summary>
    /// 사망 연출: 맞은 충격에 몸을 떨다가(flinch) 빙글빙글 돌며 바닥까지 추락, 쿵 하고 쓰러진 포즈로 멈춘다.
    /// 끝난 뒤에는 그 자리에 그대로 있음 (EagleDeathSequence 가 이어서 연출). 추락 중 onLand 가 착지 순간에 불림.
    /// </summary>
    public IEnumerator DeathFall(float flinchTime, float spinTurns, System.Action onLand)
    {
        if (fallRoutine != null) { StopCoroutine(fallRoutine); fallRoutine = null; }
        DestroyShadow();
        diving = dashing = sweeping = false;
        IsCharging = false; ParryWindow = false;
        ResetSky();
        EaglePose.FacingLocked = true;
        EaglePose.RaiseEyeFlash(false);
        grounded = true;
        externalRotation = true;

        float z = transform.position.z;
        Vector3 p0 = transform.position;

        // 1) 맞은 충격: 몸을 떨며 어질어질
        EaglePose.Raise(EaglePose.Id.SkyGroggy);
        GameSfx.Play(GameSfxId.EagleHurt);
        for (float t = 0f; t < flinchTime; t += Time.deltaTime)
        {
            float k = t / Mathf.Max(0.01f, flinchTime);
            Vector2 j = Random.insideUnitCircle * (0.18f * (1f - k * 0.4f));
            transform.position = new Vector3(p0.x + j.x, p0.y + j.y + Mathf.Sin(k * Mathf.PI) * 0.4f, z);
            yield return null;
        }

        // 2) 추락: 회전하며 점점 빨라짐
        float landY = GroundY + groggyHeight;
        var pose = GetComponent<EaglePoseController>();
        if (pose != null && pose.TryGetGroggyBottomLocalY(out float bottomLocal))
            landY = GroundY - bottomLocal * Mathf.Abs(transform.lossyScale.y) + landOffset;
        float x = transform.position.x;
        float y = transform.position.y, v = 0f;
        float startY = y;
        float dir = x < (leftLimit != null && rightLimit != null ? (leftLimit.position.x + rightLimit.position.x) * 0.5f : 0f) ? -1f : 1f;
        GameSfx.Play(GameSfxId.EagleDefeat);
        while (y > landY)
        {
            v += fallGravity * Time.deltaTime;
            y -= v * Time.deltaTime;
            y = Mathf.Max(y, landY);
            float prog = startY - landY > 0.01f ? 1f - (y - landY) / (startY - landY) : 1f;
            transform.position = new Vector3(x, y, z);
            transform.rotation = Quaternion.Euler(0f, 0f, dir * 360f * spinTurns * prog);
            yield return null;
        }

        // 3) 착지: 쓰러진 포즈, 한 번 튕김
        transform.rotation = Quaternion.identity;
        EaglePose.Raise(EaglePose.Id.Groggy);
        GameSfx.PlayAt(GameSfxId.EagleStomp, new Vector2(x, GroundY));
        onLand?.Invoke();
        float up = Mathf.Sqrt(2f * fallGravity * Mathf.Max(0.3f, bounceHeight * 1.4f)), by = 0f;
        while (up > 0f || by > 0f)
        {
            up -= fallGravity * Time.deltaTime;
            by = Mathf.Max(0f, by + up * Time.deltaTime);
            transform.position = new Vector3(x, landY + by, z);
            if (up < 0f && by <= 0f) break;
            yield return null;
        }
        transform.position = new Vector3(x, landY, z);
        tilt = 0f; tiltVel = 0f;
        externalRotation = false;
        curX = x; curY = landY;
    }

    /// <summary>그로기 시작. 그 자리(X)에서 바닥으로 떨어져 쓰러진다. 이후 Recover() 가 올 때까지 그대로 있음</summary>
    public void Fall()
    {
        if (grounded) return;
        ResetSky();
        DestroyShadow();
        diving = false;
        dashing = false;
        ParryWindow = false;
        EaglePose.FacingLocked = false;
        EaglePose.RaiseEyeFlash(false);
        grounded = true;
        if (fallRoutine != null) StopCoroutine(fallRoutine);
        fallRoutine = StartCoroutine(FallRoutine());
    }

    private IEnumerator FallRoutine()
    {
        EaglePose.Raise(EaglePose.Id.Groggy);
        GameSfx.Play(GameSfxId.EagleGroggy);

        float landY = GroundY + groggyHeight;
        var pose = GetComponent<EaglePoseController>();
        if (pose != null && pose.TryGetGroggyBottomLocalY(out float bottomLocal))
            landY = GroundY - bottomLocal * Mathf.Abs(transform.lossyScale.y) + landOffset;
        float x = transform.position.x, z = transform.position.z;
        float y = transform.position.y, v = 0f;
        while (y > landY)
        {
            v += fallGravity * Time.deltaTime;
            y -= v * Time.deltaTime;
            transform.position = new Vector3(x, Mathf.Max(y, landY), z);
            yield return null;
        }
        GameSfx.PlayAt(GameSfxId.EagleStomp, new Vector2(x, GroundY));

        // 착지 때 한 번 튕김
        if (bounceHeight > 0.01f)
        {
            float up = Mathf.Sqrt(2f * fallGravity * bounceHeight), by = 0f;
            while (up > 0f || by > 0f)
            {
                up -= fallGravity * Time.deltaTime;
                by = Mathf.Max(0f, by + up * Time.deltaTime);
                transform.position = new Vector3(x, landY + by, z);
                if (up < 0f && by <= 0f) break;
                yield return null;
            }
        }
        transform.position = new Vector3(x, landY, z);
        curX = x; curY = landY;
        fallRoutine = null;
    }

    /// <summary>그로기가 끝나면 하늘로 복귀</summary>
    public void Recover()
    {
        if (!grounded) return;
        ResetSky();
        if (fallRoutine != null) { StopCoroutine(fallRoutine); fallRoutine = null; }
        fallRoutine = StartCoroutine(RecoverRoutine());
    }

    private IEnumerator RecoverRoutine()
    {
        EaglePose.Raise(EaglePose.Id.Idle);
        GameSfx.Play(GameSfxId.EagleGroggyEnd);

        Vector3 from = transform.position;
        float hoverY = GroundY + hoverHeight;
        for (float t = 0f; t < recoverTime; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / recoverTime);
            transform.position = new Vector3(from.x, Mathf.Lerp(from.y, hoverY, k), from.z);
            yield return null;
        }
        curX = from.x;
        curY = hoverY;
        grounded = false;
        fallRoutine = null;
    }

    /// <summary>패턴이 끊겼을 때(페이즈 전환/그로기/사망) 급강하 상태와 그림자를 정리. 이후 Update 가 하늘로 부드럽게 복귀시킴</summary>
    public void CancelAll()
    {
        ResetSky();
        if (diving)
        {
            curX = transform.position.x;
            curY = transform.position.y;
        }
        if (dashing || sweeping) { curX = transform.position.x; curY = transform.position.y; }
        diving = false;
        dashing = false;
        sweeping = false;
        IsCharging = false;
        ParryWindow = false;
        EaglePose.FacingLocked = false;
        EaglePose.RaiseEyeFlash(false);
        DestroyShadow();
        if (!grounded) EaglePose.Raise(EaglePose.Id.Idle);   // 쓰러져 있는 동안엔 그로기 포즈 유지
    }

    private void OnDisable() => DestroyShadow();

    private void OnDrawGizmos()
    {
        if (!showHitGizmo) return;
        float r = lastHitRadius > 0f ? lastHitRadius : previewRadius;
        bool danger = dashing || diving;
        Gizmos.color = danger ? new Color(1f, 0.2f, 0.2f, 0.9f) : new Color(1f, 0.85f, 0.2f, 0.6f);
        var gbox = hitCollider as BoxCollider2D;
        if (gbox != null && gbox.enabled)
        {
            Transform t = gbox.transform;
            Vector3 c = t.TransformPoint(gbox.offset);
            Vector2 ls = t.lossyScale;
            Vector3 hs = new Vector3(gbox.size.x * Mathf.Abs(ls.x), gbox.size.y * Mathf.Abs(ls.y), 0f) * 0.5f;
            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(c, Quaternion.Euler(0f, 0f, t.eulerAngles.z), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, hs * 2f);
            Gizmos.matrix = old;
        }
        else if (hitCollider != null && hitCollider.enabled) Gizmos.DrawWireCube(hitCollider.bounds.center, hitCollider.bounds.size);
        else Gizmos.DrawWireSphere(transform.position, r);
    }
}