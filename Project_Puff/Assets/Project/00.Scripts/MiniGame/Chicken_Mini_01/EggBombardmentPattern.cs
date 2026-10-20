using System.Collections;
using UnityEngine;

/// <summary>
/// 패턴 1: 알 폭격. 닭이 알을 연달아 떨어뜨린다. 일부는 플레이어 머리 위를 노리고, 나머지는 무대 어디든 랜덤.
/// 분노 게이지(난이도)가 오를수록 알이 많아지고, 간격이 짧아지고, 떨어지는 시간(= 피할 여유)이 줄어들며, 조준 확률이 올라간다.
/// 에셋 만들기: Project 창 우클릭 → Create → Darkpuff → Patterns → Egg Bombardment
/// </summary>
[CreateAssetMenu(menuName = "Darkpuff/Patterns/Egg Bombardment", fileName = "Pattern_EggBombardment")]
public class EggBombardmentPattern : AttackPattern
{
    [Header("프리팹")]
    [SerializeField] private EggBomb eggPrefab;

    [Header("난이도별 수치  (왼쪽 = 쉬움 / 오른쪽 = 가장 어려움)")]
    [SerializeField] private int countEasy = 4;
    [SerializeField] private int countHard = 9;
    [Tooltip("알과 알 사이 간격(초)")]
    [SerializeField] private float intervalEasy = 0.55f;
    [SerializeField] private float intervalHard = 0.22f;
    [Tooltip("알이 떨어지는 시간(초). 짧을수록 피하기 어려움")]
    [SerializeField] private float fallTimeEasy = 1.0f;
    [SerializeField] private float fallTimeHard = 0.65f;
    [Tooltip("알이 플레이어 쪽을 노릴 확률")]
    [Range(0f, 1f)] [SerializeField] private float aimChanceEasy = 0.3f;
    [Range(0f, 1f)] [SerializeField] private float aimChanceHard = 0.7f;

    [Header("공통")]
    [Tooltip("조준할 때 플레이어 위치에서 좌우로 흩어지는 폭")]
    [SerializeField] private float aimJitter = 1.4f;
    [Tooltip("알이 시작하는 높이 (바닥 기준, 화면 위쪽 밖)")]
    [SerializeField] private float spawnHeight = 11f;
    [Tooltip("무대 가장자리에서 이만큼 안쪽으로만 떨어뜨림")]
    [SerializeField] private float edgeMargin = 0.8f;

    public override IEnumerator Run(PatternContext ctx)
    {
        if (eggPrefab == null)
        {
            Debug.LogWarning("[EggBombardment] Egg Prefab이 비어 있습니다.", this);
            yield break;
        }

        var pool = ctx.GetPool(eggPrefab, (egg, p) => egg.SetReleaser(e => p.Release(e)));

        int count = ctx.Lerp(countEasy, countHard);
        float interval = ctx.Lerp(intervalEasy, intervalHard);
        float fallTime = ctx.Lerp(fallTimeEasy, fallTimeHard);
        float aimChance = ctx.Lerp(aimChanceEasy, aimChanceHard);

        GameSfx.Play(GameSfxId.ChickenBawk);   // 알 낳는 소리

        float minX = ctx.LeftX + edgeMargin;
        float maxX = ctx.RightX - edgeMargin;

        for (int i = 0; i < count; i++)
        {
            float x;
            if (ctx.Player != null && Random.value < aimChance)
                x = ctx.Player.position.x + Random.Range(-aimJitter, aimJitter);
            else
                x = Random.Range(minX, maxX);
            x = Mathf.Clamp(x, minX, maxX);

            var egg = pool.Get();
            egg.Launch(ctx.Runner, new Vector2(x, ctx.GroundY), spawnHeight, fallTime);

            yield return new WaitForSeconds(interval);
        }

        // 마지막 알이 떨어질 때까지 기다린 뒤 패턴 종료
        yield return new WaitForSeconds(fallTime + 0.3f);
    }
}
