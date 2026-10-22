using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 닭 미니게임 진입 연출을 한 번에 지휘한다.
///   1) 닭 등장 (위에서 쿵 떨어짐 + 착지 흔들림)
///   2) 닭 포효 (ChickenRoarOverlay: 집중선/파동/의성어/먼지 + 소리)
///   3) "뻥튀기 VS 닭" 배너가 위에서 내려와 잠깐 보이다가 위로 사라짐
///   4) 끝 → Completed 이벤트 (여기에 분노 게이지 시작, BGM 재생, 입력 풀기를 연결)
/// 연출 중에는 Started 이벤트로 플레이어 입력을 잠그고, Completed에서 풀면 된다.
/// 사용: 미니게임이 시작될 때 Play() 한 번. 연출 중 Skip()을 부르면 바로 마지막 상태로 넘어간다.
/// </summary>
public class ChickenIntroSequence : MonoBehaviour, IMiniGameIntro
{
    [Header("닭")]
    [SerializeField] private Transform chicken;
    [Tooltip("닭이 최종적으로 서는 자리 기준, 시작 위치의 위쪽 거리(월드 단위)")]
    [SerializeField] private float dropHeight = 9f;
    [SerializeField] private float dropTime = 0.55f;
    [Tooltip("착지 후 포효 전까지 숨 고르는 시간")]
    [SerializeField] private float pauseBeforeRoar = 0.35f;
    [Tooltip("착지 때 화면 흔들림")]
    [SerializeField] private float landShakeTime = 0.3f;
    [SerializeField] private float landShakeDistance = 0.18f;

    [Header("포효")]
    [SerializeField] private ChickenRoarOverlay roar;
    [Tooltip("포효 연출이 끝난 뒤 배너가 나오기까지 기다리는 시간")]
    [SerializeField] private float pauseAfterRoar = 0.15f;

    [Header("배너 (뻥튀기 VS 닭)")]
    [Tooltip("Canvas 아래 Image(banner_vs.png). 앵커는 위쪽 가운데 권장")]
    [SerializeField] private RectTransform banner;
    [SerializeField] private float bannerStartOffsetY = 500f;   // 화면 위로 이만큼 벗어난 곳에서 시작
    [SerializeField] private float bannerDropTime = 0.45f;
    [SerializeField] private float bannerHoldTime = 1.4f;
    [SerializeField] private float bannerLeaveTime = 0.35f;

    [Header("BGM")]
    [Tooltip("비우면 씬에서 자동으로 찾음. 연출이 시작되면 이 BGM이 꺼진다")]
    [SerializeField] private StageBgm stageBgm;
    [SerializeField] private float bgmFadeOutTime = 0.25f;
    [Tooltip("연출이 끝난 뒤 틀 닭 전투 BGM (bgm_minigame_chicken). 비우면 원래 StageBgm 규칙대로 돌아감")]
    [SerializeField] private AudioClip fightBgm;
    [Range(0f, 1f)] [SerializeField] private float fightBgmVolume = 0.7f;
    [SerializeField] private float bgmFadeInTime = 0.6f;

    [Header("재시작")]
    [Tooltip("켜면 연출이 시작될 때 RespawnManager를 '미니게임 모드'로 바꿈 → 죽으면 체크포인트 대신 처음부터(게이지/패턴/회복템 초기화). StageMiniGame 없이 이 연출만으로 미니게임을 시작할 때 필요")]
    [SerializeField] private bool useMiniGameRespawn = true;

    [Tooltip("죽어서 씬이 다시 로드된 경우 등장/포효/배너 연출을 건너뛰고 바로 시작")]
    [SerializeField] private bool skipIntroAfterDeathReload = true;

    [Header("이벤트")]
    [Tooltip("연출 시작 (플레이어 입력 잠그기 등)")]
    public UnityEvent Started;
    [Tooltip("연출 끝 (분노 게이지 Run, BGM 재생, 입력 풀기 등)")]
    public UnityEvent Completed;

    public bool IsPlaying { get; private set; }
    public event Action Finished;

    private Coroutine routine;
    private Vector3 chickenFinalPos;
    private Vector2 bannerFinalPos;
    private bool cached;

    private void Awake()
    {
        if (stageBgm == null) stageBgm = FindFirstObjectByType<StageBgm>();
        Cache();
        HideAll();
    }

    private void Cache()
    {
        if (cached) return;
        cached = true;
        if (chicken != null) chickenFinalPos = chicken.position;
        if (banner != null) bannerFinalPos = banner.anchoredPosition;
    }

    private void HideAll()
    {
        if (chicken != null) chicken.gameObject.SetActive(false);
        if (banner != null) banner.gameObject.SetActive(false);
        if (roar != null) roar.Stop();
    }

    // ───────────── 외부 호출 ─────────────

    public void Play()
    {
        Cache();
        if (skipIntroAfterDeathReload && RespawnManager.ConsumeReloadedByDeath())
        {
            IsPlaying = true;
            if (stageBgm != null) stageBgm.Suppress(0f);
            Started?.Invoke();
            if (chicken != null) { chicken.gameObject.SetActive(true); chicken.position = chickenFinalPos; }
            Finish();
            return;
        }
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Sequence());
    }

    /// <summary>연출을 건너뛰고 닭이 서 있는 최종 상태로</summary>
    public void Skip()
    {
        if (!IsPlaying) return;
        if (routine != null) StopCoroutine(routine);
        routine = null;
        if (roar != null) roar.Stop();
        if (chicken != null) { chicken.gameObject.SetActive(true); chicken.position = chickenFinalPos; }
        if (banner != null) banner.gameObject.SetActive(false);
        Finish();
    }

    /// <summary>미니게임을 처음부터 다시 시작할 때: 닭은 그대로 서 있고(연출 없이) 바로 시작하고 싶다면 이걸 사용</summary>
    public void ShowChickenInstantly()
    {
        Cache();
        if (routine != null) StopCoroutine(routine);
        routine = null;
        IsPlaying = false;
        if (roar != null) roar.Stop();
        if (banner != null) banner.gameObject.SetActive(false);
        if (chicken != null) { chicken.gameObject.SetActive(true); chicken.position = chickenFinalPos; }
    }

    // ───────────── 연출 ─────────────

    private IEnumerator Sequence()
    {
        IsPlaying = true;
      

        if (useMiniGameRespawn && RespawnManager.Instance != null) RespawnManager.Instance.UseCheckpoints = false;
        if (stageBgm != null) stageBgm.Suppress(bgmFadeOutTime);   // 닭이 나타나면 BGM 정지
        Started?.Invoke();

        // 1) 닭 등장
        if (chicken != null)
        {
            chicken.gameObject.SetActive(true);
            Vector3 from = chickenFinalPos + Vector3.up * dropHeight;
            chicken.position = from;
            GameSfx.Play(GameSfxId.ChickenWingFlap);

            for (float t = 0f; t < dropTime; t += Time.unscaledDeltaTime)
            {
                float k = t / dropTime;
                chicken.position = Vector3.LerpUnclamped(from, chickenFinalPos, k * k);   // 가속하며 낙하
                yield return null;
            }
            chicken.position = chickenFinalPos;
            GameSfx.Play(GameSfxId.ChickenPeckHit);   // 쿵
            yield return Shake(landShakeTime, landShakeDistance);
        }
        if (pauseBeforeRoar > 0f) yield return new WaitForSecondsRealtime(pauseBeforeRoar);

        // 2) 포효 (오버레이가 끝날 때까지)
        if (roar != null)
        {
            bool done = false;
            Action onDone = () => done = true;
            roar.Finished += onDone;
            roar.Play();
            while (!done) yield return null;
            roar.Finished -= onDone;
        }
        if (pauseAfterRoar > 0f) yield return new WaitForSecondsRealtime(pauseAfterRoar);

        // 3) 배너 등장 → 유지 → 사라짐
        if (banner != null)
        {
            banner.gameObject.SetActive(true);
            Vector2 up = bannerFinalPos + Vector2.up * bannerStartOffsetY;
            banner.anchoredPosition = up;

            for (float t = 0f; t < bannerDropTime; t += Time.unscaledDeltaTime)
            {
                banner.anchoredPosition = Vector2.LerpUnclamped(up, bannerFinalPos, EaseOutBack(t / bannerDropTime));
                yield return null;
            }
            banner.anchoredPosition = bannerFinalPos;

            yield return new WaitForSecondsRealtime(bannerHoldTime);

            for (float t = 0f; t < bannerLeaveTime; t += Time.unscaledDeltaTime)
            {
                float k = t / bannerLeaveTime;
                banner.anchoredPosition = Vector2.LerpUnclamped(bannerFinalPos, up, k * k);
                yield return null;
            }
            banner.gameObject.SetActive(false);
        }

        routine = null;
        Finish();
    }

    private void Finish()
    {
        IsPlaying = false;
        if (stageBgm != null) stageBgm.Release(fightBgm, fightBgmVolume, bgmFadeInTime);   // 전투 BGM 시작
        Completed?.Invoke();
        Finished?.Invoke();
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

    // 테스트용 (⋮ 메뉴)
    [ContextMenu("Debug/Play")] private void DebugPlay() => Play();
    [ContextMenu("Debug/Skip")] private void DebugSkip() => Skip();
}
