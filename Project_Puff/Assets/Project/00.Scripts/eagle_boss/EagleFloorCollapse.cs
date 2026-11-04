using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 플레이어가 서 있는 바닥이 무너지는 연출.
/// 균열(코드로 그린 갈라진 선) → 진동/먼지 → 가운데부터 바깥으로 타일이 부서져 떨어짐 → 아래는 검은 구멍.
/// Tilemap 바닥 + (선택) 일반 오브젝트 바닥 조각 모두 지원.
/// </summary>
public class EagleFloorCollapse : MonoBehaviour
{
    [Header("무너질 바닥")]
    [Tooltip("바닥 타일맵들 (지면 + 지면 장식 등)")]
    [SerializeField] private Tilemap[] tilemaps;
    [Tooltip("타일맵이 아닌 바닥 오브젝트(스프라이트 땅 조각 등). 반경 안의 것만 떨어진다")]
    [SerializeField] private GameObject[] objectPieces;
    [Tooltip("플레이어 기준 좌우로 몇 칸까지 무너뜨릴지")]
    [SerializeField] private int radiusCells = 5;
    [Tooltip("켜면 반경 무시하고 타일맵 전체가 무너짐")]
    [SerializeField] private bool wholeFloor = false;

    [Header("타이밍")]
    [SerializeField] private float crackTime = 1.8f;
    [Tooltip("가운데에서 한 칸 멀어질 때마다 늘어나는 지연")]
    [SerializeField] private float spreadDelay = 0.07f;
    [SerializeField] private float fallGravity = 26f;

    [Header("연출")]
    [SerializeField] private Color crackColor = new Color(0.05f, 0.03f, 0.03f, 1f);
    [SerializeField] private Color dustColor = new Color(0.75f, 0.68f, 0.58f, 0.8f);
    [SerializeField] private bool pitBackdrop = true;
    [Tooltip("구멍 아래 배경 그림 (eagle_pit_bg_1920x2400). 비워두면 검은 사각형")]
    [SerializeField] private Sprite pitSprite;

    public bool IsCollapsing { get; private set; }

    private struct Piece { public Transform t; public Vector2 v; public float spin; public float life; public SpriteRenderer sr; }

    public IEnumerator Collapse(float centerX, float floorTopY)
    {
        IsCollapsing = true;
        int order = 0; int layerId = 0;
        if (tilemaps != null)
            foreach (var tm in tilemaps)
            {
                if (tm == null) continue;
                var r = tm.GetComponent<TilemapRenderer>();
                if (r != null && r.sortingOrder >= order) { order = r.sortingOrder; layerId = r.sortingLayerID; }
            }

        // 구멍 배경/떨어지는 조각이 플레이어를 가리지 않도록, 플레이어가 땅 타일보다 뒤에 그려지는 구성이면 그 아래 순서로 맞춘다
        int pitLayer = layerId, pitOrder = order - 1;
        bool useChunkOverride = false; int chunkLayer = 0, chunkOrder = 0;
        var pgo = GameObject.FindGameObjectWithTag("Player");
        var psr = pgo != null ? pgo.GetComponentInChildren<SpriteRenderer>() : null;
        if (psr != null)
        {
            int pv = SortingLayer.GetLayerValueFromID(psr.sortingLayerID), tv = SortingLayer.GetLayerValueFromID(layerId);
            if (pv < tv || (pv == tv && psr.sortingOrder <= order + 2))
            {
                pitLayer = psr.sortingLayerID; pitOrder = psr.sortingOrder - 2;
                useChunkOverride = true; chunkLayer = psr.sortingLayerID; chunkOrder = psr.sortingOrder - 1;
            }
        }

        float cell = 1f;
        if (tilemaps != null && tilemaps.Length > 0 && tilemaps[0] != null) cell = tilemaps[0].cellSize.x * Mathf.Abs(tilemaps[0].transform.lossyScale.x);
        float halfW = wholeFloor ? 60f : (radiusCells + 0.5f) * cell;

        // 1) 균열 + 진동
        var cracks = new List<SpriteRenderer>();
        BuildCracks(centerX, floorTopY, halfW, layerId, order + 3, cracks);
        float t = 0f;
        float dustT = 0f;
        EagleCamShake.Add(0.12f, crackTime);
        while (t < crackTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / crackTime);
            for (int i = 0; i < cracks.Count; i++)
            {
                // 가운데부터 바깥으로 자라남
                float reveal = Mathf.Clamp01((k * 1.25f) - cracks[i].transform.localPosition.z);
                var c = crackColor; c.a = reveal;
                cracks[i].color = c;
            }
            dustT -= Time.deltaTime;
            if (dustT <= 0f)
            {
                dustT = Mathf.Lerp(0.18f, 0.05f, k);
                float dx = centerX + Random.Range(-halfW, halfW) * Mathf.Lerp(0.3f, 1f, k);
                StartCoroutine(Puff(new Vector3(dx, floorTopY + 0.05f, 0f), layerId, order + 4));
            }
            if (k > 0.55f) EagleCamShake.Add(Mathf.Lerp(0.12f, 0.3f, k), 0.2f);
            yield return null;
        }

        // 2) 구멍 뒷배경
        SpriteRenderer pit = null;
        if (pitBackdrop)
        {
            pit = new GameObject("PitBackdrop").AddComponent<SpriteRenderer>();
            pit.sortingLayerID = pitLayer;
            pit.sortingOrder = pitOrder;
            if (pitSprite != null)
            {
                pit.sprite = pitSprite;
                var cam = Camera.main;
                float camW = cam != null ? cam.orthographicSize * 2f * cam.aspect : halfW * 2f + cell * 2f;
                float sc = camW * 1.05f / Mathf.Max(0.01f, pitSprite.bounds.size.x);
                float h = pitSprite.bounds.size.y * sc;
                float cx = cam != null ? cam.transform.position.x : centerX;
                pit.transform.position = new Vector3(cx, floorTopY - h * 0.5f, 0f);
                pit.transform.localScale = new Vector3(sc, sc, 1f);
            }
            else
            {
                pit.sprite = EagleVis.White;
                pit.color = Color.black;
                float w = halfW * 2f + cell * 2f, h = 40f;
                pit.transform.position = new Vector3(centerX, floorTopY - h * 0.5f, 0f);
                pit.transform.localScale = new Vector3(w, h, 1f);
            }
        }

        // 3) 타일 붕괴 (가운데 → 바깥)
        var fallers = new List<Piece>();
        var spawnQueue = new List<(float delay, System.Action act)>();
        EagleCamShake.Add(0.45f, 1.2f);
        GameSfx.Play(GameSfxId.EagleStomp);

        if (tilemaps != null)
            foreach (var tm in tilemaps)
            {
                if (tm == null) continue;
                var tr = tm.GetComponent<TilemapRenderer>();
                int sl = tr != null ? tr.sortingLayerID : layerId;
                int so = tr != null ? tr.sortingOrder : order;
                var bounds = tm.cellBounds;
                int cc = tm.WorldToCell(new Vector3(centerX, floorTopY, 0f)).x;
                var scale = tm.transform.lossyScale;
                foreach (var pos in bounds.allPositionsWithin)
                {
                    if (!tm.HasTile(pos)) continue;
                    int dx = Mathf.Abs(pos.x - cc);
                    if (!wholeFloor && dx > radiusCells) continue;
                    var sprite = tm.GetSprite(pos);
                    var col = tm.GetColor(pos);
                    var world = tm.GetCellCenterWorld(pos);
                    var p = pos;
                    var tmap = tm;
                    float delay = dx * spreadDelay + Random.Range(0f, 0.12f);
                    spawnQueue.Add((delay, () =>
                    {
                        tmap.SetTile(p, null);
                        if (sprite == null) return;
                        var go = new GameObject("FloorChunk");
                        go.transform.position = world;
                        go.transform.localScale = scale;
                        var sr = go.AddComponent<SpriteRenderer>();
                        sr.sprite = sprite; sr.color = col; sr.sortingLayerID = useChunkOverride ? chunkLayer : sl; sr.sortingOrder = useChunkOverride ? chunkOrder : so + 2;
                        fallers.Add(new Piece
                        {
                            t = go.transform,
                            sr = sr,
                            life = 0f,
                            v = new Vector2(Random.Range(-1.6f, 1.6f), Random.Range(0.5f, 3f)),
                            spin = Random.Range(-220f, 220f)
                        });
                    }
                    ));
                }
            }

        if (objectPieces != null)
            foreach (var go in objectPieces)
            {
                if (go == null) continue;
                float dxw = Mathf.Abs(go.transform.position.x - centerX);
                if (!wholeFloor && dxw > halfW) continue;
                var g = go;
                float delay = (dxw / cell) * spreadDelay + Random.Range(0f, 0.12f);
                spawnQueue.Add((delay, () =>
                {
                    foreach (var c in g.GetComponentsInChildren<Collider2D>()) c.enabled = false;
                    var rb = g.GetComponent<Rigidbody2D>(); if (rb != null) rb.simulated = false;
                    fallers.Add(new Piece
                    {
                        t = g.transform,
                        sr = g.GetComponentInChildren<SpriteRenderer>(),
                        life = 0f,
                        v = new Vector2(Random.Range(-1.6f, 1.6f), Random.Range(0.5f, 3f)),
                        spin = Random.Range(-160f, 160f)
                    });
                }
                ));
            }

        // 균열선 서서히 사라짐 + 구멍 보이게
        float elapsed = 0f;
        float maxDelay = 0f;
        foreach (var q in spawnQueue) if (q.delay > maxDelay) maxDelay = q.delay;
        float total = maxDelay + 1.6f;
        var done = new bool[spawnQueue.Count];
        float dustAcc = 0f;
        while (elapsed < total)
        {
            elapsed += Time.deltaTime;
            for (int i = 0; i < spawnQueue.Count; i++)
                if (!done[i] && elapsed >= spawnQueue[i].delay)
                {
                    done[i] = true; spawnQueue[i].act();
                    dustAcc += 1f;
                    if (dustAcc >= 3f) { dustAcc = 0f; StartCoroutine(Puff(fallers.Count > 0 ? fallers[fallers.Count - 1].t.position : new Vector3(centerX, floorTopY, 0f), layerId, order + 4)); }
                }
            float ck = Mathf.Clamp01(elapsed / 0.5f);
            foreach (var c in cracks) { var cc2 = c.color; cc2.a = 1f - ck; c.color = cc2; }

            for (int i = fallers.Count - 1; i >= 0; i--)
            {
                var f = fallers[i];
                if (f.t == null) { fallers.RemoveAt(i); continue; }
                f.v.y -= fallGravity * Time.deltaTime;
                f.t.position += new Vector3(f.v.x, f.v.y, 0f) * Time.deltaTime;
                f.t.Rotate(0f, 0f, f.spin * Time.deltaTime);
                f.life += Time.deltaTime;
                fallers[i] = f;
                if (f.life > 2.2f || f.t.position.y < floorTopY - 30f) { Destroy(f.t.gameObject); fallers.RemoveAt(i); }
            }
            yield return null;
        }
        foreach (var c in cracks) if (c != null) Destroy(c.gameObject);
        IsCollapsing = false;
    }

    // 균열: 가운데에서 좌우로 뻗는 지그재그 선들. localPosition.z 를 '나타나는 시점(0~0.8)' 으로 재활용
    private void BuildCracks(float cx, float topY, float halfW, int layerId, int order, List<SpriteRenderer> list)
    {
        int branches = 7;
        for (int b = 0; b < branches; b++)
        {
            float dir = b % 2 == 0 ? 1f : -1f;
            Vector2 p = new Vector2(cx + Random.Range(-0.6f, 0.6f), topY - 0.02f);
            float ang = Random.Range(-35f, 10f);
            int segs = Random.Range(7, 11);
            for (int s = 0; s < segs; s++)
            {
                float len = Random.Range(0.35f, 0.8f);
                float rad = (dir > 0 ? ang : 180f - ang) * Mathf.Deg2Rad;
                Vector2 d = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                Vector2 q = p + d * len;
                if (Mathf.Abs(q.x - cx) > halfW) break;
                var sr = new GameObject("Crack").AddComponent<SpriteRenderer>();
                sr.sprite = EagleVis.White;
                sr.sortingLayerID = layerId; sr.sortingOrder = order;
                float thick = Mathf.Lerp(0.14f, 0.05f, s / (float)segs);
                sr.transform.SetParent(transform, false);
                sr.transform.position = new Vector3((p.x + q.x) * 0.5f, (p.y + q.y) * 0.5f, 0f);
                sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                sr.transform.localScale = new Vector3(len, thick, 1f);
                var lp = sr.transform.localPosition;
                sr.transform.localPosition = new Vector3(lp.x, lp.y, Mathf.Clamp01(Mathf.Abs(p.x - cx) / halfW) * 0.8f);
                sr.color = new Color(crackColor.r, crackColor.g, crackColor.b, 0f);
                list.Add(sr);
                p = q;
                ang += Random.Range(-40f, 40f);
                ang = Mathf.Clamp(ang, -75f, 40f);
            }
        }
    }

    private IEnumerator Puff(Vector3 pos, int layerId, int order)
    {
        int n = 5;
        var objs = new Transform[n]; var srs = new SpriteRenderer[n]; var vel = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            var sr = new GameObject("Dust").AddComponent<SpriteRenderer>();
            sr.sprite = EagleVis.White; sr.sortingLayerID = layerId; sr.sortingOrder = order; sr.color = dustColor;
            sr.transform.position = pos + new Vector3(Random.Range(-0.4f, 0.4f), 0f, 0f);
            float s = Random.Range(0.12f, 0.3f);
            sr.transform.localScale = new Vector3(s, s, 1f);
            objs[i] = sr.transform; srs[i] = sr; vel[i] = new Vector2(Random.Range(-1f, 1f), Random.Range(0.8f, 2.4f));
        }
        for (float t = 0f; t < 0.9f; t += Time.deltaTime)
        {
            float k = t / 0.9f;
            for (int i = 0; i < n; i++)
            {
                if (objs[i] == null) continue;
                objs[i].position += (Vector3)(vel[i] * Time.deltaTime);
                var c = dustColor; c.a = dustColor.a * (1f - k); srs[i].color = c;
                objs[i].localScale *= 1f + Time.deltaTime * 1.4f;
            }
            yield return null;
        }
        for (int i = 0; i < n; i++) if (objs[i] != null) Destroy(objs[i].gameObject);
    }
}