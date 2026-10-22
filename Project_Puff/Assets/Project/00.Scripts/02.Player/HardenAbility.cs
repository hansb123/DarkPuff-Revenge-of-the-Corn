using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 단단해지기: 버튼을 누르면 잠시 강철 뻥튀기가 되어 무적 + 이동속도 감소. 플레이어에 붙인다.
///   - 형태(무적) 시간: formTime (기본 1초)  → 이 동안 PlayerHealth 무적 (적/함정 피해 무시, 낙사 같은 즉사는 그대로)
///   - 이동속도 배율: speedMultiplier (기본 0.5)  → 형태가 끝난 뒤에도 slowAfterForm 초 동안 유지 (기본 0)
///   - 쿨타임: 형태가 끝난 뒤 cooldown 초 (기본 5초)
/// 해금: AbilityProgress.HardUnlocked (메카 닭 클리어 알을 먹으면 해금).
/// 키: 기본 C (activateAction 에서 바꿈). 패드는 Gamepad North 버튼.
/// PlayerController 가 SpeedMultiplier 를 이동속도에 곱한다. HardenHud 가 상태를 구독해 아이콘/쿨타임을 그린다.
/// 강철 모습: Animator 에 bool 파라미터 "Hardened" 를 만들고, Idle/Run/Jump/Glide 의 강철 버전(hard_*) 상태로 전환하게 연결한다 (Any State → HardIdle 등).
/// </summary>
public class HardenAbility : MonoBehaviour
{
    [Header("효과")]
    [Tooltip("강철 상태 + 무적 지속 시간(초)")]
    [SerializeField] private float formTime = 1f;
    [Tooltip("이동속도 배율 (0.5 = 절반)")]
    [Range(0.1f, 1f)][SerializeField] private float speedMultiplier = 0.5f;
    [Tooltip("형태가 끝난 뒤에도 느린 상태가 더 이어지는 시간(초). 0이면 형태와 동시에 끝")]
    [SerializeField] private float slowAfterForm = 0f;
    [Tooltip("형태가 끝난 뒤 다시 쓸 수 있기까지(초)")]
    [SerializeField] private float cooldown = 5f;
    [Tooltip("테스트용: 해금 안 돼도 사용 가능")]
    [SerializeField] private bool forceUnlocked;

    [Header("입력")]
    [SerializeField] private InputAction activateAction = new InputAction("Harden", InputActionType.Button, "<Keyboard>/c");

    [Header("연출 (선택)")]
    [SerializeField] private Animator animator;
    [Tooltip("Animator 의 bool 파라미터 이름 (있을 때만 사용)")]
    [SerializeField] private string animatorBool = "Hardened";
    [Tooltip("변신 순간 몸 위에서 재생할 이펙트 프리팹 (OneShotEffect, hard_transform_1~3)")]
    [SerializeField] private OneShotEffect transformEffect;
    [SerializeField] private float transformEffectWidth = 2.6f;
    [Tooltip("형태 동안 몸에 겹쳐 보일 방어막 (SpriteRenderer, hard_shield_1~2 번갈아). 처음엔 비활성")]
    [SerializeField] private SpriteRenderer shield;
    [SerializeField] private Sprite[] shieldFrames;
    [SerializeField] private float shieldFps = 10f;

    [Header("이벤트")]
    public UnityEvent Activated;
    public UnityEvent Ended;

    public enum State { Locked, Ready, Active, Cooldown }

    public bool IsActive { get; private set; }
    public bool IsUnlocked => forceUnlocked || AbilityProgress.HardUnlocked;
    public float SpeedMultiplier => (IsActive || Time.time < slowUntil) ? speedMultiplier : 1f;
    public bool CanUse => IsUnlocked && !IsActive && Time.time >= readyAt && !(health != null && health.IsDead);
    public float FormTime => formTime;
    public float CooldownTime => cooldown;

    public State Current => !IsUnlocked ? State.Locked : IsActive ? State.Active : Time.time < readyAt ? State.Cooldown : State.Ready;
    /// <summary>형태가 남은 비율 1→0 (HUD 바)</summary>
    public float ActiveRemaining01 => IsActive ? Mathf.Clamp01((endAt - Time.time) / formTime) : 0f;
    /// <summary>쿨타임이 남은 비율 1→0 (HUD 쿨타임 오버레이)</summary>
    public float CooldownRemaining01 => (!IsActive && Time.time < readyAt && cooldown > 0f) ? Mathf.Clamp01((readyAt - Time.time) / cooldown) : 0f;

    /// <summary>쿨타임이 끝나 다시 쓸 수 있게 됐을 때 (HUD 번쩍)</summary>
    public event Action BecameReady;
    public event Action<bool> FormChanged;

    private PlayerHealth health;
    private float endAt, readyAt, slowUntil;
    private Coroutine routine;
    private bool hasBool, wasOnCooldown;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null)
            foreach (var p in animator.parameters)
                if (p.name == animatorBool && p.type == AnimatorControllerParameterType.Bool) { hasBool = true; break; }
        if (shield != null) shield.gameObject.SetActive(false);
    }

    private void OnEnable() { activateAction.Enable(); activateAction.performed += OnPressed; }
    private void OnDisable()
    {
        activateAction.performed -= OnPressed;
        activateAction.Disable();
        if (IsActive) End();
    }

    private void OnPressed(InputAction.CallbackContext _) => TryActivate();

    private void Update()
    {
        // 쿨타임이 끝나는 순간을 한 번 알림
        bool onCd = !IsActive && Time.time < readyAt;
        if (wasOnCooldown && !onCd && IsUnlocked) { BecameReady?.Invoke(); GameSfx.Play(GameSfxId.HardGet); }
        wasOnCooldown = onCd;

        if (IsActive && shield != null && shieldFrames != null && shieldFrames.Length > 0)
            shield.sprite = shieldFrames[Mathf.FloorToInt(Time.time * shieldFps) % shieldFrames.Length];
    }

    public bool TryActivate()
    {
        if (!CanUse) return false;
        routine = StartCoroutine(Run());
        return true;
    }

    private IEnumerator Run()
    {
        IsActive = true;
        endAt = Time.time + formTime;
        if (health != null) health.GrantInvincible(formTime);
        if (hasBool) animator.SetBool(animatorBool, true);
        if (shield != null) shield.gameObject.SetActive(true);
        GameSfx.Play(GameSfxId.HardGet);
        if (transformEffect != null) Instantiate(transformEffect).Play(transform.position, false, transformEffectWidth);
        FormChanged?.Invoke(true);
        Activated?.Invoke();
        while (Time.time < endAt) yield return null;
        End();
    }

    /// <summary>죽거나 씬이 바뀔 때 강제로 끝내기</summary>
    public void End()
    {
        if (!IsActive) return;
        if (routine != null) { StopCoroutine(routine); routine = null; }
        IsActive = false;
        readyAt = Time.time + cooldown;
        slowUntil = Time.time + slowAfterForm;
        if (hasBool) animator.SetBool(animatorBool, false);
        if (shield != null) shield.gameObject.SetActive(false);
        GameSfx.Play(GameSfxId.HardEnd);
        FormChanged?.Invoke(false);
        Ended?.Invoke();
    }

    /// <summary>새 도전(부활/재시작) 때 쿨타임을 지워 바로 쓸 수 있게</summary>
    public void ResetCooldown() { readyAt = 0f; slowUntil = 0f; }

    [ContextMenu("Debug/Activate")] private void DebugActivate() => TryActivate();
    [ContextMenu("Debug/Unlock")] private void DebugUnlock() => AbilityProgress.UnlockHard();
    [ContextMenu("Debug/Lock")] private void DebugLock() => AbilityProgress.LockHard();
}