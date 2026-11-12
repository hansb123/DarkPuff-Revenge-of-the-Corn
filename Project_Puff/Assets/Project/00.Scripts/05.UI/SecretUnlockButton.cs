using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 타이틀 구석에 숨겨 두는 '전부 해금/잠금' 테스트 버튼. 스테이지 + 능력 + 수집품을 한 번에 토글한다.
/// 거의 투명한 작은 Image 위에 붙인다 (Button 불필요, Raycast Target 켜 둘 것).
/// 제한 시간 안에 정해진 횟수만큼 연속으로 누르면 토글된다.
///  - 켜짐: 모든 챕터/스테이지 열림, 모든 능력 해금, 도감에 수집품 전부 표시
///  - 꺼짐: 원래 진행 상태로 복귀 (실제 클리어/해금/수집 기록은 지워지지 않음)
/// 기존 SecretUnlockButton(스테이지만), SecretAbilityUnlockButton(능력만)을 하나로 합친 것이다.
/// 셋 중 하나라도 꺼져 있으면 '켜기', 전부 켜져 있으면 '끄기'로 동작한다.
/// </summary>
public class SecretUnlockAllButton : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("연속으로 눌러야 하는 횟수")]
    [SerializeField] private int requiredTaps = 5;
    [Tooltip("첫 번째 누름부터 이 시간(초) 안에 다 눌러야 한다")]
    [SerializeField] private float tapWindow = 2.5f;

    [Header("알림 (선택)")]
    [SerializeField] private GameObject onNotice;    // 예: 'ALL UNLOCKED'
    [SerializeField] private GameObject offNotice;   // 예: 'ALL LOCKED'
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

        bool allOn = StageProgress.CheatUnlockAll
                  && AbilityProgress.CheatUnlockAll
                  && CollectibleProgress.CheatUnlockAll;
        bool on = !allOn;

        StageProgress.CheatUnlockAll = on;
        AbilityProgress.CheatUnlockAll = on;
        CollectibleProgress.CheatUnlockAll = on;

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