using System.Collections;
using UnityEngine;

/// <summary>
/// 독수리 일반 패턴: 깃털 부채꼴. 날개를 뒤로 젖혔다가(예고) 플레이어 쪽으로 깃털을 부채꼴로 쏜다.
/// 부채꼴 안에 틈이 한 군데 뚫려 있고, 틈의 위치는 플레이어 정면이 아니므로 틈 쪽으로 움직이거나 점프로 넘어야 한다.
/// 난이도가 오를수록 파도 수, 깃털 수, 속도가 늘고 예고가 짧아진다.
/// 에셋 만들기: Create → Darkpuff → Patterns → Eagle Feather Fan
/// </summary>
[CreateAssetMenu(menuName = "Darkpuff/Patterns/Eagle Feather Fan", fileName = "Pattern_FeatherFan")]
public class FeatherFanPattern : AttackPattern
{
    [SerializeField] private EagleFeather featherPrefab;

    [Header("난이도별 수치  (왼쪽 = 쉬움 / 오른쪽 = 가장 어려움)")]
    [Tooltip("부채꼴을 쏘는 횟수")]
    [SerializeField] private int wavesEasy = 2;
    [SerializeField] private int wavesHard = 4;
    [Tooltip("한 번에 쏘는 깃털 수 (틈 때문에 실제로는 1개 적게 나감)")]
    [SerializeField] private int countEasy = 7;
    [SerializeField] private int countHard = 11;
    [Tooltip("부채꼴 전체 각도 (도)")]
    [SerializeField] private float arcEasy = 70f;
    [SerializeField] private float arcHard = 95f;
    [SerializeField] private float speedEasy = 8f;
    [SerializeField] private float speedHard = 12f;
    [Tooltip("예고(날개 젖힘) 시간(초)")]
    [SerializeField] private float windupEasy = 0.9f;
    [SerializeField] private float windupHard = 0.5f;
    [Tooltip("파도와 파도 사이(초)")]
    [SerializeField] private float waveGapEasy = 0.9f;
    [SerializeField] private float waveGapHard = 0.5f;

    [Header("위치")]
    [Tooltip("보스 위치에서 이만큼 아래에서 발사 (보스 중심 → 가슴/발톱 높이)")]
    [SerializeField] private float originDrop = 1.2f;

    public override IEnumerator Run(PatternContext ctx)
    {
        if (featherPrefab == null)
        {
            Debug.LogWarning("[FeatherFanPattern] Feather Prefab이 비어 있습니다.", this);
            yield break;
        }

        var pool = ctx.GetPool(featherPrefab, (f, p) => f.SetReleaser(x => p.Release(x)));

        int waves = ctx.Lerp(wavesEasy, wavesHard);
        int count = Mathf.Max(5, ctx.Lerp(countEasy, countHard));
        float arc = ctx.Lerp(arcEasy, arcHard);
        float speed = ctx.Lerp(speedEasy, speedHard);
        float windup = ctx.Lerp(windupEasy, windupHard);
        float gapT = ctx.Lerp(waveGapEasy, waveGapHard);

        for (int w = 0; w < waves; w++)
        {
            Vector2 origin = ctx.Boss != null
                ? (Vector2)ctx.Boss.position + Vector2.down * originDrop
                : new Vector2((ctx.LeftX + ctx.RightX) * 0.5f, ctx.GroundY + 6f);

            // 예고
            EaglePose.Raise(EaglePose.Id.Windup);
            GameSfx.Play(GameSfxId.EagleWingFlap);
            yield return new WaitForSeconds(windup);

            // 발사 순간의 플레이어 방향을 중심으로
            Vector2 aim = ctx.Player != null ? (Vector2)ctx.Player.position + Vector2.up * 0.5f : origin + Vector2.down * 5f;
            float center = Mathf.Atan2(aim.y - origin.y, aim.x - origin.x) * Mathf.Rad2Deg;

            // 틈: 가운데에서 한두 칸 떨어진 곳 (가운데면 가만히 있어도 안 맞으므로 제외)
            int mid = count / 2;
            int off = Random.Range(1, Mathf.Min(3, mid) + 1) * (Random.value < 0.5f ? -1 : 1);
            int hole = Mathf.Clamp(mid + off, 0, count - 1);

            EaglePose.Raise(EaglePose.Id.Strike);
            GameSfx.Play(GameSfxId.EagleWingFlap);
            for (int i = 0; i < count; i++)
            {
                if (i == hole) continue;
                float t = count > 1 ? (float)i / (count - 1) - 0.5f : 0f;
                float ang = (center + t * arc) * Mathf.Deg2Rad;
                var f = pool.Get();
                f.Launch(ctx.Runner, origin, new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)), speed);
            }

            yield return new WaitForSeconds(gapT);
        }

        EaglePose.Raise(EaglePose.Id.Idle);
        yield return new WaitForSeconds(0.5f);
    }
}
