using System.Collections;
using UnityEngine;

/// <summary>
/// 스테이지 배경음악. 게임 모드가 미니게임으로 바뀌면 어두운 곡으로, 끝나면 원래 곡으로 크로스페이드한다.
/// 씬에 하나만 두고 AudioSource는 스크립트가 직접 만든다(오브젝트에 AudioSource를 붙일 필요 없음).
/// 기존에 BGM을 재생하던 오브젝트가 있다면 그 AudioSource는 꺼 두거나 지워서 겹치지 않게 한다.
/// </summary>
public class StageBgm : MonoBehaviour
{
    [SerializeField] private GameModeController gameMode;   // 비우면 씬에서 자동으로 찾음

    [Header("곡")]
    [SerializeField] private AudioClip normalClip;
    [SerializeField] private AudioClip miniGameClip;
    [Range(0f, 1f)][SerializeField] private float normalVolume = 0.6f;
    [Range(0f, 1f)][SerializeField] private float miniGameVolume = 0.7f;

    [Header("전환")]
    [SerializeField] private float fadeTime = 1.2f;
    [Tooltip("켜면 미니게임이 끝나 원래 곡으로 돌아올 때 처음부터가 아니라 멈췄던 곳에서 이어서 재생")]
    [SerializeField] private bool resumeNormalPosition = true;

    private AudioSource a, b;           // 번갈아 쓰는 두 개
    private AudioSource current;
    private AudioClip currentClip;
    private float savedNormalTime;
    private Coroutine fadeRoutine;
    private bool suppressed;

    private void Awake()
    {
        SceneBgm.StopMenuBgm();   // 챕터 선택에서 이어진 메뉴 BGM이 남아 있으면 끈다 (겹침 방지)
        a = CreateSource("BgmA"); b = CreateSource("BgmB");
        if (gameMode == null) gameMode = FindFirstObjectByType<GameModeController>();
    }

    private void OnEnable()
    {
        if (gameMode != null) gameMode.ModeChanged += OnModeChanged;
    }

    private void OnDisable()
    {
        if (gameMode != null) gameMode.ModeChanged -= OnModeChanged;
    }

    private void Start()
    {
        // 모드 이벤트는 SetMode가 Start에서 불리므로 놓쳤을 수 있어 현재 모드로 한 번 맞춘다
        Apply(gameMode != null ? gameMode.CurrentMode : GameMode.Platforming, instant: true);
    }

    private void OnModeChanged(GameMode prev, GameMode next) => Apply(next, instant: false);

    private void Apply(GameMode mode, bool instant)
    {
        if (mode == GameMode.MiniGame) Play(miniGameClip, miniGameVolume, 0f, instant);
        else if (mode == GameMode.Platforming || mode == GameMode.BossFight)
            Play(normalClip, normalVolume, resumeNormalPosition && currentClip != normalClip ? savedNormalTime : 0f, instant);
        // Title/Result 등은 그대로 둔다
    }

    private void Play(AudioClip clip, float volume, float startTime, bool instant, float fade = -1f)
    {
        if (suppressed || clip == null || clip == currentClip) return;

        // 일반 곡의 재생 위치를 기억해 둔다
        if (current != null && currentClip == normalClip) savedNormalTime = current.time;

        var next = current == a ? b : a;
        next.clip = clip;
        next.volume = 0f;
        next.time = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, clip.length - 0.1f));
        next.Play();

        var prev = current;
        current = next;
        currentClip = clip;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(Crossfade(prev, next, volume, instant ? 0f : (fade < 0f ? fadeTime : fade)));
    }

    private IEnumerator Crossfade(AudioSource from, AudioSource to, float toVolume, float time)
    {
        float fromStart = from != null ? from.volume : 0f;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float k = t / time;
            to.volume = Mathf.Lerp(0f, toVolume, k);
            if (from != null) from.volume = Mathf.Lerp(fromStart, 0f, k);
            yield return null;
        }
        to.volume = toVolume;
        if (from != null) { from.volume = 0f; from.Stop(); }
        fadeRoutine = null;
    }

    // ───────────── 연출용 ─────────────

    /// <summary>BGM을 서서히 끄고, Release()를 부르기 전까지는 모드가 바뀌어도 다시 켜지지 않게 한다 (보스 등장 연출 등)</summary>
    public void Suppress(float fade = 0.3f)
    {
        if (current != null && currentClip == normalClip) savedNormalTime = current.time;
        suppressed = true;
        currentClip = null;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeOutAll(fade));
    }

    /// <summary>
    /// Suppress를 풀고 BGM을 다시 켠다. overrideClip을 주면 그 곡(예: 닭 전투 BGM)을, 비우면 현재 게임 모드에 맞는 원래 곡을 튼다.
    /// </summary>
    public void Release(AudioClip overrideClip = null, float overrideVolume = 0.7f, float fade = 0.6f)
    {
        suppressed = false;
        if (overrideClip != null) Play(overrideClip, overrideVolume, 0f, false, fade);
        else Apply(gameMode != null ? gameMode.CurrentMode : GameMode.Platforming, instant: false);
    }

    private IEnumerator FadeOutAll(float time)
    {
        float va = a.volume, vb = b.volume;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float k = t / time;
            a.volume = Mathf.Lerp(va, 0f, k);
            b.volume = Mathf.Lerp(vb, 0f, k);
            yield return null;
        }
        a.Stop(); b.Stop();
        a.volume = b.volume = 0f;
        fadeRoutine = null;
    }

    private AudioSource CreateSource(string childName)
    {
        var go = new GameObject(childName);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.loop = true;
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        return s;
    }
}