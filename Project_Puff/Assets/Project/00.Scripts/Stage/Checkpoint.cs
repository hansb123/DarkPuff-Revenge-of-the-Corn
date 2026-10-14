using UnityEngine;

/// <summary>
/// 체크포인트. Trigger Collider2D가 있는 오브젝트에 붙이면 플레이어가 닿을 때 활성화된다.
/// 한 번 활성화되면 그 스테이지에서는 되돌아가지 않는다 (더 앞쪽 체크포인트가 항상 우선).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    [Tooltip("부활 위치. 비워 두면 이 오브젝트 위치")]
    [SerializeField] private Transform respawnPoint;

    [Header("표시 (선택)")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite inactiveSprite;
    [SerializeField] private Sprite activeSprite;

    [Header("Player")]
    [SerializeField] private string playerTag = "Player";

    public Vector2 RespawnPosition => respawnPoint != null ? respawnPoint.position : transform.position;
    public bool Activated { get; private set; }

    private void Reset()
    {
        // 컴포넌트를 처음 붙일 때 Trigger로 자동 설정
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Start()
    {
        ApplyVisual();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("닿음: " + other.name + " / 태그 " + other.tag);
        if (Activated || !other.CompareTag(playerTag)) return;

        var manager = RespawnManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[Checkpoint] 씬에 RespawnManager가 없습니다.", this);
            return;
        }

        Activated = true;
        manager.SetCheckpoint(this);
        GameSfx.PlayAt(GameSfxId.CheckpointOn, transform.position);
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        if (spriteRenderer == null) return;
        var sprite = Activated ? activeSprite : inactiveSprite;
        if (sprite != null) spriteRenderer.sprite = sprite;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Activated ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(RespawnPosition, 0.3f);
    }
}
