using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 메카 닭 미니게임 클리어 연출 (게이지가 다 차면 실행).
///   1) 과부하   : 몸이 부들부들, 눈이 붉게 번쩍, 스파크가 튀고 경보음이 점점 빨라짐
///   2) 정지     : 하얗게 번쩍 → 눈이 꺼지고 축 처짐 (연기), 정적
///   3) 코어 개방: 가슴 뚜껑이 열리고 코어 불빛 → 단단해지기 알이 튀어나와 바닥에서 통통
///   4) 알 먹기  : 먹으면 ReadyToLeave → 문 해금
///   5) 떡밥     : (문에 들어갈 때 PlayTeaser) 암전 → 쿵…쿵… → 강화된 닭 실루엣 + 붉은 눈 → "다음 상대 : 닭…?" → 큰 그림자가 스치고 검은 깃털 한 장이 떨어짐 (독수리 복선)
/// 사용: AngerGaugeHud 를 연결해 두면 게이지가 찰 때 자동 Play().  죽어서 재시작할 때는 ResetOutro().
/// </summary>
public class MechaOutroSequence : MonoBehaviour, ITeaserSequence
{
    [Header("연결")]
    [SerializeField] private AngerGaugeHud anger;
    [SerializeField] private PatternRunner runner;
    [Tooltip("메카 닭 오브젝트 (MechaPoseController 가 붙은 것)")]
    [SerializeField] private Transform mecha;
    [SerializeField] private SpriteRenderer mechaBody;
    [SerializeField] private MechaPoseController pose;
    [SerializeField] private StageBgm stageBgm;

    [Header("메카 포즈 (mecha_outro 폴더)")]
    [SerializeField] private Sprite overloadPose;     // mecha_overload
    [SerializeField] private Sprite deadPose;         // mecha_dead
    [SerializeField] private Sprite coreOpenPose;     // mecha_core_open
    [Tooltip("가슴 코어 위치에 둔 빈 오브젝트. 알이 여기서 튀어나옴. 비우면 몸 중앙")]
    [SerializeField] private Transform coreAnchor;

    [Header("1) 과부하")]
    [SerializeField] private float overloadTime = 2.0f;
    [SerializeField] private float shakeMax = 0.14f;
    [Tooltip("눈이 번쩍이는 속도(초당 횟수)")]
    [SerializeField] private float flickerRate = 10f;
    [SerializeField] private SpriteRenderer overloadFlash;    // fx_overload_flash (몸 위, 처음엔 비활성)
    [SerializeField] private float overloadFlashSize = 7f;
    [SerializeField] private Sprite[] sparkSprites;           // fx_spark_1~4
    [SerializeField] private float sparkSize = 1.6f;
    [SerializeField] private float sparkInterval = 0.12f;

    [Header("2) 정지")]
    [SerializeField] private float powerDownHold = 1.1f;
    [SerializeField] private Sprite[] smokeSprites;           // fx_smoke_1~4
    [SerializeField] private float smokeSize = 2f;
    [SerializeField] private float squash = 0.06f;

    [Header("3) 코어 개방 / 알")]
    [SerializeField] private SpriteRenderer coreGlow;         // fx_core_glow (코어 위치, 처음엔 비활성)
    [SerializeField] private float coreGlowSize = 4f;
    [SerializeField] private Sprite[] corePopSprites;         // fx_core_pop_1~3
    [SerializeField] private float corePopSize = 3.2f;
    [SerializeField] private float coreOpenHold = 0.7f;
    [SerializeField] private HardEggPickup eggPrefab;
    [Tooltip("알을 먹었을 때 띄울 '단단해지기 해금!' 팝업 (씬의 AbilityUnlockPopup, hard_unlock_banner). 활공 팝업과 다른 오브젝트")]
    [SerializeField] private AbilityUnlockPopup unlockPopup;
    [Tooltip("이미 단단해지기를 해금했으면 알을 다시 떨어뜨리지 않음 (재도전 때)")]
    [SerializeField] private bool skipEggIfOwned = true;
    [Tooltip("바닥 높이. 비우면 메카 닭의 발(피벗)")]
    [SerializeField] private Transform groundRef;

    [Header("4) 떡밥 (Canvas UI)")]
    [SerializeField] private Image darkOverlay;
    [Tooltip("teaser_chicken_silhouette (Image, 가운데). 처음엔 비활성")]
    [SerializeField] private Image silhouette;
    [Tooltip("teaser_chicken_eyes (Image, 실루엣 머리 위치). 처음엔 비활성")]
    [SerializeField] private Image teaserEyes;
    [Tooltip("teaser_sign_next (RectTransform, 앵커 가운데)")]
    [SerializeField] private RectTransform teaserSign;
    [Tooltip("shadow_pass (Image, 화면 위쪽 가로로 큼). 알파는 이미지 색에서 0.5 정도로. 처음엔 비활성")]
    [SerializeField] private RectTransform shadowPass;
    [Tooltip("feather_black_1 (Image, RectTransform). 한 장만 천천히 떨어짐. 처음엔 비활성")]
    [SerializeField] private RectTransform blackFeather;
    [SerializeField] private float darkFadeTime = 0.6f;
    [SerializeField] private float quietBeforeStomp = 0.5f;
    [SerializeField] private float stompGap = 0.75f;
    [SerializeField] private float signStartOffsetY = -400f;
    [SerializeField] private float signHold = 1.6f;
    [Tooltip("그림자가 화면 왼쪽 → 오른쪽으로 지나가는 시간")]
    [SerializeField] private float shadowTime = 2.0f;
    [Tooltip("그림자가 지나가는 화면 가로 폭(UI 단위, 1920 캔버스 기준 약 3000)")]
    [SerializeField] private float shadowTravel = 3000f;
    [SerializeField] private float featherFallTime = 3.2f;
    [SerializeField] private float featherFallDistance = 700f;
    [SerializeField] private float afterHold = 1.2f;

    [Header("재시작")]
    [SerializeField] private bool restoreCheckpointsAtEnd = true;

    [Header("이벤트")]
    public UnityEvent Started;
    [Tooltip("과부하~알이 튀어나올 때까지 연출 끝")]
    public UnityEvent Completed;
    [Tooltip("문을 열어도 되는 때: 알을 먹었을 때 (알이 안 떨어지는 경우엔 연출이 끝나는 즉시). GoalDoor.Unlock() 연결")]
    public UnityEvent ReadyToLeave;
    [Tooltip("떡밥이 끝났을 때 (다음 씬 / 결과 화면)")]
    public UnityEvent TeaserCompleted;

    public bool IsPlaying { get; private set; }
    public bool HasPlayed { get; private set; }
    public bool IsTeaserPlaying { get; private set; }
    UnityEvent ITeaserSequence.TeaserCompleted => TeaserCompleted;
    public event Action Finished;

    private Coroutine routine, teaserRoutine;
    private bool eggPending;
    private Vector3 pos0, scale0;
    private Sprite sprite0;
    private Vector2 signPos0;
    private bool cached;
    private readonly List<GameObject> spawned = new List<GameObject>();

    private void Awake()
    {
        if (stageBgm == null) stageBgm = FindFirstObjectByType<StageBgm>();
        if (mechaBody == null && mecha != null) mechaBody = mecha.GetComponentInChildren<SpriteRenderer>();
        if (pose == null && mecha != null) pose = mecha.GetComponent<MechaPoseController>();
        if (runner == null) runner = FindFirstObjectByType<PatternRunner>();
        Cache();
        HideProps();
    }

    private void OnEnable()
    {
        if (anger != null) anger.Completed.AddListener(Play);
        HardEggPickup.Collected += OnEggCollected;
    }

    private void OnDisable()
    {
        if (anger != null) anger.Completed.RemoveListener(Play);
        HardEggPickup.Collected -= OnEggCollected;
    }

    private void OnEggCollected()
    {
        if (!eggPending) return;
        eggPending = false;
        if (!IsPlaying) ReadyToLeave?.Invoke();
    }

    private void Cache()
    {
        if (cached) return;
        cached = true;
        if (mecha != null) { pos0 = mecha.position; scale0 = mecha.localScale; }
        if (mechaBody != null) sprite0 = mechaBody.sprite;
        if (teaserSign != null) signPos0 = teaserSign.anchoredPosition;
    }

    private void HideProps()
    {
        if (overloadFlash != null) overloadFlash.gameObject.SetActive(false);
        if (coreGlow != null) coreGlow.gameObject.SetActive(false);
        if (darkOverlay != null) { SetAlpha(darkOverlay, 0f); darkOverlay.gameObject.SetActive(false); }
        if (silhouette != null) { SetAlpha(silhouette, 0f); silhouette.gameObject.SetActive(false); }
        if (teaserEyes != null) { SetAlpha(teaserEyes, 0f); teaserEyes.gameObject.SetActive(false); }
        if (teaserSign != null) teaserSign.gameObject.SetActive(false);
        if (shadowPass != null) shadowPass.gameObject.SetActive(false);
        if (blackFeather != null) blackFeather.gameObject.SetActive(false);
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

    public void ResetOutro()
    {
        Cache();
        if (routine != null) StopCoroutine(routine);
        if (teaserRoutine != null) StopCoroutine(teaserRoutine);
        routine = teaserRoutine = null;
        StopAllCoroutines();
        IsPlaying = IsTeaserPlaying = HasPlayed = false;
        eggPending = false;
        HideProps();
        if (mecha != null)
        {
            mecha.gameObject.SetActive(true);
            mecha.position = pos0; mecha.rotation = Quaternion.identity; mecha.localScale = scale0;
        }
        if (mechaBody != null && sprite0 != null) mechaBody.sprite = sprite0;
        if (pose != null) pose.Unfreeze();
    }

    // ───────────── 연출 ─────────────

    private IEnumerator Sequence()
    {
        IsPlaying = true;
        eggPending = false;
        Started?.Invoke();
        if (runner != null) runner.Stop();
        if (stageBgm != null) stageBgm.Suppress(0.3f);
        if (pose != null) { pose.RebaseHere(); pose.Freeze(overloadPose); }
        else if (mechaBody != null && overloadPose != null) mechaBody.sprite = overloadPose;

        Vector3 basePos = mecha != null ? mecha.position : Vector3.zero;
        Vector3 baseScale = mecha != null ? mecha.localScale : Vector3.one;
        Bounds bounds = mechaBody != null ? mechaBody.bounds : new Bounds(basePos, Vector3.one * 4f);
        int order = mechaBody != null ? mechaBody.sortingOrder : 10;

        // 1) 과부하
        GameSfx.Play(GameSfxId.MechaOverload);
        if (overloadFlash != null)
        {
            overloadFlash.gameObject.SetActive(true);
            overloadFlash.transform.position = bounds.center + Vector3.up * bounds.size.y * 0.25f;
            overloadFlash.sortingOrder = order + 3;
            SetWorldWidth(overloadFlash, overloadFlashSize);
            SetAlpha(overloadFlash, 0f);
        }
        float nextSpark = 0f;
        for (float t = 0f; t < overloadTime; t += Time.unscaledDeltaTime)
        {
            float k = t / overloadTime;
            if (mecha != null)
            {
                float amp = shakeMax * (0.25f + 0.75f * k);
                mecha.position = basePos + new Vector3(UnityEngine.Random.Range(-amp, amp), UnityEngine.Random.Range(-amp, amp) * 0.4f, 0f);
                float pulse = 1f + Mathf.Sin(t * 45f) * 0.03f * k;
                mecha.localScale = new Vector3(baseScale.x * pulse, baseScale.y * pulse, baseScale.z);
            }
            // 눈 번쩍: 과부하 포즈와 정지 포즈를 깜빡 (깜빡임이 점점 빨라짐)
            if (mechaBody != null && deadPose != null)
            {
                bool on = Mathf.FloorToInt(t * flickerRate * (1f + k)) % 2 == 0 || k < 0.2f;
                mechaBody.sprite = on ? overloadPose : deadPose;
            }
            if (overloadFlash != null) SetAlpha(overloadFlash, Mathf.Clamp01(k * 0.6f) * (0.6f + 0.4f * Mathf.Sin(t * 30f)));
            if (t >= nextSpark)
            {
                nextSpark = t + Mathf.Lerp(sparkInterval * 1.6f, sparkInterval * 0.5f, k);
                SpawnSparkAt(RandomPointIn(bounds), order + 4);
            }
            yield return null;
        }
        if (mecha != null) { mecha.position = basePos; mecha.localScale = baseScale; }

        // 2) 정지: 하얗게 번쩍 → 꺼짐
        GameSfx.Play(GameSfxId.MechaPowerDown);
        if (overloadFlash != null) SetAlpha(overloadFlash, 1f);
        if (mechaBody != null && deadPose != null) mechaBody.sprite = deadPose;
        for (int i = 0; i < 4; i++) SpawnSmoke(RandomPointIn(bounds), order + 4);
        yield return Ease(0.35f, k =>
        {
            if (overloadFlash != null) SetAlpha(overloadFlash, 1f - k);
            if (mecha != null) mecha.localScale = new Vector3(baseScale.x * (1f + squash * k), baseScale.y * (1f - squash * k), baseScale.z);
        });
        if (overloadFlash != null) overloadFlash.gameObject.SetActive(false);
        Vector3 deadScale = mecha != null ? mecha.localScale : baseScale;
        float nextSmoke = 0f;
        for (float t = 0f; t < powerDownHold; t += Time.unscaledDeltaTime)
        {
            if (t >= nextSmoke) { nextSmoke = t + 0.28f; SpawnSmoke(RandomPointIn(bounds), order + 4); }
            yield return null;
        }

        // 3) 코어 개방 → 알
        GameSfx.Play(GameSfxId.MechaCoreOpen);
        if (mechaBody != null && coreOpenPose != null) mechaBody.sprite = coreOpenPose;
        Vector3 corePos = coreAnchor != null ? coreAnchor.position : bounds.center;
        if (coreGlow != null)
        {
            coreGlow.gameObject.SetActive(true);
            coreGlow.transform.position = corePos;
            coreGlow.sortingOrder = order + 1;
            SetWorldWidth(coreGlow, coreGlowSize);
            yield return Ease(0.4f, k => SetAlpha(coreGlow, k));
        }
        yield return new WaitForSecondsRealtime(coreOpenHold);

        if (eggPrefab != null && !(skipEggIfOwned && AbilityProgress.HardUnlocked))
        {
            eggPending = true;
            GameSfx.Play(GameSfxId.HardEggPop);
            PlayFrames(corePopSprites, corePos, corePopSize, 14f, order + 5);
            float ground = groundRef != null ? groundRef.position.y : basePos.y;
            var player = FindFirstObjectByType<PlayerController>();
            var egg = Instantiate(eggPrefab);
            egg.SetPopup(unlockPopup);
            egg.Launch(corePos, ground, player != null ? player.transform.position.x : corePos.x + 1f);
            spawned.Add(egg.gameObject);
        }
        yield return new WaitForSecondsRealtime(0.5f);
        if (coreGlow != null) yield return Ease(0.6f, k => SetAlpha(coreGlow, 1f - 0.65f * k));   // 은은하게 남음

        routine = null;
        IsPlaying = false;
        if (restoreCheckpointsAtEnd && RespawnManager.Instance != null) RespawnManager.Instance.UseCheckpoints = true;
        Completed?.Invoke();
        if (!eggPending) ReadyToLeave?.Invoke();
        Finished?.Invoke();
    }

    /// <summary>떡밥만 따로 재생. 문에 들어가는 순간에 부른다. 끝나면 TeaserCompleted.</summary>
    public void PlayTeaser()
    {
        if (teaserRoutine != null) return;
        Cache();
        teaserRoutine = StartCoroutine(TeaserOnly());
    }

    private IEnumerator TeaserOnly()
    {
        IsTeaserPlaying = true;
        if (stageBgm != null) stageBgm.Suppress(0.3f);

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

        // 강화된 닭 실루엣이 어둠 속에서 떠오르고 눈이 번쩍
        GameSfx.Play(GameSfxId.TeaserSting);
        if (silhouette != null)
        {
            silhouette.gameObject.SetActive(true);
            yield return Ease(0.35f, k => SetAlpha(silhouette, k));
        }
        if (teaserEyes != null)
        {
            teaserEyes.gameObject.SetActive(true);
            for (int i = 0; i < 3; i++)
            {
                SetAlpha(teaserEyes, 0.2f); yield return new WaitForSecondsRealtime(0.06f);
                SetAlpha(teaserEyes, 1f); yield return new WaitForSecondsRealtime(0.08f);
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
        yield return new WaitForSecondsRealtime(signHold);

        // 복선: 큰 그림자가 화면 위를 스치고, 검은 깃털 한 장이 천천히 떨어진다 (무엇인지는 알 수 없게)
        if (shadowPass != null)
        {
            shadowPass.gameObject.SetActive(true);
            GameSfx.Play(GameSfxId.ShadowPass);
            var start = new Vector2(-shadowTravel * 0.5f, shadowPass.anchoredPosition.y);
            var end = new Vector2(shadowTravel * 0.5f, shadowPass.anchoredPosition.y);
            // 실루엣/팻말보다 위에 그려지게
            shadowPass.SetAsLastSibling();
            StartCoroutine(FeatherFall(0.5f));
            yield return Ease(shadowTime, k => shadowPass.anchoredPosition = Vector2.Lerp(start, end, k));
            shadowPass.gameObject.SetActive(false);
        }
        else if (blackFeather != null)
        {
            StartCoroutine(FeatherFall(0f));
        }
        yield return new WaitForSecondsRealtime(afterHold + (blackFeather != null ? featherFallTime * 0.4f : 0f));

        IsTeaserPlaying = false;
        teaserRoutine = null;
        TeaserCompleted?.Invoke();
    }

    private IEnumerator FeatherFall(float delay)
    {
        if (blackFeather == null) yield break;
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        blackFeather.gameObject.SetActive(true);
        Vector2 from = blackFeather.anchoredPosition;
        for (float t = 0f; t < featherFallTime; t += Time.unscaledDeltaTime)
        {
            float k = t / featherFallTime;
            blackFeather.anchoredPosition = from + new Vector2(Mathf.Sin(t * 1.7f) * 90f, -featherFallDistance * k);
            blackFeather.localRotation = Quaternion.Euler(0f, 0f, Mathf.Cos(t * 1.7f) * 26f);
            yield return null;
        }
        // 마지막에 어둠 속으로 사라짐 (남겨 두지 않음)
        var img = blackFeather.GetComponent<Image>();
        if (img != null) yield return Ease(0.4f, k => SetAlpha(img, 1f - k));
        blackFeather.gameObject.SetActive(false);
        if (img != null) SetAlpha(img, 1f);
    }

    // ───────────── 이펙트 보조 ─────────────

    private Vector3 RandomPointIn(Bounds b)
        => new Vector3(UnityEngine.Random.Range(b.min.x, b.max.x), UnityEngine.Random.Range(b.min.y + b.size.y * 0.15f, b.max.y), 0f);

    private void SpawnSparkAt(Vector3 pos, int order)
    {
        GameSfx.Play(GameSfxId.SparkZap);
        if (sparkSprites == null || sparkSprites.Length == 0) return;
        var go = new GameObject("Spark");
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = order;
        spawned.Add(go);
        StartCoroutine(SparkRoutine(go, sr));
    }

    private IEnumerator SparkRoutine(GameObject go, SpriteRenderer sr)
    {
        int frames = 3;
        for (int i = 0; i < frames && go != null; i++)
        {
            sr.sprite = sparkSprites[UnityEngine.Random.Range(0, sparkSprites.Length)];
            SetWorldWidth(sr, sparkSize * UnityEngine.Random.Range(0.8f, 1.3f));
            yield return new WaitForSecondsRealtime(0.05f);
        }
        if (go != null) Destroy(go);
    }

    private void SpawnSmoke(Vector3 pos, int order)
    {
        if (smokeSprites == null || smokeSprites.Length == 0) return;
        var go = new GameObject("Smoke");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = order;
        spawned.Add(go);
        StartCoroutine(SmokeRoutine(go.transform, sr));
    }

    private IEnumerator SmokeRoutine(Transform tr, SpriteRenderer sr)
    {
        float life = UnityEngine.Random.Range(1.0f, 1.5f);
        float size = smokeSize * UnityEngine.Random.Range(0.8f, 1.2f);
        float drift = UnityEngine.Random.Range(-0.5f, 0.5f);
        for (float t = 0f; t < life && tr != null; t += Time.unscaledDeltaTime)
        {
            float k = t / life;
            sr.sprite = smokeSprites[Mathf.Min(smokeSprites.Length - 1, Mathf.FloorToInt(k * smokeSprites.Length))];
            tr.position += new Vector3(drift, 1.4f, 0f) * Time.unscaledDeltaTime;
            SetWorldWidth(sr, size * (0.6f + k * 0.8f));
            sr.color = new Color(1f, 1f, 1f, 1f - k * k);
            yield return null;
        }
        if (tr != null) Destroy(tr.gameObject);
    }

    private void PlayFrames(Sprite[] frames, Vector3 pos, float width, float fps, int order)
    {
        if (frames == null || frames.Length == 0) return;
        var go = new GameObject("Fx");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = order;
        spawned.Add(go);
        StartCoroutine(FramesRoutine(go, sr, frames, width, fps));
    }

    private IEnumerator FramesRoutine(GameObject go, SpriteRenderer sr, Sprite[] frames, float width, float fps)
    {
        foreach (var f in frames)
        {
            if (go == null) yield break;
            sr.sprite = f; SetWorldWidth(sr, width);
            yield return new WaitForSecondsRealtime(1f / fps);
        }
        if (go != null) Destroy(go);
    }

    // ───────────── 공통 보조 ─────────────

    private static void SetWorldWidth(SpriteRenderer sr, float worldWidth)
    {
        if (sr == null || sr.sprite == null) return;
        float w = sr.sprite.bounds.size.x;
        float s = w > 0f ? worldWidth / w : 1f;
        sr.transform.localScale = new Vector3(s, s, 1f);
    }

    private static void SetAlpha(SpriteRenderer sr, float a) { var c = sr.color; c.a = a; sr.color = c; }
    private static void SetAlpha(Image img, float a) { var c = img.color; c.a = a; img.color = c; }

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