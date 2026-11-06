using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 노말 모드 목숨 HUD. 아이콘 하나 + "x5" 숫자로 남은 목숨을 보여준다.
/// RespawnManager.LivesChanged를 구독해 갱신하고, 이지 모드에서는 통째로 숨겨진다.
/// 목숨이 줄면 배지가 톡 튀고, 마지막 1개가 남으면 배지가 붉게(low 스프라이트) 바뀐다.
/// </summary>
public class LivesHud : MonoBehaviour
{
    [SerializeField] private RespawnManager respawn;     // 비워두면 자동 검색

    [Header("표시")]
    [Tooltip("배지 이미지 (아이콘이 그려진 틀)")]
    [SerializeField] private Image badge;
    [SerializeField] private TMP_Text countText;
    [Tooltip("숫자 앞에 붙는 글자. {0}이 남은 목숨. 예: x{0}")]
    [SerializeField] private string format = "x{0}";
    [SerializeField] private Sprite normalSprite;        // life_badge
    [SerializeField] private Sprite lowSprite;           // life_badge_low (마지막 1개 남았을 때, 선택)
    [SerializeField] private Color normalColor = new Color(0.08f, 0.08f, 0.08f);
    [SerializeField] private Color lowColor = new Color(0.8f, 0.2f, 0.18f);

    [Header("연출")]
    [Tooltip("보너스를 받았을 때 잠깐 보여줄 배너 (선택, '목숨 +5')")]
    [SerializeField] private GameObject bonusBanner;
    [SerializeField] private float bannerTime = 1.5f;
    [SerializeField] private float popScale = 1.3f;
    [SerializeField] private float popTime = 0.25f;

    private int shownLives = -1;
    private Coroutine popRoutine;

    private void Start()
    {
        if (respawn == null) respawn = RespawnManager.Instance != null
            ? RespawnManager.Instance : FindFirstObjectByType<RespawnManager>();

        if (bonusBanner != null) bonusBanner.SetActive(false);

        if (respawn == null || !respawn.HasLives)
        {
            gameObject.SetActive(false);   // 이지 모드 / 리스폰 매니저 없음
            return;
        }

        respawn.LivesChanged += OnChanged;
        Refresh(respawn.Lives);
        shownLives = respawn.Lives;
    }

    private void OnDestroy()
    {
        if (respawn != null) respawn.LivesChanged -= OnChanged;
    }

    private void OnChanged(int lives, int capacity, bool gained)
    {
        int prev = shownLives;
        Refresh(lives);
        shownLives = lives;

        if (gained && bonusBanner != null) StartCoroutine(Banner());
        if (prev >= 0 && lives != prev) Pop();
    }

    private void Refresh(int lives)
    {
        if (countText != null)
        {
            countText.text = string.Format(format, lives);
            countText.color = lives <= 1 ? lowColor : normalColor;
        }
        if (badge != null)
        {
            bool low = lives <= 1 && lowSprite != null;
            if (normalSprite != null || low) badge.sprite = low ? lowSprite : normalSprite;
        }
    }

    private void Pop()
    {
        if (badge == null) return;
        if (popRoutine != null) StopCoroutine(popRoutine);
        popRoutine = StartCoroutine(PopRoutine(badge.transform));
    }

    private IEnumerator PopRoutine(Transform t)
    {
        float e = 0f;
        while (e < popTime)
        {
            e += Time.unscaledDeltaTime;
            t.localScale = Vector3.one * Mathf.Lerp(1f, popScale, Mathf.Sin(Mathf.Clamp01(e / popTime) * Mathf.PI));
            yield return null;
        }
        t.localScale = Vector3.one;
        popRoutine = null;
    }

    private IEnumerator Banner()
    {
        bonusBanner.SetActive(true);
        yield return new WaitForSecondsRealtime(bannerTime);
        bonusBanner.SetActive(false);
    }
}