using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 최종 보스 연출: 가짜 보스(진화한 닭)의 포효 → 독수리 등장 → 닭이 한 방에 날아감 → 독수리가 진짜 보스로 전투 시작.
///   1) 포효     : 닭이 포효 (BGM 줄임, 공격 없음)
///   2) 접근     : 오른쪽에서 검은 독수리 실루엣이 점점 커지며(가까워지며) 천천히 다가옴. 발걸음마다 땅이 울리고 번개. 닭의 포효가 멈칫
///   3) 정체     : 독수리가 닭 바로 앞에서 실루엣을 벗고 정체를 드러냄 (번쩍)
///   4) 타격     : 날개를 휘둘러 닭을 침. 히트스톱 + 흰 번쩍임 + 큰 흔들림
///   5) 날아감   : 닭이 왼쪽 위로 포물선을 그리며 회전하고 작아지며 화면 밖으로, 깃털이 흩날림. 화면 구석에서 별이 "띵"
///   6) 포효     : 독수리가 울부짖음 → 보스 BGM 시작 → 전투 위치로 날아오름 → EagleBoss.Begin()
/// 사용: 씬에 빈 오브젝트로 두고 필드를 연결. Play On Start 를 켜거나 Play() 를 다른 곳(트리거 등)에서 호출.
/// 죽어서 처음부터 다시 시작해도 연출은 다시 나오지 않는다 (HasPlayed). 씬을 새로 불러오면 처음부터.
/// </summary>
public class EagleIntroSequence : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private EagleBoss boss;
    [SerializeField] private PatternRunner runner;
    [SerializeField] private StageBgm stageBgm;
    [Tooltip("닭 오브젝트 (피벗 Bottom Center 권장)")]
    [SerializeField] private Transform chicken;
    [SerializeField] private SpriteRenderer chickenBody;
    [Tooltip("닭에 붙은 ChickenPoseController (연출 중에는 멈춰 둠)")]
    [SerializeField] private ChickenPoseController pose;
    [SerializeField] private ChickenRoarOverlay roarOverlay;

    [Header("독수리")]
    [SerializeField] private Transform eagle;
    [SerializeField] private SpriteRenderer eagleBody;
    [Tooltip("eagle_stand: 땅에 선 자세 (쿵쿵 걸어올 때)")]
    [SerializeField] private Sprite standPose;
    [Tooltip("eagle_swing: 날개를 휘두르는 자세")]
    [SerializeField] private Sprite swingPose;
    [Tooltip("eagle_idle: 연출 끝 / 전투 자세")]
    [SerializeField] private Sprite idlePose;
    [Tooltip("eagle_spread: 포효 자세")]
    [SerializeField] private Sprite roarPose;
    [Tooltip("전투가 시작될 때 독수리가 날아가 자리잡는 위치 (하늘 쪽 빈 오브젝트)")]
    [SerializeField] private Transform fightAnchor;

    [Header("닭 포즈")]
    [Tooltip("포효 포즈 (chicken_front_roar)")]
    [SerializeField] private Sprite chickenRoarPose;
    [Tooltip("독수리 발소리에 겁먹은 포즈 (없으면 그대로)")]
    [SerializeField] private Sprite chickenNervousPose;
    [Tooltip("맞고 날아가는 포즈 (chicken_hurt)")]
    [SerializeField] private Sprite chickenFlyPose;

    [Header("1) 포효")]
    [SerializeField] private float roarTime = 2.0f;
    [SerializeField] private float roarShake = 0.1f;

    [Header("2) 접근 (검은 실루엣이 오른쪽에서 점점 다가옴)")]
    [Tooltip("화면 오른쪽 밖에서 닭 앞까지 다가오는 시간(초)")]
    [SerializeField] private float approachTime = 4.0f;
    [Tooltip("발걸음 간격(초). 걸음마다 쿵 소리 + 땅 울림 (번개는 Flash Every N Steps 걸음마다)")]
    [SerializeField] private float stepInterval = 0.6f;
    [Tooltip("N 걸음마다 번개가 번쩍임 (1 = 매 걸음)")]
    [SerializeField] private int flashEveryNSteps = 2;
    [Tooltip("시작할 때의 크기 배율 (멀리 있는 느낌). 다가오며 1 로 커짐")]
    [Range(0.3f, 1f)][SerializeField] private float approachScaleStart = 0.7f;
    [Tooltip("다가오는 속도 곡선: 1 = 일정, 클수록 처음엔 느리고 뒤로 갈수록 빨라짐")]
    [SerializeField] private float approachAccel = 1.4f;
    [Tooltip("독수리가 닭 앞에서 멈출 거리 (닭 위치에서 오른쪽으로)")]
    [SerializeField] private float stopDistance = 5.5f;
    [Tooltip("독수리 실루엣 색 (번개 때만 보이는 검은 그림자)")]
    [SerializeField] private Color silhouetteColor = new Color(0.04f, 0.04f, 0.06f, 1f);
    [SerializeField] private float stompShakeStart = 0.08f;
    [SerializeField] private float stompShakeEnd = 0.24f;

    [Header("3~4) 정체 공개 / 타격")]
    [SerializeField] private float revealPause = 0.7f;
    [SerializeField] private float windupTime = 0.35f;
    [SerializeField] private float lungeTime = 0.12f;
    [Tooltip("타격 때 독수리가 닭 쪽으로 돌진하는 거리")]
    [SerializeField] private float lungeDistance = 1.6f;
    [SerializeField] private float hitStopTime = 0.14f;
    [SerializeField] private float hitShakeTime = 0.45f;
    [SerializeField] private float hitShakeDistance = 0.4f;

    [Header("5) 닭이 날아감")]
    [SerializeField] private float flyTime = 1.8f;
    [Tooltip("총 회전 (바퀴 수, 반시계 +)")]
    [SerializeField] private float spinTurns = 3.5f;
    [Tooltip("날아가는 끝에서의 크기 배율 (멀어지는 느낌)")]
    [Range(0.05f, 1f)][SerializeField] private float endScale = 0.25f;
    [Tooltip("날아가는 경로의 정점 높이 (화면 위쪽 밖으로 얼마나 올라가는지)")]
    [SerializeField] private float arcAboveScreen = 5f;
    [SerializeField] private Sprite[] featherSprites;     // chicken_feather 등
    [SerializeField] private float featherEvery = 0.05f;

    [Header("별 반짝임 (화면 구석)")]
    [SerializeField] private SpriteRenderer twinkle;
    [SerializeField] private Sprite[] twinkleFrames;
    [SerializeField] private float twinkleFps = 12f;
    [Tooltip("화면 왼쪽 위 모서리에서 안쪽으로 이만큼")]
    [SerializeField] private Vector2 twinkleInset = new Vector2(1.6f, 1.4f);
    [SerializeField] private float twinkleHold = 0.9f;

    [Header("6) 독수리 포효 / 전투 시작")]
    [SerializeField] private float screechShakeTime = 0.6f;
    [SerializeField] private float screechShakeDistance = 0.28f;
    [Tooltip("전투 위치로 날아오르는 시간")]
    [SerializeField] private float liftTime = 1.3f;
    [Tooltip("보스 1페이즈 BGM (bgm_boss_eagle_p1)")]
    [SerializeField] private AudioClip bossBgm;
    [Range(0f, 1f)][SerializeField] private float bossBgmVolume = 0.7f;

    [Header("화면 효과 (Canvas UI)")]
    [Tooltip("화면 전체를 덮는 흰색 Image (처음엔 알파 0). 번개 / 타격 번쩍임용")]
    [SerializeField] private Image flash;

    [Header("흐름")]
    [SerializeField] private bool playOnStart = false;
    [SerializeField] private float startDelay = 1.0f;
    [Tooltip("연출이 끝나면 EagleBoss.Begin() 호출")]
    [SerializeField] private bool beginBossAtEnd = true;

    [Header("이벤트")]
    [Tooltip("연출 시작 (플레이어 입력 잠금, HUD 숨김 등)")]
    public UnityEvent Started;
    [Tooltip("연출 끝 (입력 풀기, 보스 HUD 표시 등)")]
    public UnityEvent Completed;

    public bool IsPlaying { get; private set; }
    public bool HasPlayed { get; private set; }

    private Coroutine routine;
    private Vector3 chickenPos0, chickenScale0;
    private Sprite chickenSprite0;
    private Color eagleColor0 = Color.white;
    private Vector3 eagleScale0 = Vector3.one;
    private bool cached;
    private float timeScale0 = 1f;
    private readonly List<GameObject> spawned = new List<GameObject>();

    private void Awake()
    {
        if (stageBgm == null) stageBgm = FindFirstObjectByType<StageBgm>();
        if (runner == null) runner = FindFirstObjectByType<PatternRunner>();
        if (chickenBody == null && chicken != null) chickenBody = chicken.GetComponentInChildren<SpriteRenderer>();
        if (pose == null && chicken != null) pose = chicken.GetComponent<ChickenPoseController>();
        Cache();
        HideProps();
    }

    private void Start()
    {
        if (playOnStart) StartCoroutine(PlayLater());
    }

    private IEnumerator PlayLater()
    {
        yield return new WaitForSecondsRealtime(startDelay);
        Play();
    }

    private void OnDisable()
    {
        if (Time.timeScale == 0f) Time.timeScale = timeScale0;   // 히트스톱 중에 꺼져도 시간이 멈춘 채로 남지 않게
    }

    private void Cache()
    {
        if (cached) return;
        cached = true;
        if (chicken != null) { chickenPos0 = chicken.position; chickenScale0 = chicken.localScale; }
        if (chickenBody != null) chickenSprite0 = chickenBody.sprite;
        if (eagleBody != null) eagleColor0 = eagleBody.color;
        if (eagle != null) eagleScale0 = eagle.localScale;
    }

    private void HideProps()
    {
        if (eagleBody != null) eagleBody.enabled = false;
        if (twinkle != null) twinkle.gameObject.SetActive(false);
        if (flash != null) { SetAlpha(flash, 0f); }
        foreach (var g in spawned) if (g != null) Destroy(g);
        spawned.Clear();
    }

    // ───────────── 외부 호출 ─────────────

    public void Play()
    {
        if (IsPlaying || HasPlayed) return;
        Cache();
        HasPlayed = true;
        routine = StartCoroutine(Sequence());
    }

    /// <summary>테스트용: 연출 흔적을 지우고 처음 상태로</summary>
    public void ResetIntro()
    {
        Cache();
        if (routine != null) StopCoroutine(routine);
        routine = null;
        if (Time.timeScale == 0f) Time.timeScale = timeScale0;
        IsPlaying = false;
        HasPlayed = false;
        if (roarOverlay != null) roarOverlay.Stop();
        HideProps();
        if (chicken != null)
        {
            chicken.gameObject.SetActive(true);
            chicken.position = chickenPos0; chicken.rotation = Quaternion.identity; chicken.localScale = chickenScale0;
        }
        if (chickenBody != null && chickenSprite0 != null) chickenBody.sprite = chickenSprite0;
        if (eagleBody != null) eagleBody.color = eagleColor0;
        if (eagle != null) eagle.localScale = eagleScale0;
        if (pose != null) pose.Unfreeze();
    }

    // ───────────── 연출 ─────────────

    private IEnumerator Sequence()
    {
        IsPlaying = true;
        Started?.Invoke();
        if (chicken != null) chicken.gameObject.SetActive(true);   // 닭은 처음부터 있는 상태로 시작
        if (runner != null) runner.Stop();
        if (stageBgm != null) stageBgm.Suppress(0.5f);
        if (pose != null) { pose.RebaseHere(); pose.Freeze(chickenRoarPose); }
        else if (chickenBody != null && chickenRoarPose != null) chickenBody.sprite = chickenRoarPose;

        Vector3 cPos = chicken != null ? chicken.position : Vector3.zero;
        Vector3 cScale = chicken != null ? chicken.localScale : Vector3.one;
        Rect cam = CameraRect();

        // 1) 가짜 보스의 포효
        GameSfx.Play(GameSfxId.ChickenRoar);
        if (roarOverlay != null) roarOverlay.Play();
        for (float t = 0f; t < roarTime; t += Time.unscaledDeltaTime)
        {
            if (chicken != null)
            {
                float a = roarShake * (0.5f + 0.5f * (t / roarTime));
                chicken.position = cPos + new Vector3(UnityEngine.Random.Range(-a, a), UnityEngine.Random.Range(-a, a) * 0.4f, 0f);
            }
            yield return null;
        }
        if (chicken != null) chicken.position = cPos;

        // 2) 쿵쿵: 번개 칠 때마다 실루엣이 한 걸음씩
        float stopX = cPos.x + stopDistance;
        float eagleHalf = eagleBody != null && eagleBody.sprite != null ? eagleBody.bounds.extents.x : 3f;
        float startX = cam.xMax + eagleHalf + 1f;
        float groundY = chicken != null ? chicken.position.y : 0f;
        if (eagle != null && eagleBody != null)
        {
            eagleBody.enabled = true;
            if (standPose != null) eagleBody.sprite = standPose;
            eagleBody.color = silhouetteColor;
            eagle.position = new Vector3(startX, groundY, eagle.position.z);
        }
        if (eagle != null)
        {
            // 점점 다가옴: 실루엣이 커지며(가까워지며) 일정한 발걸음으로 닭 앞까지
            float nextStep = 0f;
            int stepNo = 0;
            float stopAt = Mathf.Max(0.5f, approachTime);
            bool nervous = false;
            for (float t = 0f; t < stopAt; t += Time.unscaledDeltaTime)
            {
                float k = t / stopAt;
                float e = Mathf.Pow(k, Mathf.Max(0.5f, approachAccel));          // 뒤로 갈수록 빨라짐
                float hop = Mathf.Abs(Mathf.Sin(t / Mathf.Max(0.2f, stepInterval) * Mathf.PI)) * 0.14f;   // 걸을 때마다 작게 들썩
                eagle.position = new Vector3(Mathf.Lerp(startX, stopX, e), groundY + hop, eagle.position.z);
                eagle.localScale = eagleScale0 * Mathf.Lerp(approachScaleStart, 1f, e);

                if (t >= nextStep)   // 발걸음: 가까울수록 더 크게 울림
                {
                    nextStep = t + stepInterval;
                    stepNo++;
                    GameSfx.Play(GameSfxId.EagleStomp);
                    if (flashEveryNSteps > 0 && stepNo % flashEveryNSteps == 0) StartCoroutine(FlashRoutine(0.4f, 0.2f));
                    StartCoroutine(Shake(0.3f, Mathf.Lerp(stompShakeStart, stompShakeEnd, k)));
                }
                if (!nervous && k > 0.35f) { nervous = true; SetChickenPose(chickenNervousPose); }   // 포효가 멈칫
                yield return null;
            }
            eagle.position = new Vector3(stopX, groundY, eagle.position.z);
            eagle.localScale = eagleScale0;
        }

        // 3) 정체 공개
        yield return new WaitForSecondsRealtime(0.35f);
        GameSfx.Play(GameSfxId.EagleScreech);
        yield return FlashRoutine(0.9f, 0.3f);
        if (eagleBody != null) eagleBody.color = eagleColor0;
        yield return Shake(0.3f, 0.2f);
        yield return new WaitForSecondsRealtime(revealPause);

        // 4) 타격
        if (eagleBody != null && swingPose != null) eagleBody.sprite = swingPose;
        GameSfx.Play(GameSfxId.EagleWingFlap);
        yield return new WaitForSecondsRealtime(windupTime);
        if (eagle != null)
        {
            Vector3 from = eagle.position, to = from + Vector3.left * lungeDistance;
            for (float t = 0f; t < lungeTime; t += Time.unscaledDeltaTime) { eagle.position = Vector3.Lerp(from, to, t / lungeTime); yield return null; }
            eagle.position = to;
        }
        GameSfx.Play(GameSfxId.ChickenSmashed);
        SetChickenPose(chickenFlyPose);
        yield return HitStop(hitStopTime);
        StartCoroutine(FlashRoutine(1f, 0.2f));
        StartCoroutine(Shake(hitShakeTime, hitShakeDistance));

        // 5) 닭이 날아감
        yield return FlyAway(cPos, cScale, cam);

        // 6) 독수리 포효 → 보스 BGM → 전투 위치로
        if (eagleBody != null && roarPose != null) eagleBody.sprite = roarPose;
        GameSfx.Play(GameSfxId.EaglePhaseRoar);
        yield return Shake(screechShakeTime, screechShakeDistance);
        yield return new WaitForSecondsRealtime(0.4f);

        if (stageBgm != null && bossBgm != null) stageBgm.Release(bossBgm, bossBgmVolume, 0.6f);
        if (eagleBody != null && idlePose != null) eagleBody.sprite = idlePose;
        if (eagle != null && fightAnchor != null)
        {
            GameSfx.Play(GameSfxId.EagleWingFlap);
            Vector3 from = eagle.position, to = fightAnchor.position;
            for (float t = 0f; t < liftTime; t += Time.unscaledDeltaTime)
            {
                float e = t / liftTime; e = e * e * (3f - 2f * e);
                eagle.position = Vector3.Lerp(from, to, e);
                yield return null;
            }
            eagle.position = to;
        }

        IsPlaying = false;
        Completed?.Invoke();
        if (beginBossAtEnd && boss != null) boss.Begin();
    }

    // 닭: 왼쪽 위 포물선 + 회전 + 축소 + 깃털, 끝에 별 반짝
    private IEnumerator FlyAway(Vector3 start, Vector3 baseScale, Rect cam)
    {
        if (chicken == null) yield break;
        Vector3 p0 = chicken.position;
        Vector3 p2 = new Vector3(cam.xMin - 3f, cam.yMax + 3f, p0.z);                // 화면 왼쪽 위 밖
        Vector3 p1 = new Vector3(Mathf.Lerp(p0.x, p2.x, 0.35f), cam.yMax + arcAboveScreen, p0.z);   // 먼저 높이 솟았다가
        GameSfx.Play(GameSfxId.ChickenFlyAway);

        float nextFeather = 0f;
        for (float t = 0f; t < flyTime; t += Time.unscaledDeltaTime)
        {
            float k = t / flyTime;
            float e = 1f - (1f - k) * (1f - k);              // 처음엔 빠르고 점점 느려짐
            Vector3 pos = Bezier(p0, p1, p2, e);
            chicken.position = pos;
            chicken.rotation = Quaternion.Euler(0f, 0f, 360f * spinTurns * e);
            chicken.localScale = baseScale * Mathf.Lerp(1f, endScale, e);
            if (t >= nextFeather) { nextFeather = t + featherEvery; SpawnFeather(pos); }
            if (Mathf.Abs(k - 0.45f) < 0.02f) StartCoroutine(FlashRoutine(0.35f, 0.15f));   // 구름 속 번개 한 번
            yield return null;
        }
        chicken.gameObject.SetActive(false);

        // 화면 구석에서 별이 반짝
        yield return new WaitForSecondsRealtime(0.25f);
        if (twinkle != null && twinkleFrames != null && twinkleFrames.Length > 0)
        {
            twinkle.gameObject.SetActive(true);
            twinkle.transform.position = new Vector3(cam.xMin + twinkleInset.x, cam.yMax - twinkleInset.y, 0f);
            GameSfx.Play(GameSfxId.ChickenTwinkle);
            float dur = twinkleFrames.Length / twinkleFps;
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                twinkle.sprite = twinkleFrames[Mathf.Min(twinkleFrames.Length - 1, (int)(t * twinkleFps))];
                float pop = 1f + 0.35f * Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
                twinkle.transform.localScale = Vector3.one * pop;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(twinkleHold);
            twinkle.gameObject.SetActive(false);
        }
        else
        {
            GameSfx.Play(GameSfxId.ChickenTwinkle);
            yield return new WaitForSecondsRealtime(twinkleHold);
        }
    }

    private void SpawnFeather(Vector3 pos)
    {
        if (featherSprites == null || featherSprites.Length == 0) return;
        var go = new GameObject("Feather");
        go.transform.position = pos + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.5f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = featherSprites[UnityEngine.Random.Range(0, featherSprites.Length)];
        sr.sortingOrder = chickenBody != null ? chickenBody.sortingOrder + 1 : 20;
        spawned.Add(go);
        StartCoroutine(FeatherRoutine(go.transform, sr));
    }

    private IEnumerator FeatherRoutine(Transform tr, SpriteRenderer sr)
    {
        float life = UnityEngine.Random.Range(1.0f, 1.6f);
        Vector3 vel = new Vector3(UnityEngine.Random.Range(-2.5f, 2.5f), UnityEngine.Random.Range(0.5f, 2.5f), 0f);
        float spin = UnityEngine.Random.Range(-360f, 360f);
        float size = UnityEngine.Random.Range(0.6f, 1.1f);
        for (float t = 0f; t < life && tr != null; t += Time.unscaledDeltaTime)
        {
            float k = t / life;
            vel += Vector3.down * 5f * Time.unscaledDeltaTime;
            vel.x *= 1f - 1.2f * Time.unscaledDeltaTime;
            tr.position += vel * Time.unscaledDeltaTime;
            tr.Rotate(0f, 0f, spin * Time.unscaledDeltaTime);
            tr.localScale = Vector3.one * size;
            sr.color = new Color(1f, 1f, 1f, 1f - k * k);
            yield return null;
        }
        if (tr != null) Destroy(tr.gameObject);
    }

    // ───────────── 도우미 ─────────────

    private void SetChickenPose(Sprite s)
    {
        if (s == null) return;
        if (pose != null) pose.Freeze(s);
        else if (chickenBody != null) chickenBody.sprite = s;
    }

    private IEnumerator HitStop(float time)
    {
        timeScale0 = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(time);
        Time.timeScale = timeScale0;
    }

    private IEnumerator FlashRoutine(float peak, float time)
    {
        if (flash == null) yield break;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            SetAlpha(flash, peak * (1f - t / time));
            yield return null;
        }
        SetAlpha(flash, 0f);
    }

    private IEnumerator Shake(float time, float distance)
    {
        var cam = Camera.main != null ? Camera.main.transform : null;
        if (cam == null || time <= 0f) yield break;
        Vector3 basePos = cam.position;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float fade = 1f - t / time;
            cam.position = basePos + new Vector3(Mathf.Sin(t * 90f), Mathf.Cos(t * 77f), 0f) * distance * fade;
            yield return null;
        }
        cam.position = basePos;
    }

    private static Rect CameraRect()
    {
        var c = Camera.main;
        if (c == null) return new Rect(-9f, -5f, 18f, 10f);
        float h = c.orthographicSize * 2f, w = h * c.aspect;
        Vector3 p = c.transform.position;
        return new Rect(p.x - w * 0.5f, p.y - h * 0.5f, w, h);
    }

    private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    private static void SetAlpha(Image img, float a)
    {
        var c = img.color; c.a = a; img.color = c;
    }

    [ContextMenu("Debug/Play")] private void DebugPlay() => Play();
    [ContextMenu("Debug/Reset")] private void DebugReset() => ResetIntro();
}