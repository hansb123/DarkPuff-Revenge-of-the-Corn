using System.Collections;
using UnityEngine;

/// <summary>
/// 하늘 그로기에서 독수리가 날갯짓으로 바람을 일으키는 연출 (EagleMotion.IsSummoning 동안).
///  - 날갯짓 한 번(Gust Interval)마다 "돌풍 고리"가 독수리 아래로 퍼져 나가며 사라진다
///  - 아래로 쏟아지는 바람 줄기들 (플랫폼을 쓸어 내리는 느낌)
///  - 깃털 모양 없이 코드로 그린다 (이미지 필요 없음). 보스 오브젝트에 붙이기만 하면 됨.
/// </summary>
public class EagleSkyWindFx : MonoBehaviour
{
    [SerializeField] private EagleMotion motion;

    [Header("색")]
    [SerializeField] private Color windColor = new Color(0.85f, 0.95f, 1f, 1f);

    [Header("돌풍 (날갯짓마다)")]
    [Tooltip("돌풍이 생기는 간격(초)")]
    [SerializeField] private float gustInterval = 0.32f;
    [Tooltip("돌풍이 퍼지는 최대 가로 폭(유닛)")]
    [SerializeField] private float gustWidth = 7f;
    [Tooltip("돌풍이 내려가는 거리(유닛)")]
    [SerializeField] private float gustDrop = 5f;
    [SerializeField] private float gustLife = 0.5f;
    [Tooltip("돌풍 한 번에 그려지는 줄 수 (아치 모양)")]
    [SerializeField] private int gustLines = 7;

    [Header("바람 줄기")]
    [SerializeField] private float streaksPerSecond = 55f;
    [SerializeField] private Vector2 streakLength = new Vector2(1.5f, 3.5f);
    [SerializeField] private Vector2 streakWidth = new Vector2(0.04f, 0.09f);
    [Tooltip("독수리 중심에서 좌우로 퍼지는 폭")]
    [SerializeField] private float spread = 2.6f;
    [SerializeField] private float streakSpeed = 14f;
    [SerializeField] private float streakLife = 0.45f;

    [SerializeField] private int sortingOrder = 3;

    private float gustT, streakT;

    private void Awake()
    {
        if (motion == null) motion = GetComponent<EagleMotion>();
    }

    private void Update()
    {
        if (motion == null || !motion.IsSummoning) { gustT = 0f; streakT = 0f; return; }

        gustT -= Time.deltaTime;
        if (gustT <= 0f) { gustT = gustInterval; StartCoroutine(Gust(transform.position)); }

        streakT += Time.deltaTime * streaksPerSecond;
        while (streakT >= 1f) { streakT -= 1f; StartCoroutine(Streak(transform.position)); }
    }

    // 아래로 퍼지는 아치형 돌풍: 가운데는 더 많이 내려가고 양끝은 덜 내려간다
    private IEnumerator Gust(Vector3 origin)
    {
        int n = Mathf.Max(2, gustLines);
        var lines = new SpriteRenderer[n];
        for (int i = 0; i < n; i++)
        {
            var go = new GameObject("SkyGust");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = EagleVis.White;
            sr.sortingOrder = sortingOrder;
            lines[i] = sr;
        }
        Color c = windColor;
        for (float t = 0f; t < gustLife; t += Time.deltaTime)
        {
            float k = t / gustLife;
            float e = 1f - (1f - k) * (1f - k);   // 빠르게 퍼지고 느려짐
            for (int i = 0; i < n; i++)
            {
                float u = n == 1 ? 0f : (i / (float)(n - 1)) * 2f - 1f;   // -1..1
                float x = origin.x + u * gustWidth * 0.5f * e;
                float y = origin.y - 0.8f - gustDrop * e * (1f - 0.45f * Mathf.Abs(u));
                var tr = lines[i].transform;
                tr.position = new Vector3(x, y, 0f);
                tr.rotation = Quaternion.Euler(0f, 0f, -u * 35f);
                tr.localScale = new Vector3(0.07f, Mathf.Lerp(1.6f, 0.6f, k), 1f);
                c.a = 0.8f * (1f - k) * (1f - k);
                lines[i].color = c;
            }
            yield return null;
        }
        foreach (var sr in lines) if (sr != null) Destroy(sr.gameObject);
    }

    private IEnumerator Streak(Vector3 around)
    {
        float x = around.x + Random.Range(-spread, spread);
        float y = around.y - Random.Range(0.5f, 2.0f);
        float len = Random.Range(streakLength.x, streakLength.y);
        float wid = Random.Range(streakWidth.x, streakWidth.y);
        float lean = Random.Range(-0.25f, 0.25f) + (x - around.x) * 0.08f;   // 바깥쪽으로 살짝 벌어짐
        Vector2 dir = new Vector2(lean, -1f).normalized;

        var go = new GameObject("SkyStreak");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = EagleVis.White;
        sr.sortingOrder = sortingOrder;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        go.transform.localScale = new Vector3(len, wid, 1f);
        Color c = windColor;
        float life = streakLife * Random.Range(0.8f, 1.2f);
        Vector2 p = new Vector2(x, y);
        for (float t = 0f; t < life; t += Time.deltaTime)
        {
            float k = t / life;
            p += dir * streakSpeed * Time.deltaTime;
            go.transform.position = new Vector3(p.x, p.y, 0f);
            c.a = 0.8f * (1f - k) * (1f - k);
            sr.color = c;
            yield return null;
        }
        Destroy(go);
    }
}