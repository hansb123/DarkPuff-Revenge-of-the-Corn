using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 크레딧 씬. content(글자와 그림이 세로로 쌓인 RectTransform)를 아래에서 위로 올리고,
/// 끝까지 올라가면 잠깐 머문 뒤 다음 씬(타이틀)으로 넘어간다.
///  - Space/Enter/↓/마우스 클릭을 누르고 있는 동안 빨리 감김
///  - ESC로 언제든 건너뛰고 타이틀로
/// 타이틀의 '크레딧' 버튼과 엔딩 직후 모두 이 씬으로 오면 된다.
/// 구성: Canvas 아래 content(Anchor 가운데 위, Pivot (0.5, 1)) 안에 위에서부터 차례로 이미지/TMP를 쌓는다.
///       content의 높이는 Vertical Layout Group + Content Size Fitter(Preferred Size)로 자동 계산되게 하면 편하다.
/// </summary>
public class CreditsScroll : MonoBehaviour
{
    [SerializeField] private RectTransform content;
    [Tooltip("캔버스(또는 화면)의 RectTransform. 화면 높이를 구하는 데 쓴다")]
    [SerializeField] private RectTransform viewport;
    [SerializeField] private float speed = 90f;
    [Tooltip("누르고 있을 때 배속")]
    [SerializeField] private float fastMultiplier = 4f;
    [Tooltip("다 올라간 뒤 머무는 시간 (초)")]
    [SerializeField] private float endHold = 2.5f;
    [SerializeField] private string nextSceneName = "Title";

    private float startY, endY, endTimer;
    private bool finished;

    private void Start()
    {
        Time.timeScale = 1f;
        Canvas.ForceUpdateCanvases();
        float viewH = viewport != null ? viewport.rect.height : Screen.height;

        // 화면 바로 아래에서 시작 → 내용 끝이 화면 맨 위에서 사라지기 직전까지
        startY = -viewH;
        endY = content.rect.height;
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, startY);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Leave(); return; }

        if (finished)
        {
            endTimer += Time.unscaledDeltaTime;
            if (endTimer >= endHold) Leave();
            return;
        }

        float mul = IsFastPressed() ? fastMultiplier : 1f;
        var p = content.anchoredPosition;
        p.y += speed * mul * Time.unscaledDeltaTime;
        content.anchoredPosition = p;

        // 마지막(감사 그림)이 화면 가운데쯤 오면 멈추고 머문다
        if (p.y >= endY - (viewport != null ? viewport.rect.height : Screen.height) * 0.5f)
            finished = true;
    }

    private static bool IsFastPressed()
    {
        var kb = Keyboard.current; var mouse = Mouse.current; var pad = Gamepad.current;
        return (kb != null && (kb.spaceKey.isPressed || kb.enterKey.isPressed || kb.downArrowKey.isPressed))
            || (mouse != null && mouse.leftButton.isPressed)
            || (pad != null && (pad.buttonSouth.isPressed || pad.dpad.down.isPressed));
    }

    private void Leave()
    {
        SceneManager.LoadScene(nextSceneName);
    }
}