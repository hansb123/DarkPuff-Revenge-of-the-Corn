using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 하트 3칸 HUD. PlayerHealth.Changed를 구독해서 칸을 갱신한다.
/// 하트가 깨질 때는 잠깐 깨진 하트(lost)를 보여준 뒤 빈 하트(empty)로 바뀐다.
/// Canvas 아래에 Image 3개(HorizontalLayoutGroup 권장)를 만들고 hearts에 순서대로 넣는다.
/// </summary>
public class HeartsUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth health;      // 비워두면 씬에서 자동으로 찾음
    [SerializeField] private Image[] hearts;
    [SerializeField] private Sprite fullSprite;
    [SerializeField] private Sprite emptySprite;
    [SerializeField] private Sprite lostSprite;        // 깨지는 순간 잠깐 보여줄 스프라이트
    [SerializeField] private float lostShowTime = 0.4f;

    private int shownHp;
    private Coroutine lostRoutine;

    private void Start()
    {
        if (health == null) health = FindFirstObjectByType<PlayerHealth>();

        if (health == null)
        {
            Debug.LogWarning("[HeartsUI] 씬에서 PlayerHealth를 찾지 못했습니다.", this);
            return;
        }

        health.Changed += OnChanged;
        shownHp = health.Current;
        Refresh(shownHp);
    }

    private void OnDestroy()
    {
        if (health != null) health.Changed -= OnChanged;
    }

    private void OnChanged(int current, int max)
    {
        int previous = shownHp;
        shownHp = current;

        if (lostRoutine != null) StopCoroutine(lostRoutine);

        if (current < previous)
        {
            lostRoutine = StartCoroutine(LostRoutine(current, previous));
        }
        else
        {
            Refresh(current);
        }
    }

    private IEnumerator LostRoutine(int current, int previous)
    {
        Refresh(current);

        // 방금 잃은 칸만 깨진 하트로
        for (int i = current; i < previous && i < hearts.Length; i++)
        {
            hearts[i].sprite = lostSprite;
        }

        yield return new WaitForSecondsRealtime(lostShowTime);

        Refresh(current);
        lostRoutine = null;
    }

    private void Refresh(int current)
    {
        for (int i = 0; i < hearts.Length; i++)
        {
            hearts[i].sprite = i < current ? fullSprite : emptySprite;
        }
    }
}
