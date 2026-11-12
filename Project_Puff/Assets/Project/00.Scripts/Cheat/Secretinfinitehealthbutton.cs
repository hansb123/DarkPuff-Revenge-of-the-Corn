using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 타이틀 구석에 숨겨 두는 '체력 무한' 치트 ON/OFF 버튼.
/// 거의 투명한 작은 Image 위에 붙인다 (Button 불필요, Raycast Target 켜 둘 것).
/// 제한 시간 안에 정해진 횟수만큼 연속으로 누르면 켜짐/꺼짐이 토글된다. 실수로 눌러도 바뀌지 않는다.
/// 상태는 DebugCheats.InfiniteHealth(PlayerPrefs)에 저장되어 스테이지에서 PlayerHealth가 읽는다.
/// 다른 치트(스테이지/능력/수집품 해금)와는 별개로 켜고 끈다.
/// </summary>
public class SecretInfiniteHealthButton : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("연속으로 눌러야 하는 횟수")]
    [SerializeField] private int requiredTaps = 5;
    [Tooltip("첫 번째 누름부터 이 시간(초) 안에 다 눌러야 한다")]
    [SerializeField] private float tapWindow = 2.5f;

    [Header("알림 (선택)")]
    [SerializeField] private GameObject onNotice;    // 예: 'INFINITE HP ON'
    [SerializeField] private GameObject offNotice;   // 예: 'INFINITE HP OFF'
    [SerializeField] private float noticeTime = 1.5f;

    private int taps;
    private float firstTapTime;
    private Coroutine noticeRoutine;

    private void Start()
    {
        if (onNotice != null) onNotice.SetActive(false);
        if (offNotice != null) offNotice.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        float now = Time.unscaledTime;
        if (taps == 0 || now - firstTapTime > tapWindow) { taps = 0; firstTapTime = now; }

        taps++;
        if (taps < requiredTaps) return;
        taps = 0;

        bool on = !DebugCheats.InfiniteHealth;
        DebugCheats.InfiniteHealth = on;
        UISfx.Play(on ? UISfxId.Submit : UISfxId.Back);
        ShowNotice(on);
    }

    private void ShowNotice(bool on)
    {
        if (noticeRoutine != null) StopCoroutine(noticeRoutine);
        noticeRoutine = StartCoroutine(NoticeRoutine(on ? onNotice : offNotice));
    }

    private IEnumerator NoticeRoutine(GameObject obj)
    {
        if (onNotice != null) onNotice.SetActive(false);
        if (offNotice != null) offNotice.SetActive(false);
        if (obj == null) yield break;
        obj.SetActive(true);
        yield return new WaitForSecondsRealtime(noticeTime);
        obj.SetActive(false);
    }
}