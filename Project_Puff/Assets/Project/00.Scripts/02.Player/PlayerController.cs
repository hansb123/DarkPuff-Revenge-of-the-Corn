using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어 컨트롤러
/// 어떤 상태에서 뭘 할지는 PlayerStates의 각 상태 클래스가 결정
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpSpeed = 12f;
    [Tooltip("착지 직전에 눌러도 이 시간(초) 안이면 점프로 인정")]
    [SerializeField] private float jumpBufferTime = 0.1f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;      // 발 밑에 둔 자식 오브젝트
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;          

    [Header("Debug")]
    [SerializeField] private bool showDebugLabel = true; // 확인용 화면 표시.

    private Rigidbody2D rb;
    private InputAction moveAction;
    private InputAction jumpAction;

    private readonly StateMachine machine = new StateMachine();
    private float lastJumpPressedTime = float.NegativeInfinity;

    public Rigidbody2D Rb => rb;
    public float MoveSpeed => moveSpeed;
    public bool IsGrounded { get; private set; }

    /// <summary>
    /// 플레이어 State 프로퍼티
    /// </summary>
    public PlayerState IdleState { get; private set; }
    public PlayerState RunState { get; private set; }
    public PlayerState JumpState { get; private set; }

    public float MoveInput => moveAction != null ? moveAction.ReadValue<Vector2>().x : 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (inputActions != null)
        {
            moveAction = inputActions.FindAction($"{InputMaps.Platformer}/Move", false);
            jumpAction = inputActions.FindAction($"{InputMaps.Platformer}/Jump", false);
        }
        else
        {
            
        }

        IdleState = new PlayerIdleState(this);
        RunState = new PlayerRunState(this);
        JumpState = new PlayerJumpState(this);
    }

    private void OnEnable()
    {
        if (jumpAction != null) jumpAction.performed += OnJumpPerformed;
    }

    private void OnDisable()
    {
        if (jumpAction != null) jumpAction.performed -= OnJumpPerformed;
    }

    private void Start()
    {
        machine.ChangeState(IdleState);
    }

    private void Update()
    {
        IsGrounded = CheckGrounded();
        machine.Tick();
    }

    // ---------------- 입력 ----------------

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        lastJumpPressedTime = Time.time;
    }

    /// <summary>
    /// 점프 입력이 버퍼 시간 안에 있었는지 확인하고, 있었다면 소비(한 번만 true).
    /// </summary>
    public bool ConsumeJump()
    {
        if (Time.time - lastJumpPressedTime > jumpBufferTime)
        {
            return false;
        }

        lastJumpPressedTime = float.NegativeInfinity;
        return true;
    }

    public void ChangeState(PlayerState next) //스테이트 전환.
    {
        machine.ChangeState(next);
    }

    public void SetHorizontalVelocity(float velocityX)
    {
        rb.linearVelocity = new Vector2(velocityX, rb.linearVelocity.y);
    }

    public void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpSpeed);
    }

    public void FaceDirection(float directionX) //캐릭터 바라보는거 
    {

        if (spriteRenderer != null && Mathf.Abs(directionX) > 0.01f)
        {
            spriteRenderer.flipX = directionX < 0f;
        }
    }

    public void PlayAnimation(string stateName) //애니메이션
    {
        if (animator != null)
        {
            animator.Play(stateName);
        }
    }

    private bool CheckGrounded() //땅 체크 
    {
        if (groundCheck == null) return false;

        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer) != null;
    }



    //기즈모, 각종 디버그 

    private void OnGUI()
    {
        if (!showDebugLabel) return;

        string stateName = machine.Current != null ? machine.Current.GetType().Name : "-";
        GUI.Label(new Rect(10, 10, 360, 24), $"Player: {stateName}  |  Grounded: {IsGrounded}");
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}