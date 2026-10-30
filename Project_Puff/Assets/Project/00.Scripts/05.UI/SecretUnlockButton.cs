using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 타이틀 구석에 숨겨 두는 '모든 챕터 해금' 버튼.
/// 거의 투명한 작은 Image 위에 이 스크립트를 붙인다 (Button 컴포넌트는 필요 없음. Raycast Target 은 켜 둘 것).
/// 제한 시간 안에 정해진 횟수만큼 연속으로 누르면 켜짐/꺼짐이 토글된다. 실수로 눌러도 바뀌지 않는다.
/// 상태는 PlayerPrefs(StageProgress.CheatUnlockAll)에 저장되고, 챕터 선택 화면이 이 값을 보고 전부 연다.
/// 클리어 기록은 건드리지 않는다. 끄면 원래 진행 상태로 돌아간다.
/// </summary>
public class SecretUnlockButton : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("연속으로 눌러야 하는 횟수")]
    [SerializeField] private int requiredTaps = 5;
    [Tooltip("첫 번째 누름부터 이 시간(초) 안에 다 눌러야 한다")]
    [SerializeField] private float tapWindow = 2.5f;

    [Header("알림 (선택)")]
    [Tooltip("켜졌을 때 잠깐 보여줄 오브젝트 (예: 'ALL UNLOCKED' 텍스트)")]
    [SerializeField] private GameObject onNotice;
    [Tooltip("꺼졌을 때 잠깐 보여줄 오브젝트")]
    [SerializeField] private GameObject offNotice;
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
        if (taps == 0 || now - firstTapTime > tapWindow)
        {
            taps = 0;
            firstTapTime = now;
        }

        taps++;
        if (taps < requiredTaps) return;

        taps = 0;
        bool on = !StageProgress.CheatUnlockAll;
        StageProgress.CheatUnlockAll = on;
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
