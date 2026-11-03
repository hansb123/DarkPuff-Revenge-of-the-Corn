using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 독수리 일반 패턴: 낙뢰 레인. 무대를 여러 칸으로 나누고, 안전한 칸만 남기고 나머지에 번개 경고가 뜬다. 안전한 칸으로 피하면 된다.
/// 난이도가 오를수록 라운드가 늘고, 안전한 칸이 줄고, 예고 시간이 짧아진다.
/// 높은 난이도에서는 '지금 플레이어가 서 있는 칸'이 반드시 위험해져 가만히 있을 수 없다.
/// 에셋 만들기: Create → Darkpuff → Patterns → Eagle Lightning Lane
/// </summary>
[CreateAssetMenu(menuName = "Darkpuff/Patterns/Eagle Lightning Lane", fileName = "Pattern_LightningLane")]
public class LightningLanePattern : AttackPattern
{
    [SerializeField] private LightningStrike strikePrefab;

    [Header("난이도별 수치  (왼쪽 = 쉬움 / 오른쪽 = 가장 어려움)")]
    [SerializeField] private int roundsEasy = 2;
    [SerializeField] private int roundsHard = 4;
    [Tooltip("안전한 칸 수 (적을수록 어려움)")]
    [SerializeField] private int safeLanesEasy = 3;
    [SerializeField] private int safeLanesHard = 2;
    [Tooltip("예고 시간(초)")]
    [SerializeField] private float warnEasy = 1.3f;
    [SerializeField] private float warnHard = 0.7f;
    [Tooltip("라운드 사이 쉬는 시간(초)")]
    [SerializeField] private float betweenEasy = 0.6f;
    [SerializeField] private float betweenHard = 0.25f;

    [Header("칸")]
    [Tooltip("한 칸의 대략적인 폭 (무대 폭에 맞춰 칸 수가 정해짐)")]
    [SerializeField] private float laneWidth = 2.2f;
    [SerializeField] private float edgeMargin = 0.3f;
    [Tooltip("번개 기둥의 높이 (바닥 기준)")]
    [SerializeField] private float boltHeight = 12f;
    [Tooltip("난이도가 이 값 이상이면 플레이어가 서 있는 칸은 항상 위험")]
    [Range(0f, 1f)] [SerializeField] private float forceMoveFrom = 0.3f;

    public override IEnumerator Run(PatternContext ctx)
    {
        if (strikePrefab == null)
        {
            Debug.LogWarning("[LightningLanePattern] Strike Prefab이 비어 있습니다.", this);
            yield break;
        }

        var pool = ctx.GetPool(strikePrefab, (s, p) => s.SetReleaser(x => p.Release(x)));

        float left = ctx.LeftX + edgeMargin;
        float width = Mathf.Max(laneWidth * 3f, ctx.RightX - ctx.LeftX - edgeMargin * 2f);
        int lanes = Mathf.Max(3, Mathf.FloorToInt(width / laneWidth));
        float w = width / lanes;

        int rounds = ctx.Lerp(roundsEasy, roundsHard);
        int safe = Mathf.Clamp(ctx.Lerp(safeLanesEasy, safeLanesHard), 1, lanes - 1);
        float warn = ctx.Lerp(warnEasy, warnHard);
        float between = ctx.Lerp(betweenEasy, betweenHard);

        EaglePose.Raise(EaglePose.Id.Roar);
        GameSfx.Play(GameSfxId.EagleScreech);
        yield return new WaitForSeconds(0.5f);

        var order = new List<int>(lanes);
        for (int r = 0; r < rounds; r++)
        {
            order.Clear();
            for (int i = 0; i < lanes; i++) order.Add(i);
            for (int i = lanes - 1; i > 0; i--)   // 섞기
            {
                int j = Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            // order[0..safe-1] = 안전한 칸. 플레이어 칸이 안전이면 위험한 칸과 맞바꿈
            if (ctx.Player != null && ctx.Difficulty >= forceMoveFrom)
            {
                int pl = Mathf.Clamp(Mathf.FloorToInt((ctx.Player.position.x - left) / w), 0, lanes - 1);
                int idx = order.IndexOf(pl);
                if (idx >= 0 && idx < safe)
                {
                    int swapWith = Random.Range(safe, lanes);
                    (order[idx], order[swapWith]) = (order[swapWith], order[idx]);
                }
            }

            for (int k = safe; k < lanes; k++)
            {
                float cx = left + (order[k] + 0.5f) * w;
                var s = pool.Get();
                s.Launch(ctx.Runner, cx, ctx.GroundY, w, warn, ctx.GroundY + boltHeight);
            }

            yield return new WaitForSeconds(warn + 0.35f + between);
        }

        EaglePose.Raise(EaglePose.Id.Idle);
        yield return new WaitForSeconds(0.2f);
    }
}
