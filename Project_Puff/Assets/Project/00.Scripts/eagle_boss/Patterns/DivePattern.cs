using System.Collections;
using UnityEngine;

/// <summary>
/// 독수리 일반 패턴: 급강하. 하늘에서 플레이어 위치로 그림자가 뜨고(예고), 독수리가 솟았다가 바닥까지 내리꽂힌다.
/// 그림자가 정해진 뒤에는 위치가 고정되므로 그림자 밖으로 벗어나면 된다. 난이도가 오르면 횟수가 늘고 빨라진다.
/// 독수리 오브젝트에 EagleMotion 이 있어야 한다. (2·3페이즈에서만 사용 — 1페이즈는 정면 고정)
/// 에셋 만들기: Create → Darkpuff → Patterns → Eagle Dive
/// </summary>
[CreateAssetMenu(menuName = "Darkpuff/Patterns/Eagle Dive", fileName = "Pattern_Dive")]
public class DivePattern : AttackPattern
{
    [Header("난이도별 수치  (왼쪽 = 쉬움 / 오른쪽 = 가장 어려움)")]
    [SerializeField] private int divesEasy = 1;
    [SerializeField] private int divesHard = 3;
    [Tooltip("그림자 예고 시간(초)")]
    [SerializeField] private float windupEasy = 1.0f;
    [SerializeField] private float windupHard = 0.6f;
    [Tooltip("내리꽂는 데 걸리는 시간(초)")]
    [SerializeField] private float diveEasy = 0.38f;
    [SerializeField] private float diveHard = 0.24f;
    [Tooltip("급강하 사이 쉬는 시간(초)")]
    [SerializeField] private float betweenEasy = 0.7f;
    [SerializeField] private float betweenHard = 0.3f;

    [Tooltip("그림자 위치에 도착한 뒤, 내리꽂기 전까지 떠서 기다리는 시간(초). 0 = 기존과 같음")]
    [SerializeField] private float lockHoldEasy = 0f;
    [SerializeField] private float lockHoldHard = 0f;

    [Header("공통")]
    [Tooltip("바닥에 머무는 시간(초)")]
    [SerializeField] private float hold = 0.35f;
    [SerializeField] private float returnTime = 0.8f;
    [Tooltip("착지했을 때 독수리 중심이 바닥에서 떠 있는 높이")]
    [SerializeField] private float impactHeight = 1.6f;
    [Tooltip("독수리 몸에 닿는 판정 반경")]
    [SerializeField] private float hitRadius = 1.5f;
    [SerializeField] private int damage = 1;
    [SerializeField] private LayerMask playerLayer;

    public override IEnumerator Run(PatternContext ctx)
    {
        var motion = ctx.Boss != null ? ctx.Boss.GetComponent<EagleMotion>() : null;
        if (motion == null)
        {
            Debug.LogWarning("[DivePattern] 독수리에 EagleMotion 이 필요합니다.", this);
            yield break;
        }

        int dives = ctx.Lerp(divesEasy, divesHard);
        float windup = ctx.Lerp(windupEasy, windupHard);
        float dive = ctx.Lerp(diveEasy, diveHard);
        float between = ctx.Lerp(betweenEasy, betweenHard);

        for (int i = 0; i < dives; i++)
        {
            yield return motion.DiveRoutine(windup, dive, hold, returnTime, impactHeight, hitRadius, playerLayer, damage, null, null, 0f, ctx.Lerp(lockHoldEasy, lockHoldHard));
            if (i < dives - 1) yield return new WaitForSeconds(between);
        }

        yield return new WaitForSeconds(0.2f);
    }
}