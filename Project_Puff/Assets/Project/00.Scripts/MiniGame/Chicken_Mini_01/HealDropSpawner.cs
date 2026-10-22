using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 닭 미니게임의 회복템 낙하 담당. PatternRunner가 돌아가는 동안만 시간이 흐르고, 일정 간격마다 하늘에서 HealDrop을 떨어뜨린다.
///   - 죽어서 재시작하거나 클리어로 PatternRunner가 멈추면 남은 회복템을 모두 치우고 타이머를 처음으로 되돌림
///   - 따로 Begin/Stop을 연결할 필요 없음 (Runner 상태를 보고 알아서 움직임)
/// 낙하 위치: 플레이어와 너무 겹치지 않게 무대 범위 안에서 무작위.
/// </summary>
public class HealDropSpawner : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private PatternRunner runner;
    [SerializeField] private HealDrop dropPrefab;
    [Tooltip("무대 범위. PatternRunner에 쓴 것과 같은 Left/Right/Ground 오브젝트")]
    [SerializeField] private Transform leftLimit;
    [SerializeField] private Transform rightLimit;
    [SerializeField] private Transform groundLine;
    [Tooltip("낙하 시작 높이(바닥 기준). 화면 위쪽 밖이면 자연스러움")]
    [SerializeField] private float spawnHeight = 12f;

    [Header("타이밍")]
    [Tooltip("패턴이 시작된 뒤 첫 회복템까지, 그리고 그 뒤 간격")]
    [SerializeField] private float interval = 10f;
    [Tooltip("체력이 가득 차 있으면 이번 낙하를 건너뜀")]
    [SerializeField] private bool skipWhenFull = false;
    [Tooltip("한 번에 화면에 있을 수 있는 최대 개수. 이미 있으면 새로 안 떨어뜨림")]
    [SerializeField] private int maxActive = 1;

    [Header("위치")]
    [Tooltip("무대 가장자리에서 이만큼 안쪽으로")]
    [SerializeField] private float edgeMargin = 1.2f;
    [Tooltip("플레이어 바로 위에서 이만큼은 피해서 떨어짐 (너무 쉽게 먹거나 맞는 느낌 방지)")]
    [SerializeField] private float avoidPlayerRadius = 2.0f;

    private PlayerHealth health;
    private float timer;
    private bool wasRunning;
    private readonly List<HealDrop> active = new List<HealDrop>();

    private void Start()
    {
        if (runner == null) runner = FindFirstObjectByType<PatternRunner>();
        health = FindFirstObjectByType<PlayerHealth>();
    }

    private void Update()
    {
        bool running = runner != null && runner.IsRunning;

        if (!running)
        {
            if (wasRunning) { ClearAll(); timer = 0f; }   // 멈췄을 때 정리
            wasRunning = false;
            return;
        }
        wasRunning = true;

        timer += Time.deltaTime;
        if (timer < interval) return;
        timer = 0f;

        active.RemoveAll(d => d == null);
        if (active.Count >= maxActive) return;
        if (skipWhenFull && health != null && health.Current >= health.Max) return;
        Spawn();
    }

    private void Spawn()
    {
        if (dropPrefab == null) return;
        float left = (leftLimit != null ? leftLimit.position.x : -8f) + edgeMargin;
        float right = (rightLimit != null ? rightLimit.position.x : 8f) - edgeMargin;
        float ground = groundLine != null ? groundLine.position.y : 0f;

        float x = Random.Range(left, right);
        if (health != null)
        {
            // 플레이어 근처면 몇 번 다시 뽑기
            for (int i = 0; i < 8 && Mathf.Abs(x - health.transform.position.x) < avoidPlayerRadius; i++)
                x = Random.Range(left, right);
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
