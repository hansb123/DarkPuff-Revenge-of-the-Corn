using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// '게임 진행상황 초기화' 버튼 + 확인창.
///  - 버튼을 누르면 확인창이 뜨고, '초기화'를 눌러야 실제로 지운다. (기본 선택은 '취소')
///  - 지우는 것: 클리어 기록, 능력 해금, 수집품(도감), 만화 열람 기록, 보스 페이즈 저장, 난이도 등 PlayerPrefs 전부
///  - keepSettings를 켜 두면 소리 크기와 화면 설정(전체화면/해상도)은 그대로 둔다.
///  - 지운 뒤 타이틀 씬을 다시 불러 화면이 새 상태로 보이게 한다.
/// 구성: 설정창이나 타이틀에 버튼(openButton)을 두고, 확인창 패널(confirmPanel, 처음엔 꺼 두기)에 yes/no 버튼을 둔다.
/// 이 컴포넌트는 항상 켜져 있는 오브젝트(예: 설정 페이지)에 붙일 것 (확인창 패널 자신에 붙이면 안 됨).
/// </summary>
public class ResetProgressButton : MonoBehaviour
{
    [SerializeField] private Button openButton;        // '게임 진행상황 초기화'
    [SerializeField] private GameObject confirmPanel;  // 확인창 (처음엔 꺼 두기)
    [SerializeField] private Button yesButton;         // '초기화'
    [SerializeField] private Button noButton;          // '취소'

    [Header("옵션")]
    [Tooltip("켜면 소리 크기와 화면 설정은 지우지 않는다")]
    [SerializeField] private bool keepSettings = true;
    [Tooltip("지운 뒤 불러올 씬 (Build Settings에 등록되어 있어야 함)")]
    [SerializeField] private string titleSceneName = "Title";

    private void Awake()
    {
        if (openButton != null) openButton.onClick.AddListener(OpenConfirm);
        if (yesButton != null) yesButton.onClick.AddListener(DoReset);
        if (noButton != null) noButton.onClick.AddListener(CloseConfirm);
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    private void OnDisable()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    private void Update()
    {
        // ESC로 확인창만 닫는다 (설정창까지 같이 닫히지 않게 프레임을 소비하지는 못하므로 확인창이 열려 있을 때만 처리)
        if (confirmPanel != null && confirmPanel.activeSelf &&
            Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseConfirm();
    }

    private void OpenConfirm()
    {
        if (confirmPanel == null) return;
        UISfx.Play(UISfxId.Pop);
        confirmPanel.SetActive(true);
        // 실수로 지우지 않게 기본 선택은 '취소'
        if (EventSystem.current != null && noButton != null)
            EventSystem.current.SetSelectedGameObject(noButton.gameObject);
    }

    private void CloseConfirm()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
        UISfx.Play(UISfxId.Back);
        if (EventSystem.current != null && openButton != null)
            EventSystem.current.SetSelectedGameObject(openButton.gameObject);
    }

    private void DoReset()
    {
        // 유지할 설정 값을 먼저 기억
        float _ = GameAudioSettings.BgmVolume + GameAudioSettings.SfxVolume;   // 메모리에 값을 읽어 둠
        bool full = GameDisplaySettings.Fullscreen;
        Vector2Int res = GameDisplaySettings.WindowedResolution;

        PlayerPrefs.DeleteAll();
        CollectibleProgress.ClearPending();

        if (keepSettings)
        {
            // (세터는 값이 같으면 저장을 건너뛰므로, 지운 뒤에는 직접 다시 기록한다)
            GameAudioSettings.PersistCurrent();
            GameDisplaySettings.Persist(full, res);
        }
        PlayerPrefs.Save();

        UISfx.Play(UISfxId.Submit);
        Time.timeScale = 1f;
        SceneManager.LoadScene(titleSceneName);
    }
}