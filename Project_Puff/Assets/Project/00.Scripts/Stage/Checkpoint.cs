using UnityEngine;

/// <summary>
/// 체크포인트. Trigger Collider2D가 있는 오브젝트에 붙이면 플레이어가 닿을 때 활성화된다.
/// 한 번 활성화되면 그 스테이지에서는 되돌아가지 않는다 (더 앞쪽 체크포인트가 항상 우선).
///
/// Mini Game Only 체크:
///   - 켜면 미니게임 중에만 작동하고 (일반 상태에서는 닿아도 무시), 미니게임이 끝나거나 새로 시작하면 처음 상태로 돌아감
///   - 끄면 일반 체크포인트: 미니게임 중에는 작동하지 않음
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    [Tooltip("부활 위치. 비워 두면 이 오브젝트 위치")]
    [SerializeField] private Transform respawnPoint;

    [Header("종류")]
    [Tooltip("켜면 미니게임 중에만 작동하는 체크포인트")]
    [SerializeField] private bool miniGameOnly;

    [Header("표시 (선택)")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite inactiveSprite;
    [SerializeField] private Sprite activeSprite;

    [Header("Player")]
    [SerializeField] private string playerTag = "Player";

    public Vector2 RespawnPosition => respawnPoint != null ? respawnPoint.position : transform.position;
    public bool Activated { get; private set; }
    public bool MiniGameOnly => miniGameOnly;

    private void Reset()
    {
        // 컴포넌트를 처음 붙일 때 Trigger로 자동 설정
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Start()
    {
        ApplyVisual();
        if (miniGameOnly && RespawnManager.Instance != null)
            RespawnManager.Instance.MiniGameCheckpointsCleared += OnMiniGameCheckpointsCleared;
    }

    private void OnDestroy()
    {
        if (RespawnManager.Instance != null)
            RespawnManager.Instance.MiniGameCheckpointsCleared -= OnMiniGameCheckpointsCleared;
    }

    // 미니게임이 시작/종료될 때 전용 체크포인트를 처음 상태로
    private void OnMiniGameCheckpointsCleared()
    {
        Activated = false;
        ApplyVisual();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (Activated || !other.CompareTag(playerTag)) return;

        var manager = RespawnManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[Checkpoint] 씬에 RespawnManager가 없습니다.", this);
            return;
        }

        // 미니게임 중이면 전용 체크포인트만, 평소에는 일반 체크포인트만 작동
        bool inMiniGame = !manager.UseCheckpoints;
        if (miniGameOnly != inMiniGame) return;

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
        Gizmos.color = miniGameOnly
            ? (Activated ? Color.magenta : new Color(1f, 0.5f, 0f))
            : (Activated ? Color.green : Color.cyan);
        Gizmos.DrawWireSphere(RespawnPosition, 0.3f);
    }
}