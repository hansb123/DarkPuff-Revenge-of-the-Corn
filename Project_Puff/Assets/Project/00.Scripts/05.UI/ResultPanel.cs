using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 스테이지 클리어 화면. Result 모드가 되면 패널을 띄우고 시간을 멈춘 뒤, 버튼 세 개로 갈 곳을 고른다.
///   - 타이틀로
///   - 다음 챕터(또는 다음 스테이지): Next Scene Name이 비어 있거나 빌드에 없으면 버튼이 비활성(점선 모양)
///   - 모든 챕터 보기
/// 버튼 클릭은 코드에서 연결하므로 인스펙터 OnClick은 비워 둔다. 키보드/패드는 EventSystem의 이동/확인 입력으로 조작한다.
/// 씬 이름은 File → Build Profiles(Build Settings)의 Scene List에 들어 있어야 로드된다.
/// </summary>
public class ResultPanel : MonoBehaviour
{
    [SerializeField] private GameModeController gameMode;   // 비워두면 씬에서 자동으로 찾음

    [Header("UI")]
    [SerializeField] private GameObject panel;              // 클리어 화면 전체 (처음엔 꺼 둠)
    [SerializeField] private RectTransform popTarget;       // 등장할 때 통통 튀는 대상 (보통 panel의 RectTransform)
    [SerializeField] private Button titleButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button chaptersButton;

    [Header("Scenes")]
    [SerializeField] private string titleSceneName = "Title";
    [Tooltip("비워 두면 '다음' 버튼은 비활성이 됨 (마지막 스테이지/챕터)")]
    [SerializeField] private string nextSceneName = "";
    [SerializeField] private string chapterSelectSceneName = "ChapterSelect";

    [Header("Motion")]
    [SerializeField] private float popDuration = 0.3f;
    [SerializeField] private float popOvershoot = 1.08f;

    private void Start()
    {
        if (gameMode == null) gameMode = FindFirstObjectByType<GameModeController>();

        if (gameMode == null)
        {
            Debug.LogWarning("[ResultPanel] GameModeController를 찾지 못했습니다.", this);
            return;
        }

        if (panel != null) panel.SetActive(false);

        // 클릭 연결
        if (titleButton != null) titleButton.onClick.AddListener(() => LoadScene(titleSceneName));
        if (nextButton != null) nextButton.onClick.AddListener(() => LoadScene(nextSceneName));
        if (chaptersButton != null) chaptersButton.onClick.AddListener(() => LoadScene(chapterSelectSceneName));

        gameMode.ModeChanged += OnModeChanged;
    }

    private void OnDestroy()
    {
        if (gameMode != null) gameMode.ModeChanged -= OnModeChanged;
    }

    private void OnModeChanged(GameMode previous, GameMode next)
    {
        if (next != GameMode.Result) return;

        Time.timeScale = 0f;   // 뒤에서 적이 계속 움직이지 않게
        Show();
    }

    private void Show()
    {
        if (panel != null) panel.SetActive(true);

        // 다음 갈 곳이 없으면 비활성 (Button의 Disabled Sprite로 점선 모양이 됨)
        if (nextButton != null) nextButton.interactable = CanLoad(nextSceneName);

        // 키보드/패드 조작을 위해 첫 버튼을 선택해 둠 (다음이 가능하면 다음, 아니면 타이틀)
        Button first = (nextButton != null && nextButton.interactable) ? nextButton : titleButton;
        if (first != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        if (popTarget != null) StartCoroutine(PopIn());
    }

    private IEnumerator PopIn()
    {
        // timeScale이 0이므로 unscaled 시간으로 진행
        float t = 0f;

        while (t < popDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / popDuration);

            // 0.6 → overshoot → 1 (easeOutBack 느낌)
            float s = Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(u, popOvershoot));
            popTarget.localScale = new Vector3(s, s, 1f);
            yield return null;
        }

        popTarget.localScale = Vector3.one;
    }

    private static float EaseOutBack(float x, float overshoot)
    {
        float c1 = (overshoot - 1f) * 10f;   // 값이 클수록 더 많이 튐
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    private static bool CanLoad(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);
    }

    private void LoadScene(string sceneName)
    {
        if (!CanLoad(sceneName))
        {
            Debug.LogWarning($"[ResultPanel] '{sceneName}' 씬을 불러올 수 없습니다. 이름과 Build Settings의 Scene List를 확인하세요.", this);
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }
}
