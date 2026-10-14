using UnityEngine;

/// <summary>
/// 클리어 문. 평소에는 잠겨 있고(보스 처치 등), Unlock()되면 열린 모양이 되어 상호작용 가능.
/// 문 앞에서 위쪽 방향키를 누르면 Result 모드로 전환(승리 UI).
/// 문 오브젝트에 SpriteRenderer, Animator(선택), Is Trigger BoxCollider2D, 이 스크립트를 붙인다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class GoalDoor : MonoBehaviour, IInteractable
{
    [SerializeField] private bool startUnlocked = true;    // 보스가 없는 스테이지는 true. 보스 스테이지는 false로 두고 보스 처치 시 Unlock()

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite closedSprite;          // goal_door_closed
    [SerializeField] private Animator animator;            // 열린 모습 2프레임 애니메이션(선택). Open 트리거 사용

    private GameModeController gameMode;
    private bool unlocked;

    public bool CanInteract => unlocked;
    public InteractionPromptKind PromptKind => InteractionPromptKind.KeyUp;
    public InteractionInput Input => InteractionInput.Up;

    private void Awake()
    {
        gameMode = FindFirstObjectByType<GameModeController>();
        if (gameMode == null) Debug.LogWarning("[GoalDoor] GameModeController를 찾지 못했습니다.", this);

        var col = GetComponent<Collider2D>();
        if (!col.isTrigger) Debug.LogWarning("[GoalDoor] 콜라이더의 Is Trigger를 켜 주세요.", this);
    }

    private void Start()
    {
        if (startUnlocked) ApplyUnlocked(); else Lock();   // 씬 시작 때는 소리 없이
    }

    public void Lock()
    {
        unlocked = false;
        if (animator != null) animator.enabled = false;
        if (spriteRenderer != null && closedSprite != null) spriteRenderer.sprite = closedSprite;
    }

    /// <summary>보스를 잡았을 때 등 외부에서 호출</summary>
    public void Unlock()
    {
        if (unlocked) return;
        ApplyUnlocked();
        GameSfx.PlayAt(GameSfxId.DoorOpen, transform.position);
    }

    private void ApplyUnlocked()
    {
        unlocked = true;
        if (animator != null)
        {
            animator.enabled = true;
            animator.SetTrigger("Open");
        }
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (!unlocked || gameMode == null) return;
        GameSfx.PlayAt(GameSfxId.DoorOpen, transform.position);
        gameMode.SetMode(GameMode.Result);
    }
}
