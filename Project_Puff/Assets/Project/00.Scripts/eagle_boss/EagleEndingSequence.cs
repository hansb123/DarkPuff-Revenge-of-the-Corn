using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 엔딩 컷 만화 재생기. 사망 연출의 Completed 이벤트에서 Play() 를 호출하면
/// 검은 화면 위에 컷을 한 장씩 페이드로 보여주고, 끝나면 쿠키 스테이지(씬 로드 또는 이벤트)로 넘어간다.
/// UI(캔버스/이미지)는 자동 생성되므로 스프라이트만 넣으면 된다.
/// 컷 넘기기: Space / Z / Enter / 마우스 클릭,  전체 건너뛰기: Esc
/// </summary>
public class EagleEndingSequence : MonoBehaviour
{
    [Header("컷 (순서대로)")]
    [SerializeField] private Sprite[] panels;
    [Tooltip("컷이 뜬 뒤 자동으로 넘어가는 시간 (0 이하면 입력할 때만 넘어감)")]
    [SerializeField] private float autoAdvance = 3.2f;
    [Tooltip("마지막 컷은 이 시간 동안 보여줌 ('끝...?')")]
    [SerializeField] private float lastHold = 3.5f;
    [SerializeField] private float fadeIn = 0.5f;
    [SerializeField] private float fadeOut = 0.35f;
    [Tooltip("입력으로 넘길 수 있게 되는 시점 (컷이 뜬 뒤 몇 초부터)")]
    [SerializeField] private float inputLock = 0.6f;

    [Header("연출")]
    [Tooltip("이 번호의 컷(0부터)에서는 화면이 흔들림. 예: 엔딩 3번째 컷(뻥!)이면 2")]
    [SerializeField] private int[] shakePanels;
    [SerializeField] private AudioClip[] panelSfx;       // 컷마다 효과음 (없으면 비워둠, 길이 달라도 됨)
    [SerializeField] private AudioSource sfxSource;
    [Tooltip("컷 사이에 잠깐 완전히 검은 화면 (초)")]
    [SerializeField] private float blackGap = 0.25f;
    [Tooltip("컷이 화면에 차지하는 비율 (1 = 가로 전체)")]
    [SerializeField, Range(0.5f, 1f)] private float screenFill = 0.9f;

    [Header("진행 기록 / 다시 보기")]
    [Tooltip("엔딩을 다 봤을 때(건너뛰기 포함) 이 ID로 '본 만화'를 기록한다. 챕터 선택의 ComicGallery 해금용. 엔딩 ComicData의 comicId와 같게")]
    [SerializeField] private string progressId = "ending";

    [Header("끝나면")]
    [Tooltip("비워두면 씬을 로드하지 않고 Finished 이벤트만 부른다 (쿠키 스테이지 씬 이름)")]
    [SerializeField] private string nextSceneName = "";
    public UnityEvent Finished;

    private Canvas canvas;
    private Image back, shot;
    private bool playing;

    public void Play()
    {
        if (playing) return;
        playing = true;
        StartCoroutine(Run());
    }

    private void Build()
    {
        canvas = new GameObject("EndingComicCanvas").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 6000;
        back = new GameObject("Back").AddComponent<Image>();
        back.transform.SetParent(canvas.transform, false);
        back.color = Color.black; back.raycastTarget = false;
        Stretch(back.rectTransform);
        shot = new GameObject("Panel").AddComponent<Image>();
        shot.transform.SetParent(canvas.transform, false);
        shot.preserveAspect = true; shot.raycastTarget = false;
        Stretch(shot.rectTransform);
        shot.rectTransform.anchorMin = new Vector2((1f - screenFill) * 0.5f, (1f - screenFill) * 0.5f);
        shot.rectTransform.anchorMax = new Vector2(1f - (1f - screenFill) * 0.5f, 1f - (1f - screenFill) * 0.5f);
        shot.color = new Color(1, 1, 1, 0);
    }

    private static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

    private IEnumerator Run()
    {
        Time.timeScale = 1f;
        Build();
        bool skipAll = false;
        for (int i = 0; i < panels.Length && !skipAll; i++)
        {
            if (panels[i] == null) continue;
            shot.sprite = panels[i];
            shot.rectTransform.anchoredPosition = Vector2.zero;
            if (panelSfx != null && i < panelSfx.Length && panelSfx[i] != null)
            {
                if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.PlayOneShot(panelSfx[i]);
            }
            bool shake = System.Array.IndexOf(shakePanels, i) >= 0;
            bool last = i == panels.Length - 1;

            for (float t = 0f; t < fadeIn; t += Time.unscaledDeltaTime) { SetA(t / fadeIn); yield return null; }
            SetA(1f);

            float hold = last ? lastHold : autoAdvance;
            float elapsed = 0f;
            while (true)
            {
                elapsed += Time.unscaledDeltaTime;
                if (shake && elapsed < 0.9f)
                    shot.rectTransform.anchoredPosition = Random.insideUnitCircle * (28f * (1f - elapsed / 0.9f));
                else shot.rectTransform.anchoredPosition = Vector2.zero;
                if (Skip()) { skipAll = true; break; }
                if (!last && elapsed > inputLock && Advance()) break;
                if (hold > 0f && elapsed >= hold) break;
                yield return null;
            }
            shot.rectTransform.anchoredPosition = Vector2.zero;
            for (float t = 0f; t < fadeOut; t += Time.unscaledDeltaTime) { SetA(1f - t / fadeOut); yield return null; }
            SetA(0f);
            if (!skipAll && blackGap > 0f) yield return new WaitForSecondsRealtime(blackGap);
        }
        ComicProgress.MarkSeen(progressId);
        Finished?.Invoke();
        if (!string.IsNullOrEmpty(nextSceneName)) SceneManager.LoadScene(nextSceneName);
        else Destroy(canvas.gameObject, 0.1f);   // 씬 이동이 없으면 쿠키 스테이지가 이 위에서 시작되도록 정리
    }

    private void SetA(float a) { var c = shot.color; c.a = Mathf.Clamp01(a); shot.color = c; }

    private static bool Advance()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current; var m = Mouse.current;
        if (k != null && (k.spaceKey.wasPressedThisFrame || k.zKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame)) return true;
        if (m != null && m.leftButton.wasPressedThisFrame) return true;
        return false;
#else
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);
#endif
    }

    private static bool Skip()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current; return k != null && k.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}
