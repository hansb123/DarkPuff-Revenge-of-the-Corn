using System.Collections;
using UnityEngine;

/// <summary>
/// 패링 공격 패턴: 독수리가 황금 깃털을 쏜다. 한 발씩 결과(성공/피격/회피)가 ParryEvents 로 전해진다.
/// 이 패턴은 결과와 상관없이 모든 투사체가 정리되면 끝난다 → 다음 패턴은 보스 트리가 고른다.
///
/// 두 가지 방식
///  - 곡선 모드 (Curved, 독수리 2페이즈~): 독수리 몸(Boss)에서 나와 위로 솟았다가 → 꼭대기에서 잠깐 멈춰 조준 → 플레이어에게 내리꽂힌다.
///    예고는 독수리 포즈(windup → swing)로 한다. 꼭대기에서 멈춘 순간부터 패링할 수 있다.
///  - 직선 모드 (Curved 끔): 화면 위쪽에서 플레이어를 향해 날아옴 (예전 방식, 닭 보스용)
/// 에셋 만들기: Project 창 우클릭 → Create → Darkpuff → Patterns → Parry Strike
/// </summary>
[CreateAssetMenu(menuName = "Darkpuff/Patterns/Parry Strike", fileName = "Pattern_ParryStrike")]
public class ParryStrikePattern : AttackPattern
{
    [SerializeField] private ParryProjectile projectilePrefab;

    [Header("난이도별 수치  (왼쪽 = 쉬움 / 오른쪽 = 가장 어려움)")]
    [Tooltip("연속으로 쏘는 발 수")]
    [SerializeField] private int countEasy = 1;
    [SerializeField] private int countHard = 3;
    [Tooltip("직선 모드: 예고 시간(초). 곡선 모드에서는 쓰지 않음")]
    [SerializeField] private float telegraphEasy = 0.8f;
    [SerializeField] private float telegraphHard = 0.45f;
    [Tooltip("날아가는(내리꽂는) 속도")]
    [SerializeField] private float speedEasy = 9f;
    [SerializeField] private float speedHard = 14f;
    [Tooltip("발과 발 사이 간격(초)")]
    [SerializeField] private float gapEasy = 0.9f;
    [SerializeField] private float gapHard = 0.5f;

    [Header("곡선 모드 (독수리)")]
    [SerializeField] private bool curved = true;
    [Tooltip("쏘기 전 독수리가 자세를 잡는 시간(초) = windup 포즈. 짧을수록 어려움")]
    [SerializeField] private float windupEasy = 0.7f;
    [SerializeField] private float windupHard = 0.4f;
    [Tooltip("솟아오르는 시간(초). 이 동안은 패링 불가")]
    [SerializeField] private float riseTimeEasy = 0.55f;
    [SerializeField] private float riseTimeHard = 0.38f;
    [Tooltip("꼭대기에서 멈춰 조준하는 시간(초) = 패링/회피를 판단할 시간. 짧을수록 어려움")]
    [SerializeField] private float apexPauseEasy = 0.5f;
    [SerializeField] private float apexPauseHard = 0.28f;
    [Tooltip("솟는 높이 / 플레이어 쪽으로 휘는 폭")]
    [SerializeField] private float riseHeight = 3.5f;
    [SerializeField] private float riseSide = 2.5f;
    [Tooltip("독수리 중심에서 깃털이 나오는 위치 (X 는 플레이어 쪽이 +)")]
    [SerializeField] private Vector2 muzzleOffset = new Vector2(1.0f, -0.3f);

    [Header("직선 모드 위치")]
    [Tooltip("투사체가 나타나는 높이 (바닥 기준)")]
    [SerializeField] private float spawnHeight = 5f;
    [SerializeField] private float spawnEdgeMargin = 1.0f;
    [SerializeField] private float maxDistance = 20f;

    public override IEnumerator Run(PatternContext ctx)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("[ParryStrikePattern] Projectile Prefab이 비어 있습니다.", this);
            yield break;
        }

        var pool = ctx.GetPool(projectilePrefab, (p, pl) => p.SetReleaser(x => pl.Release(x)));

        int count = ctx.Lerp(countEasy, countHard);
        float speed = ctx.Lerp(speedEasy, speedHard);
        float gap = ctx.Lerp(gapEasy, gapHard);
        bool fromBoss = curved && ctx.Boss != null;

        int pending = 0;

        for (int i = 0; i < count; i++)
        {
            if (fromBoss)
            {
                // 독수리 자세 잡기 (예고) → 쏘기
                EaglePose.Raise(EaglePose.Id.Windup);
                yield return new WaitForSeconds(ctx.Lerp(windupEasy, windupHard));

                float side = ctx.Player != null && ctx.Player.position.x < ctx.Boss.position.x ? -1f : 1f;
                Vector2 start = (Vector2)ctx.Boss.position + new Vector2(muzzleOffset.x * side, muzzleOffset.y);

                EaglePose.Raise(EaglePose.Id.Strike);
                GameSfx.Play(GameSfxId.EagleWingFlap);

                pending++;
                var p = pool.Get();
                p.LaunchCurved(ctx.Runner, start, ctx.Player, riseSide, riseHeight,
                               ctx.Lerp(riseTimeEasy, riseTimeHard), ctx.Lerp(apexPauseEasy, apexPauseHard),
                               speed, maxDistance, () => pending--);
            }
            else
            {
                float x = Random.Range(ctx.LeftX + spawnEdgeMargin, ctx.RightX - spawnEdgeMargin);
                Vector2 start = new Vector2(x, ctx.GroundY + spawnHeight);
                Vector2 target = ctx.Player != null ? (Vector2)ctx.Player.position : new Vector2(x, ctx.GroundY);

                pending++;
                var p = pool.Get();
                p.Launch(ctx.Runner, start, target, ctx.Lerp(telegraphEasy, telegraphHard), speed, maxDistance, () => pending--);
            }

            yield return new WaitForSeconds(gap);
        }

        while (pending > 0) yield return null;   // 마지막 발이 성공/피격/회피로 끝날 때까지
        if (fromBoss) EaglePose.Raise(EaglePose.Id.Idle);
        yield return new WaitForSeconds(0.3f);
    }
}
