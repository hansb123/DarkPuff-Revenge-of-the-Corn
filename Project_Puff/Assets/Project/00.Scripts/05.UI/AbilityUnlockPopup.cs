using System.Collections;
using UnityEngine;

/// <summary>
/// 능력 해금 배너 ("활공 해금! 공중에서 SPACE를 꾹"). Canvas 아래에 이미지를 두고 연결하면
/// Show() 때 위에서 내려와 잠깐 보이다가 올라간다. FeatherPickup이 먹힐 때 불러 준다.
/// </summary>
public class AbilityUnlockPopup : MonoBehaviour
{
    [Tooltip("배너 Image의 RectTransform (glide_unlock_banner). 앵커는 위쪽 가운데 권장")]
    [SerializeField] private RectTransform banner;
    [SerializeField] private float startOffsetY = 400f;
    [SerializeField] private float dropTime = 0.45f;
    [SerializeField] private float holdTime = 3f;
    [SerializeField] private float leaveTime = 0.35f;

    private Vector2 finalPos;
    private bool cached;

    private void Awake()
    {
        Cache();
        if (banner != null) banner.gameObject.SetActive(false);
    }

    private void Cache()
    {
        if (cached || banner == null) return;
        cached = true;
        finalPos = banner.anchoredPosition;
    }

    public void Show()
    {
        if (banner == null) return;
        Cache();
        StopAllCoroutines();
        StartCoroutine(Routine());
    }

    private IEnumerator Routine()
    {
        banner.gameObject.SetActive(true);
        Vector2 up = finalPos + Vector2.up * startOffsetY;
        for (float t = 0f; t < dropTime; t += Time.unscaledDeltaTime)
        {
            banner.anchoredPosition = Vector2.LerpUnclamped(up, finalPos, EaseOutBack(t / dropTime));
            yield return null;
        }
        banner.anchoredPosition = finalPos;
        yield return new WaitForSecondsRealtime(holdTime);
        for (float t = 0f; t < leaveTime; t += Time.unscaledDeltaTime)
        {
            float k = t / leaveTime;
            banner.anchoredPosition = Vector2.LerpUnclamped(finalPos, up, k * k);
            yield return null;
        }
        banner.gameObject.SetActive(false);
    }

    private static float EaseOutBack(float x)
    {
        x = Mathf.Clamp01(x);
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    [ContextMenu("Debug/Show")] private void DebugShow() => Show();
}
