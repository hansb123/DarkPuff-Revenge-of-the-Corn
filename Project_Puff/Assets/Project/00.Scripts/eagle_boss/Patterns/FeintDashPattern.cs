using System.Collections;
using UnityEngine;

/// <summary>
/// 3페이즈 일반 패턴: 페이크 돌진 (3연속). 앞의 돌진들은 "가짜", 마지막 한 번만 진짜(패링 가능).
///  - 가짜 돌진 (1, 2타): 플레이어의 머리 위를 스치고 지나간다. 가만히 서 있으면 안 맞고,
///    놀라서 점프하거나 높이 뛰면 몸에 맞는다. 눈이 번쩍이지 않는다.
///  - 진짜 돌진 (마지막): 눈이 번쩍인 뒤 플레이어 높이로 돌진. 몸을 때려 쳐내면 패링 성공, 점프로 피하면 회피, 맞으면 데미지 + 패링 실패.
/// 읽는 법: "눈이 번쩍이지 않는 돌진은 머리 위로 지나간다 (서 있기), 눈이 번쩍이면 패링".
/// 독수리 몸 콜라이더가 Enemy 레이어여야 패링이 된다. 패링 성공 시 독수리가 튕겨나며 포효하고 그 뒤 돌진은 이어지지 않는다.
/// 에셋 만들기: Create → Darkpuff → Patterns → Eagle Feint Dash
/// </summary>
[CreateAssetMenu(menuName = "Darkpuff/Patterns/Eagle Feint Dash", fileName = "Pattern_FeintDash")]
public class FeintDashPattern : AttackPattern
{
    [Header("구성")]
    [Tooltip("가짜 돌진 횟수 (뒤에 진짜 돌진이 한 번 더 옴). 2 = 가짜, 가짜, 진짜")]
    [SerializeField] private int fakesEasy = 2;
    [SerializeField] private int fakesHard = 2;

    [Header("가짜 돌진 (머리 위로 지나감)")]
    [Tooltip("플레이어 높이보다 이만큼 위를 지나감. 서 있는 플레이어의 머리 위로는 넘고, 점프하면 닿는 높이로 맞출 것")]
    [SerializeField] private float fakeHeightOffset = 2.4f;
    [Tooltip("자세 잡는 시간(초). 이 동안 독수리 높이가 플레이어를 따라감: 미리 점프해도 따라와서 맞음")]
    [SerializeField] private float fakeWindupEasy = 0.75f;
    [SerializeField] private float fakeWindupHard = 0.5f;
    [SerializeField] private float fakeSpeedEasy = 15f;
    [SerializeField] private float fakeSpeedHard = 21f;

    [Header("진짜 돌진 (패링)")]
    [Tooltip("눈 번쩍임이 나오는 시점 (윈드업의 0~1 비율). 작을수록 일찍 알려줌")]
    [Range(0.05f, 0.9f)][SerializeField] private float realFlashAt = 0.2f;
    [SerializeField] private float realWindupEasy = 0.9f;
    [SerializeField] private float realWindupHard = 0.6f;
    [SerializeField] private float realSpeedEasy = 15f;
    [SerializeField] private float realSpeedHard = 20f;

    [Header("돌진 사이")]
    [Tooltip("돌진과 돌진 사이 쉬는 시간(초)")]
    [SerializeField] private float gapEasy = 0.6f;
    [SerializeField] private float gapHard = 0.3f;
    [Tooltip("앞 돌진을 피했을 때(스쳐 지나감) 간격 배율. 작을수록 곧바로 몰아침")]
    [Range(0.1f, 1f)][SerializeField] private float dodgedGapScale = 0.5f;

    [Header("공통")]
    [SerializeField] private float backOff = 4f;
    [SerializeField] private float overshoot = 6f;
    [SerializeField] private float minHeight = 1.2f;
    [SerializeField] private float hitRadius = 1.1f;
    [SerializeField] private int damage = 1;
    [SerializeField] private LayerMask playerLayer;

    [Header("패링당했을 때")]
    [SerializeField] private float recoil = 3f;
    [SerializeField] private float stun = 0.9f;
    [SerializeField] private float returnTime = 0.8f;

    public override IEnumerator Run(PatternContext ctx)
    {
        var motion = ctx.Boss != null ? ctx.Boss.GetComponent<EagleMotion>() : null;
        if (motion == null)
        {
            Debug.LogWarning("[FeintDashPattern] 독수리에 EagleMotion 이 필요합니다.", this);
            yield break;
        }

        int fakes = Mathf.Max(0, ctx.Lerp(fakesEasy, fakesHard));
        float gap = ctx.Lerp(gapEasy, gapHard);

        for (int i = 0; i <= fakes; i++)
        {
            bool real = i == fakes;   // 마지막이 진짜
            var s = new EagleMotion.DashSettings
            {
                diagonal = false,
                parry = real,
                eyeFlash = real,
                flashAt = real ? realFlashAt : 0f,
                aimYOffset = real ? 0f : fakeHeightOffset,
                windup = real ? ctx.Lerp(realWindupEasy, realWindupHard) : ctx.Lerp(fakeWindupEasy, fakeWindupHard),
                backOff = backOff,
                speed = real ? ctx.Lerp(realSpeedEasy, realSpeedHard) : ctx.Lerp(fakeSpeedEasy, fakeSpeedHard),
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

            if (motion.LastDashResult == EagleMotion.DashResult.Parried) break;
            if (!real)
            {
                bool dodged = motion.LastDashResult != EagleMotion.DashResult.Hit;   // 서서 피했으면 곧바로 몰아침
                yield return new WaitForSeconds(dodged ? gap * dodgedGapScale : gap);
            }
        }

        yield return new WaitForSeconds(0.2f);
    }
}