using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 독수리 보스: 패링에 성공할 때마다 하늘에서 회복템(HealDrop)을 떨어뜨린다.
///  - 3페이즈부터만 (Min Phase Index = 2). 1~2페이즈에서는 나오지 않는다.
///  - 3번째 패링(그로기 직행)도 똑같이 한 번 떨어진다.
/// 미니게임의 HealDrop 프리팹을 그대로 쓴다 (떨어지는 그림자 / 통통 / 깜빡임 / 사라짐 모두 HealDrop 이 처리).
/// 보스 오브젝트(또는 아무 곳)에 붙이면 된다.
/// </summary>
public class EagleParryHealDrop : MonoBehaviour
{
    [SerializeField] private EagleBoss boss;
    [SerializeField] private HealDrop dropPrefab;
    [Tooltip("무대 범위. PatternRunner / EagleBoss 에 쓴 것과 같은 Left/Right/Ground 오브젝트")]
    [SerializeField] private Transform leftLimit;
    [SerializeField] private Transform rightLimit;
    [SerializeField] private Transform groundLine;
    [Tooltip("낙하 시작 높이(바닥 기준). 화면 위쪽 밖이면 자연스러움")]
    [SerializeField] private float spawnHeight = 12f;

    [Header("조건")]
    [Tooltip("이 페이즈 번호(0부터)부터 나옴. 2 = 3페이즈")]
    [SerializeField] private int minPhaseIndex = 2;
    [Tooltip("N 번 패링할 때마다 한 번 (1 = 매번)")]
    [SerializeField] private int everyNthParry = 1;
    [Tooltip("체력이 가득이면 이번엔 안 떨어뜨림")]
    [SerializeField] private bool skipWhenFull = false;
    [Tooltip("동시에 바닥에 있을 수 있는 최대 개수")]
    [SerializeField] private int maxActive = 2;

    [Header("위치")]
    [Tooltip("무대 가장자리에서 이만큼 안쪽으로")]
    [SerializeField] private float edgeMargin = 1.5f;
    [Tooltip("플레이어 바로 위에서 이만큼은 피해서 떨어짐")]
    [SerializeField] private float avoidPlayerRadius = 1.5f;
    [Tooltip("켜면 플레이어에서 너무 멀지 않게(Near Range 안쪽) 떨어져서 바로 주울 수 있음")]
    [SerializeField] private bool dropNearPlayer = true;
    [SerializeField] private float nearRange = 5f;

    private PlayerHealth health;
    private int parryCount;
    private readonly List<HealDrop> active = new List<HealDrop>();

    private void Awake()
    {
        if (boss == null) boss = GetComponent<EagleBoss>();
        if (boss == null) boss = FindFirstObjectByType<EagleBoss>();
        health = FindFirstObjectByType<PlayerHealth>();
    }

    private void OnEnable()
    {
        ParryEvents.Succeeded += OnParry;
        if (boss != null) boss.Defeated += ClearAll;
    }

    private void OnDisable()
    {
        ParryEvents.Succeeded -= OnParry;
        if (boss != null) boss.Defeated -= ClearAll;
    }

    private void OnParry()
    {
        if (boss == null || !boss.IsFighting || boss.PhaseIndex < minPhaseIndex) return;
        parryCount++;
        if (everyNthParry > 1 && parryCount % everyNthParry != 0) return;
        if (skipWhenFull && health != null && health.Current >= health.Max) return;

        active.RemoveAll(d => d == null);
        if (maxActive > 0 && active.Count >= maxActive) return;
        Spawn();
    }

    private void Spawn()
    {
        if (dropPrefab == null) return;
        float left = (leftLimit != null ? leftLimit.position.x : -8f) + edgeMargin;
        float right = (rightLimit != null ? rightLimit.position.x : 8f) - edgeMargin;
        float ground = groundLine != null ? groundLine.position.y : 0f;

        float lo = left, hi = right;
        if (dropNearPlayer && health != null)
        {
            float px = health.transform.position.x;
            lo = Mathf.Max(left, px - nearRange);
            hi = Mathf.Min(right, px + nearRange);
        }
        float x = Random.Range(lo, hi);
        if (health != null)
        {
            for (int i = 0; i < 8 && Mathf.Abs(x - health.transform.position.x) < avoidPlayerRadius; i++)
                x = Random.Range(lo, hi);
        }

        var d = Instantiate(dropPrefab);
        d.Launch(x, ground + spawnHeight, ground);
        active.Add(d);
    }

    public void ClearAll()
    {
        foreach (var d in active) if (d != null) d.Remove();
        active.Clear();
    }

    [ContextMenu("Debug/Drop Now")] private void DebugDrop() => Spawn();
}