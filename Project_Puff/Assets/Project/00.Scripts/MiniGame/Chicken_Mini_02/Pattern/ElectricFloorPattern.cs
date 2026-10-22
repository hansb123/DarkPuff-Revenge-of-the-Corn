using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 메카 닭 패턴 3: 전기 바닥 (활공 회피용). 바닥에 절연 매트 몇 장이 솟아오르고, 매트가 아닌 바닥이 노랗게 점멸하다가 감전된다.
/// 땅에 발이 닿아 있으면 맞으므로 → 매트로 뛰어 건너거나 활공으로 매트 사이를 날아가야 한다.
/// 난이도가 오를수록 매트가 줄고 간격이 벌어지며(활공 필수), 경고 시간이 짧아지고 라운드 수가 늘어난다.
/// 라운드마다 매트 위치가 바뀐다.
/// 에셋 만들기: Project 창 우클릭 → Create → Darkpuff → Patterns → Electric Floor
/// </summary>
[CreateAssetMenu(menuName = "Darkpuff/Patterns/Electric Floor", fileName = "Pattern_ElectricFloor")]
public class ElectricFloorPattern : AttackPattern
{
    [Header("프리팹")]
    [SerializeField] private ElectricZone zonePrefab;
    [SerializeField] private SafeMat matPrefab;

    [Header("난이도별 수치  (왼쪽 = 쉬움 / 오른쪽 = 가장 어려움)")]
    [Tooltip("라운드 수 (매트 위치가 바뀌며 반복)")]
    [SerializeField] private int roundsEasy = 2;
    [SerializeField] private int roundsHard = 3;
    [Tooltip("매트 개수")]
    [SerializeField] private int matsEasy = 4;
    [SerializeField] private int matsHard = 2;
    [Tooltip("경고(점멸) 시간. 이 동안 매트로 이동")]
    [SerializeField] private float warnEasy = 1.6f;
    [SerializeField] private float warnHard = 1.0f;
    [Tooltip("감전 시간")]
    [SerializeField] private float shockEasy = 1.2f;
    [SerializeField] private float shockHard = 1.8f;

    [Header("매트")]
    [SerializeField] private float matWidth = 2.6f;
    [Tooltip("매트가 솟아오른 뒤 경고가 시작되기까지(초) — 위치를 보고 판단할 시간")]
    [SerializeField] private float matRevealTime = 0.6f;
    [Tooltip("벽/무대 끝에서 이만큼 안쪽에만 매트를 놓음")]
    [SerializeField] private float edgeMargin = 1.0f;
    [Tooltip("플레이어 바로 아래에는 매트를 놓지 않는 반경 (안 맞고 가만있는 걸 막으려면 0보다 크게)")]
    [SerializeField] private float avoidPlayerRadius = 1.6f;
    [Tooltip("라운드 사이 쉬는 시간")]
    [SerializeField] private float betweenRounds = 0.5f;

    public override IEnumerator Run(PatternContext ctx)
    {
        if (zonePrefab == null || matPrefab == null)
        {
            Debug.LogWarning("[ElectricFloorPattern] Zone Prefab / Mat Prefab이 비어 있습니다.", this);
            yield break;
        }
        var zonePool = ctx.GetPool(zonePrefab, (z, p) => z.SetReleaser(x => p.Release(x)));
        var matPool = ctx.GetPool(matPrefab, (m, p) => m.SetReleaser(x => p.Release(x)));

        int rounds = ctx.Lerp(roundsEasy, roundsHard);
        float warnT = ctx.Lerp(warnEasy, warnHard);
        float shockT = ctx.Lerp(shockEasy, shockHard);
        float left = ctx.LeftX, right = ctx.RightX;

        for (int r = 0; r < rounds; r++)
        {
            int matCount = ctx.Lerp(matsEasy, matsHard);
            var centers = PlaceMats(matCount, left + edgeMargin + matWidth * 0.5f, right - edgeMargin - matWidth * 0.5f,
                ctx.Player != null ? ctx.Player.position.x : float.NaN);

            // 매트 솟아오름
            var mats = new List<SafeMat>();
            foreach (float cx in centers)
            {
                var m = matPool.Get();
                m.Show(ctx.Runner, cx, ctx.GroundY, matWidth);
                mats.Add(m);
            }
            yield return new WaitForSeconds(matRevealTime);

            // 매트 사이의 빈 바닥 = 감전 구역
            float cursor = left;
            foreach (float cx in centers)
            {
                float mL = cx - matWidth * 0.5f;
                if (mL - cursor > 0.2f) PlayZone(zonePool, ctx, cursor, mL, warnT, shockT);
                cursor = cx + matWidth * 0.5f;
            }
            if (right - cursor > 0.2f) PlayZone(zonePool, ctx, cursor, right, warnT, shockT);

            yield return new WaitForSeconds(warnT + shockT + 0.2f);

            foreach (var m in mats) m.Hide();
            yield return new WaitForSeconds(betweenRounds);
        }
    }

    private void PlayZone(PrefabPool<ElectricZone> pool, PatternContext ctx, float l, float r, float warnT, float shockT)
    {
        var z = pool.Get();
        z.Play(ctx.Runner, l, r, ctx.GroundY, ctx.Player, warnT, shockT);
    }

    /// <summary>겹치지 않게, 대체로 고르게 흩어진 매트 중심 X 목록 (왼쪽→오른쪽 정렬)</summary>
    private List<float> PlaceMats(int count, float minX, float maxX, float playerX)
    {
        var result = new List<float>();
        if (maxX <= minX) { result.Add((minX + maxX) * 0.5f); return result; }
        // 구간을 count 등분해 각 구간 안에서 랜덤 → 한쪽에 몰리지 않음
        float seg = (maxX - minX) / count;
        for (int i = 0; i < count; i++)
        {
            float a = minX + seg * i + matWidth * 0.1f;
            float b = minX + seg * (i + 1) - matWidth * 0.1f;
            float x = Random.Range(Mathf.Min(a, b), Mathf.Max(a, b));
            // 플레이어가 서 있는 자리는 피해서 이동을 유도
            if (!float.IsNaN(playerX) && Mathf.Abs(x - playerX) < avoidPlayerRadius)
            {
                float push = x >= playerX ? avoidPlayerRadius : -avoidPlayerRadius;
                x = Mathf.Clamp(playerX + push, minX, maxX);
            }
            result.Add(x);
        }
        result.Sort();
        // 겹침 방지: 매트 폭만큼 밀어냄
        for (int i = 1; i < result.Count; i++)
            if (result[i] - result[i - 1] < matWidth) result[i] = result[i - 1] + matWidth;
        // 오른쪽 끝을 넘으면 전체를 되돌림
        float over = result[result.Count - 1] - maxX;
        if (over > 0f) for (int i = 0; i < result.Count; i++) result[i] -= over;
        return result;
    }
}
