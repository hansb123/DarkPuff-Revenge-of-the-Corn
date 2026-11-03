using System.Collections;
using UnityEngine;

/// <summary>
/// 돌진/급강하 궤적 연출. 독수리가 전속력으로 날아가는 동안(EagleMotion.IsCharging)
///  - 잔상: 독수리 모습이 뒤에 남았다가 흐려진다
///  - 바람 줄기: 날아가는 방향과 나란한 가는 줄이 몸 주변에 생겼다 사라진다 (바람을 가르는 느낌)
/// 일반 돌진은 차가운 흰색/하늘색, 패링 가능한 돌진은 금빛으로 달라 보인다.
/// 보스 오브젝트에 붙이기만 하면 된다 (이미지 필요 없음, 코드로 그림).
/// </summary>
public class EagleDashFx : MonoBehaviour
{
    [SerializeField] private EagleMotion motion;
    [SerializeField] private SpriteRenderer bossRenderer;

    [Header("색")]
    [SerializeField] private Color normalColor = new Color(0.82f, 0.94f, 1f, 1f);
    [Tooltip("패링 가능한 돌진 (눈이 번쩍이는 돌진)")]
    [SerializeField] private Color parryColor = new Color(1f, 0.82f, 0.3f, 1f);

    [Header("잔상")]
    [SerializeField] private bool afterimage = true;
    [Tooltip("잔상이 생기는 간격(초). 작을수록 촘촘")]
    [SerializeField] private float ghostInterval = 0.04f;
    [SerializeField] private float ghostLife = 0.28f;
    [Range(0f, 1f)][SerializeField] private float ghostAlpha = 0.5f;
    [Tooltip("잔상 색을 이 정도로 입힘 (0 = 원래 색)")]
    [Range(0f, 1f)][SerializeField] private float ghostTint = 0.7f;

    [Header("바람 줄기")]
    [SerializeField] private bool windStreaks = true;
    [Tooltip("초당 몇 줄기 생길지")]
    [SerializeField] private float streaksPerSecond = 70f;
    [Tooltip("줄기 길이 (최소~최대, 유닛)")]
    [SerializeField] private Vector2 streakLength = new Vector2(1.6f, 4.0f);
    [Tooltip("줄기 굵기 (최소~최대)")]
    [SerializeField] private Vector2 streakWidth = new Vector2(0.04f, 0.10f);
    [Tooltip("몸 중심에서 위아래로 퍼지는 폭")]
    [SerializeField] private float spread = 1.1f;
    [SerializeField] private float streakLife = 0.35f;
    [SerializeField] private int sortingOrder = 3;

    private Vector3 lastPos;
    private Vector2 dir = Vector2.right;
    private float ghostT, streakT;

    private void Awake()
    {
        if (motion == null) motion = GetComponent<EagleMotion>();
        if (bossRenderer == null) bossRenderer = GetComponent<SpriteRenderer>();
        lastPos = transform.position;
    }

    private void LateUpdate()
    {
        Vector3 pos = transform.position;
        Vector2 delta = pos - lastPos;
        lastPos = pos;
        if (motion == null || !motion.IsCharging) { ghostT = streakT = 0f; return; }

        if (delta.sqrMagnitude > 0.00001f) dir = delta.normalized;
        Color col = motion.ChargeIsParry ? parryColor : normalColor;

        if (afterimage && bossRenderer != null)
        {
            ghostT += Time.deltaTime;
            if (ghostT >= ghostInterval) { ghostT = 0f; StartCoroutine(Ghost(col)); }
        }
        if (windStreaks)
        {
            streakT += Time.deltaTime * streaksPerSecond;
            while (streakT >= 1f) { streakT -= 1f; StartCoroutine(Streak(pos, col)); }
        }
    }

    private IEnumerator Ghost(Color tint)
    {
        var go = new GameObject("DashGhost");
        go.transform.SetPositionAndRotation(transform.position, transform.rotation);
        go.transform.localScale = transform.lossyScale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = bossRenderer.sprite;
        sr.flipX = bossRenderer.flipX;
        sr.flipY = bossRenderer.flipY;
        sr.sortingLayerID = bossRenderer.sortingLayerID;
        sr.sortingOrder = bossRenderer.sortingOrder - 1;
        Color baseCol = Color.Lerp(Color.white, tint, ghostTint);
        for (float t = 0f; t < ghostLife; t += Time.deltaTime)
        {
            float k = t / ghostLife;
            baseCol.a = ghostAlpha * (1f - k);
            sr.color = baseCol;
            yield return null;
        }
        Destroy(go);
    }

    private IEnumerator Streak(Vector3 around, Color tint)
    {
        Vector2 perp = new Vector2(-dir.y, dir.x);
        Vector2 at = (Vector2)around + perp * Random.Range(-spread, spread) - dir * Random.Range(0f, 1.0f);
        float len = Random.Range(streakLength.x, streakLength.y);
        float wid = Random.Range(streakWidth.x, streakWidth.y);
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        var go = new GameObject("DashStreak");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = EagleVis.White;
        sr.sortingOrder = sortingOrder;
        go.transform.rotation = Quaternion.Euler(0f, 0f, ang);
        Color c = tint;
        float life = streakLife * Random.Range(0.7f, 1.1f);
        for (float t = 0f; t < life; t += Time.deltaTime)
        {
            float k = t / life;
            // 꼬리 쪽으로 늘어나며 사라짐: 앞끝은 처음 위치에 고정, 길이는 줄고 투명해짐
            float l = len * (1f - k * 0.7f);
            go.transform.position = (Vector3)(at - dir * (len - l) * 0.5f) + Vector3.zero;
            go.transform.localScale = new Vector3(l, wid * (1f - k * 0.5f), 1f);
            c.a = 0.85f * (1f - k) * (1f - k);
            sr.color = c;
            yield return null;
        }
        Destroy(go);
    }
}