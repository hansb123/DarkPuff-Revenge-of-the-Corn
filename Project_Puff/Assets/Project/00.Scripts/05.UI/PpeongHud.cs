using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 뻥! 충전 HUD (슬롯 3칸). PlayerSpecialAttack.ChargesChanged를 구독한다.
/// 충전될 때: gain 스프라이트로 커졌다가 full로 돌아옴. 사용할 때: lost 스프라이트로 번쩍한 뒤 empty.
/// 가득 찬 상태에서 아이템을 먹으려 하면 슬롯이 좌우로 흔들림.
/// Canvas 아래에 Image 3개(HorizontalLayoutGroup 권장)를 만들고 slots에 순서대로 넣는다.
/// </summary>
public class PpeongHud : MonoBehaviour
{
    [SerializeField] private PlayerSpecialAttack special;   // 비워두면 씬에서 자동으로 찾음
    [SerializeField] private Image[] slots;
    [SerializeField] private Sprite fullSprite;
    [SerializeField] private Sprite emptySprite;
    [SerializeField] private Sprite lostSprite;
    [SerializeField] private Sprite gainSprite;

    [Header("연출")]
    [SerializeField] private float gainTime = 0.22f;
    [SerializeField] private float gainScale = 1.25f;
    [SerializeField] private float lostTime = 0.25f;
    [SerializeField] private float shakeTime = 0.25f;
    [SerializeField] private float shakeDistance = 8f;

    private int shown;
    private Coroutine[] routines;
    private Coroutine shakeRoutine;
    private Vector2[] basePositions;

    private void Start()
    {
        if (special == null) special = FindFirstObjectByType<PlayerSpecialAttack>();

        if (special == null)
        {
            Debug.LogWarning("[PpeongHud] 씬에서 PlayerSpecialAttack을 찾지 못했습니다.", this);
            return;
        }

        routines = new Coroutine[slots.Length];
        basePositions = new Vector2[slots.Length];
        for (int i = 0; i < slots.Length; i++) basePositions[i] = ((RectTransform)slots[i].transform).anchoredPosition;

        special.ChargesChanged += OnChanged;
        special.ChargeRejected += OnRejected;

        shown = special.Charges;
        Refresh(shown);
    }

    private void OnDestroy()
    {
        if (special == null) return;
        special.ChargesChanged -= OnChanged;
        special.ChargeRejected -= OnRejected;
    }

    private void OnChanged(int current, int max)
    {
        int previous = shown;
        shown = current;

        if (current > previous)
        {
            Refresh(previous);
            for (int i = previous; i < current && i < slots.Length; i++) Play(i, GainRoutine(i));
        }
        else if (current < previous)
        {
            Refresh(current);
            for (int i = current; i < previous && i < slots.Length; i++) Play(i, LostRoutine(i));
        }
        else
        {
            Refresh(current);
        }
    }

    private void Play(int index, IEnumerator routine)
    {
        if (routines[index] != null) StopCoroutine(routines[index]);
        routines[index] = StartCoroutine(routine);
    }

    private void Refresh(int count)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (routines != null && routines[i] != null) continue;   // 연출 중인 칸은 건드리지 않음
            slots[i].sprite = i < count ? fullSprite : emptySprite;
            slots[i].transform.localScale = Vector3.one;
        }
    }

    private IEnumerator GainRoutine(int index)
    {
        var img = slots[index];
        img.sprite = gainSprite != null ? gainSprite : fullSprite;

        float t = 0f;
        while (t < gainTime)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / gainTime);
            float s = Mathf.Lerp(gainScale, 1f, u * u);   // 커졌다가 빠르게 제자리로
            img.transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }

        img.sprite = fullSprite;
        img.transform.localScale = Vector3.one;
        routines[index] = null;
        Refresh(shown);
    }

    private IEnumerator LostRoutine(int index)
    {
        var img = slots[index];
        img.sprite = lostSprite != null ? lostSprite : emptySprite;
        img.transform.localScale = Vector3.one;

        yield return new WaitForSecondsRealtime(lostTime);

        routines[index] = null;
        Refresh(shown);
    }

    private void OnRejected()
    {
        if (shakeRoutine != null) return;   // 이미 흔들리는 중이면 중복 시작하지 않음
        shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        float t = 0f;
        while (t < shakeTime)
        {
            t += Time.unscaledDeltaTime;
            float damp = 1f - t / shakeTime;
            float offset = Mathf.Sin(t * 60f) * shakeDistance * damp;

            for (int i = 0; i < slots.Length; i++)
                ((RectTransform)slots[i].transform).anchoredPosition = basePositions[i] + new Vector2(offset, 0f);

            yield return null;
        }

        for (int i = 0; i < slots.Length; i++)
            ((RectTransform)slots[i].transform).anchoredPosition = basePositions[i];

        shakeRoutine = null;
    }
}
