using System.Collections;
using UnityEngine;

/// <summary>
/// 3페이즈 일반 패턴: 급강하 + 충격파. 그림자 예고 → 내리꽂기(몸에 닿으면 데미지) → 착지 순간 양옆으로 낮은 충격파가 달린다.
/// 급강하를 피해도 충격파를 점프로 넘어야 한다. 난이도가 오르면 횟수가 늘고 빨라진다.
/// 마지막 급강하에서 "금빛 충격파"를 켜 두면(Gold On Last Dive), 플레이어가 있는 쪽 충격파 하나가 금빛(패링용)으로 나간다:
/// 쳐내면 독수리 쪽으로 튕겨 가서 패링 성공, 못 쳐내면 데미지 + 패링 실패. 금빛이 나오는 급강하는 내리꽂기 전에 눈이 번쩍인다.
/// 에셋 만들기: Create → Darkpuff → Patterns → Eagle Dive Shockwave
/// 충격파 프리팹은 GroundShockwave (금빛용은 Layer 를 Enemy 로).
/// </summary>
[CreateAssetMenu(menuName = "Darkpuff/Patterns/Eagle Dive Shockwave", fileName = "Pattern_DiveShockwave")]
public class DiveShockwavePattern : AttackPattern
{
    [SerializeField] private GroundShockwave wavePrefab;
    [Tooltip("금빛(패링) 충격파 프리팹. Gold On Last Dive 를 쓰지 않으면 비워도 됨")]
    [SerializeField] private GroundShockwave parryWavePrefab;
    [Tooltip("마지막 급강하 때 플레이어 쪽 충격파 하나를 금빛(패링)으로")]
    [SerializeField] private bool goldOnLastDive = false;
    [Tooltip("이 난이도 이상일 때만 금빛이 나옴")]
    [Range(0f, 1f)][SerializeField] private float goldMinDifficulty = 0.5f;

    [Header("난이도별 수치  (왼쪽 = 쉬움 / 오른쪽 = 가장 어려움)")]
    [SerializeField] private int divesEasy = 1;
    [SerializeField] private int divesHard = 2;
    [Tooltip("그림자 예고 시간(초)")]
    [SerializeField] private float windupEasy = 1.0f;
    [SerializeField] private float windupHard = 0.65f;
    [SerializeField] private float diveEasy = 0.36f;
    [SerializeField] private float diveHard = 0.24f;
    [Tooltip("급강하 사이 쉬는 시간(초)")]
    [SerializeField] private float betweenEasy = 0.8f;
    [SerializeField] private float betweenHard = 0.4f;
    [Tooltip("그림자 위치에 도착한 뒤, 내리꽂기 전까지 떠서 기다리는 시간(초). 길수록 여유 있음")]
    [SerializeField] private float lockHoldEasy = 0.6f;
    [SerializeField] private float lockHoldHard = 0.4f;
    [Tooltip("충격파 속도 (유닛/초)")]
    [SerializeField] private float speedEasy = 5.5f;
    [SerializeField] private float speedHard = 8.5f;
    [Tooltip("충격파 높이. 이보다 높이 뛰면 피함")]
    [SerializeField] private float heightEasy = 0.8f;
    [SerializeField] private float heightHard = 1.0f;
    [Tooltip("착지하고 충격파가 나오기까지 기다리는 시간(초). 급강하를 피한 뒤 충격파를 넘을 준비 시간. 짧을수록 어려움")]
    [SerializeField] private float waveDelayEasy = 0.55f;
    [SerializeField] private float waveDelayHard = 0.35f;

    [Header("급강하")]
    [Tooltip("바닥에 머무는 시간(초). 금빛 충격파를 쏘는 급강하는 이 시간 동안 독수리가 바닥에 있음")]
    [SerializeField] private float hold = 0.5f;
    [SerializeField] private float returnTime = 0.8f;
    [SerializeField] private float impactHeight = 1.6f;
    [SerializeField] private float hitRadius = 1.5f;
    [SerializeField] private int damage = 1;
    [SerializeField] private LayerMask playerLayer;

    [Header("충격파 모양")]
    [SerializeField] private float visualWidth = 3.2f;
    [Tooltip("착지 지점에서 얼마나 떨어져서 시작할지")]
    [SerializeField] private float startOffset = 2.6f;

    [Header("패링당했을 때")]
    [Tooltip("제자리에서 포효하며 멈추는 시간(초) = 보상 시간")]
    [SerializeField] private float stun = 0.9f;

    public override IEnumerator Run(PatternContext ctx)
    {
        var motion = ctx.Boss != null ? ctx.Boss.GetComponent<EagleMotion>() : null;
        if (motion == null || wavePrefab == null)
        {
            Debug.LogWarning("[DiveShockwavePattern] 독수리에 EagleMotion 과 Wave Prefab 이 필요합니다.", this);
            yield break;
        }

        var pool = ctx.GetPool(wavePrefab, (w, pl) => w.SetReleaser(x => pl.Release(x)));
        PrefabPool<GroundShockwave> parryPool = parryWavePrefab != null
            ? ctx.GetPool(parryWavePrefab, (w, pl) => w.SetReleaser(x => pl.Release(x))) : null;

        int dives = Mathf.Max(1, ctx.Lerp(divesEasy, divesHard));
        float windup = ctx.Lerp(windupEasy, windupHard);
        float dive = ctx.Lerp(diveEasy, diveHard);
        float between = ctx.Lerp(betweenEasy, betweenHard);
        float speed = ctx.Lerp(speedEasy, speedHard);
        float height = ctx.Lerp(heightEasy, heightHard);

        bool parried = false;
        int parryAlive = 0;

        for (int i = 0; i < dives; i++)
        {
            bool gold = goldOnLastDive && parryPool != null && i == dives - 1 && ctx.Difficulty >= goldMinDifficulty;
            if (gold) EaglePose.RaiseEyeFlash(true);   // 금빛이 나온다는 신호 (그림자 예고 내내 번쩍)

            yield return motion.DiveRoutine(windup, dive, hold, returnTime, impactHeight, hitRadius, playerLayer, damage,
              impactX => EaglePose.RaiseEyeFlash(false),
              impactX =>
              {
                  float playerX = ctx.Player != null ? ctx.Player.position.x : impactX;
                  float goldDir = playerX < impactX ? -1f : 1f;   // 금빛은 플레이어가 있는 쪽으로
                  for (int sd = -1; sd <= 1; sd += 2)
                  {
                      float d = sd;
                      float endX = d > 0f ? ctx.RightX + visualWidth : ctx.LeftX - visualWidth;
                      float sx = impactX + d * startOffset;
                      if (gold && d == goldDir)
                      {
                          parryAlive++;
                          var pw = parryPool.Get();
                          pw.LaunchParry(ctx.Runner, ctx.Boss, sx, d, ctx.GroundY, speed * 0.85f, height, visualWidth, endX,
                                         r => { parryAlive--; if (r == GroundShockwave.Result.Parried) parried = true; });
                      }
                      else
                      {
                          var w = pool.Get();
                          w.Launch(ctx.Runner, sx, d, ctx.GroundY, speed, height, visualWidth, endX);
                      }
                  }
              }, ctx.Lerp(waveDelayEasy, waveDelayHard), ctx.Lerp(lockHoldEasy, lockHoldHard));

            if (i < dives - 1) yield return new WaitForSeconds(between);
        }

        // 금빛 충격파 결과를 기다렸다가, 쳐냈다면 제자리에서 휘청이며 포효
        while (parryAlive > 0) yield return null;
        EaglePose.RaiseEyeFlash(false);
        if (parried) yield return motion.RoarInPlace(stun);

        yield return new WaitForSeconds(0.2f);
    }
}