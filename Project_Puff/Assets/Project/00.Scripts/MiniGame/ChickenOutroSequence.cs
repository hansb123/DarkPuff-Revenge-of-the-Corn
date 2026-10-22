using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 닭 미니게임 클리어 연출 (게이지가 다 차면 실행).
///   1) 분노 폭발   : 닭이 부들부들 떨며 포효, 김이 치솟음
///   2) 탈진        : 모든 공격 정지, 눈이 빙글빙글 돌며 쓰러지듯 주저앉음
///   3) 집게 등장   : 위에서 크레인 집게가 내려와 닭을 덥석 잡음 ("꼬꼬댁?!")
///   4) 끌려감      : 닭이 대롱대롱 매달려 위로 사라짐
///   5) 떡밥        : 암전 → 쿵… 쿵… 발소리 → 붉은 눈 → "다음 상대 : ???" 팻말
///   6) 끝 → Completed 이벤트 (결과 화면 / 다음 미니게임 씬으로 연결)
/// 사용: AngerGaugeHud 필드를 연결해 두면 게이지가 찰 때 자동으로 Play() 된다. (Completed 이벤트에 따로 연결할 필요 없음)
/// 죽어서 처음부터 다시 시작할 때는 ResetOutro()를 불러 원래 상태로 되돌린다.
/// </summary>
public class ChickenOutroSequence : MonoBehaviour, ITeaserSequence
{
    [Header("연결")]
    [Tooltip("연결해 두면 게이지가 가득 찰 때 자동 실행")]
    [SerializeField] private AngerGaugeHud anger;
    [SerializeField] private PatternRunner runner;
    [Tooltip("닭 오브젝트 (정면 닭, 피벗 Bottom Center 권장)")]
    [SerializeField] private Transform chicken;
    [SerializeField] private SpriteRenderer chickenBody;
    [Tooltip("닭에 붙은 ChickenPoseController. 연출 중에는 멈춰 둔다")]
    [SerializeField] private ChickenPoseController pose;
    [SerializeField] private ChickenRoarOverlay roarOverlay;
    [SerializeField] private StageBgm stageBgm;

    [Header("닭 포즈")]
    [Tooltip("분노 폭발 때 포즈 (chicken_front_roar)")]
    [SerializeField] private Sprite angryPose;
    [Tooltip("chicken_front_exhausted")]
    [SerializeField] private Sprite exhaustedPose;

    [Header("1) 분노 폭발")]
    [SerializeField] private float angryTime = 1.3f;
    [SerializeField] private float angryShake = 0.12f;
    [SerializeField] private float angryPulse = 0.06f;
    [Tooltip("김이 터져 나오는 위치 (닭 기준). 머리 양옆 쯤")]
    [SerializeField] private Vector2 steamOffsetLeft = new Vector2(-1.4f, 2.2f);
    [SerializeField] private Vector2 steamOffsetRight = new Vector2(1.4f, 2.2f);
    [SerializeField] private Sprite[] steamSprites;       // steam_1~4
    [SerializeField] private float steamSize = 1.2f;

    [Header("2) 탈진")]
    [SerializeField] private float collapseSquash = 0.12f;
    [SerializeField] private float exhaustedTime = 1.3f;
    [Tooltip("머리 위에서 도는 별 (SpriteRenderer, 처음엔 비활성). fx_dizzy_stars_1~2")]
    [SerializeField] private SpriteRenderer dizzyStars;
    [SerializeField] private Sprite[] dizzyFrames;
    [SerializeField] private float dizzyFps = 6f;
    [SerializeField] private Vector2 dizzyOffset = new Vector2(0f, 2.3f);

    [Header("3) 집게")]
    [Tooltip("집게 SpriteRenderer. 스프라이트 피벗은 Top Center (crane_claw_open)")]
    [SerializeField] private SpriteRenderer claw;
    [SerializeField] private Sprite clawOpen;
    [SerializeField] private Sprite clawClosed;
    [Tooltip("줄 SpriteRenderer (crane_cable). 피벗은 Bottom Center. 집게를 따라다니며 위로 늘어난다")]
    [SerializeField] private SpriteRenderer cable;
    [Tooltip("집게 머리(피벗~팔이 시작되는 곳)의 높이. 닭 머리 위에 이만큼 얹으면 팔이 닭을 감싼다. PPU 100 기준 약 1.4")]
    [SerializeField] private float clawHeadHeight = 1.4f;
    [Tooltip("화면 위로 이만큼 벗어난 곳에서 내려오기 시작")]
    [SerializeField] private float clawStartAbove = 4f;
    [SerializeField] private float clawDescendTime = 1.1f;
    [SerializeField] private float clawGrabHold = 0.35f;
    [SerializeField] private float liftTime = 1.2f;
    [SerializeField] private float dangleSwing = 6f;

    [Header("4) 떡밥 (Canvas UI)")]
    [Tooltip("화면 전체를 덮는 검은 Image (처음엔 알파 0 / 비활성 상관없음)")]
    [SerializeField] private Image darkOverlay;
    [Tooltip("teaser_eyes (Image)")]
    [SerializeField] private Image teaserEyes;
    [Tooltip("teaser_sign (RectTransform, 앵커 가운데)")]
    [SerializeField] private RectTransform teaserSign;
    [SerializeField] private float darkFadeTime = 0.5f;
    [SerializeField] private float quietBeforeStomp = 0.5f;
    [SerializeField] private float stompGap = 0.7f;
    [SerializeField] private float signStartOffsetY = -400f;
    [SerializeField] private float teaserHold = 2.2f;

    [Header("깃털 (활공 해금)")]
    [Tooltip("집게에 끌려가는 닭에게서 떨어지는 깃털 프리팹 (FeatherPickup)")]
    [SerializeField] private FeatherPickup featherPrefab;
    [Tooltip("끌려 올라가는 진행도(0~1)가 이 값을 넘으면 깃털이 떨어짐")]
    [Range(0f, 1f)] [SerializeField] private float featherDropAt = 0.25f;
    [Tooltip("이미 활공을 해금했으면 깃털을 다시 떨어뜨리지 않음 (재도전 때)")]
    [SerializeField] private bool skipFeatherIfOwned = true;

    [Header("떡밥 타이밍")]
    [Tooltip("켜면 예전처럼 연출 마지막에 바로 떡밥까지 재생. 끄면 PlayTeaser()를 직접 불러야 함 (다음 씬으로 넘어갈 때)")]
    [SerializeField] private bool playTeaserInline = false;

    [Header("재시작")]
    [Tooltip("연출이 끝나면 RespawnManager를 일반 모드(체크포인트 사용)로 되돌림. 같은 씬에서 플랫포머가 이어질 때 필요")]
    [SerializeField] private bool restoreCheckpointsAtEnd = true;

    [Header("이벤트")]
    [Tooltip("연출 시작 (플레이어 입력 잠그기, 게이지 숨기기 등)")]
    public UnityEvent Started;
    [Tooltip("연출 끝 (결과 화면 / 다음 미니게임 씬 이동)")]
    public UnityEvent Completed;
    [Tooltip("문을 열어도 되는 때: 깃털을 먹었을 때. (깃털이 안 떨어지는 경우 = 이미 활공 해금 / 프리팹 없음 → 연출이 끝나는 즉시). GoalDoor.Unlock()을 여기에 연결")]
    public UnityEvent ReadyToLeave;
    [Tooltip("떡밥 연출이 끝났을 때 (다음 씬 로드 / 결과 화면 연결)")]
    public UnityEvent TeaserCompleted;

    public bool IsTeaserPlaying { get; private set; }
    UnityEvent ITeaserSequence.TeaserCompleted => TeaserCompleted;
    public bool IsPlaying { get; private set; }
    public bool HasPlayed { get; private set; }
    public event Action Finished;

    private Coroutine routine;
    private Coroutine teaserRoutine;
    private bool featherDropped;
    private bool featherPending;     // 깃털이 떨어졌고 아직 안 먹음
    private Vector3 chickenPos0, chickenScale0;
    private Sprite chickenSprite0;
    private Vector2 signPos0;
    private bool cached;
    private readonly System.Collections.Generic.List<GameObject> spawned = new System.Collections.Generic.List<GameObject>();

    private void Awake()
    {
        if (stageBgm == null) stageBgm = FindFirstObjectByType<StageBgm>();
        if (chickenBody == null && chicken != null) chickenBody = chicken.GetComponentInChildren<SpriteRenderer>();
        if (pose == null && chicken != null) pose = chicken.GetComponent<ChickenPoseController>();
        if (runner == null) runner = FindFirstObjectByType<PatternRunner>();
        Cache();
        HideProps();
    }

    private void OnEnable()
    {
        if (anger != null) anger.Completed.AddListener(Play);
        FeatherPickup.Collected += OnFeatherCollected;
    }

    private void OnDisable()
    {
        if (anger != null) anger.Completed.RemoveListener(Play);
        FeatherPickup.Collected -= OnFeatherCollected;
    }

    private void OnFeatherCollected()
    {
        if (!featherPending) return;
        featherPending = false;
        if (!IsPlaying) ReadyToLeave?.Invoke();   // 연출이 끝난 뒤 먹었으면 바로 문 열기 (끝나기 전에 먹었다면 연출 끝에서 처리)
    }

    private void Cache()
    {
        if (cached) return;
        cached = true;
        if (chicken != null) { chickenPos0 = chicken.position; chickenScale0 = chicken.localScale; }
        if (chickenBody != null) chickenSprite0 = chickenBody.sprite;
        if (teaserSign != null) signPos0 = teaserSign.anchoredPosition;
    }

    private void HideProps()
    {
        if (claw != null) claw.gameObject.SetActive(false);
        if (cable != null) cable.gameObject.SetActive(false);
        if (dizzyStars != null) dizzyStars.gameObject.SetActive(false);
        if (darkOverlay != null) { SetAlpha(darkOverlay, 0f); darkOverlay.gameObject.SetActive(false); }
        if (teaserEyes != null) { SetAlpha(teaserEyes, 0f); teaserEyes.gameObject.SetActive(false); }
        if (teaserSign != null) teaserSign.gameObject.SetActive(false);
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

    /// <summary>처음부터 다시 시작할 때(죽어서 재시작 등): 연출 흔적을 지우고 닭을 원래대로</summary>
    public void ResetOutro()
    {
        Cache();
        if (routine != null) StopCoroutine(routine);
        routine = null;
        if (teaserRoutine != null) StopCoroutine(teaserRoutine);
        teaserRoutine = null;
        IsTeaserPlaying = false;
        IsPlaying = false;
        HasPlayed = false;
        if (roarOverlay != null) roarOverlay.Stop();
        HideProps();
        if (chicken != null)
        {
            chicken.gameObject.SetActive(true);
            chicken.position = chickenPos0;
            chicken.rotation = Quaternion.identity;
            chicken.localScale = chickenScale0;
        }
        if (chickenBody != null && chickenSprite0 != null) chickenBody.sprite = chickenSprite0;
        if (pose != null) pose.Unfreeze();
    }

    // ───────────── 연출 ─────────────

    private IEnumerator Sequence()
    {
        IsPlaying = true;
        featherDropped = false;
        featherPending = false;
        Started?.Invoke();
        if (runner != null) runner.Stop();                       // 공격 정지 + 남은 알/기둥 정리
        if (stageBgm != null) stageBgm.Suppress(0.4f);
        if (pose != null) { pose.RebaseHere(); pose.Freeze(angryPose); }
        else if (chickenBody != null && angryPose != null) chickenBody.sprite = angryPose;

        Vector3 basePos = chicken != null ? chicken.position : Vector3.zero;
        Vector3 baseScale = chicken != null ? chicken.localScale : Vector3.one;

        // 1) 분노 폭발
        GameSfx.Play(GameSfxId.ChickenRoar);
        if (roarOverlay != null) roarOverlay.Play();
        float nextSteam = 0f;
        for (float t = 0f; t < angryTime; t += Time.unscaledDeltaTime)
        {
            float k = t / angryTime;
            if (chicken != null)
            {
                float amp = angryShake * (0.4f + 0.6f * k);
                chicken.position = basePos + new Vector3(UnityEngine.Random.Range(-amp, amp), UnityEngine.Random.Range(-amp, amp) * 0.4f, 0f);
                float pulse = 1f + Mathf.Sin(t * 40f) * angryPulse * k;
                chicken.localScale = new Vector3(baseScale.x * pulse, baseScale.y * pulse, baseScale.z);
            }
            if (t >= nextSteam)
            {
                nextSteam = t + 0.18f;
                SpawnSteam(basePos + (Vector3)steamOffsetLeft, -1);
                SpawnSteam(basePos + (Vector3)steamOffsetRight, 1);
                GameSfx.Play(GameSfxId.SteamHiss);
            }
            yield return null;
        }
        if (chicken != null) { chicken.position = basePos; chicken.localScale = baseScale; }

        // 2) 탈진: 폭발이 끝나고 푹 주저앉음
        if (chickenBody != null && exhaustedPose != null) chickenBody.sprite = exhaustedPose;
        GameSfx.Play(GameSfxId.ChickenCollapse);
        for (int i = 0; i < 3; i++) SpawnSteam(basePos + new Vector3(UnityEngine.Random.Range(-1.6f, 1.6f), UnityEngine.Random.Range(0.3f, 1.6f), 0f), 0);
        yield return Ease(0.18f, k =>
        {
            if (chicken == null) return;
            chicken.localScale = new Vector3(baseScale.x * (1f + collapseSquash * k), baseScale.y * (1f - collapseSquash * k), baseScale.z);
        });
        Vector3 exhaustedScale = chicken != null ? chicken.localScale : baseScale;
        yield return Shake(0.2f, 0.1f);

        if (dizzyStars != null && dizzyFrames != null && dizzyFrames.Length > 0)
        {
            dizzyStars.gameObject.SetActive(true);
            dizzyStars.transform.position = basePos + (Vector3)dizzyOffset;
            StartCoroutine(DizzyLoop());
        }
        // 숨 헐떡임
        for (float t = 0f; t < exhaustedTime; t += Time.unscaledDeltaTime)
        {
            if (chicken != null)
            {
                float b = 1f + Mathf.Sin(t * 9f) * 0.015f;
                chicken.localScale = new Vector3(exhaustedScale.x, exhaustedScale.y * b, exhaustedScale.z);
            }
            yield return null;
        }

        // 3) 집게가 내려와 닭을 잡음
        float topY = chickenBody != null ? chickenBody.bounds.max.y : basePos.y + 4f;
        float grabY = topY + clawHeadHeight;
        float camTop = CameraTop();
        float startY = camTop + clawStartAbove;
        if (claw != null)
        {
            claw.gameObject.SetActive(true);
            claw.sprite = clawOpen;
            if (cable != null) cable.gameObject.SetActive(true);
            SetClaw(basePos.x, startY);
            GameSfx.Play(GameSfxId.CraneMotor);

            bool surprised = false;
            for (float t = 0f; t < clawDescendTime; t += Time.unscaledDeltaTime)
            {
                float k = t / clawDescendTime;
                float e = 1f - (1f - k) * (1f - k);
                SetClaw(basePos.x, Mathf.Lerp(startY, grabY, e));
                if (!surprised && k > 0.55f) { surprised = true; GameSfx.Play(GameSfxId.ChickenDizzy); if (dizzyStars != null) dizzyStars.gameObject.SetActive(false); }
                yield return null;
            }
            SetClaw(basePos.x, grabY);
            yield return new WaitForSecondsRealtime(0.12f);
            claw.sprite = clawClosed;
            GameSfx.Play(GameSfxId.CraneClank);
            yield return Shake(0.2f, 0.12f);
            yield return new WaitForSecondsRealtime(clawGrabHold);

            // 4) 끌려감: 닭이 집게에 매달려 대롱대롱 위로
            Vector3 hang = chicken != null ? chicken.position - claw.transform.position : Vector3.zero;
            float liftTo = camTop + clawStartAbove + (chickenBody != null ? chickenBody.bounds.size.y : 4f);
            GameSfx.Play(GameSfxId.CraneMotor);
            for (float t = 0f; t < liftTime; t += Time.unscaledDeltaTime)
            {
                float k = t / liftTime;
                float e = k * k;
                SetClaw(basePos.x, Mathf.Lerp(grabY, liftTo, e));
                if (!featherDropped && k >= featherDropAt) DropFeather(basePos);
                if (chicken != null)
                {
                    chicken.position = claw.transform.position + hang;
                    chicken.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 10f) * dangleSwing * (1f - k * 0.3f));
                }
                yield return null;
            }
            claw.gameObject.SetActive(false);
            if (cable != null) cable.gameObject.SetActive(false);
        }
        if (chicken != null) chicken.gameObject.SetActive(false);

        // 5) 끝: 닭은 사라지고 깃털만 남음. 플레이어가 깃털을 먹은 뒤 다음 씬으로 넘어갈 때 PlayTeaser()를 부른다
        routine = null;
        IsPlaying = false;
        if (restoreCheckpointsAtEnd && RespawnManager.Instance != null) RespawnManager.Instance.UseCheckpoints = true;
        if (playTeaserInline) { yield return TeaserRoutine(); }
        Completed?.Invoke();
        if (!featherPending) ReadyToLeave?.Invoke();   // 깃털이 없었거나 이미 먹었으면 바로
        Finished?.Invoke();
    }

    /// <summary>
    /// 떡밥(암전 → 쿵쿵 → 붉은 눈 → "다음 상대 : ???")만 따로 재생. 다음 씬으로 넘어가기 직전(문에 들어갈 때 등)에 부른다.
    /// 끝나면 TeaserCompleted 이벤트 → 여기에 씬 이동 / 결과 화면을 연결.
    /// </summary>
    public void PlayTeaser()
    {
        if (teaserRoutine != null) return;
        Cache();
        teaserRoutine = StartCoroutine(TeaserOnly());
    }

    private IEnumerator TeaserOnly()
    {
        if (stageBgm != null) stageBgm.Suppress(0.3f);
        yield return TeaserRoutine();
        teaserRoutine = null;
        TeaserCompleted?.Invoke();
    }

    private IEnumerator TeaserRoutine()
    {
        IsTeaserPlaying = true;
        if (darkOverlay != null)
        {
            darkOverlay.gameObject.SetActive(true);
            yield return Ease(darkFadeTime, k => SetAlpha(darkOverlay, k));
        }
        yield return new WaitForSecondsRealtime(quietBeforeStomp);

        GameSfx.Play(GameSfxId.MechaStomp);
        yield return Shake(0.25f, 0.1f);
        yield return new WaitForSecondsRealtime(stompGap);
        GameSfx.Play(GameSfxId.MechaStomp);
        yield return Shake(0.3f, 0.18f);
        yield return new WaitForSecondsRealtime(stompGap * 0.8f);

        GameSfx.Play(GameSfxId.TeaserSting);
        if (teaserEyes != null)
        {
            teaserEyes.gameObject.SetActive(true);
            SetAlpha(teaserEyes, 1f);
            // 번쩍번쩍
            for (int i = 0; i < 3; i++)
            {
                SetAlpha(teaserEyes, 0.2f); yield return new WaitForSecondsRealtime(0.06f);
                SetAlpha(teaserEyes, 1f);   yield return new WaitForSecondsRealtime(0.08f);
            }
        }
        yield return Shake(0.3f, 0.22f);

        if (teaserSign != null)
        {
            teaserSign.gameObject.SetActive(true);
            Vector2 from = signPos0 + Vector2.up * signStartOffsetY;
            teaserSign.anchoredPosition = from;
            yield return Ease(0.4f, k => teaserSign.anchoredPosition = Vector2.LerpUnclamped(from, signPos0, EaseOutBack(k)));
            GameSfx.Play(GameSfxId.CraneClank);
        }
        yield return new WaitForSecondsRealtime(teaserHold);

        IsTeaserPlaying = false;
    }

    // ───────────── 보조 ─────────────

    private void DropFeather(Vector3 basePos)
    {
        featherDropped = true;
        if (featherPrefab == null) return;
        if (skipFeatherIfOwned && AbilityProgress.GlideUnlocked) return;
        featherPending = true;

        float fromY = chicken != null ? chicken.position.y + (chickenBody != null ? chickenBody.bounds.size.y * 0.5f : 2f) : basePos.y + 4f;
        Vector3 pos = chicken != null ? chicken.position : basePos;
        var f = Instantiate(featherPrefab);
        f.Launch(pos.x, fromY, basePos.y);   // 닭이 서 있던 바닥 높이까지 팔랑팔랑
        spawned.Add(f.gameObject);
    }

    private void SetClaw(float x, float y)
    {
        claw.transform.position = new Vector3(x, y, claw.transform.position.z);
        if (cable == null) return;
        cable.transform.position = new Vector3(x, y, cable.transform.position.z);   // 줄 피벗 = 아래 → 집게 윗부분에 닿음
        float unit = cable.sprite != null ? cable.sprite.bounds.size.y : 1f;
        float need = Mathf.Max(0f, CameraTop() + 2f - y);
        var s = cable.transform.localScale;
        cable.transform.localScale = new Vector3(s.x, unit > 0f ? need / unit : 1f, s.z);
    }

    private static float CameraTop()
    {
        var cam = Camera.main;
        return cam != null ? cam.transform.position.y + cam.orthographicSize : 6f;
    }

    private void SpawnSteam(Vector3 pos, int dir)
    {
        if (steamSprites == null || steamSprites.Length == 0) return;
        var go = new GameObject("Steam");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = steamSprites[UnityEngine.Random.Range(0, steamSprites.Length)];
        sr.sortingOrder = chickenBody != null ? chickenBody.sortingOrder + 2 : 20;
        spawned.Add(go);
        StartCoroutine(SteamRoutine(go.transform, sr, dir));
    }

    private IEnumerator SteamRoutine(Transform tr, SpriteRenderer sr, int dir)
    {
        float life = UnityEngine.Random.Range(0.7f, 1.0f);
        Vector3 vel = new Vector3(dir * UnityEngine.Random.Range(0.6f, 1.6f), UnityEngine.Random.Range(1.6f, 2.8f), 0f);
        float size = steamSize * UnityEngine.Random.Range(0.8f, 1.2f);
        for (float t = 0f; t < life && tr != null; t += Time.unscaledDeltaTime)
        {
            float k = t / life;
            tr.position += vel * Time.unscaledDeltaTime;
            tr.localScale = Vector3.one * size * (0.6f + k * 0.9f);
            sr.color = new Color(1f, 1f, 1f, 1f - k * k);
            yield return null;
        }
        if (tr != null) Destroy(tr.gameObject);
    }

    private IEnumerator DizzyLoop()
    {
        int i = 0;
        while (dizzyStars != null && dizzyStars.gameObject.activeSelf)
        {
            dizzyStars.sprite = dizzyFrames[i++ % dizzyFrames.Length];
            yield return new WaitForSecondsRealtime(1f / dizzyFps);
        }
    }

    private static IEnumerator Ease(float time, Action<float> apply)
    {
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime) { apply(t / time); yield return null; }
        apply(1f);
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

    private static void SetAlpha(Image img, float a)
    {
        var c = img.color; c.a = a; img.color = c;
    }

    private static float EaseOutBack(float x)
    {
        x = Mathf.Clamp01(x);
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    [ContextMenu("Debug/Play")] private void DebugPlay() => Play();
    [ContextMenu("Debug/Play Teaser")] private void DebugTeaser() => PlayTeaser();
    [ContextMenu("Debug/Reset")] private void DebugReset() => ResetOutro();
}
