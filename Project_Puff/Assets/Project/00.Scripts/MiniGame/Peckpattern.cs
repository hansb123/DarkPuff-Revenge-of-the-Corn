using System.Collections;
using UnityEngine;

/// <summary>
/// 패턴 2: 쪼기. 닭이 부리로 바닥을 내리찍는다. 경고 기둥이 플레이어를 따라다니다 멈추고, 곧 그 자리를 찍는다.
/// 난이도가 오를수록 횟수가 늘고, 따라오는 속도는 빨라지고, 멈춘 뒤 피할 여유와 연속 간격이 줄어들며,
/// 높은 난이도에서는 한 번에 두 곳을 동시에 찍는다 (한 곳은 플레이어, 다른 곳은 근처 고정 위치).
/// 에셋 만들기: Project 창 우클릭 → Create → Darkpuff → Patterns → Peck
/// </summary>
[CreateAssetMenu(menuName = "Darkpuff/Patterns/Peck", fileName = "Pattern_Peck")]
public class PeckPattern : AttackPattern
{
    [Header("프리팹")]
    [SerializeField] private PeckStrike peckPrefab;

    [Header("난이도별 수치  (왼쪽 = 쉬움 / 오른쪽 = 가장 어려움)")]
    [Tooltip("연속으로 찍는 횟수")]
    [SerializeField] private int countEasy = 3;
    [SerializeField] private int countHard = 6;
    [Tooltip("경고 기둥이 플레이어를 따라다니는 시간(초)")]
    [SerializeField] private float trackTimeEasy = 0.8f;
    [SerializeField] private float trackTimeHard = 0.45f;
    [Tooltip("멈춘 뒤 찍기까지 시간(초) = 마지막으로 피할 여유. 짧을수록 어려움")]
    [SerializeField] private float lockTimeEasy = 0.6f;
    [SerializeField] private float lockTimeHard = 0.32f;
    [Tooltip("따라가는 속도 (유닛/초)")]
    [SerializeField] private float trackSpeedEasy = 3.5f;
    [SerializeField] private float trackSpeedHard = 7f;
    [Tooltip("찍은 뒤 다음 찍기까지 쉬는 시간(초)")]
    [SerializeField] private float gapEasy = 0.5f;
    [SerializeField] private float gapHard = 0.1f;

    [Header("동시에 두 곳 찍기")]
    [Tooltip("난이도가 이 값을 넘으면 매번 한 곳을 더 찍음 (0~1)")]
    [Range(0f, 1f)][SerializeField] private float doubleFrom = 0.6f;
    [Tooltip("추가로 찍는 곳이 플레이어에서 떨어진 거리 (최소, 최대)")]
    [SerializeField] private Vector2 extraOffset = new Vector2(2.5f, 4.5f);

    [Header("공통")]
    [Tooltip("무대 가장자리에서 이만큼 안쪽으로만 찍음")]
    [SerializeField] private float edgeMargin = 0.8f;

    public override IEnumerator Run(PatternContext ctx)
    {
        if (peckPrefab == null)
        {
            Debug.LogWarning("[PeckPattern] Peck Prefab이 비어 있습니다.", this);
            yield break;
        }

        var pool = ctx.GetPool(peckPrefab, (s, p) => s.SetReleaser(x => p.Release(x)));

        int count = ctx.Lerp(countEasy, countHard);
        float track = ctx.Lerp(trackTimeEasy, trackTimeHard);
        float lockT = ctx.Lerp(lockTimeEasy, lockTimeHard);
        float speed = ctx.Lerp(trackSpeedEasy, trackSpeedHard);
        float gap = ctx.Lerp(gapEasy, gapHard);
        bool doubled = ctx.Difficulty >= doubleFrom;

        float minX = ctx.LeftX + edgeMargin;
        float maxX = ctx.RightX - edgeMargin;

        GameSfx.Play(GameSfxId.ChickenPeckWarn);   // 쪼기 시작 신호 (똑똑똑)

        for (int i = 0; i < count; i++)
        {
            float startX = ctx.Player != null ? ctx.Player.position.x : (minX + maxX) * 0.5f;
            startX = Mathf.Clamp(startX, minX, maxX);

            // 플레이어를 따라다니는 한 곳
            var main = pool.Get();
            main.Launch(ctx.Runner, ctx.Player, startX, ctx.GroundY, track, lockT, speed, minX, maxX);

            // 높은 난이도: 근처 한 곳을 추가로 고정 위치에 찍음 (따라가지 않음)
            if (doubled)
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                float ex = Mathf.Clamp(startX + side * Random.Range(extraOffset.x, extraOffset.y), minX, maxX);
                var extra = pool.Get();
                extra.Launch(ctx.Runner, null, ex, ctx.GroundY, track, lockT, 0f, minX, maxX);
            }

            // 이번 찍기가 끝나고 쉬는 시간까지 기다린 뒤 다음으로
            yield return new WaitForSeconds(track + lockT + gap);
        }

        yield return new WaitForSeconds(0.4f);
    }
}