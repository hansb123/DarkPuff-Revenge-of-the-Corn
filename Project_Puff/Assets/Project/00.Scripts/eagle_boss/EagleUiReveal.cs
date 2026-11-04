using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 연출(인트로) 동안 숨겨 둔 UI 를 1페이즈가 시작되는 순간 서서히 보이게 한다.
/// 씬이 시작되면 지정한 UI(Canvas 또는 HUD 루트)를 투명하게 만들어 두고, 보스 전투가 시작되면(EagleBoss.Begin → 1페이즈 진입) 페이드 인.
/// 오브젝트를 끄지 않고 CanvasGroup 알파만 쓰므로 안에 있는 스크립트(AngerGaugeHud 등)의 이벤트 구독은 그대로 유지된다.
///
/// 주의: 인트로의 화면 번쩍임용 Flash Image 가 들어 있는 Canvas 는 여기에 넣지 말 것 (연출 중에 보여야 함). HUD 만 따로 넣는다.
/// 아무 오브젝트에 붙이면 된다.
/// </summary>
public class EagleUiReveal : MonoBehaviour
{
    [SerializeField] private EagleBoss boss;
    [Tooltip("전투가 시작될 때까지 숨겨 둘 UI 루트들 (분노 게이지, 체력 UI, 플레이어 HUD 등). Canvas 오브젝트나 그 자식도 됨")]
    [SerializeField] private GameObject[] roots;
    [SerializeField] private float fadeIn = 0.6f;
    [Tooltip("보스가 죽어도 UI 를 그대로 둠 (끄면 사망 시 다시 숨김)")]
    [SerializeField] private bool keepAfterDefeat = true;

    private CanvasGroup[] groups;
    private bool revealed;

    private void Awake()
    {
        if (boss == null) boss = FindFirstObjectByType<EagleBoss>();
        groups = new CanvasGroup[roots != null ? roots.Length : 0];
        for (int i = 0; i < groups.Length; i++)
        {
            if (roots[i] == null) continue;
            var g = roots[i].GetComponent<CanvasGroup>();
            if (g == null) g = roots[i].AddComponent<CanvasGroup>();
            groups[i] = g;
        }
        if (boss == null || !boss.IsFighting) SetAlpha(0f);   // 이미 전투 중(리스폰 등)이면 숨기지 않음
        else revealed = true;
    }

    private void OnEnable()
    {
        if (boss == null) return;
        boss.PhaseEntered += OnPhase;
        boss.Defeated += OnDefeated;
    }

    private void OnDisable()
    {
        if (boss == null) return;
        boss.PhaseEntered -= OnPhase;
        boss.Defeated -= OnDefeated;
    }

    private void OnPhase(int index)
    {
        if (revealed) return;
        revealed = true;
        StartCoroutine(Fade());
    }

    private void OnDefeated()
    {
        if (keepAfterDefeat) return;
        revealed = false;
        StopAllCoroutines();
        SetAlpha(0f);
    }

    private IEnumerator Fade()
    {
        for (float t = 0f; t < fadeIn; t += Time.unscaledDeltaTime)
        {
            SetAlpha(t / Mathf.Max(0.01f, fadeIn));
            yield return null;
        }
        SetAlpha(1f);
    }

    private void SetAlpha(float a)
    {
        if (groups == null) return;
        foreach (var g in groups)
        {
            if (g == null) continue;
            g.alpha = a;
            g.blocksRaycasts = a > 0.99f;
            g.interactable = a > 0.99f;
        }
    }
}