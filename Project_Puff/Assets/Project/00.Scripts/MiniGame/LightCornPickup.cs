using UnityEngine;

/// <summary>
/// 미니게임 중에만 정해진 자리에 나타나는 "빛 뻥튀기". 가까이 가서 닿으면 먹고, 시야와 밝기가 한 단계 영구적으로 올라간다(이 스테이지 동안).
/// 프리팹: SpriteRenderer(Unlit 추천) + Is Trigger Collider2D(CircleCollider2D) + 이 스크립트.
/// 씬에 직접 배치하고, StageMiniGame의 "Only In Mini Game" 목록에 넣으면 미니게임 때만 나타난다.
/// 한 번 먹으면 죽거나 낙사해도 다시 나타나지 않는다(빛 단계도 유지).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class LightCornPickup : MonoBehaviour
{
    [SerializeField] private StageMiniGame miniGame;      // 비우면 씬에서 자동으로 찾음
    [SerializeField] private float bobHeight = 0.12f;
    [SerializeField] private float bobSpeed = 2.2f;

    private SpriteRenderer sr;
    private Collider2D col;
    private Vector3 basePos;
    private bool collected;

    private void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        basePos = transform.position;
        if (!col.isTrigger) Debug.LogWarning("[LightCornPickup] 콜라이더의 Is Trigger를 켜 주세요.", this);
    }

    private void Update()
    {
        if (collected) return;
        transform.position = basePos + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected || other.GetComponentInParent<PlayerHealth>() == null) return;

        collected = true;
        if (sr != null) sr.enabled = false;
        col.enabled = false;

        if (miniGame == null) miniGame = FindFirstObjectByType<StageMiniGame>();
        if (miniGame != null) miniGame.AddLight();
        GameSfx.PlayAt(GameSfxId.ItemPpeongGet, transform.position);
    }
}
