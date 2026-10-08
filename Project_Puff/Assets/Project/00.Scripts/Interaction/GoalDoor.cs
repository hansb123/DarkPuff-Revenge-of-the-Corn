using UnityEngine;

/// <summary>
/// 클리어 문. 평소에는 잠겨 있고(보스 처치 등), Unlock()되면 열린 모양이 되어 상호작용 가능.
/// 상호작용하면 Result 모드로 전환(승리 UI).
/// 문 오브젝트에 SpriteRenderer, Animator(선택), Is Trigger BoxCollider2D, 이 스크립트를 붙인다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class GoalDoor : MonoBehaviour, IInteractable
{
    [SerializeField] private bool startUnlocked = false;   // 1차 빌드처럼 보스가 없으면 true

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite closedSprite;          // goal_door_closed
    [SerializeField] private Animator animator;            // 열린 모습 2프레임 애니메이션(선택). Open 트리거 사용

    private GameModeController gameMode;
    private bool unlocked;

    public bool CanInteract => unlocked;
    public InteractionPromptKind PromptKind => InteractionPromptKind.Key;

    private void Awake()
    {
        gameMode = FindFirstObjectByType<GameModeController>();
        if (gameMode == null) Debug.Log(" GameModeController를 찾지 못했습니다.", this);

        var col = GetComponent<Collider2D>();
        
    }

    private void Start()
    {
        if (startUnlocked) Unlock(); else Lock();
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
        gameMode.SetMode(GameMode.Result);
    }
}
