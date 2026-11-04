using System.Collections;
using UnityEngine;

/// <summary>
/// 특정 페이즈 진입 시 힌트 이미지(캔버스)를 잠깐 띄웠다가 사라지게 한다.
/// 힌트 오브젝트에 CanvasGroup 이 없으면 자동으로 붙인다. 시작할 때는 꺼진 상태로 둔다.
/// </summary>
public class EaglePhaseHint : MonoBehaviour
{
    [SerializeField] private EagleBoss boss;
    [Tooltip("힌트 이미지 오브젝트 (ui_phase2_hint)")]
    [SerializeField] private GameObject hintObject;
    [Tooltip("몇 번째 페이즈에서 띄울지 (0=1페이즈, 1=2페이즈)")]
    [SerializeField] private int phaseIndex = 1;
    [SerializeField] private float delay = 1.5f;     // 포효 연출이 끝나길 기다리는 시간
    [SerializeField] private float fadeIn = 0.4f;
    [SerializeField] private float hold = 2.5f;
    [SerializeField] private float fadeOut = 0.6f;
    [SerializeField] private bool onlyOnce = true;

    private CanvasGroup group;
    private bool shown;

    private void Awake()
    {
        if (boss == null) boss = FindFirstObjectByType<EagleBoss>();
        if (hintObject != null)
        {
            group = hintObject.GetComponent<CanvasGroup>();
            if (group == null) group = hintObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            hintObject.SetActive(false);
        }
    }

    private void OnEnable() { if (boss != null) boss.PhaseEntered += OnPhase; }
    private void OnDisable() { if (boss != null) boss.PhaseEntered -= OnPhase; }

    private void OnPhase(int idx)
    {
        if (idx != phaseIndex || hintObject == null) return;
        if (onlyOnce && shown) return;
        shown = true;
        StopAllCoroutines();
        StartCoroutine(Show());
    }

    private IEnumerator Show()
    {
        yield return new WaitForSeconds(delay);
        hintObject.SetActive(true);
        for (float t = 0f; t < fadeIn; t += Time.deltaTime) { group.alpha = t / fadeIn; yield return null; }
        group.alpha = 1f;
        yield return new WaitForSeconds(hold);
        for (float t = 0f; t < fadeOut; t += Time.deltaTime) { group.alpha = 1f - t / fadeOut; yield return null; }
        group.alpha = 0f;
        hintObject.SetActive(false);
    }
}