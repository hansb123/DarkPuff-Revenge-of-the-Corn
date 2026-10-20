using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 닭 미니게임의 "분노 게이지" HUD. 게이지가 차오를수록 닭 얼굴이 차분 → 화남 → 폭발 직전으로 바뀌고 점점 떨린다.
/// 게이지 = 생존 진행도. 다 차면(= 버티기 성공) Completed 이벤트가 발생한다.
///
/// 사용법
///  - 미니게임 시작 때 Run(duration) 을 호출 (예: 60~90초). 스스로 시간에 맞춰 차오른다.
///  - 직접 제어하고 싶으면 SetProgress(0~1) 를 매 프레임 호출 (이때는 Run을 쓰지 않음).
///  - 죽어서 처음부터 다시 시작하면 ResetGauge() (resetOnRestart 를 켜 두면 RespawnManager가 알아서 호출).
///
/// UI 구성 (Canvas 아래)
///  Root(이 스크립트) ─ BarBack(bar_back.png) ─ Fill(bar_fill.png, Image Type=Filled/Horizontal/Left)
///                     ├ Frame(bar_frame.png) ─ Icon(icon_calm.png) ─ (선택) Label(label.png)
/// </summary>
public class AngerGaugeHud : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private Image fill;
    [Tooltip("닭 얼굴 아이콘. 단계별로 스프라이트가 바뀌고 떨림")]
    [SerializeField] private Image icon;
    [SerializeField] private Sprite calmSprite;
    [SerializeField] private Sprite angrySprite;
    [SerializeField] private Sprite furiousSprite;

    [Header("단계 (게이지 비율)")]
    [Range(0f, 1f)][SerializeField] private float angryAt = 0.34f;
    [Range(0f, 1f)][SerializeField] private float furiousAt = 0.67f;

    [Header("연출")]
    [Tooltip("채워지는 모습을 부드럽게 따라가는 속도")]
    [SerializeField] private float smooth = 12f;
    [Tooltip("단계가 오를 때 아이콘이 커졌다 돌아오는 시간 / 크기")]
    [SerializeField] private float popTime = 0.25f;
    [SerializeField] private float popScale = 1.35f;
    [Tooltip("아이콘 떨림 세기 (픽셀). 단계별로 [차분, 화남, 폭발 직전]")]
    [SerializeField] private float[] shakeByStage = { 0f, 2.5f, 6f };
    [Tooltip("가득 찼을 때 게이지 전체가 흔들리는 시간 / 세기")]
    [SerializeField] private float fullShakeTime = 0.6f;
    [SerializeField] private float fullShakeDistance = 10f;

    [Header("소리")]
    [SerializeField] private bool playSfx = true;

    [Header("재시작")]
    [Tooltip("미니게임에서 죽어 처음부터 다시 시작할 때 게이지도 0으로 되돌림 (체크포인트를 안 쓰는 모드에서만)")]
    [SerializeField] private bool resetOnRestart = true;

    [Header("이벤트")]
    [Tooltip("게이지가 가득 찼을 때 (미니게임 클리어 처리 연결)")]
    public UnityEvent Completed;

    /// <summary>단계가 바뀔 때 (0 차분, 1 화남, 2 폭발 직전)</summary>
    public event Action<int> StageChanged;

    public float Progress => target;
    public int Stage => stage;
    public bool IsFull => target >= 1f;
    public bool IsRunning => running;

    private float target;      // 실제 값
    private float shown;       // 화면에 보이는 값
    private int stage;
    private bool running;
    private bool completedFired;
    private Coroutine runRoutine;
    private Coroutine popRoutine;
    private Coroutine fullShakeRoutine;
    private Vector2 iconBase;
    private Vector2 rootBase;
    private Vector3 iconBaseScale = Vector3.one;
    private bool subscribed;

    private RectTransform Rt => (RectTransform)transform;

    private void Awake()
    {
        if (icon != null) { iconBase = icon.rectTransform.anchoredPosition; iconBaseScale = icon.rectTransform.localScale; }
        rootBase = Rt.anchoredPosition;
        Apply(0f, true);
    }

    private void OnEnable()
    {
        if (resetOnRestart && RespawnManager.Instance != null && !subscribed)
        {
            RespawnManager.Instance.RespawnApplied += OnRestarted;
            subscribed = true;
        }
    }

    private void OnDisable()
    {
        if (subscribed && RespawnManager.Instance != null) RespawnManager.Instance.RespawnApplied -= OnRestarted;
        subscribed = false;
    }

    private void OnRestarted()
    {
        if (!resetOnRestart) return;
        if (RespawnManager.Instance != null && RespawnManager.Instance.UseCheckpoints) return;   // 미니게임 중에만
        ResetGauge();
    }

    // ───────────── 외부 호출 ─────────────

    /// <summary>duration초 동안 0 → 1로 차오름. 다시 부르면 처음부터</summary>
    public void Run(float duration)
    {
        ResetGauge();
        running = true;
        runRoutine = StartCoroutine(RunRoutine(Mathf.Max(0.1f, duration)));
    }

    /// <summary>시간 진행을 멈춤 (값은 유지)</summary>
    public void Pause() { running = false; if (runRoutine != null) { StopCoroutine(runRoutine); runRoutine = null; } }

    public void ResetGauge()
    {
        Pause();
        completedFired = false;
        target = shown = 0f;
        stage = 0;
        Apply(0f, true);
    }

    /// <summary>직접 값을 넣고 싶을 때 (0~1)</summary>
    public void SetProgress(float value)
    {
        target = Mathf.Clamp01(value);
        UpdateStage();
        if (target >= 1f) FireCompleted();
    }

    // ───────────── 내부 ─────────────

    private IEnumerator RunRoutine(float duration)
    {
        // 일시정지(timeScale 0) 중에는 시간이 가지 않음
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            SetProgress(t / duration);
            yield return null;
        }
        SetProgress(1f);
        running = false;
        runRoutine = null;
    }

    private void Update()
    {
        shown = Mathf.Lerp(shown, target, 1f - Mathf.Exp(-smooth * Time.unscaledDeltaTime));
        if (Mathf.Abs(shown - target) < 0.001f) shown = target;
        Apply(shown, false);

#if UNITY_EDITOR
        // 테스트용(에디터 전용): F5=리셋, F6=40%, F7=70%, F8=100%
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.f6Key.wasPressedThisFrame) SetProgress(0.4f);
            if (kb.f7Key.wasPressedThisFrame) SetProgress(0.7f);
            if (kb.f8Key.wasPressedThisFrame) SetProgress(1f);
            if (kb.f5Key.wasPressedThisFrame) ResetGauge();
        }
#endif
    }

    private void Apply(float value, bool instant)
    {
        if (fill != null) fill.fillAmount = value;

        if (icon != null && popRoutine == null)
        {
            float s = (stage >= 0 && stage < shakeByStage.Length) ? shakeByStage[stage] : 0f;
            Vector2 jitter = s > 0f
                ? new Vector2(Mathf.Sin(Time.unscaledTime * 61f), Mathf.Cos(Time.unscaledTime * 53f)) * s
                : Vector2.zero;
            icon.rectTransform.anchoredPosition = iconBase + jitter;
        }
    }

    private void UpdateStage()
    {
        int s = target >= furiousAt ? 2 : target >= angryAt ? 1 : 0;
        if (s == stage) return;

        bool up = s > stage;
        stage = s;
        if (icon != null)
        {
            icon.sprite = stage == 2 ? (furiousSprite != null ? furiousSprite : icon.sprite)
                        : stage == 1 ? (angrySprite != null ? angrySprite : icon.sprite)
                        : (calmSprite != null ? calmSprite : icon.sprite);
        }
        StageChanged?.Invoke(stage);

        if (up)
        {
            if (playSfx) GameSfx.Play(GameSfxId.AngerTick);
            if (icon != null)
            {
                if (popRoutine != null) StopCoroutine(popRoutine);
                popRoutine = StartCoroutine(PopRoutine());
            }
        }
    }

    private IEnumerator PopRoutine()
    {
        var rt = icon.rectTransform;
        for (float t = 0f; t < popTime; t += Time.unscaledDeltaTime)
        {
            float k = t / popTime;
            float s = Mathf.Lerp(popScale, 1f, k * k);
            rt.localScale = iconBaseScale * s;
            yield return null;
        }
        rt.localScale = iconBaseScale;
        popRoutine = null;
    }

    private void FireCompleted()
    {
        if (completedFired) return;
        completedFired = true;

        if (playSfx) GameSfx.Play(GameSfxId.AngerFull);
        if (fullShakeRoutine != null) StopCoroutine(fullShakeRoutine);
        fullShakeRoutine = StartCoroutine(FullShakeRoutine());
        Completed?.Invoke();
    }

    private IEnumerator FullShakeRoutine()
    {
        for (float t = 0f; t < fullShakeTime; t += Time.unscaledDeltaTime)
        {
            float fade = 1f - t / fullShakeTime;
            float x = Mathf.Sin(t * 70f) * fullShakeDistance * fade;
            Rt.anchoredPosition = rootBase + new Vector2(x, 0f);
            yield return null;
        }
        Rt.anchoredPosition = rootBase;
        fullShakeRoutine = null;
    }

    // 테스트용 (⋮ 메뉴)
    [ContextMenu("Debug/Run 10s")] private void DebugRun() => Run(10f);
    [ContextMenu("Debug/Reset")] private void DebugReset() => ResetGauge();
}