using System.Collections;
using UnityEngine;

/// <summary>
/// 낙뢰 구역(3챕터 함정). 플레이어가 이 구역 안에 있는 동안 일정 주기로 바닥에 경고가 뜨고 번개가 떨어진다.
/// 번개 위치는 구역 안에서 정해진다. 일부는 플레이어 근처를 노린다(Target Player Chance).
/// 근처에 LightningRod(피뢰침)가 있으면 번개가 그쪽으로 끌려가 주변이 안전지대가 된다.
/// 구성: BoxCollider2D(Is Trigger)로 구역을 잡고, 번개 프리팹은 CloudLightningStrike(번개구름과 같은 것)를 쓴다.
/// 구역 안에 플레이어가 있어야만 동작하므로 화면 밖에서 번개가 낭비되지 않는다.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class LightningField : MonoBehaviour
{
    [Header("번개")]
    [SerializeField] private CloudLightningStrike strikePrefab;
    [SerializeField] private LayerMask playerLayer;
    [Tooltip("번개가 닿을 바닥 레이어 (Ground)")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private int damage = 2;
    [SerializeField] private float strikeWidth = 1.6f;
    [Tooltip("경고가 뜨고 번개가 떨어지기까지 = 플레이어가 피할 시간")]
    [SerializeField] private float warnTime = 1.1f;
    [SerializeField] private float strikeTime = 0.25f;

    [Header("주기")]
    [Tooltip("구역에 들어온 뒤 첫 번개까지 (놀라지 않게 여유)")]
    [SerializeField] private float firstDelay = 1.2f;
    [Tooltip("한 번에 떨어지는 번개 수 (최소~최대, 포함)")]
    [SerializeField] private Vector2Int strikesPerVolley = new Vector2Int(1, 2);
    [Tooltip("같은 묶음 안 번개 사이 시간 간격")]
    [SerializeField] private float volleyGap = 0.25f;
    [Tooltip("묶음과 묶음 사이 쉬는 시간 (최소~최대 랜덤)")]
    [SerializeField] private Vector2 pauseBetween = new Vector2(1.6f, 2.6f);

    [Header("위치")]
    [Tooltip("번개 중 플레이어 근처를 노리는 비율 (0~1). 나머지는 구역 안 랜덤")]
    [SerializeField, Range(0f, 1f)] private float targetPlayerChance = 0.5f;
    [Tooltip("플레이어를 노릴 때 위치에 더하는 무작위 오차(±). 0이면 정확히 그 자리")]
    [SerializeField] private float targetJitter = 0.8f;
    [Tooltip("같은 묶음 안 번개끼리 최소 이만큼 떨어뜨림")]
    [SerializeField] private float minSpacing = 2.5f;

    [Header("피뢰침")]
    [Tooltip("이 거리 안에 피뢰침이 있으면 번개가 그쪽으로 떨어짐")]
    [SerializeField] private float rodAttractRange = 6f;

    private BoxCollider2D box;
    private Transform player;
    private Coroutine routine;
    private bool playerInside;

    private void Awake()
    {
        box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;
    }

    private void Start()
    {
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;
    }

    private void OnTriggerEnter2D(Collider2D other) { if (IsPlayer(other)) SetInside(true); }
    private void OnTriggerExit2D(Collider2D other) { if (IsPlayer(other)) SetInside(false); }

    private bool IsPlayer(Collider2D c) => c.GetComponentInParent<PlayerController>() != null;

    private void SetInside(bool inside)
    {
        if (inside == playerInside) return;
        playerInside = inside;
        if (inside) { if (routine == null) routine = StartCoroutine(Run()); }
        else if (routine != null) { StopCoroutine(routine); routine = null; }   // 이미 떨어지는 중인 번개는 그대로 끝까지
    }

    private void OnDisable() { routine = null; playerInside = false; }

    private IEnumerator Run()
    {
        yield return new WaitForSeconds(firstDelay);
        var used = new System.Collections.Generic.List<float>();

        while (true)
        {
            used.Clear();
            int n = Random.Range(strikesPerVolley.x, strikesPerVolley.y + 1);
            for (int i = 0; i < Mathf.Max(1, n); i++)
            {
                SpawnStrike(PickX(used));
                if (i < n - 1) yield return new WaitForSeconds(volleyGap);
            }
            yield return new WaitForSeconds(Random.Range(pauseBetween.x, pauseBetween.y));
        }
    }

    private float PickX(System.Collections.Generic.List<float> used)
    {
        Bounds b = box.bounds;
        float x = b.center.x;
        for (int tries = 0; tries < 8; tries++)
        {
            if (player != null && Random.value < targetPlayerChance)
                x = player.position.x + Random.Range(-targetJitter, targetJitter);
            else
                x = Random.Range(b.min.x, b.max.x);
            x = Mathf.Clamp(x, b.min.x, b.max.x);

            bool ok = true;
            foreach (var u in used) if (Mathf.Abs(u - x) < minSpacing) { ok = false; break; }
            if (ok) break;
        }
        used.Add(x);
        return x;
    }

    private void SpawnStrike(float x)
    {
        if (strikePrefab == null)
        {
            Debug.LogWarning("[LightningField] Strike Prefab이 연결되지 않았습니다.", this);
            return;
        }

        LightningRod rod = LightningRod.FindNear(x, rodAttractRange);
        Vector3 pos;
        if (rod != null) pos = rod.transform.position;
        else pos = new Vector3(x, GroundYAt(x), 0f);

        var s = Instantiate(strikePrefab, pos, Quaternion.identity);
        s.Init(damage, strikeWidth, warnTime, strikeTime, playerLayer, rod);
    }

    /// <summary>x 위치 바닥 높이. 구역 위에서 아래로 쏴서 찾음, 없으면 구역 바닥</summary>
    private float GroundYAt(float x)
    {
        Bounds b = box.bounds;
        if (groundLayer.value != 0)
        {
            var hit = Physics2D.Raycast(new Vector2(x, b.max.y), Vector2.down, b.size.y + 5f, groundLayer);
            if (hit.collider != null) return hit.point.y;
        }
        return b.min.y;
    }

    private void OnDrawGizmos()
    {
        var bx = box != null ? box : GetComponent<BoxCollider2D>();
        if (bx == null) return;
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(bx.bounds.center, bx.bounds.size);
    }
}