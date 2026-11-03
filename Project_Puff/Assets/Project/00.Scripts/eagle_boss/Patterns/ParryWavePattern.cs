using System.Collections;
using UnityEngine;

/// <summary>
/// 패링 패턴: 금빛 충격파 쳐내기. 독수리가 화면 끝에 내려앉아 날개를 젖히고(눈이 번쩍) → 금빛 충격파 한 발을 쏜다.
///  - 뻥/기본 공격으로 쳐내면 충격파가 보스 쪽으로 튕겨 나가 보스를 맞힌다 → 패링 성공 (보스는 휘청이며 포효)
///  - 못 쳐내고 맞으면 데미지 + 패링 실패, 점프로 넘기면 회피
/// 에셋 만들기: Project 창 우클릭 → Create → Darkpuff → Patterns → Parry Wave
/// 충격파 프리팹은 GroundShockwave 이고, 오브젝트 Layer 를 Enemy 로 해야 플레이어 공격이 맞는다.
/// </summary>
[CreateAssetMenu(menuName = "Darkpuff/Patterns/Parry Wave", fileName = "Pattern_ParryWave")]
public class ParryWavePattern : AttackPattern
{
    [SerializeField] private GroundShockwave wavePrefab;

    [Header("난이도별 수치  (왼쪽 = 쉬움 / 오른쪽 = 가장 어려움)")]
    [Tooltip("날개를 젖히는 예고 시간(초). 눈 번쩍임이 이 안에 들어 있음. 짧을수록 어려움")]
    [SerializeField] private float windupEasy = 1.0f;
    [SerializeField] private float windupHard = 0.65f;
    [Tooltip("충격파 속도 (유닛/초). 빠를수록 쳐낼 시간이 짧음")]
    [SerializeField] private float speedEasy = 6f;
    [SerializeField] private float speedHard = 9f;
    [Tooltip("충격파 높이")]
    [SerializeField] private float heightEasy = 1.1f;
    [SerializeField] private float heightHard = 1.3f;

    [Header("충격파 모양")]
    [SerializeField] private float visualWidth = 3.4f;
    [SerializeField] private float startOffset = 1.2f;

    [Header("독수리 움직임")]
    [SerializeField] private float edgeInset = 1.5f;
    [SerializeField] private float approachTime = 0.7f;
    [SerializeField] private float settle = 0.35f;
    [Tooltip("충격파가 끝난 뒤 머무는 시간")]
    [SerializeField] private float hold = 0.4f;
    [SerializeField] private float returnTime = 0.6f;
    [SerializeField] private float landOffset = 0f;

    [Header("패링 당했을 때")]
    [Tooltip("휘청이며 밀려나는 거리")]
    [SerializeField] private float recoil = 2.5f;
    [Tooltip("포효하며 멈춰 있는 시간(초) = 보상 시간")]
    [SerializeField] private float stun = 0.9f;

    public override IEnumerator Run(PatternContext ctx)
    {
        var motion = ctx.Boss != null ? ctx.Boss.GetComponent<EagleMotion>() : null;
        if (motion == null || wavePrefab == null)
        {
            Debug.LogWarning("[ParryWavePattern] 독수리에 EagleMotion 과 Wave Prefab 이 필요합니다.", this);
            yield break;
        }

        var pool = ctx.GetPool(wavePrefab, (w, pl) => w.SetReleaser(x => pl.Release(x)));

        var s = new EagleMotion.SweepSettings
        {
            edgeInset = edgeInset,
            approachTime = approachTime,
            settle = settle,
            windup = ctx.Lerp(windupEasy, windupHard),
            hold = hold,
            returnTime = returnTime,
            landOffset = landOffset,
        };

        // 플레이어에서 먼 쪽 끝에 내려앉아 플레이어 쪽으로 쏨
        float mid = (ctx.LeftX + ctx.RightX) * 0.5f;
        float px = ctx.Player != null ? ctx.Player.position.x : mid;
        float dirX = px < mid ? -1f : 1f;

        yield return motion.SweepLand(s, dirX, true);   // 날개 젖히기 + 눈 번쩍

        motion.SweepStrike();
        EaglePose.RaiseEyeFlash(false);

        bool done = false;
        GroundShockwave.Result result = GroundShockwave.Result.Dodged;
        var w = pool.Get();
        float endX = dirX > 0f ? ctx.RightX + visualWidth : ctx.LeftX - visualWidth;
        w.LaunchParry(ctx.Runner, ctx.Boss, motion.transform.position.x + dirX * startOffset, dirX, ctx.GroundY,
                      ctx.Lerp(speedEasy, speedHard), ctx.Lerp(heightEasy, heightHard), visualWidth, endX,
                      r => { result = r; done = true; });

        while (!done) yield return null;

        if (result == GroundShockwave.Result.Parried)
            yield return motion.SweepStagger(-dirX, recoil, stun);   // 맞은 충격으로 가장자리 쪽으로 밀려남

        yield return motion.SweepLeave(s);
        yield return new WaitForSeconds(0.2f);
    }
}