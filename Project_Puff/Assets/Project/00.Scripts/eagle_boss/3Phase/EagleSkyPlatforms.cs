using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 하늘 그로기(3페이즈)에서 나타나는 숨은 플랫폼. 독수리 체력 5~1 에 맞춰 5가지 세트를 준비한다.
/// 독수리가 하늘에서 날갯짓(Summoning 단계)을 시작하면 지금까지 맞은 횟수에 해당하는 세트가
/// 위(독수리 쪽)부터 아래로 차례차례 "삭" 하고 나타난다 (바람에 실려 생기는 느낌: 살짝 커지며 튕김).
/// 그로기가 끝나면(맞았거나 시간 초과) 잠시 뒤 사라진다.
///
/// 설정: 씬에 플랫폼 묶음을 5개 만들고(각각 빈 부모 오브젝트 아래에 플랫폼들), 이 컴포넌트의 Sets 에 순서대로 넣는다.
///   Element 0 = 첫 번째 그로기(HP 5) / Element 1 = 두 번째(HP 4) / … / Element 4 = 다섯 번째(HP 1)
/// 플랫폼에는 SpriteRenderer 와 Collider2D 가 있어야 한다 (페이드 인/아웃과 충돌 켜기/끄기를 해 줌).
/// </summary>
public class EagleSkyPlatforms : MonoBehaviour
{
    [SerializeField] private EagleBoss boss;
    [Tooltip("그로기 순서대로 플랫폼 묶음 (부모 오브젝트). 개수가 모자라면 마지막 세트를 계속 씀")]
    [SerializeField] private GameObject[] sets;

    [Header("연출")]
    [Tooltip("플랫폼 하나가 나타나는 데 걸리는 시간")]
    [SerializeField] private float popTime = 0.35f;
    [Tooltip("끄면 크기/위치를 전혀 건드리지 않고 투명도만 바꿈 (오브젝트가 설치한 위치에서 안 움직임). 켜면 제자리에서 커지며 튕김")]
    [SerializeField] private bool popScale = true;
    [Tooltip("나타날 때 잠깐 커졌다 줄어드는 정도 (1 = 없음)")]
    [SerializeField] private float popOvershoot = 1.15f;
    [Tooltip("전체 생성이 이 비율의 시간 안에 끝남 (Summon Time 의 몇 %). 1 이면 날갯짓이 끝날 때까지 이어짐")]
    [Range(0.2f, 1f)][SerializeField] private float spreadRatio = 0.85f;
    [Tooltip("타일맵 발판이 위에서 내려오며 나타나는 거리 (유닛)")]
    [SerializeField] private float tilemapSlide = 1.5f;
    [Tooltip("사라질 때 걸리는 시간")]
    [SerializeField] private float fadeOut = 0.8f;
    [Tooltip("그로기가 끝난 뒤 플랫폼이 사라지기 시작하기까지 (플레이어가 안전하게 내려올 시간)")]
    [SerializeField] private float lingerAfterEnd = 2.5f;
    [Tooltip("나타나기 전에 콜라이더를 켜는 시점 (0~1, 페이드의 몇 % 지점부터 밟을 수 있는지)")]
    [Range(0f, 1f)][SerializeField] private float solidAt = 0.3f;

    private Coroutine routine;
    private GameObject active;

    private void Awake()
    {
        if (boss == null) boss = GetComponent<EagleBoss>();
        if (boss == null) boss = FindFirstObjectByType<EagleBoss>();
        if (sets != null)
            foreach (var st in sets)
            {
                if (st == null) continue;
                foreach (var r in st.GetComponentsInChildren<SpriteRenderer>(true)) origAlpha[r] = r.color.a;   // 원래 투명도 기억 (반투명 오브젝트 보호)
                st.SetActive(false);   // 평소엔 안 보이고 막지도 않음
            }
    }

    private void OnEnable()
    {
        if (boss == null) return;
        boss.SkyStageChanged += OnStage;
        boss.SkyGroggyEnded += OnEnd;
    }

    private void OnDisable()
    {
        if (boss == null) return;
        boss.SkyStageChanged -= OnStage;
        boss.SkyGroggyEnded -= OnEnd;
    }

    private void OnStage(EagleBoss.SkyStage stage, int level)
    {
        if (stage != EagleBoss.SkyStage.Summoning) return;
        OnStart(level);
    }

    private void OnStart(int level)
    {
        if (sets == null || sets.Length == 0) return;
        var set = sets[Mathf.Min(level, sets.Length - 1)];
        if (set == null) return;
        if (routine != null) StopCoroutine(routine);
        if (current != null) { foreach (var it in current) { it.tr.localScale = it.baseScale; it.tr.localPosition = it.basePos; } current = null; }   // 중간에 끊긴 크기 복구
        if (active != null && active != set) active.SetActive(false);
        active = set;
        routine = StartCoroutine(Show(set));
    }

    private void OnEnd()
    {
        if (active == null) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Hide(active));
    }

    private System.Collections.Generic.List<Item> current;
    private readonly System.Collections.Generic.Dictionary<SpriteRenderer, float> origAlpha = new System.Collections.Generic.Dictionary<SpriteRenderer, float>();

    private class Item
    {
        public Transform tr;
        public Vector3 baseScale;
        public SpriteRenderer[] srs;
        public Tilemap[] tms;
        public Vector3 basePos;
        public Vector3 baseWorld;
        public Vector3 centerOffset;   // 피벗에서 보이는 모양의 중심까지 (월드). 커지고 작아질 때 이 중심이 고정되도록 쓴다
        public Collider2D[] cols;
        public float delay;
        public bool solid;
    }

    private IEnumerator Show(GameObject set)
    {
        // 직계 자식 하나하나가 플랫폼. 위(높은 곳)부터 차례로 나타난다
        var items = new System.Collections.Generic.List<Item>();
        foreach (Transform ch in set.transform)
        {
            items.Add(new Item
            {
                tr = ch,
                baseScale = ch.localScale,
                basePos = ch.localPosition,
                tms = ch.GetComponentsInChildren<Tilemap>(true),
                srs = ch.GetComponentsInChildren<SpriteRenderer>(true),
                cols = ch.GetComponentsInChildren<Collider2D>(true),
            });
        }
        current = items;
        items.Sort((p, q) => q.tr.position.y.CompareTo(p.tr.position.y));

        float total = (boss != null ? boss.SkySummonTime : 1.5f) * spreadRatio;
        total = Mathf.Max(0f, total - popTime);
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            it.delay = items.Count <= 1 ? 0f : total * i / (items.Count - 1);
        }
        set.SetActive(true);
        foreach (var it in items)
        {
            it.baseWorld = it.tr.position;
            var rends = it.tr.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                Bounds bb = rends[0].bounds;
                for (int r = 1; r < rends.Length; r++) bb.Encapsulate(rends[r].bounds);
                it.centerOffset = bb.center - it.tr.position;
            }
            foreach (var c in it.cols) c.enabled = false;
            SetAlpha(it, 0f);
            if (it.tms.Length > 0) it.tr.localPosition = it.basePos + Vector3.up * tilemapSlide;
            else if (popScale) it.tr.localScale = Vector3.zero;
        }

        float t = 0f;
        bool done = false;
        while (!done)
        {
            done = true;
            foreach (var it in items)
            {
                float k = Mathf.Clamp01((t - it.delay) / Mathf.Max(0.01f, popTime));
                if (k < 1f) done = false;
                if (k <= 0f) continue;
                // 0 → overshoot → 1 로 튕기며 커짐
                float sc = k < 0.7f ? Mathf.Lerp(0f, popOvershoot, k / 0.7f) : Mathf.Lerp(popOvershoot, 1f, (k - 0.7f) / 0.3f);
                if (it.tms.Length > 0)
                {
                    // 타일맵은 크기를 줄이면 모양이 틀어지므로, 위에서 내려오며 나타난다
                    float e = 1f - (1f - k) * (1f - k);
                    it.tr.localPosition = it.basePos + Vector3.up * tilemapSlide * (1f - e);
                }
                else if (popScale)
                {
                    it.tr.localScale = it.baseScale * sc;
                    it.tr.position = it.baseWorld + it.centerOffset * (1f - sc);   // 모양의 중심은 설치한 자리에 고정
                }
                SetAlpha(it, Mathf.Clamp01(k * 2f));
                if (!it.solid && k >= solidAt) { it.solid = true; foreach (var c in it.cols) c.enabled = true; }
            }
            t += Time.deltaTime;
            yield return null;
        }
        foreach (var it in items)
        {
            it.tr.localScale = it.baseScale;
            it.tr.localPosition = it.basePos;   // 최종 위치는 항상 설치한 그대로
            SetAlpha(it, 1f);
            foreach (var c in it.cols) c.enabled = true;
        }
        routine = null;
    }

    private IEnumerator Hide(GameObject set)
    {
        yield return new WaitForSeconds(lingerAfterEnd);
        var srs = set.GetComponentsInChildren<SpriteRenderer>(true);
        var cols = set.GetComponentsInChildren<Collider2D>(true);
        var tms = set.GetComponentsInChildren<Tilemap>(true);
        for (float t = 0f; t < fadeOut; t += Time.deltaTime)
        {
            float a = 1f - t / Mathf.Max(0.01f, fadeOut);
            SetAlpha(srs, a);
            foreach (var tm in tms) { var c = tm.color; c.a = a; tm.color = c; }
            yield return null;
        }
        foreach (var tm in tms) { var c = tm.color; c.a = 1f; tm.color = c; }
        foreach (var c in cols) c.enabled = false;
        set.SetActive(false);
        SetAlpha(srs, 1f);
        if (current != null) { foreach (var it in current) { it.tr.localScale = it.baseScale; it.tr.localPosition = it.basePos; } current = null; }
        if (active == set) active = null;
        routine = null;
    }

    private void SetAlpha(Item it, float a)
    {
        SetAlpha(it.srs, a);
        foreach (var tm in it.tms) { var c = tm.color; c.a = a; tm.color = c; }
    }

    private void SetAlpha(SpriteRenderer[] srs, float a)
    {
        foreach (var r in srs)
        {
            float o;
            if (!origAlpha.TryGetValue(r, out o)) o = 1f;
            var c = r.color; c.a = o * a; r.color = c;
        }
    }
}