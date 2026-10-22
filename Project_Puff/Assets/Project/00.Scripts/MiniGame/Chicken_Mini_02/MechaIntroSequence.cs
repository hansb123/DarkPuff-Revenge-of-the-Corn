using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 메카 닭 미니게임 진입 연출 ("정전 → 붉은 눈 → 부팅").
///   1) 정전      : 화면이 어두워지고 BGM이 끊김 (전원 내려가는 소리)
///   2) 붉은 눈   : 어둠 속에서 눈만 켜지고 경보음이 두 번 울림
///   3) 부팅      : 조명이 깜빡이며 켜지고 메카 닭이 드러남 (쿵 + 화면 흔들림)
///   4) 날개 펼침 : 유압 소리와 함께 날개를 활짝 펴고 포효 오버레이(선택)
///   5) 배너      : "뻥튀기 VS 메카 닭" + "메카 닭의 공격을 75초간 피해라!"가 같이 내려왔다가 올라감
///   6) 끝 → Completed 이벤트 (분노 게이지 Run(75), PatternRunner Begin, 입력 풀기를 연결)
/// ChickenIntroSequence와 같은 방식으로 쓴다: MiniGameStartTrigger의 대상으로 연결하거나 Play()를 직접 호출.
/// 죽어서 씬이 다시 로드된 경우 RespawnManager.ConsumeReloadedByDeath()를 읽어 연출을 건너뛴다.
/// </summary>
public class MechaIntroSequence : MonoBehaviour, IMiniGameIntro
{
    [Header("메카 닭")]
    [SerializeField] private Transform mecha;
    [SerializeField] private SpriteRenderer mechaBody;
    [Tooltip("mecha_idle")]
    [SerializeField] private Sprite idlePose;
    [Tooltip("mecha_wing_spread")]
    [SerializeField] private Sprite spreadPose;

    [Header("정전 / 눈 (Canvas UI)")]
    [Tooltip("화면 전체를 덮는 검은 Image. 메카 닭보다 위, 눈/배너보다 아래에 둠")]
    [SerializeField] private Image blackout;
    [Tooltip("intro_eyes_glow Image. 메카 닭 눈 위치에 맞게 놓고 크기를 조절해 둠")]
    [SerializeField] private Image glowEyes;
    [SerializeField] private float blackoutFadeTime = 0.25f;
    [SerializeField] private float darkHold = 0.6f;
    [SerializeField] private float eyesFadeTime = 0.6f;
    [SerializeField] private float eyesHold = 0.7f;

    [Header("부팅")]
    [Tooltip("조명이 깜빡이는 정도. 값은 blackout의 알파(1=완전 어둠). 마지막은 0")]
    [SerializeField] private float[] flickerAlpha = { 0.55f, 1f, 0.25f, 0.9f, 0.1f, 0f };
    [SerializeField] private float flickerStep = 0.09f;
    [SerializeField] private float bootShakeTime = 0.35f;
    [SerializeField] private float bootShakeDistance = 0.18f;
    [Tooltip("부팅 직후 숨 고르는 시간")]
    [SerializeField] private float pauseBeforeSpread = 0.4f;

    [Header("날개 펼침")]
    [SerializeField] private ChickenRoarOverlay roar;      // 선택. 닭 때 쓰던 포효 오버레이 재활용
    [SerializeField] private float spreadHold = 0.8f;
    [SerializeField] private float pauseAfterSpread = 0.2f;

    [Header("배너")]
    [Tooltip("banner_vs_mecha (앵커 위쪽 가운데 권장)")]
    [SerializeField] private RectTransform bannerVs;
    [Tooltip("banner_survive_mecha. 비워도 됨")]
    [SerializeField] private RectTransform bannerSurvive;
    [SerializeField] private float bannerStartOffsetY = 600f;
    [SerializeField] private float bannerDropTime = 0.45f;
    [Tooltip("두 번째 배너가 첫 배너보다 늦게 내려오는 시간")]
    [SerializeField] private float surviveDelay = 0.15f;
    [SerializeField] private float bannerHoldTime = 1.5f;
    [SerializeField] private float bannerLeaveTime = 0.35f;

    [Header("BGM")]
    [SerializeField] private StageBgm stageBgm;
    [SerializeField] private float bgmFadeOutTime = 0.2f;
    [Tooltip("연출이 끝난 뒤 틀 전투 BGM (bgm_minigame_mecha)")]
    [SerializeField] private AudioClip fightBgm;
    [Range(0f, 1f)] [SerializeField] private float fightBgmVolume = 0.7f;
    [SerializeField] private float bgmFadeInTime = 0.6f;

    [Header("재시작")]
    [Tooltip("연출이 시작될 때 RespawnManager를 미니게임 모드로 바꿈 (죽으면 처음부터)")]
    [SerializeField] private bool useMiniGameRespawn = true;
    [Tooltip("죽어서 씬이 다시 로드된 경우 연출을 건너뛰고 바로 시작")]
    [SerializeField] private bool skipIntroAfterDeathReload = true;

    [Header("이벤트")]
    [Tooltip("연출 시작 (플레이어 입력 잠그기 등)")]
    public UnityEvent Started;
    [Tooltip("연출 끝 (분노 게이지 Run(75), PatternRunner Begin, 입력 풀기)")]
    public UnityEvent Completed;

    public bool IsPlaying { get; private set; }
    public event Action Finished;

    private Coroutine routine;
    private Vector2 vsPos, survivePos;
    private bool cached;

    private void Awake()
    {
        if (stageBgm == null) stageBgm = FindFirstObjectByType<StageBgm>();
        if (mechaBody == null && mecha != null) mechaBody = mecha.GetComponentInChildren<SpriteRenderer>();
        Cache();
        HideAll();
    }

    private void Cache()
    {
        if (cached) return;
        cached = true;
        if (bannerVs != null) vsPos = bannerVs.anchoredPosition;
        if (bannerSurvive != null) survivePos = bannerSurvive.anchoredPosition;
    }

    private void HideAll()
    {
        if (mecha != null) mecha.gameObject.SetActive(false);
        if (blackout != null) { SetAlpha(blackout, 0f); blackout.gameObject.SetActive(false); }
        if (glowEyes != null) { SetAlpha(glowEyes, 0f); glowEyes.gameObject.SetActive(false); }
        if (bannerVs != null) bannerVs.gameObject.SetActive(false);
        if (bannerSurvive != null) bannerSurvive.gameObject.SetActive(false);
        if (roar != null) roar.Stop();
    }

    // ───────────── 외부 호출 ─────────────

    public void Play()
    {
        Cache();
        if (routine != null) StopCoroutine(routine);

        if (skipIntroAfterDeathReload && RespawnManager.ConsumeReloadedByDeath())
        {
            IsPlaying = true;
            if (useMiniGameRespawn && RespawnManager.Instance != null) RespawnManager.Instance.UseCheckpoints = false;
            if (stageBgm != null) stageBgm.Suppress(0f);
            Started?.Invoke();
            ShowMechaInstantly();
            Finish();
            return;
        }
        routine = StartCoroutine(Sequence());
    }

    /// <summary>연출을 건너뛰고 메카 닭이 서 있는 상태로</summary>
    public void Skip()
    {
        if (!IsPlaying) return;
        if (routine != null) StopCoroutine(routine);
        routine = null;
        HideAll();
        ShowMechaInstantly();
        Finish();
    }

    public void ShowMechaInstantly()
    {
        Cache();
        if (roar != null) roar.Stop();
        if (blackout != null) { SetAlpha(blackout, 0f); blackout.gameObject.SetActive(false); }
        if (glowEyes != null) glowEyes.gameObject.SetActive(false);
        if (bannerVs != null) bannerVs.gameObject.SetActive(false);
        if (bannerSurvive != null) bannerSurvive.gameObject.SetActive(false);
        if (mecha != null) mecha.gameObject.SetActive(true);
        SetPose(idlePose);
    }

    // ───────────── 연출 ─────────────

    private IEnumerator Sequence()
    {
        IsPlaying = true;
        HideAll();
        if (useMiniGameRespawn && RespawnManager.Instance != null) RespawnManager.Instance.UseCheckpoints = false;
        if (stageBgm != null) stageBgm.Suppress(bgmFadeOutTime);
        Started?.Invoke();

        // 메카 닭은 이미 서 있지만 어둠에 가려져 안 보인다
        if (mecha != null) { mecha.gameObject.SetActive(true); SetPose(idlePose); }

        // 1) 정전
        GameSfx.Play(GameSfxId.LaserEnd);   // 전원 내려가는 소리 (하강음)
        if (blackout != null)
        {
            blackout.gameObject.SetActive(true);
            yield return Ease(blackoutFadeTime, k => SetAlpha(blackout, k));
        }
        yield return new WaitForSecondsRealtime(darkHold);

        // 2) 붉은 눈 점등 + 경보
        if (glowEyes != null)
        {
            glowEyes.gameObject.SetActive(true);
            GameSfx.Play(GameSfxId.LaserLock);
            yield return Ease(eyesFadeTime, k => SetAlpha(glowEyes, k * k));
        }
        GameSfx.Play(GameSfxId.MechaAlarm);
        yield return new WaitForSecondsRealtime(eyesHold);

        // 3) 부팅: 조명 깜빡이며 켜짐
        GameSfx.Play(GameSfxId.MechaIntro);
        if (blackout != null && flickerAlpha != null)
        {
            foreach (float a in flickerAlpha)
            {
                SetAlpha(blackout, a);
                yield return new WaitForSecondsRealtime(flickerStep);
            }
        }
        if (blackout != null) { SetAlpha(blackout, 0f); blackout.gameObject.SetActive(false); }
        if (glowEyes != null) glowEyes.gameObject.SetActive(false);
        yield return Shake(bootShakeTime, bootShakeDistance);
        if (pauseBeforeSpread > 0f) yield return new WaitForSecondsRealtime(pauseBeforeSpread);

        // 4) 날개 펼침 (+ 포효 오버레이)
        SetPose(spreadPose);
        GameSfx.Play(GameSfxId.MechaWingSpread);
        if (roar != null)
        {
            bool done = false;
            Action onDone = () => done = true;
            roar.Finished += onDone;
            roar.Play();
            float guard = 0f;
            while (!done && guard < 3f) { guard += Time.unscaledDeltaTime; yield return null; }
            roar.Finished -= onDone;
        }
        else
        {
            yield return Shake(0.25f, 0.12f);
        }
        yield return new WaitForSecondsRealtime(spreadHold);
        SetPose(idlePose);
        if (pauseAfterSpread > 0f) yield return new WaitForSecondsRealtime(pauseAfterSpread);

        // 5) 배너 (두 장이 같이 내려옴)
        if (bannerVs != null || bannerSurvive != null)
        {
            Vector2 upVs = vsPos + Vector2.up * bannerStartOffsetY;
            Vector2 upSv = survivePos + Vector2.up * bannerStartOffsetY;
            if (bannerVs != null) { bannerVs.gameObject.SetActive(true); bannerVs.anchoredPosition = upVs; }
            if (bannerSurvive != null) { bannerSurvive.gameObject.SetActive(true); bannerSurvive.anchoredPosition = upSv; }

            float total = bannerDropTime + surviveDelay;
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                if (bannerVs != null) bannerVs.anchoredPosition = Vector2.LerpUnclamped(upVs, vsPos, EaseOutBack(t / bannerDropTime));
                if (bannerSurvive != null) bannerSurvive.anchoredPosition = Vector2.LerpUnclamped(upSv, survivePos, EaseOutBack((t - surviveDelay) / bannerDropTime));
                yield return null;
            }
            if (bannerVs != null) bannerVs.anchoredPosition = vsPos;
            if (bannerSurvive != null) bannerSurvive.anchoredPosition = survivePos;

            yield return new WaitForSecondsRealtime(bannerHoldTime);

            for (float t = 0f; t < bannerLeaveTime; t += Time.unscaledDeltaTime)
            {
                float k = t / bannerLeaveTime;
                if (bannerVs != null) bannerVs.anchoredPosition = Vector2.LerpUnclamped(vsPos, upVs, k * k);
                if (bannerSurvive != null) bannerSurvive.anchoredPosition = Vector2.LerpUnclamped(survivePos, upSv, k * k);
                yield return null;
            }
            if (bannerVs != null) bannerVs.gameObject.SetActive(false);
            if (bannerSurvive != null) bannerSurvive.gameObject.SetActive(false);
        }

        routine = null;
        Finish();
    }

    private void Finish()
    {
        IsPlaying = false;
        if (stageBgm != null) stageBgm.Release(fightBgm, fightBgmVolume, bgmFadeInTime);
        Completed?.Invoke();
        Finished?.Invoke();
    }

    // ───────────── 보조 ─────────────

    private void SetPose(Sprite s) { if (mechaBody != null && s != null) mechaBody.sprite = s; }

    private static IEnumerator Ease(float time, Action<float> apply)
    {
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime) { apply(Mathf.Clamp01(t / time)); yield return null; }
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

    private static void SetAlpha(Image img, float a) { var c = img.color; c.a = a; img.color = c; }

    private static float EaseOutBack(float x)
    {
        x = Mathf.Clamp01(x);
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    [ContextMenu("Debug/Play")] private void DebugPlay() => Play();
    [ContextMenu("Debug/Skip")] private void DebugSkip() => Skip();
}
