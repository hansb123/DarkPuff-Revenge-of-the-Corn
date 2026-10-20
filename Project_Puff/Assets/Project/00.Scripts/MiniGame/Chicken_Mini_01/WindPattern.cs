using System.Collections;
using UnityEngine;

/// <summary>
/// 패턴 3: 날개바람. 닭이 날개를 퍼덕이면 한쪽 끝에서 반대쪽 끝까지 바닥을 훑는 돌풍이 지나간다. 점프로 넘어야 한다.
/// 난이도가 오를수록 횟수가 늘고, 바람이 빨라지고, 예고 시간이 줄고, 간격이 짧아지며,
/// 높은 난이도에서는 양쪽에서 동시에 불어와 가운데서 만난다.
/// 에셋 만들기: Project 창 우클릭 → Create → Darkpuff → Patterns → Wind
/// </summary>
[CreateAssetMenu(menuName = "Darkpuff/Patterns/Wind", fileName = "Pattern_Wind")]
public class WindPattern : AttackPattern
{
    [Header("프리팹")]
    [SerializeField] private GustWave gustPrefab;

    [Header("난이도별 수치  (왼쪽 = 쉬움 / 오른쪽 = 가장 어려움)")]
    [SerializeField] private int countEasy = 2;
    [SerializeField] private int countHard = 5;
    [Tooltip("예고 시간(초). 짧을수록 어려움")]
    [SerializeField] private float warnEasy = 1.1f;
    [SerializeField] private float warnHard = 0.6f;
    [Tooltip("바람 속도 (유닛/초)")]
    [SerializeField] private float speedEasy = 7f;
    [SerializeField] private float speedHard = 13f;
    [Tooltip("바람이 지나간 뒤 다음 바람 예고까지 쉬는 시간(초)")]
    [SerializeField] private float gapEasy = 0.9f;
    [SerializeField] private float gapHard = 0.3f;

    [Header("방향")]
    [Tooltip("켜면 왼쪽 → 오른쪽 → 왼쪽… 번갈아 분다. 끄면 매번 랜덤")]
    [SerializeField] private bool alternateSides = true;
    [Tooltip("난이도가 이 값 이상이면 양쪽에서 동시에 분다 (1이면 사용 안 함)")]
    [Range(0f, 1f)] [SerializeField] private float bothSidesFrom = 0.8f;

    public override IEnumerator Run(PatternContext ctx)
    {
        if (gustPrefab == null)
        {
            Debug.LogWarning("[WindPattern] Gust Prefab이 비어 있습니다.", this);
            yield break;
        }

        var pool = ctx.GetPool(gustPrefab, (g, p) => g.SetReleaser(x => p.Release(x)));

        int count = ctx.Lerp(countEasy, countHard);
        float warnT = ctx.Lerp(warnEasy, warnHard);
        float speed = ctx.Lerp(speedEasy, speedHard);
        float gap = ctx.Lerp(gapEasy, gapHard);
        bool both = ctx.Difficulty >= bothSidesFrom;

        float width = Mathf.Max(1f, ctx.RightX - ctx.LeftX);
        float travel = width / speed + 0.4f;   // 바람이 한쪽에서 다른 쪽까지 가는 데 걸리는 시간

        int dir = Random.value < 0.5f ? -1 : 1;

        for (int i = 0; i < count; i++)
        {
            var a = pool.Get();
            a.Launch(ctx.Runner, dir, ctx.LeftX, ctx.RightX, ctx.GroundY, warnT, speed);

            if (both)
            {
                var b = pool.Get();
                b.Launch(ctx.Runner, -dir, ctx.LeftX, ctx.RightX, ctx.GroundY, warnT, speed);
            }

            yield return new WaitForSeconds(warnT + travel + gap);

            dir = alternateSides ? -dir : (Random.value < 0.5f ? -1 : 1);
        }

        yield return new WaitForSeconds(0.3f);
    }
}
