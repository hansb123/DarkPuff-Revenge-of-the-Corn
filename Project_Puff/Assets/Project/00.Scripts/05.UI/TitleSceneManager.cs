using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 타이틀 씬 매니저. 시작하기 / 챕터 선택 / 종료 버튼 처리, 씬 전환(페이드), 키보드 포커스 유지, 로고·캐릭터 연출.

public class TitleSceneManager : MonoBehaviour
{
    [Header("버튼")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button chapterSelectButton;
    [SerializeField] private Button exitButton;

    [Header("Scenes (Build Settings에 등록된 씬 이름)")]
    [Tooltip("'시작하기'로 들어갈 씬 (1챕터 1스테이지)")]
    [SerializeField] private string firstStageSceneName = "";
    [SerializeField] private string chapterSelectSceneName = "ChapterSelect";

    [Header("로고 / 캐릭터 연출 (비워두면 생략)")]
    [SerializeField] private RectTransform logo;
    [SerializeField] private float logoSwayAngle = 1.2f;
    [SerializeField] private float logoSwaySpeed = 1.6f;
    [SerializeField] private float logoPulse = 0.015f;
    [SerializeField] private RectTransform character;
    [SerializeField] private float characterBobHeight = 8f;
    [SerializeField] private float characterBobSpeed = 2.4f;

    [Header("전환")]
    [Tooltip("'시작하기' 때 '뻥!' 소리가 들릴 시간")]
    [SerializeField] private float startDelay = 0.45f;
    [Tooltip("페이드 인/아웃 시간 (0이면 페이드 없음)")]
    [SerializeField] private float fadeTime = 0.35f;

    private readonly List<Button> buttons = new();
    private Button lastSelected;
    private CanvasGroup fade;
    private Vector3 logoBaseScale = Vector3.one;
    private Vector2 characterBasePos;
    private bool loading;

    private void Start()
    {
        Time.timeScale = 1f;
        UISelectSound.IgnoreFor(0.3f);   // 첫 버튼 선택 소리 방지

        Register(startButton, OnStart);
        Register(chapterSelectButton, OnChapterSelect);
        Register(exitButton, OnExit);

        if (logo != null) logoBaseScale = logo.localScale;
        if (character != null) characterBasePos = character.anchoredPosition;

        BuildFade();
        if (startButton != null) Select(startButton);

        if (fade != null && fadeTime > 0f) StartCoroutine(Fade(1f, 0f));
    }

    private void Update()
    {
        Animate();
        KeepFocus();
    }

    // ───────────── 버튼 ─────────────

    private void Register(Button b, UnityEngine.Events.UnityAction action)
    {
        if (b == null) return;
        b.onClick.AddListener(action);
        if (b.GetComponent<UISelectSound>() == null) b.gameObject.AddComponent<UISelectSound>();
        buttons.Add(b);
    }

    private void OnStart()
    {
        // 메뉴 BGM은 스테이지 BGM과 겹치지 않게 끈다
        Load(firstStageSceneName, UISfxId.Start, startDelay, stopMenuBgm: true);
    }

    private void OnChapterSelect()
    {
        // 타이틀 → 챕터 선택은 같은 BGM이 이어지므로 끄지 않음
        Load(chapterSelectSceneName, UISfxId.Submit, 0.05f, stopMenuBgm: false);
    }

    private void OnExit()
    {
        if (loading) return;
        loading = true;
        UISfx.Play(UISfxId.Back);
        StartCoroutine(QuitRoutine());
    }

    private IEnumerator QuitRoutine()
    {
        yield return Fade(0f, 1f);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ───────────── 씬 전환 ─────────────

    private void Load(string sceneName, UISfxId sfx, float delay, bool stopMenuBgm)
    {
        if (loading) return;

        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            UISfx.Play(UISfxId.Denied);
            Debug.LogWarning($"[TitleSceneManager] 씬 '{sceneName}' 을(를) 불러올 수 없습니다. (이름 / Build Settings 확인)", this);
            return;
        }

        loading = true;
        UISfx.Play(sfx);
        StartCoroutine(LoadRoutine(sceneName, delay, stopMenuBgm));
    }

    private IEnumerator LoadRoutine(string sceneName, float delay, bool stopMenuBgm)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        yield return Fade(0f, 1f);
        if (stopMenuBgm) SceneBgm.StopMenuBgm();
        SceneManager.LoadScene(sceneName);
    }

    // ───────────── 페이드 ─────────────

    private void BuildFade()
    {
        if (fadeTime <= 0f) return;

        var go = new GameObject("TitleFade", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(Image));
        go.transform.SetParent(transform, false);

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 999;

        var img = go.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;   // 버튼 클릭을 막지 않음

        fade = go.GetComponent<CanvasGroup>();
        fade.alpha = 1f;
        fade.blocksRaycasts = false;
        fade.interactable = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (fade == null || fadeTime <= 0f) yield break;

        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            fade.alpha = Mathf.Lerp(from, to, t / fadeTime);
            yield return null;
        }
        fade.alpha = to;
    }

    // ───────────── 연출 / 포커스 ─────────────

    private void Animate()
    {
        float t = Time.unscaledTime;

        if (logo != null)
        {
            float s = t * logoSwaySpeed;
            logo.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(s) * logoSwayAngle);
            logo.localScale = logoBaseScale * (1f + Mathf.Sin(s * 2f) * logoPulse);
        }

        if (character != null)
        {
            float bob = Mathf.Abs(Mathf.Sin(t * characterBobSpeed)) * characterBobHeight;
            character.anchoredPosition = characterBasePos + new Vector2(0f, bob);
        }
    }

    /// <summary>마우스로 빈 곳을 눌러 선택이 풀려도, 방향키/Enter를 누르면 마지막 버튼으로 돌아옴</summary>
    private void KeepFocus()
    {
        var es = EventSystem.current;
        if (es == null || loading) return;

        var current = es.currentSelectedGameObject;
        if (current != null)
        {
            var b = current.GetComponent<Button>();
            if (b != null && buttons.Contains(b)) lastSelected = b;
            return;
        }

        var kb = Keyboard.current;
        if (kb == null) return;

        bool pressed = kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame
                    || kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame
                    || kb.enterKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame;
        if (!pressed) return;

        Select(lastSelected != null ? lastSelected : startButton);
    }

    private void Select(Button b)
    {
        if (b == null || EventSystem.current == null) return;
        UISelectSound.IgnoreFor(0.05f);   // 포커스를 되돌릴 때는 소리 없이
        EventSystem.current.SetSelectedGameObject(b.gameObject);
        lastSelected = b;
    }
}