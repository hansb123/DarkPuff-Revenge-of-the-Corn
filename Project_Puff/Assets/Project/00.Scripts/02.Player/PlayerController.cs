using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어(튀밥) 컨트롤러. 입력 읽기, 이동/점프 물리, 바닥 판정, 상태머신을 담당.
/// 어떤 상태에서 뭘 할지는 PlayerStates.cs의 각 상태 클래스가 결정함.
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

    [Header("Jump Feel")]
    [Tooltip("발판에서 벗어난 직후에도 이 시간(초) 안이면 점프 가능 (코요테 타임)")]
    [SerializeField] private float coyoteTime = 0.1f;
    [Tooltip("상승 중 점프 버튼을 떼면 위쪽 속도에 곱함. 작을수록 짧게 뜀 (0.5 = 절반)")]
    [Range(0f, 1f)]
    [SerializeField] private float jumpCutMultiplier = 0.5f;
    [Tooltip("내려올 때 중력 배율. 클수록 쫀쫀하게 떨어짐")]
    [SerializeField] private float fallGravityMultiplier = 1.8f;
    [Tooltip("낙하 최대 속도(양수로 입력)")]
    [SerializeField] private float maxFallSpeed = 20f;

    [Header("Hit")]
    [SerializeField] private PlayerHealth health;       // 비워두면 같은 오브젝트에서 자동으로 찾음
    [Tooltip("맞았을 때 밀려나는 속도 (x: 옆으로, y: 위로)")]
    [SerializeField] private Vector2 hitKnockback = new Vector2(5f, 6f);
    [Tooltip("맞은 뒤 조작이 막히는 시간(초). PlayerHealth의 무적 시간보다 짧아야 함")]
    [SerializeField] private float hitStunTime = 0.35f;

    [Header("Attack")]
    [SerializeField] private PlayerAttack attack;       // 비워두면 같은 오브젝트에서 자동으로 찾음
    [Tooltip("공격 키를 미리 눌러도 이 시간(초) 안이면 공격으로 인정")]
    [SerializeField] private float attackBufferTime = 0.12f;

    [Header("Special Attack")]
    [SerializeField] private PlayerSpecialAttack special;   // 비워두면 같은 오브젝트에서 자동으로 찾음
    [SerializeField] private float specialBufferTime = 0.12f;

    [Header("Glide (활공)")]
    [Tooltip("비워두면 같은 오브젝트에서 자동으로 찾음. 없거나 해금 전이면 활공 불가")]
    [SerializeField] private GlideAbility glide;

    [Header("Sound")]
    [Tooltip("달릴 때 발소리 간격(초)")]
    [SerializeField] private float stepInterval = 0.28f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;      // 발 밑에 둔 자식 오브젝트
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;          // 선택. 없으면 애니메이션만 건너뜀

    [Header("Debug")]
    [SerializeField] private bool showDebugLabel = true; // 확인용 화면 표시. 나중에 끄세요

    private Rigidbody2D rb;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction attackAction;
    private InputAction specialAction;

    private readonly StateMachine machine = new StateMachine();
    private float lastJumpPressedTime = float.NegativeInfinity;

    private float baseGravityScale;
    private float lastGroundedTime = float.NegativeInfinity;
    private bool coyoteAvailable;   // 점프하면 false, 다시 땅에 서면 true
    private bool jumpCutDone;       // 한 번의 점프에서 컷은 한 번만
    private HardenAbility harden;   // 단단해지기 (있으면 이동속도 배율 적용)
    private bool wasGliding;
    private bool glideArmed;        // 공중에서 점프 버튼을 (다시) 눌렀다 → 누르고 있는 동안 활공

    public Rigidbody2D Rb => rb;
    public float MoveSpeed => moveSpeed * (harden != null ? harden.SpeedMultiplier : 1f);
    public bool IsGrounded { get; private set; }

    public PlayerState IdleState { get; private set; }
    public PlayerState RunState { get; private set; }
    public PlayerState JumpState { get; private set; }
    public PlayerHitState HitState { get; private set; }
    public PlayerState DeadState { get; private set; }
    public PlayerAttackState AttackState { get; private set; }
    public PlayerSpecialState SpecialState { get; private set; }
    public PlayerSpecialAttack Special => special;
    private float lastSpecialPressedTime = float.NegativeInfinity;

    public PlayerAttack Attack => attack;
    private float lastAttackPressedTime = float.NegativeInfinity;

    /// <summary>바라보는 방향 (1 = 오른쪽, -1 = 왼쪽)</summary>
    public int FacingDir => spriteRenderer != null && spriteRenderer.flipX ? -1 : 1;

    public float HitStunTime => hitStunTime;
    public float StepInterval => stepInterval;
    public bool IsDead => health != null && health.IsDead;
    private Vector2 lastHitFrom;

    // 위/아래 입력(-1 ~ 1). 아래 + 특수공격 = 로켓점프 판정에 사용
    public float MoveInputY => moveAction != null ? moveAction.ReadValue<Vector2>().y : 0f;

    // 좌우 입력(-1 ~ 1). Platformer 맵이 꺼져 있으면 자동으로 0
    public float MoveInput => moveAction != null ? moveAction.ReadValue<Vector2>().x : 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        baseGravityScale = rb.gravityScale;

        if (inputActions != null)
        {
            moveAction = inputActions.FindAction($"{InputMaps.Platformer}/Move", false);
            jumpAction = inputActions.FindAction($"{InputMaps.Platformer}/Jump", false);
            attackAction = inputActions.FindAction($"{InputMaps.Platformer}/Attack", false);
            specialAction = inputActions.FindAction($"{InputMaps.Platformer}/Special", false);

            if (moveAction == null)
                Debug.LogWarning($"[PlayerController] '{InputMaps.Platformer}/Move' 액션을 찾지 못했습니다. " +
                                 $"에셋 '{inputActions.name}'의 맵/액션 이름을 확인하세요. (이 상태에서는 이동 입력이 항상 0)", this);
            else if (moveAction.expectedControlType != "Vector2")
                Debug.LogWarning($"[PlayerController] Move 액션의 Control Type이 {moveAction.expectedControlType}입니다. Vector2여야 합니다.", this);

            if (jumpAction == null)
                Debug.LogWarning($"[PlayerController] '{InputMaps.Platformer}/Jump' 액션을 찾지 못했습니다.", this);

            if (attackAction == null)
                Debug.LogWarning($"[PlayerController] '{InputMaps.Platformer}/Attack' 액션을 찾지 못했습니다. (공격 입력 없음)", this);
        }
        else
        {
            Debug.LogError("[PlayerController] Input Actions 에셋이 연결되지 않았습니다.", this);
        }

        if (special == null) special = GetComponent<PlayerSpecialAttack>();
        if (special == null) Debug.LogWarning("[PlayerController] PlayerSpecialAttack이 없습니다. 특수공격(X)을 쓸 수 없습니다.", this);
        if (specialAction == null && inputActions != null)
            Debug.LogWarning($"[PlayerController] '{InputMaps.Platformer}/Special' 액션을 찾지 못했습니다. (Input Actions에 Special 액션 + X 키 추가)", this);

        if (glide == null) glide = GetComponent<GlideAbility>();
        if (harden == null) harden = GetComponent<HardenAbility>();

        if (attack == null) attack = GetComponent<PlayerAttack>();
        if (attack == null) Debug.LogWarning("[PlayerController] PlayerAttack이 없습니다. 공격할 수 없습니다.", this);

        if (health == null) health = GetComponent<PlayerHealth>();
        if (health == null) Debug.LogWarning("[PlayerController] PlayerHealth가 없습니다. 맞아도 아무 일도 일어나지 않습니다.", this);

        IdleState = new PlayerIdleState(this);
        RunState = new PlayerRunState(this);
        JumpState = new PlayerJumpState(this);
        HitState = new PlayerHitState(this);
        DeadState = new PlayerDeadState(this);
        AttackState = new PlayerAttackState(this);
        SpecialState = new PlayerSpecialState(this);
    }

    private void OnEnable()
    {
        if (jumpAction != null) jumpAction.performed += OnJumpPerformed;
        if (attackAction != null) attackAction.performed += OnAttackPerformed;
        if (specialAction != null) specialAction.performed += OnSpecialPerformed;

        if (health != null)
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }
        if (harden != null) harden.FormChanged += OnHardenFormChanged;
    }

    private void OnDisable()
    {
        if (jumpAction != null) jumpAction.performed -= OnJumpPerformed;
        if (attackAction != null) attackAction.performed -= OnAttackPerformed;
        if (specialAction != null) specialAction.performed -= OnSpecialPerformed;

        if (health != null)
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }
        if (harden != null) harden.FormChanged -= OnHardenFormChanged;
    }

    private void Start()
    {
        machine.ChangeState(IdleState);
    }

    private void Update()
    {
        IsGrounded = CheckGrounded();

        if (IsGrounded)
        {
            lastGroundedTime = Time.time;

            // 점프 직후(상승 중)에는 아직 땅에 닿아 있다고 나오므로, 내려오거나 멈춰 있을 때만 코요테 충전
            if (rb.linearVelocity.y <= 0.01f) coyoteAvailable = true;
        }

        machine.Tick();
    }

    private void FixedUpdate()
    {
        // 내려올 때만 중력을 키워 점프를 쫀쫀하게
        bool falling = rb.linearVelocity.y < 0f;
        rb.gravityScale = falling ? baseGravityScale * fallGravityMultiplier : baseGravityScale;

        if (rb.linearVelocity.y < -maxFallSpeed)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
        }

        // 활공: 해금 후, 공중에서 점프 버튼을 누른 채 내려가는 동안 천천히 떨어짐
        bool gliding = glide != null && glide.CanGlide && glideArmed && IsJumpHeld
                       && machine.Current == JumpState && rb.linearVelocity.y < 0f;
        if (glide != null) glide.SetGliding(gliding);
        if (gliding != wasGliding)
        {
            wasGliding = gliding;
            // 활공 모션 (Animator에 "Glide" 상태가 있을 때만). 끝나면 낙하(Jump) 모션으로 복귀
            if (gliding) PlayIfExists("Glide");
            else if (machine.Current == JumpState) PlayIfExists("Jump");
        }
        if (gliding)
        {
            rb.gravityScale = baseGravityScale * glide.GravityMultiplier;
            if (rb.linearVelocity.y < -glide.FallSpeed)
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, -glide.FallSpeed);
        }
        if (IsGrounded || !IsJumpHeld) glideArmed = false;
    }

    // ---------------- 피격 / 사망 ----------------

    private void OnDamaged(Vector2 hitFrom)
    {
        if (machine.Current == DeadState) return;

        lastHitFrom = hitFrom;
        GameSfx.Play(GameSfxId.PlayerHurt);
        machine.ChangeState(HitState);   // 무적 시간이 경직보다 길어서 Hit 중에 다시 불리지 않음
    }

    private void OnDied()
    {
        GameSfx.Play(GameSfxId.PlayerDead);
        machine.ChangeState(DeadState);
    }

    /// <summary>
    /// 부활/재시작 시 호출. 속도와 입력 버퍼를 지우고 Idle로 돌려놓음.
    /// 위치 이동과 체력 회복은 RespawnManager가 먼저 처리한다.
    /// </summary>
    public void Revive()
    {
        rb.linearVelocity = Vector2.zero;
        lastJumpPressedTime = float.NegativeInfinity;
        lastAttackPressedTime = float.NegativeInfinity;
        lastSpecialPressedTime = float.NegativeInfinity;
        coyoteAvailable = false;
        glideArmed = false;
        if (glide != null) glide.SetGliding(false);
        GameSfx.Play(GameSfxId.PlayerRespawn);
        machine.ChangeState(IdleState);
    }

    /// <summary>공격이 온 반대 방향으로 튕겨 나감 (Hit 상태 진입 시 호출)</summary>
    public void ApplyHitKnockback()
    {
        float dir = transform.position.x >= lastHitFrom.x ? 1f : -1f;
        rb.linearVelocity = new Vector2(dir * hitKnockback.x, hitKnockback.y);
        coyoteAvailable = false;     // 맞고 튕긴 뒤 공중 점프 방지
    }

    // ---------------- 입력 ----------------

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        lastJumpPressedTime = Time.time;
        if (!IsGrounded) glideArmed = true;   // 공중에서 누름 → 활공 준비
    }

    private void OnAttackPerformed(InputAction.CallbackContext context)
    {
        lastAttackPressedTime = Time.time;
    }

    private void OnSpecialPerformed(InputAction.CallbackContext context)
    {
        lastSpecialPressedTime = Time.time;
    }

    /// <summary>특수공격 입력이 버퍼 시간 안에 있고 쿨타임이 끝났으면 소비(한 번만 true)</summary>
    public bool ConsumeSpecial()
    {
        if (special == null || !special.CanStart) return false;
        if (Time.time - lastSpecialPressedTime > specialBufferTime) return false;

        lastSpecialPressedTime = float.NegativeInfinity;
        return true;
    }

    /// <summary>
    /// 공격 입력이 버퍼 시간 안에 있고 쿨타임이 끝났으면 소비(한 번만 true).
    /// 쿨타임 중이면 입력을 소비하지 않아서, 버퍼 시간 안에 쿨타임이 끝나면 자동으로 이어서 공격한다.
    /// </summary>
    public bool ConsumeAttack()
    {
        if (attack == null || !attack.CanStart) return false;
        if (Time.time - lastAttackPressedTime > attackBufferTime) return false;

        lastAttackPressedTime = float.NegativeInfinity;
        return true;
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

    // ---------------- 상태 전환 / 물리 (상태 클래스가 호출) ----------------

    public void ChangeState(PlayerState next)
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
        coyoteAvailable = false;  // 공중 점프 방지 (코요테로 두 번 뛰는 것 막기)
        jumpCutDone = false;
        glideArmed = false;
        GameSfx.Play(GameSfxId.PlayerJump);
    }

    /// <summary>
    /// 로켓점프: 일반 점프의 heightMultiplier 배 높이로 솟구침. (점프 높이는 속도² 에 비례하므로 속도는 √배)
    /// 버튼 뗌으로 인한 가변 점프 컷은 적용하지 않음.
    /// </summary>
    public void RocketJump(float heightMultiplier)
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpSpeed * Mathf.Sqrt(Mathf.Max(0.01f, heightMultiplier)));
        coyoteAvailable = false;
        jumpCutDone = true;
        glideArmed = false;
    }

    /// <summary>발판에서 막 벗어난 직후라 점프를 허용할 수 있는지 (코요테 타임)</summary>
    public bool CanCoyoteJump => coyoteAvailable && Time.time - lastGroundedTime <= coyoteTime;

    public bool IsJumpHeld => jumpAction != null && jumpAction.IsPressed();

    /// <summary>상승 중 점프 버튼을 뗐을 때 한 번만 호출해 점프를 낮춤 (가변 점프)</summary>
    public void CutJumpIfReleased()
    {
        if (jumpCutDone || IsJumpHeld) return;

        if (rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
            jumpCutDone = true;
        }
    }

    public void FaceDirection(float directionX)
    {
        // 스프라이트가 기본적으로 오른쪽을 보는 기준
        if (spriteRenderer != null && Mathf.Abs(directionX) > 0.01f)
        {
            spriteRenderer.flipX = directionX < 0f;
        }
    }

    private string lastAnim;   // 마지막으로 재생한 상태 이름 (단단해지기 시작/끝날 때 같은 동작의 강철/일반 버전으로 바꾸기 위해)

    private void PlayIfExists(string stateName)
    {
        if (animator != null && animator.HasState(0, Animator.StringToHash(stateName)))
        {
            lastAnim = stateName;
            PlayResolved(stateName);
        }
    }

    public void PlayAnimation(string stateName)
    {
        if (animator != null)
        {
            lastAnim = stateName;
            PlayResolved(stateName);
        }
    }

    /// <summary>단단해지기 중이고 "Hard"+이름 상태(HardIdle, HardRun ...)가 있으면 그것을, 아니면 원래 상태를 재생</summary>
    private void PlayResolved(string stateName)
    {
        if (harden != null && harden.IsActive)
        {
            string hard = "Hard" + stateName;
            if (animator.HasState(0, Animator.StringToHash(hard))) { animator.Play(hard); return; }
        }
        animator.Play(stateName);
    }

    private void OnHardenFormChanged(bool on)
    {
        if (animator != null && !string.IsNullOrEmpty(lastAnim)) PlayResolved(lastAnim);
    }

    private bool CheckGrounded()
    {
        if (groundCheck == null) return false;

        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer) != null;
    }

    // ---------------- 디버그 ----------------

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