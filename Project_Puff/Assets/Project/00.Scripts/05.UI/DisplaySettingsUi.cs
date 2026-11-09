using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정창의 '화면' 줄. 소리 페이지(SoundPage) 아래쪽에 이 줄들을 놓으면 된다.
///  - 화면 모드 버튼: 누를 때마다 전체화면 ↔ 창 모드
///  - 해상도 ◀ ▶ : 창 모드일 때만 동작 (전체화면은 모니터 해상도를 그대로 사용)
/// 설정은 GameDisplaySettings가 저장하고 바로 적용한다.
/// </summary>
public class DisplaySettingsUi : MonoBehaviour
{
    [Header("화면 모드")]
    [SerializeField] private Button modeButton;
    [SerializeField] private TMP_Text modeLabel;
    [SerializeField] private string fullscreenText = "전체화면";
    [SerializeField] private string windowedText = "창 모드";
    [Tooltip("버튼 그림이 모드에 따라 바뀌게 하려면 아래 6장을 연결 (글씨가 그림에 들어 있으므로 modeLabel은 비워도 됨)")]
    [SerializeField] private Sprite fullscreenNormal;
    [SerializeField] private Sprite fullscreenPressed;
    [SerializeField] private Sprite fullscreenSelected;
    [SerializeField] private Sprite windowedNormal;
    [SerializeField] private Sprite windowedPressed;
    [SerializeField] private Sprite windowedSelected;

    [Header("해상도 (창 모드 전용)")]
    [SerializeField] private Button resPrevButton;
    [SerializeField] private Button resNextButton;
    [SerializeField] private TMP_Text resLabel;
    [Tooltip("전체화면일 때 해상도 줄에 표시할 글자. 예: '모니터 해상도'")]
    [SerializeField] private string fullscreenResText = "모니터 해상도";
    [Tooltip("전체화면에서는 흐리게 보이게 할 묶음 (선택, CanvasGroup)")]
    [SerializeField] private CanvasGroup resGroup;
    [Range(0f, 1f)][SerializeField] private float disabledAlpha = 0.4f;

    private void Awake()
    {
        if (modeButton != null) modeButton.onClick.AddListener(ToggleMode);
        if (resPrevButton != null) resPrevButton.onClick.AddListener(() => StepRes(-1));
        if (resNextButton != null) resNextButton.onClick.AddListener(() => StepRes(+1));
    }

    private void OnEnable()
    {
        GameDisplaySettings.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        GameDisplaySettings.Changed -= Refresh;
        GameDisplaySettings.Save();
    }

    private void ToggleMode()
    {
        UISfx.Play(UISfxId.Pop);
        GameDisplaySettings.Fullscreen = !GameDisplaySettings.Fullscreen;
    }

    private void StepRes(int dir)
    {
        if (GameDisplaySettings.Fullscreen) { UISfx.Play(UISfxId.Denied); return; }

        var list = GameDisplaySettings.AvailableWindowed();
        int cur = Mathf.Max(0, list.IndexOf(GameDisplaySettings.WindowedResolution));
        int next = (cur + dir + list.Count) % list.Count;
        UISfx.Play(UISfxId.Pop);
        GameDisplaySettings.WindowedResolution = list[next];
    }

    private void ApplyModeSprites(bool full)
    {
        if (modeButton == null) return;
        Sprite n = full ? fullscreenNormal : windowedNormal;
        if (n == null) return;
        Sprite p = full ? fullscreenPressed : windowedPressed;
        Sprite sel = full ? fullscreenSelected : windowedSelected;

        if (modeButton.targetGraphic is Image img) img.sprite = n;
        modeButton.transition = Selectable.Transition.SpriteSwap;
        var st = modeButton.spriteState;
        st.highlightedSprite = sel;
        st.selectedSprite = sel;
        st.pressedSprite = p != null ? p : n;
        st.disabledSprite = n;
        modeButton.spriteState = st;
    }

    private void Refresh()
    {
        bool full = GameDisplaySettings.Fullscreen;
        if (modeLabel != null) modeLabel.text = full ? fullscreenText : windowedText;
        ApplyModeSprites(full);

        if (resLabel != null)
        {
            var r = GameDisplaySettings.WindowedResolution;
            resLabel.text = full ? fullscreenResText : $"{r.x} × {r.y}";
        }
        if (resPrevButton != null) resPrevButton.interactable = !full;
        if (resNextButton != null) resNextButton.interactable = !full;
        if (resGroup != null) resGroup.alpha = full ? disabledAlpha : 1f;
    }
}