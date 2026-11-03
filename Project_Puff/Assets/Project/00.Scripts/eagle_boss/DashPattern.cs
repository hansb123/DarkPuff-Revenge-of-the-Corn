using System.Collections;
using UnityEngine;

/// <summary>
/// 독수리 돌진 공격. 에셋 하나가 돌진 한 종류다 (같은 스크립트로 세 가지를 만든다).
///  - 돌진 1: Style = Horizontal, Parry 끔 → 뒤로 물러났다가 플레이어 높이로 수평 돌진. 점프나 이동으로 피함
///  - 돌진 2: Style = Diagonal,   Parry 끔 → 하늘에서 비스듬히 내리꽂음. 바닥에서 옆으로 피함
///  - 돌진 3: Style = Horizontal, Parry 켬, Eye Flash 켬 → 눈이 번쩍인 뒤 더 빠르게 돌진. 돌진 중 독수리 몸을 때리면 패링 성공
/// 패링 성공: 독수리가 튕겨나며 멍해진다 (보스가 패링 횟수를 세어 그로기를 판단). 몸에 닿으면 데미지 + 패링 실패. 지나가면 회피.
/// 독수리 오브젝트에 EagleMotion 이 있어야 하고, 독수리 몸의 콜라이더가 Enemy 레이어여야 뻥/일반공격이 닿는다.
/// 에셋 만들기: Create → Darkpuff → Patterns → Eagle Dash
/// </summary>
[CreateAssetMenu(menuName = "Darkpuff/Patterns/Eagle Dash", fileName = "Pattern_Dash")]
public class DashPattern : AttackPattern
{
    public enum Style { Horizontal, Diagonal }

    [Header("종류")]
    [SerializeField] private Style style = Style.Horizontal;
    [Tooltip("켜면 패링할 수 있는 돌진 (몸을 맞히면 패링 성공)")]
    [SerializeField] private bool parry = false;
    [Tooltip("돌진 직전 눈이 번쩍임 (패링 돌진의 신호)")]
    [SerializeField] private bool eyeFlash = false;

    [Header("난이도별 수치  (왼쪽 = 쉬움 / 오른쪽 = 가장 어려움)")]
    [Tooltip("연속 돌진 횟수")]
    [SerializeField] private int countEasy = 1;
    [SerializeField] private int countHard = 2;
    [Tooltip("자세 잡는 시간(초). 짧을수록 어려움 (패링 돌진은 눈 번쩍임이 이 안에 들어 있음)")]
    [SerializeField] private float windupEasy = 0.9f;
    [SerializeField] private float windupHard = 0.55f;
    [Tooltip("돌진 속도 (유닛/초)")]
    [SerializeField] private float speedEasy = 16f;
    [SerializeField] private float speedHard = 22f;
    [Tooltip("돌진과 돌진 사이 쉬는 시간(초)")]
    [SerializeField] private float gapEasy = 0.6f;
    [SerializeField] private float gapHard = 0.25f;

    [Header("공통")]
    [Tooltip("뒤로 물러나는 거리")]
    [SerializeField] private float backOff = 4f;
    [Tooltip("플레이어를 지나쳐서 더 가는 거리")]
    [SerializeField] private float overshoot = 6f;
    [Tooltip("수평 돌진의 최소 높이 (바닥 기준). 낮을수록 점프로 피하기 쉬움")]
    [SerializeField] private float minHeight = 1.2f;
    [Tooltip("몸 판정 반경")]
    [SerializeField] private float hitRadius = 1.1f;
    [SerializeField] private int damage = 1;
    [SerializeField] private LayerMask playerLayer;

    [Header("패링당했을 때")]
    [SerializeField] private float recoil = 3f;
    [Tooltip("패링당한 뒤 포효하며 멈춰 있는 시간(초). 길수록 포효가 길고 패링 보상 시간이 늘어남")]
    [SerializeField] private float stun = 0.9f;
    [Tooltip("플레이어가 돌진을 피했을 때 다음 돌진까지 간격 배율. 작을수록 곧바로 몰아침 (1 = 그대로)")]
    [Range(0.1f, 1f)][SerializeField] private float dodgedGapScale = 0.4f;
    [SerializeField] private float returnTime = 0.8f;

    public override IEnumerator Run(PatternContext ctx)
    {
        var motion = ctx.Boss != null ? ctx.Boss.GetComponent<EagleMotion>() : null;
        if (motion == null)
        {
            Debug.LogWarning("[DashPattern] 독수리에 EagleMotion 이 필요합니다.", this);
            yield break;
        }

        int count = ctx.Lerp(countEasy, countHard);
        float gap = ctx.Lerp(gapEasy, gapHard);

        for (int i = 0; i < count; i++)
        {
            var s = new EagleMotion.DashSettings
            {
                diagonal = style == Style.Diagonal,
                parry = parry,
                eyeFlash = eyeFlash,
                windup = ctx.Lerp(windupEasy, windupHard),
                backOff = backOff,
                speed = ctx.Lerp(speedEasy, speedHard),
                overshoot = overshoot,
                minHeight = minHeight,
                hitRadius = hitRadius,
                playerLayer = playerLayer,
                damage = damage,
                recoil = recoil,
                stun = stun,
                returnTime = returnTime,
            };

            yield return motion.DashRoutine(s);

            // 패링 당했으면 멍해진 상태에서 연속 돌진을 이어가지 않고 끝낸다
            if (motion.LastDashResult == EagleMotion.DashResult.Parried) break;
            if (i < count - 1)
            {
                bool dodged = motion.LastDashResult == EagleMotion.DashResult.Dodged;   // 피했으면 곧바로 몰아침
                yield return new WaitForSeconds(dodged ? gap * dodgedGapScale : gap);
            }
        }

        yield return new WaitForSeconds(0.2f);
    }
}