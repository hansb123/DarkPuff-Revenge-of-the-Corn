using System.Collections;
using UnityEngine;

/// <summary>
/// 메카 닭 패턴 1: 눈 레이저. 눈에서 조준선이 나와 플레이어를 따라다니다 멈추고(깜빡 + 삑삑), 곧 빔을 쏜다.
/// 난이도가 오를수록 횟수가 늘고, 충전/고정 시간이 줄고, 따라오는 속도가 빨라지고, 발사 중에도 천천히 따라오며,
/// 높은 난이도에서는 양쪽 눈이 동시에 쏜다 (한쪽은 플레이어, 다른 쪽은 옆으로 비껴 겨냥해 양쪽에서 조여 옴).
/// 에셋 만들기: Project 창 우클릭 → Create → Darkpuff → Patterns → Laser
/// </summary>
[CreateAssetMenu(menuName = "Darkpuff/Patterns/Laser", fileName = "Pattern_Laser")]
public class LaserPattern : AttackPattern
{
    [Header("프리팹")]
    [SerializeField] private LaserBeam beamPrefab;

    [Header("난이도별 수치  (왼쪽 = 쉬움 / 오른쪽 = 가장 어려움)")]
    [Tooltip("연속으로 쏘는 횟수")]
    [SerializeField] private int shotsEasy = 2;
    [SerializeField] private int shotsHard = 4;
    [Tooltip("충전: 조준선이 따라다니는 시간(초). laser_charge 소리가 1.1초")]
    [SerializeField] private float chargeEasy = 1.1f;
    [SerializeField] private float chargeHard = 0.8f;
    [Tooltip("고정: 멈춰서 깜빡이는 시간(초) = 마지막으로 피할 여유")]
    [SerializeField] private float lockEasy = 0.6f;
    [SerializeField] private float lockHard = 0.32f;
    [Tooltip("발사 시간(초)")]
    [SerializeField] private float fireEasy = 0.6f;
    [SerializeField] private float fireHard = 0.9f;
    [Tooltip("충전 중 조준선이 따라가는 각속도(도/초)")]
    [SerializeField] private float trackEasy = 45f;
    [SerializeField] private float trackHard = 95f;
    [Tooltip("발사 중 따라가는 각속도(도/초). 0이면 쏜 자리에 고정")]
    [SerializeField] private float sweepEasy = 0f;
    [SerializeField] private float sweepHard = 18f;
    [Tooltip("한 발이 끝난 뒤 다음 발까지 쉬는 시간(초)")]
    [SerializeField] private float gapEasy = 0.6f;
    [SerializeField] private float gapHard = 0.25f;

    [Header("양쪽 눈 동시 발사")]
    [Tooltip("난이도가 이 값을 넘으면 양쪽 눈이 동시에 쏨 (0~1)")]
    [Range(0f, 1f)] [SerializeField] private float doubleFrom = 0.55f;
    [Tooltip("두 번째 눈이 플레이어에서 비껴 겨냥하는 좌우 거리 (최소, 최대)")]
    [SerializeField] private Vector2 pincerOffset = new Vector2(2.5f, 4.5f);

    public override IEnumerator Run(PatternContext ctx)
    {
        if (beamPrefab == null)
        {
            Debug.LogWarning("[LaserPattern] Beam Prefab이 비어 있습니다.", this);
            yield break;
        }
        var eyes = Object.FindFirstObjectByType<MechaEyes>();
        if (eyes == null || !eyes.IsValid)
        {
            Debug.LogWarning("[LaserPattern] 씬에서 MechaEyes(왼쪽/오른쪽 눈 연결)를 찾지 못했습니다.", this);
            yield break;
        }

        var pool = ctx.GetPool(beamPrefab, (b, p) => b.SetReleaser(x => p.Release(x)));

        int shots = ctx.Lerp(shotsEasy, shotsHard);
        bool doubled = ctx.Difficulty >= doubleFrom;
        var s = new LaserBeam.Settings
        {
            chargeTime = ctx.Lerp(chargeEasy, chargeHard),
            lockTime = ctx.Lerp(lockEasy, lockHard),
            fireTime = ctx.Lerp(fireEasy, fireHard),
            trackDegPerSec = ctx.Lerp(trackEasy, trackHard),
            sweepDegPerSec = ctx.Lerp(sweepEasy, sweepHard),
            groundY = ctx.GroundY,
        };
        float gap = ctx.Lerp(gapEasy, gapHard);
        float total = s.chargeTime + s.lockTime + s.fireTime;

        GameSfx.Play(GameSfxId.MechaAlarm);

        for (int i = 0; i < shots; i++)
        {
            if (doubled)
            {
                // 한쪽 눈은 플레이어를, 다른 눈은 옆으로 비껴서 겨냥 (조여 오는 느낌)
                int mainEye = i % 2;
                float side = Random.value < 0.5f ? -1f : 1f;
                var b1 = pool.Get(); s.aimOffsetX = 0f;
                b1.Launch(ctx.Runner, eyes.Get(mainEye), ctx.Player, mainEye, s);
                var b2 = pool.Get(); s.aimOffsetX = side * Random.Range(pincerOffset.x, pincerOffset.y);
                b2.Launch(ctx.Runner, eyes.Get(1 - mainEye), ctx.Player, 1 - mainEye, s);
            }
            else
            {
                int eye = i % 2;
                var b = pool.Get(); s.aimOffsetX = 0f;
                b.Launch(ctx.Runner, eyes.Get(eye), ctx.Player, eye, s);
            }
            yield return new WaitForSeconds(total + gap);
        }

        yield return new WaitForSeconds(0.3f);
    }
}
