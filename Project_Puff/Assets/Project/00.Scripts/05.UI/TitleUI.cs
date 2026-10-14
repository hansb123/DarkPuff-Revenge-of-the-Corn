using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면. 시작하기 / 챕터 선택 / 종료.
/// 로고는 살짝 흔들려서 B급 영화 간판 느낌을 냄.
/// </summary>
public class TitleUI : MonoBehaviour
{
    [Header("버튼")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button chaptersButton;
    [SerializeField] private Button quitButton;

    [Header("Scenes")]
    [Tooltip("'시작하기'로 들어갈 씬 (보통 1챕터 1스테이지)")]
    [SerializeField] private string firstStageSceneName = "";
    [SerializeField] private string chapterSelectSceneName = "ChapterSelect";

    [Header("로고 연출")]
    [SerializeField] private RectTransform logo;
    [SerializeField] private float logoSwayAngle = 1.2f;
    [SerializeField] private float logoSwaySpeed = 1.6f;
    [SerializeField] private float logoPulse = 0.015f;

    [Header("시작 연출")]
    [SerializeField] private float startDelay = 0.45f;   // '뻥!' 소리가 들릴 시간

    private bool loading;

    private void Start()
    {
        Time.timeScale = 1f;
        UISelectSound.IgnoreFor(0.25f);

        Hook(startButton, OnStart);
        Hook(chaptersButton, () => Go(chapterSelectSceneName, UISfxId.Submit, 0.05f));
        Hook(quitButton, OnQuit);

        if (startButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(startButton.gameObject);
    }

    private void Update()
    {
        if (logo == null) return;

        float t = Time.unscaledTime * logoSwaySpeed;
        logo.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t) * logoSwayAngle);
        float s = 1f + Mathf.Sin(t * 2f) * logoPulse;
        logo.localScale = new Vector3(s, s, 1f);
    }

    private void Hook(Button b, UnityEngine.Events.UnityAction action)
    {
        if (b == null) return;
        b.onClick.AddListener(action);
        if (b.GetComponent<UISelectSound>() == null) b.gameObject.AddComponent<UISelectSound>();
    }

    private void OnStart()
    {
        // 처음 시작은 '뻥!' 소리를 들려주고 잠깐 뒤에 이동
        Go(firstStageSceneName, UISfxId.Start, startDelay);
    }

    private void OnQuit()
    {
        UISfx.Play(UISfxId.Back);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void Go(string sceneName, UISfxId sfx, float delay)
    {
        if (loading) return;

        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            UISfx.Play(UISfxId.Denied);
            Debug.LogWarning($"[TitleUI] 씬 '{sceneName}' 을(를) 불러올 수 없습니다. (이름 / Build Settings 확인)", this);
            return;
        }

        loading = true;
        UISfx.Play(sfx);
        StartCoroutine(LoadAfter(sceneName, delay));
    }

    private IEnumerator LoadAfter(string sceneName, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        SceneManager.LoadScene(sceneName);
    }
}
