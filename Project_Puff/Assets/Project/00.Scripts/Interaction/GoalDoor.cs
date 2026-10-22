using UnityEngine;

/// <summary>
/// 클리어 문. 평소에는 잠겨 있고(보스 처치 등), Unlock()되면 열린 모양이 되어 상호작용 가능.
/// 문 앞에서 위쪽 방향키를 누르면 Result 모드로 전환(승리 UI).
/// 문 오브젝트에 SpriteRenderer, Animator(선택), Is Trigger BoxCollider2D, 이 스크립트를 붙인다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class GoalDoor : MonoBehaviour, IInteractable
{
    [SerializeField] private bool startUnlocked = true;
    [Tooltip("연결하면 이 미니게임을 깨기 전까지 문이 잠겨 있다. 문 앞에서 ↑ = 미니게임 시작, 미니게임 중 문에 도착해 ↑ = 클리어 후 문 해금")]
    [SerializeField] private StageMiniGame miniGame;    // 보스가 없는 스테이지는 true. 보스 스테이지는 false로 두고 보스 처치 시 Unlock()

    [Header("다음 씬으로 넘어가기 전 연출 (선택)")]
    [Tooltip("연결하면 문에 들어갈 때 이 연출의 떡밥(PlayTeaser)을 먼저 보여주고, 끝난 뒤 Result로 넘어간다. ChickenOutroSequence / MechaOutroSequence 를 끌어다 놓는다")]
    [SerializeField] private MonoBehaviour teaserBeforeResult;
    private ITeaserSequence teaser;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite closedSprite;          // goal_door_closed
    [SerializeField] private Animator animator;            // 열린 모습 2프레임 애니메이션(선택). Open 트리거 사용

    private GameModeController gameMode;
    private bool unlocked;
    private bool leaving;       // 문에 들어가 떡밥/전환이 진행 중

    private bool NeedsMiniGame => miniGame != null && !miniGame.Cleared;
    public bool CanInteract => !leaving && (unlocked || NeedsMiniGame);
    public InteractionPromptKind PromptKind => InteractionPromptKind.KeyUp;
    public InteractionInput Input => InteractionInput.Up;

    private void Awake()
    {
        teaser = teaserBeforeResult as ITeaserSequence;
        if (teaserBeforeResult != null && teaser == null) Debug.LogWarning("[GoalDoor] Teaser Before Result 에는 ITeaserSequence(치킨/메카 아웃트로)를 연결해야 합니다.", this);
        gameMode = FindFirstObjectByType<GameModeController>();
        if (gameMode == null) Debug.LogWarning("[GoalDoor] GameModeController를 찾지 못했습니다.", this);

        var col = GetComponent<Collider2D>();
        if (!col.isTrigger) Debug.LogWarning("[GoalDoor] 콜라이더의 Is Trigger를 켜 주세요.", this);
    }

    private void Start()
    {
        if (startUnlocked && !NeedsMiniGame) ApplyUnlocked(); else Lock();   // 씬 시작 때는 소리 없이
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
        if (gameMode == null || leaving) return;

        if (NeedsMiniGame)
        {
            if (miniGame.IsRunning) { miniGame.Clear(); Unlock(); }   // 미니게임 중 문에 도착 = 클리어
            else miniGame.Enter();                                     // 처음 도착 = 미니게임 시작
            return;
        }

        if (!unlocked) return;
        GameSfx.PlayAt(GameSfxId.DoorOpen, transform.position);

        // 떡밥 연출이 연결돼 있으면 먼저 보여주고, 끝나면 Result로
        if (teaser != null && !teaser.IsTeaserPlaying)
        {
            leaving = true;
            teaser.TeaserCompleted.AddListener(GoToResult);
            teaser.PlayTeaser();
            return;
        }

        gameMode.SetMode(GameMode.Result);
    }

    private void GoToResult()
    {
        if (teaser != null) teaser.TeaserCompleted.RemoveListener(GoToResult);
        if (gameMode != null) gameMode.SetMode(GameMode.Result);
    }
}
