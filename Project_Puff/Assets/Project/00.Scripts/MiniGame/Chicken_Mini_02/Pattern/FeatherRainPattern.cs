using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 메카 닭 패턴 2: 깃털 화살비. 날개를 펼쳐 깃털을 하늘로 쏘아 올리면, 바닥에 착탄 표시가 깔리고 잠시 뒤 비스듬히 내리꽂힌다.
/// 달걀과 다른 점: 플레이어의 '현재 위치'를 노린 조준 사격이 섞이고, 난이도가 높으면 일부 깃털이 바닥에 박혀 잠시 장애물로 남는다.
///   - 조준: 솟구친 직후의 플레이어 X 를 노림 (표시가 뜬 뒤 warnTime 안에 움직여야 함)
///   - 랜덤: 무대 안 임의의 X
///   - 박힘: 난이도가 stickFrom 을 넘으면 stickChance 확률로 박혀서 남음
/// 에셋 만들기: Project 창 우클릭 → Create → Darkpuff → Patterns → Feather Rain
/// </summary>
[CreateAssetMenu(menuName = "Darkpuff/Patterns/Feather Rain", fileName = "Pattern_FeatherRain")]
public class FeatherRainPattern : AttackPattern
{
    [Header("프리팹")]
    [SerializeField] private MechaFeather featherPrefab;

    [Header("난이도별 수치  (왼쪽 = 쉬움 / 오른쪽 = 가장 어려움)")]
    [Tooltip("한 번에 쏘아 올리는 깃털 수")]
    [SerializeField] private int countEasy = 5;
    [SerializeField] private int countHard = 11;
    [Tooltip("깃털을 연달아 쏘는 간격(초)")]
    [SerializeField] private float intervalEasy = 0.35f;
    [SerializeField] private float intervalHard = 0.18f;
    [Tooltip("착탄 표시가 뜬 뒤 떨어지기까지(초) = 피할 시간")]
    [SerializeField] private float warnEasy = 0.9f;
    [SerializeField] private float warnHard = 0.55f;
    [Tooltip("내리꽂히는 데 걸리는 시간(초)")]
    [SerializeField] private float fallEasy = 0.5f;
    [SerializeField] private float fallHard = 0.32f;
    [Tooltip("플레이어를 노리는 깃털의 비율 (0~1)")]
    [SerializeField] private float aimedEasy = 0.4f;
    [SerializeField] private float aimedHard = 0.65f;

    [Header("모양")]
    [Tooltip("솟구치는 속도")]
    [SerializeField] private float riseSpeed = 22f;
    [Tooltip("바닥에서 이 높이까지 솟구침 (화면 위 밖)")]
    [SerializeField] private float topAboveGround = 13f;
    [Tooltip("떨어지기 시작하는 X 가 착탄점에서 벗어난 거리 (좌우 랜덤)")]
    [SerializeField] private Vector2 slantRange = new Vector2(1.5f, 4f);
    [Tooltip("날개 위치에서 이만큼 좌우 범위에서 쏘아 올림")]
    [SerializeField] private float wingSpan = 2.6f;
    [Tooltip("조준 깃털이 플레이어 좌우로 흩어지는 폭")]
    [SerializeField] private float aimJitter = 0.8f;

    [Header("박히는 깃털")]
    [Tooltip("난이도가 이 값을 넘으면 일부 깃털이 박혀서 남음 (0~1)")]
    [Range(0f, 1f)] [SerializeField] private float stickFrom = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float stickChance = 0.3f;
    [SerializeField] private float stuckTime = 2.2f;

    /// <summary>true = 날개를 펼친 동안 (메카 닭 포즈가 구독)</summary>
    public static event Action<bool> VolleyChanged;

    public override IEnumerator Run(PatternContext ctx)
    {
        if (featherPrefab == null)
        {
            Debug.LogWarning("[FeatherRainPattern] Feather Prefab이 비어 있습니다.", this);
            yield break;
        }
        var mecha = UnityEngine.Object.FindFirstObjectByType<MechaPoseController>();
        Vector2 origin = mecha != null ? (Vector2)mecha.transform.position + Vector2.up * 2.5f : new Vector2((ctx.LeftX + ctx.RightX) * 0.5f, ctx.GroundY + 3f);

        var pool = ctx.GetPool(featherPrefab, (f, p) => f.SetReleaser(x => p.Release(x)));

        int count = ctx.Lerp(countEasy, countHard);
        float interval = ctx.Lerp(intervalEasy, intervalHard);
        float aimed = ctx.Lerp(aimedEasy, aimedHard);
        bool canStick = ctx.Difficulty >= stickFrom;

        var s = new MechaFeather.Settings
        {
            groundY = ctx.GroundY,
            topY = ctx.GroundY + topAboveGround,
            riseSpeed = riseSpeed,
            warnTime = ctx.Lerp(warnEasy, warnHard),
            fallTime = ctx.Lerp(fallEasy, fallHard),
            stuckTime = stuckTime,
        };

        // 날개 펼치기
        GameSfx.Play(GameSfxId.MechaWingSpread);
        VolleyChanged?.Invoke(true);
        yield return new WaitForSeconds(0.45f);

        for (int i = 0; i < count; i++)
        {
            var f = pool.Get();
            Vector2 from = origin + new Vector2(UnityEngine.Random.Range(-wingSpan, wingSpan), 0f);
            float slant = UnityEngine.Random.Range(slantRange.x, slantRange.y) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
            s.slantX = slant;
            bool stick = canStick && UnityEngine.Random.value < stickChance;

            if (ctx.Player != null && UnityEngine.Random.value < aimed)
            {
                f.Launch(ctx.Runner, from, ctx.Player, UnityEngine.Random.Range(-aimJitter, aimJitter), stick, s);
            }
            else
            {
                float x = UnityEngine.Random.Range(ctx.LeftX + 1f, ctx.RightX - 1f);
                f.Launch(ctx.Runner, from, x, stick, s);
            }
            yield return new WaitForSeconds(interval);
        }

        VolleyChanged?.Invoke(false);
        // 마지막 깃털이 솟구치고 떨어질 시간
        yield return new WaitForSeconds(s.warnTime + s.fallTime + 0.5f);
    }
}
