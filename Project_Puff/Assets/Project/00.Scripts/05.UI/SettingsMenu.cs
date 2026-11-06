using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 인게임 설정창. ESC(일시정지 키) 또는 우측 상단 버튼으로 열고 닫는다. 열려 있는 동안 게임은 멈춘다.
///  - 소리 탭: 배경음악 / 효과음 슬라이더 (바로 적용, 자동 저장)
///  - 조작 탭: 조작 설명. 아직 해금하지 않은 능력(활공, 단단해지기)의 줄은 숨겨진다
/// 난이도는 여기서 바꾸지 않는다 (메인 화면에서 변경).
///
/// 열기/닫기는 GameModeController의 일시정지(Pause/Resume)를 그대로 쓴다.
/// → ESC가 이미 일시정지로 연결되어 있으므로 따로 입력을 만들 필요가 없다.
/// 씬에 GameModeController가 없으면(메인 화면 등) 스스로 시간을 멈추는 단독 모드로 동작한다.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    public enum Requirement { None, Glide, Harden }

    [Serializable]
    public class ControlRow
    {
        [Tooltip("조작 설명 한 줄 오브젝트")]
        public GameObject root;
        [Tooltip("이 능력을 해금해야 보인다. 처음부터 쓰는 조작은 None")]
        public Requirement requires = Requirement.None;
    }

    [Header("연결")]
    [SerializeField] private GameModeController gameMode;   // 비우면 씬에서 자동 검색
    [SerializeField] private GameObject panelRoot;          // 어둡게 깐 배경 + 설정 패널 전체 (처음엔 꺼 두기)
    [SerializeField] private Button openButton;             // 우측 상단 설정 버튼
    [SerializeField] private Button closeButton;            // 패널의 X 버튼
    [SerializeField] private Button resumeButton;           // '계속하기' 버튼
    [SerializeField] private Button titleButton;            // '타이틀로' 버튼
    [Tooltip("타이틀 씬 이름 (Build Settings에 등록되어 있어야 함)")]
    [SerializeField] private string titleSceneName = "Title";

    [Header("탭")]
    [SerializeField] private Button soundTabButton;
    [SerializeField] private Button controlsTabButton;
    [SerializeField] private GameObject soundPage;
    [SerializeField] private GameObject controlsPage;
    [SerializeField] private Image soundTabImage;
    [SerializeField] private Image controlsTabImage;
    [SerializeField] private Sprite tabNormal_Sound;
    [SerializeField] private Sprite tabSelected_Sound;
    [SerializeField] private Sprite tabNormal_Controls;
    [SerializeField] private Sprite tabSelected_Controls;

    [Header("소리")]
    [SerializeField] private Slider bgmSlider;              // Min 0, Max 1
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TMP_Text bgmPercent;
    [SerializeField] private TMP_Text sfxPercent;
    [Tooltip("효과음 슬라이더를 움직일 때 들려줄 미리듣기 소리 간격")]
    [SerializeField] private float previewInterval = 0.15f;

    [Header("조작 설명")]
    [SerializeField] private ControlRow[] controlRows;

    public bool IsOpen { get; private set; }

    private bool standalone;       // GameModeController 없이 직접 시간을 멈추는 경우
    private float lastPreview;
    private bool showingSound = true;

    private void Awake()
    {
        if (gameMode == null) gameMode = FindFirstObjectByType<GameModeController>();
        standalone = gameMode == null;

        if (openButton != null) openButton.onClick.AddListener(Toggle);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (resumeButton != null) resumeButton.onClick.AddListener(Close);
        if (titleButton != null) titleButton.onClick.AddListener(GoToTitle);
        if (soundTabButton != null) soundTabButton.onClick.AddListener(() => ShowPage(true));
        if (controlsTabButton != null) controlsTabButton.onClick.AddListener(() => ShowPage(false));

        if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(OnBgm);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfx);

        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (gameMode != null) gameMode.PauseChanged += OnPauseChanged;
    }

    private void OnDisable()
    {
        if (gameMode != null) gameMode.PauseChanged -= OnPauseChanged;
        if (standalone && IsOpen) Time.timeScale = 1f;
    }

    private void Update()
    {
        if (!IsOpen) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        // Tab 키로 소리 ↔ 조작 전환 (좌우 방향키는 슬라이더가 쓰므로)
        if (kb.tabKey.wasPressedThisFrame) ShowPage(!showingSound);

        // 단독 모드(메인 화면 등)에서는 ESC로 직접 닫는다
        if (standalone && kb.escapeKey.wasPressedThisFrame) Close();
    }

    // ---------------- 열기 / 닫기 ----------------

    public void Toggle()
    {
        if (IsOpen) Close(); else Open();
    }

    public void Open()
    {
        if (IsOpen) return;
        if (standalone) { Time.timeScale = 0f; Show(); }
        else gameMode.Pause();          // 성공하면 PauseChanged → Show()
    }

    public void Close()
    {
        if (!IsOpen) return;
        if (standalone) { Time.timeScale = 1f; Hide(); }
        else gameMode.Resume();         // PauseChanged → Hide()
    }

    /// <summary>타이틀 화면으로 나가기. 시간을 되돌리고 씬을 바꾼다 (진행 중이던 스테이지는 저장되지 않음)</summary>
    public void GoToTitle()
    {
        GameAudioSettings.Save();
        UISfx.Play(UISfxId.Submit);
        Time.timeScale = 1f;   // 멈춘 채로 다음 씬에 넘어가지 않게
        SceneManager.LoadScene(titleSceneName);
    }

    // ESC 등 다른 경로로 일시정지가 바뀌어도 창이 따라가도록
    private void OnPauseChanged(bool paused)
    {
        if (paused) Show(); else Hide();
    }

    private void Show()
    {
        IsOpen = true;
        if (panelRoot != null) panelRoot.SetActive(true);

        // 저장된 값을 슬라이더에 반영 (이벤트 없이)
        if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(GameAudioSettings.BgmVolume);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(GameAudioSettings.SfxVolume);
        UpdatePercent();
        RefreshControlRows();
        ShowPage(true);
        UISfx.Play(UISfxId.Pop);
    }

    private void Hide()
    {
        IsOpen = false;
        if (panelRoot != null) panelRoot.SetActive(false);
        GameAudioSettings.Save();
        UISfx.Play(UISfxId.Back);
    }

    // ---------------- 탭 ----------------

    private void ShowPage(bool sound)
    {
        showingSound = sound;
        if (soundPage != null) soundPage.SetActive(sound);
        if (controlsPage != null) controlsPage.SetActive(!sound);
        if (soundTabImage != null && tabNormal_Sound != null)
            soundTabImage.sprite = sound ? tabSelected_Sound : tabNormal_Sound;
        if (controlsTabImage != null && tabNormal_Controls != null)
            controlsTabImage.sprite = sound ? tabNormal_Controls : tabSelected_Controls;

        // 키보드/패드로도 조작되게 첫 항목을 선택해 둔다
        if (EventSystem.current != null)
        {
            GameObject first = sound && bgmSlider != null ? bgmSlider.gameObject
                             : resumeButton != null ? resumeButton.gameObject : null;
            EventSystem.current.SetSelectedGameObject(first);
        }
        if (!sound) UISfx.Play(UISfxId.Tab);
    }

    // ---------------- 소리 ----------------

    private void OnBgm(float v)
    {
        GameAudioSettings.BgmVolume = v;
        UpdatePercent();
    }

    private void OnSfx(float v)
    {
        GameAudioSettings.SfxVolume = v;
        UpdatePercent();

        // 바꾼 크기를 바로 들어볼 수 있게 (너무 촘촘하게는 안 울리도록)
        if (Time.unscaledTime - lastPreview >= previewInterval)
        {
            lastPreview = Time.unscaledTime;
            UISfx.Play(UISfxId.Move);
        }
    }

    private void UpdatePercent()
    {
        if (bgmPercent != null) bgmPercent.text = Mathf.RoundToInt(GameAudioSettings.BgmVolume * 100f) + "%";
        if (sfxPercent != null) sfxPercent.text = Mathf.RoundToInt(GameAudioSettings.SfxVolume * 100f) + "%";
    }

    // ---------------- 조작 설명 ----------------

    /// <summary>해금하지 않은 능력의 설명은 숨긴다</summary>
    private void RefreshControlRows()
    {
        if (controlRows == null) return;
        foreach (var row in controlRows)
        {
            if (row == null || row.root == null) continue;
            bool unlocked = row.requires switch
            {
                Requirement.Glide => AbilityProgress.GlideUnlocked,
                Requirement.Harden => AbilityProgress.HardUnlocked,
                _ => true
            };
            row.root.SetActive(unlocked);
        }
    }
}