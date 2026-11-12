using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 타이틀 구석에 숨겨 두는 '모든 능력 해금/잠금' 버튼 (SecretUnlockButton의 능력 버전).
/// 거의 투명한 작은 Image 위에 붙인다 (Button 컴포넌트 불필요, Raycast Target 켜 둘 것).
/// 제한 시간 안에 정해진 횟수만큼 연속으로 누르면 토글된다.
///  - 하나라도 잠겨 있으면: 활공 + 단단해지기 모두 해금
///  - 전부 해금되어 있으면: 모두 다시 잠금
/// 스테이지 해금(SecretUnlockButton)과는 별개이며, 서로 영향을 주지 않는다.
/// 새 능력이 생기면 AbilityProgress에 맞춰 ApplyAll()만 고치면 된다.
/// </summary>
public class SecretAbilityUnlockButton : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("연속으로 눌러야 하는 횟수")]
    [SerializeField] private int requiredTaps = 5;
    [Tooltip("첫 번째 누름부터 이 시간(초) 안에 다 눌러야 한다")]
    [SerializeField] private float tapWindow = 2.5f;

    [Header("알림 (선택)")]
    [Tooltip("해금했을 때 잠깐 보여줄 오브젝트 (예: 'ABILITIES UNLOCKED')")]
    [SerializeField] private GameObject onNotice;
    [Tooltip("잠갔을 때 잠깐 보여줄 오브젝트")]
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
        bool allUnlocked = AbilityProgress.GlideUnlocked && AbilityProgress.HardUnlocked;
        bool on = !allUnlocked;
        ApplyAll(on);
        UISfx.Play(on ? UISfxId.Submit : UISfxId.Back);
        ShowNotice(on);
    }

    private static void ApplyAll(bool unlock)
    {
        if (unlock)
        {
            AbilityProgress.UnlockGlide();
            AbilityProgress.UnlockHard();
        }
        else
        {
            AbilityProgress.LockGlide();
            AbilityProgress.LockHard();
        }
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