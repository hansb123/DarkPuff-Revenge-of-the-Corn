using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 난이도 선택 UI. 시작 화면/설정 화면에 두고 버튼 2개를 연결한다.
/// 현재 선택된 난이도의 표시(selectMark)만 켜진다.
/// 버튼 OnClick을 직접 연결하지 않아도 스크립트가 알아서 등록한다.
/// </summary>
public class DifficultySelectUi : MonoBehaviour
{
    [Header("메인 화면 '난이도 설정' 버튼으로 여닫기 (선택)")]
    [SerializeField] private GameObject panelRoot;       // 난이도 패널 전체 (처음엔 꺼 두기)
    [SerializeField] private Button openButton;          // 메인 화면의 '난이도 설정' 버튼
    [SerializeField] private Button closeButton;         // 패널의 X 버튼

    [Header("난이도 카드")]
    [SerializeField] private Button easyButton;
    [SerializeField] private Button normalButton;
    [Tooltip("선택된 카드 이미지를 바꾸고 싶으면 (선택 사항). 비워 두면 Mark 오브젝트만 켜고 끔")]
    [SerializeField] private Image easyImage;
    [SerializeField] private Image normalImage;
    [SerializeField] private Sprite easyNormalSprite, easySelectedSprite, normalNormalSprite, normalSelectedSprite;
    [Tooltip("선택된 버튼 표시(테두리/체크 등). 선택 사항")]
    [SerializeField] private GameObject easyMark;
    [SerializeField] private GameObject normalMark;
    [Tooltip("난이도 설명 문구 오브젝트 (선택 사항)")]
    [SerializeField] private GameObject easyDescription;
    [SerializeField] private GameObject normalDescription;

    private void OnEnable()
    {
        if (easyButton != null) easyButton.onClick.AddListener(SelectEasy);
        if (normalButton != null) normalButton.onClick.AddListener(SelectNormal);
        if (openButton != null) openButton.onClick.AddListener(Open);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (panelRoot != null) panelRoot.SetActive(false);
        GameDifficulty.Changed += OnChanged;
        Refresh();
    }

    private void OnDisable()
    {
        if (easyButton != null) easyButton.onClick.RemoveListener(SelectEasy);
        if (normalButton != null) normalButton.onClick.RemoveListener(SelectNormal);
        if (openButton != null) openButton.onClick.RemoveListener(Open);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        GameDifficulty.Changed -= OnChanged;
    }

    public void Open()
    {
        if (panelRoot == null) return;
        panelRoot.SetActive(true);
        Refresh();
        UISfx.Play(UISfxId.Pop);
        // 키보드로도 고를 수 있게 현재 난이도 카드를 선택해 둔다
        Focus(GameDifficulty.IsNormal ? normalButton : easyButton);
    }
    public void Close()
    {
        if (panelRoot == null) return;
        panelRoot.SetActive(false);
        UISfx.Play(UISfxId.Back);
        Focus(openButton);   // 메인 메뉴의 '난이도 설정' 버튼으로 돌아감
    }

    private static void Focus(Button b)
    {
        if (b == null || EventSystem.current == null) return;
        UISelectSound.IgnoreFor(0.05f);
        EventSystem.current.SetSelectedGameObject(b.gameObject);
    }

    private void Update()
    {
        // 패널이 열려 있으면 ESC로 닫기
        if (panelRoot != null && panelRoot.activeSelf && UnityEngine.InputSystem.Keyboard.current != null
            && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }

    public void SelectEasy() { GameDifficulty.Current = Difficulty.Easy; UISfx.Play(UISfxId.Submit); }
    public void SelectNormal() { GameDifficulty.Current = Difficulty.Normal; UISfx.Play(UISfxId.Submit); }

    private void OnChanged(Difficulty _) => Refresh();

    private void Refresh()
    {
        bool normal = GameDifficulty.IsNormal;
        if (easyImage != null && easySelectedSprite != null) easyImage.sprite = normal ? easyNormalSprite : easySelectedSprite;
        if (normalImage != null && normalSelectedSprite != null) normalImage.sprite = normal ? normalSelectedSprite : normalNormalSprite;
        if (easyMark != null) easyMark.SetActive(!normal);
        if (normalMark != null) normalMark.SetActive(normal);
        if (easyDescription != null) easyDescription.SetActive(!normal);
        if (normalDescription != null) normalDescription.SetActive(normal);
    }
}