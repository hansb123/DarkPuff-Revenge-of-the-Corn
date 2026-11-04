using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3페이즈 일반 패턴: 그림자 돌진 (분신). 독수리가 잔상 분신 2~3마리로 갈라져 사방에서 차례로 돌진한다.
///  - 분신: 반투명한 푸른 그림자. 자세를 잡다가 조준이 고정되면 붉은 예고선이 뜨고, 그 선을 따라 돌진. 맞으면 데미지, 패링은 안 됨.
///  - 진짜: 불투명한 독수리 본체. 눈이 번쩍이고(금빛), 붉은 예고선이 없다. 쳐내면 패링 성공 → 분신은 모두 연기처럼 사라진다.
///  - 돌진 순서는 무작위 (진짜가 몇 번째로 오는지 매번 다름). 한 번에 한 마리씩, 일정 간격으로 온다.
/// 읽는 법: "붉은 선이 뜨는 건 피한다 / 눈이 번쩍이는 진짜는 쳐낸다".
/// 독수리 몸 콜라이더가 Enemy 레이어여야 패링이 된다. 독수리 오브젝트에 EagleMotion, EaglePoseController 가 있어야 한다. (분신은 프리팹 없이 코드로 만들어짐)
/// 에셋 만들기: Create → Darkpuff → Patterns → Eagle Shadow Dash
/// </summary>
[CreateAssetMenu(menuName = "Darkpuff/Patterns/Eagle Shadow Dash", fileName = "Pattern_ShadowDash")]
public class ShadowDashPattern : AttackPattern
{
    [Header("난이도별 수치  (왼쪽 = 쉬움 / 오른쪽 = 가장 어려움)")]
    [Tooltip("분신 수 (진짜 제외)")]
    [SerializeField] private int clonesEasy = 2;
    [SerializeField] private int clonesHard = 3;
    [Tooltip("갈라진 뒤 첫 돌진까지 걸리는 시간(초)")]
    [SerializeField] private float setupEasy = 1.5f;
    [SerializeField] private float setupHard = 1.2f;
    [Tooltip("돌진과 돌진 사이 간격(초). 짧을수록 어려움")]
    [SerializeField] private float gapEasy = 0.8f;
    [SerializeField] private float gapHard = 0.55f;
    [Tooltip("돌진 속도 (유닛/초). 분신과 진짜가 같음")]
    [SerializeField] private float speedEasy = 14f;
    [SerializeField] private float speedHard = 20f;

    [Header("읽을 수 있는 시간")]
    [Tooltip("분신의 붉은 예고선이 돌진 몇 초 전부터 뜰지")]
    [SerializeField] private float lineLead = 0.7f;
    [Tooltip("진짜의 눈이 돌진 몇 초 전부터 번쩍일지")]
    [SerializeField] private float realFlashLead = 0.9f;
    [Tooltip("켜면 진짜가 항상 마지막 순서 (끄면 무작위)")]
    [SerializeField] private bool realAlwaysLast = false;

    [Header("분신 길")]
    [Tooltip("위에서 비스듬히 내리꽂는 분신을 쓸지")]
    [SerializeField] private bool diagonalClones = true;
    [Tooltip("위에서 내리꽂는 분신이 시작하는 높이 (바닥 기준)")]
    [SerializeField] private float diagonalHeight = 7.5f;
    [Tooltip("플레이어 좌우에서 시작하는 거리")]
    [SerializeField] private float sideDistance = 8f;

    [Header("모양")]
    [SerializeField] private Color cloneTint = new Color(0.5f, 0.6f, 0.95f, 0.6f);
    [SerializeField] private Color lineColor = new Color(1f, 0.25f, 0.2f, 0.55f);

    [Header("공통")]
    [SerializeField] private float backOff = 4f;
    [SerializeField] private float overshoot = 6f;
    [SerializeField] private float minHeight = 1.2f;
    [SerializeField] private float hitRadius = 1.0f;
    [SerializeField] private int damage = 1;
    [SerializeField] private LayerMask playerLayer;

    [Header("패링당했을 때 (진짜)")]
    [SerializeField] private float recoil = 3f;
    [SerializeField] private float stun = 0.9f;
    [SerializeField] private float returnTime = 0.8f;

    public override IEnumerator Run(PatternContext ctx)
    {
        var motion = ctx.Boss != null ? ctx.Boss.GetComponent<EagleMotion>() : null;
        var bossSr = ctx.Boss != null ? ctx.Boss.GetComponent<SpriteRenderer>() : null;
        var poses = ctx.Boss != null ? ctx.Boss.GetComponent<EaglePoseController>() : null;
        if (motion == null || bossSr == null)
        {
            Debug.LogWarning("[ShadowDashPattern] 독수리에 EagleMotion 과 SpriteRenderer 가 필요합니다.", this);
            yield break;
        }

        int cloneCount = Mathf.Clamp(ctx.Lerp(clonesEasy, clonesHard), 1, 3);
        float setup = ctx.Lerp(setupEasy, setupHard);
        float gap = ctx.Lerp(gapEasy, gapHard);
        float speed = ctx.Lerp(speedEasy, speedHard);

        // 돌진 순서: 진짜 한 마리 + 분신들을 섞는다
        int total = cloneCount + 1;
        int realSlot = realAlwaysLast ? total - 1 : Random.Range(0, total);
        var cloneSlots = new List<int>();
        for (int i = 0; i < total; i++) if (i != realSlot) cloneSlots.Add(i);
        // 분신 슬롯 순서 섞기
        for (int i = cloneSlots.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); int tmp = cloneSlots[i]; cloneSlots[i] = cloneSlots[j]; cloneSlots[j] = tmp; }

        // 진짜가 돌진할 방향 (DashRoutine 과 같은 규칙: 플레이어 쪽)
        Vector2 bossPos = ctx.Boss.position;
        Vector2 playerPos = ctx.Player != null ? (Vector2)ctx.Player.position : bossPos + Vector2.right * 5f;
        float realDir = playerPos.x < bossPos.x ? -1f : 1f;
        float levelY = Mathf.Max(playerPos.y + 0.4f, ctx.GroundY + minHeight);
        float lo = ctx.LeftX + 0.5f, hi = ctx.RightX - 0.5f;

        // 분신 시작 위치 (각자 다른 길)
        var starts = new List<Vector2>();
        float farX = Mathf.Clamp(playerPos.x + realDir * sideDistance, lo, hi);          // 진짜가 향하는 쪽 끝 → 반대로 돌진
        if (Mathf.Abs(farX - playerPos.x) >= 4f) starts.Add(new Vector2(farX, levelY));
        else starts.Add(new Vector2(Mathf.Clamp(playerPos.x - realDir * sideDistance * 0.6f, lo, hi), ctx.GroundY + diagonalHeight));
        if (diagonalClones)
        {
            starts.Add(new Vector2(Mathf.Clamp(playerPos.x - realDir * sideDistance * 0.55f, lo, hi), ctx.GroundY + diagonalHeight));
            starts.Add(new Vector2(Mathf.Clamp(playerPos.x + realDir * sideDistance * 0.55f, lo, hi), ctx.GroundY + diagonalHeight));
        }
        else
        {
            starts.Add(new Vector2(Mathf.Clamp(playerPos.x - realDir * sideDistance, lo, hi), levelY));
            starts.Add(new Vector2(Mathf.Clamp(playerPos.x - realDir * sideDistance * 0.5f, lo, hi), levelY));
        }

        Sprite windupSprite = poses != null ? poses.PoseSprite(EaglePose.Id.Windup) : bossSr.sprite;
        Sprite diveSprite = poses != null ? poses.PoseSprite(EaglePose.Id.Dive) : bossSr.sprite;

        // 분신 만들기 (보스 위치에서 갈라져 나옴)
        var clones = new List<EagleShadowClone>();
        for (int i = 0; i < cloneSlots.Count; i++)
        {
            float launch = setup + cloneSlots[i] * gap;
            var plan = new EagleShadowClone.Plan
            {
                from = bossPos,
                start = starts[Mathf.Min(i, starts.Count - 1)],
                arriveTime = 0.45f,
                lockAt = Mathf.Max(0.5f, launch - lineLead),
                launchAt = launch,
                speed = speed,
                overshoot = overshoot,
                hitRadius = hitRadius,
                damage = damage,
                playerLayer = playerLayer,
                groundY = ctx.GroundY,
                player = ctx.Player,
                windupSprite = windupSprite,
                diveSprite = diveSprite,
                tint = cloneTint,
                lineColor = lineColor,
            };
            clones.Add(EagleShadowClone.Spawn(ctx.Runner, bossSr, plan));
        }

        // 진짜: 자기 차례에 돌진하도록 윈드업을 길게 잡고 눈은 돌진 직전에 번쩍
        float realLaunch = setup + realSlot * gap;
        var s = new EagleMotion.DashSettings
        {
            diagonal = false,
            parry = true,
            eyeFlash = true,
            flashAt = Mathf.Clamp(1f - realFlashLead / Mathf.Max(0.2f, realLaunch), 0.05f, 0.95f),
            windup = Mathf.Max(0.6f, realLaunch),
            backOff = backOff,
            speed = speed,
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

        // 남은 분신이 끝날 때까지 기다림 (패링에 성공했다면 분신은 이미 사라짐)
        bool any = true;
        float guard = 0f;
        while (any && guard < 6f)
        {
            any = false;
            foreach (var c in clones) if (c != null && !c.IsFinished) { any = true; break; }
            guard += Time.deltaTime;
            if (any) yield return null;
        }
        foreach (var c in clones) if (c != null) c.Cancel();   // 혹시 남았으면 정리

        yield return new WaitForSeconds(0.2f);
    }
}