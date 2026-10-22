using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어 체력(하트 칸 수). 데미지를 받으면 무적 시간 동안 깜빡이고, 0이 되면 Died를 알린다.
/// 이 컴포넌트는 "체력 숫자와 무적"만 책임진다. 맞았을 때의 움직임(넉백, 경직)은 PlayerController의 Hit 상태가,
/// 하트 표시는 HeartsUI가, 부활/결과 화면은 Died를 구독하는 쪽이 처리한다.
/// 적의 몸통/공격은 IDamageable.TakeDamage만 호출하면 되므로 이 컴포넌트를 몰라도 된다.
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private int maxHp = 3;

    [Header("Invincibility")]
    [Tooltip("피격 후 무적 시간(초). PlayerController의 Hit Stun Time보다 길어야 함")]
    [SerializeField] private float invincibleTime = 1.2f;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float blinkInterval = 0.08f;
    [Range(0f, 1f)]
    [SerializeField] private float blinkAlpha = 0.3f;

    private float invincibleUntil = float.NegativeInfinity;
    private Coroutine blinkRoutine;

    public int Current { get; private set; }
    public int Max => maxHp;
    public bool IsDead => Current <= 0;
    public bool IsInvincible => Time.time < invincibleUntil;

    /// <summary>(현재 HP, 최대 HP). 맞았을 때, 회복했을 때, 리셋했을 때</summary>
    public event Action<int, int> Changed;

    /// <summary>죽지 않고 맞았을 때. 인자는 공격이 온 위치(넉백 방향용)</summary>
    public event Action<Vector2> Damaged;

    public event Action Died;

    private void Awake()
    {
        Current = maxHp;
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public void TakeDamage(int amount, Vector2 hitFrom)
    {
        if (IsDead || IsInvincible || amount <= 0) return;

        Current = Mathf.Max(0, Current - amount);
        Changed?.Invoke(Current, maxHp);

        if (Current == 0)
        {
            StopBlink();
            Died?.Invoke();
            return;
        }

        invincibleUntil = Time.time + invincibleTime;
        StartBlink();
        Damaged?.Invoke(hitFrom);
    }

    /// <summary>능력 등으로 일정 시간 무적을 준다 (깜빡임 없음). 이미 더 긴 무적이 있으면 그대로 둔다.</summary>
    public void GrantInvincible(float seconds)
    {
        if (IsDead || seconds <= 0f) return;
        invincibleUntil = Mathf.Max(invincibleUntil, Time.time + seconds);
    }

    /// <summary>GrantInvincible 로 준 무적을 일찍 끝낼 때 (남은 무적을 지움)</summary>
    public void EndInvincible() { invincibleUntil = float.NegativeInfinity; }

    /// <summary>낙사처럼 무적/하트와 상관없이 즉사. (이미 죽었으면 무시)</summary>
    public void Kill()
    {
        if (IsDead) return;

        Current = 0;
        Changed?.Invoke(Current, maxHp);
        StopBlink();
        Died?.Invoke();
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;

        Current = Mathf.Min(maxHp, Current + amount);
        Changed?.Invoke(Current, maxHp);
    }

    /// <summary>부활/체크포인트 재시작용. 체력을 가득 채우고 짧게 무적</summary>
    public void ResetFull(float invincibleSeconds = 1f)
    {
        Current = maxHp;
        invincibleUntil = Time.time + invincibleSeconds;
        Changed?.Invoke(Current, maxHp);
        StartBlink();
    }

    // ---------------- 깜빡임 ----------------

    private void StartBlink()
    {
        StopBlink();
        blinkRoutine = StartCoroutine(BlinkRoutine());
    }

    private void StopBlink()
    {
        if (blinkRoutine != null) StopCoroutine(blinkRoutine);
        blinkRoutine = null;
        SetAlpha(1f);
    }

    private IEnumerator BlinkRoutine()
    {
        bool faint = true;

        while (IsInvincible)
        {
            SetAlpha(faint ? blinkAlpha : 1f);
            faint = !faint;
            yield return new WaitForSeconds(blinkInterval);
        }

        SetAlpha(1f);
        blinkRoutine = null;
    }

    private void SetAlpha(float a)
    {
        if (spriteRenderer == null) return;

        Color c = spriteRenderer.color;
        c.a = a;
        spriteRenderer.color = c;
    }
}
