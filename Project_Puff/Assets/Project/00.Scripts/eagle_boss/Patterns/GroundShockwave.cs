using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 바닥을 따라 달리는 낮은 충격파.
///  - 일반 모드 (Launch): 점프로 넘어서 피한다. 한 번만 맞는다. 이벤트 없음.
///  - 패링 모드 (LaunchParry): 뻥/기본 공격으로 쳐내면 보스 쪽으로 튕겨 나가고, 보스에 닿으면 패링 성공.
///      못 쳐내고 플레이어에게 닿으면 데미지 + 패링 실패, 그냥 넘어가 끝까지 가면 회피.
/// 프리팹: 빈 오브젝트 + 이 컴포넌트 (스프라이트는 Frames 에 shockwave_1~4. 비우면 사각형)
/// ※ 패링 모드로 쓸 프리팹은 오브젝트의 Layer 를 Enemy 로 해야 플레이어 공격이 맞는다 (이 컴포넌트가 콜라이더를 자동으로 붙임).
/// </summary>
public class GroundShockwave : MonoBehaviour, IPatternHazard, IDamageable
{
    public enum Result { Parried, Hit, Dodged }

    [Header("충돌")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private int damage = 1;

    [Header("스프라이트 (선택)")]
    [Tooltip("shockwave_1~4. 번갈아 재생. 오른쪽으로 달리는 그림이라 왼쪽으로 갈 땐 자동으로 뒤집힘")]
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float frameTime = 0.07f;
    [Tooltip("그림에 곱하는 색. 일반 충격파는 하늘색 계열, 패링 충격파는 흰색(=원래 금빛)으로 하면 구분됨")]
    [SerializeField] private Color tint = Color.white;
    [Tooltip("튕겨 나간 뒤의 색")]
    [SerializeField] private Color reflectedTint = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color rectColor = new Color(1f, 0.85f, 0.35f, 0.7f);
    [SerializeField] private int sortingOrder = 6;
    [Tooltip("충격파 앞쪽 끝(초승달의 볼록한 쪽) 위치 = 그림 폭의 이 비율만큼 앞. 판정이 그림과 안 맞으면 조절")]
    [Range(0f, 0.5f)][SerializeField] private float hitForwardRatio = 0.27f;
    [Tooltip("판정 폭 = 그림 폭 × 이 값")]
    [Range(0.05f, 0.6f)][SerializeField] private float hitWidthRatio = 0.2f;

    [Header("패링 모드")]
    [Tooltip("쳐내는 판정을 앞쪽으로 이만큼 더 넓힘 (플레이어 공격이 닿기 쉽게). 데미지 판정은 그대로")]
    [SerializeField] private float parryReach = 1.0f;
    [Tooltip("튕겨 나갈 때 속도 배율")]
    [SerializeField] private float reflectSpeedScale = 1.7f;
    [Tooltip("패링 충격파가 반짝이는 속도 (0 = 안 반짝임)")]
    [SerializeField] private float pulseSpeed = 14f;

    private SpriteRenderer sr;
    private BoxCollider2D parryBox;
    private Action<GroundShockwave> release;
    private PatternRunner runner;
    private Coroutine routine;
    private Action<Result> onResolved;

    // 패링 모드 상태
    private bool parryMode;
    private bool reflected;
    private bool resolved;

    public void SetReleaser(Action<GroundShockwave> r) => release = r;

    private void Awake()
    {
        sr = EagleVis.MakeRect(transform, "Wave", sortingOrder);
    }

    /// <summary>일반 충격파: startX 에서 dirX 방향으로 speed 로 달려 endX 를 넘으면 사라진다. height = 충격파 높이(이보다 높이 뛰면 피함)</summary>
    public void Launch(PatternRunner owner, float startX, float dirX, float groundY, float speed, float height, float visualWidth, float endX)
    {
        Begin(owner, false, null, null);
        routine = StartCoroutine(Run(startX, dirX, groundY, speed, height, visualWidth, endX, null));
    }

    /// <summary>패링 충격파: 쳐내면 boss 쪽으로 튕겨 나가 닿으면 Parried. onResolved 로 결과를 알려줌</summary>
    public void LaunchParry(PatternRunner owner, Transform boss, float startX, float dirX, float groundY, float speed, float height, float visualWidth, float endX, Action<Result> onResolved)
    {
        Begin(owner, true, onResolved, boss);
        routine = StartCoroutine(Run(startX, dirX, groundY, speed, height, visualWidth, endX, boss));
    }

    private void Begin(PatternRunner owner, bool parry, Action<Result> cb, Transform boss)
    {
        runner = owner;
        runner?.Register(this);
        parryMode = parry;
        reflected = false;
        reflectRequested = false;
        resolved = false;
        onResolved = cb;
        if (routine != null) StopCoroutine(routine);

        if (parry)
        {
            if (parryBox == null) { parryBox = GetComponent<BoxCollider2D>(); if (parryBox == null) parryBox = gameObject.AddComponent<BoxCollider2D>(); }
            parryBox.isTrigger = true;
            parryBox.enabled = true;
        }
        else if (parryBox != null) parryBox.enabled = false;
    }

    private IEnumerator Run(float startX, float dirX, float groundY, float speed, float height, float visualWidth, float endX, Transform boss)
    {
        sr.enabled = true;
        float x = startX;
        float animT = 0f;
        bool hitDone = false;
        float hitW = visualWidth * hitWidthRatio;
        float curSpeed = speed;

        while (true)
        {
            if (!reflected)
            {
                if (!((dirX > 0f && x < endX) || (dirX < 0f && x > endX))) break;   // 끝까지 감 → 회피
            }
            else
            {
                // 튕겨 나간 충격파: 보스에 닿으면 패링 성공
                float bx = boss != null ? boss.position.x : x;
                if ((dirX > 0f && x + visualWidth * hitForwardRatio >= bx) || (dirX < 0f && x - visualWidth * hitForwardRatio <= bx))
                {
                    Resolve(Result.Parried);
                    yield break;
                }
            }

            x += dirX * curSpeed * Time.deltaTime;
            animT += Time.deltaTime;
            float frontOff = visualWidth * hitForwardRatio * dirX;
            transform.position = new Vector3(x, groundY, 0f);

            // 그림
            Color col = reflected ? reflectedTint : tint;
            if (parryMode && !reflected && pulseSpeed > 0f) col.a *= 0.75f + 0.25f * Mathf.Sin(animT * pulseSpeed);
            sr.flipX = dirX < 0f;
            if (frames != null && frames.Length > 0 && frames[0] != null)
            {
                var sp = frames[Mathf.FloorToInt(animT / frameTime) % frames.Length];
                sr.sprite = sp; sr.color = col;
                float scale = height * 1.25f / sp.bounds.size.y;
                float yPos = groundY - sp.bounds.min.y * scale;
                sr.transform.position = new Vector3(x, yPos, 0f);
                sr.transform.localScale = new Vector3(scale, scale, 1f);
            }
            else
            {
                sr.sprite = EagleVis.White; sr.color = col * rectColor;
                EagleVis.Place(sr, x + frontOff, groundY + height * 0.5f, hitW, height);
            }

            // 쳐내기 판정 상자 (패링 모드): 앞쪽으로 parryReach 만큼 넓힘
            if (parryMode && parryBox != null)
            {
                float w = hitW + parryReach;
                parryBox.size = new Vector2(w, height);
                parryBox.offset = new Vector2(frontOff + dirX * parryReach * 0.5f, height * 0.5f);
            }

            // 튕김 요청 처리 (TakeDamage 가 표시만 해 두고 여기서 방향을 바꿈)
            if (reflectRequested && !reflected)
            {
                reflected = true;
                dirX = -dirX;
                curSpeed = speed * reflectSpeedScale;
                if (parryBox != null) parryBox.enabled = false;
                GameSfx.Play(GameSfxId.EagleWingFlap);
            }
            // 플레이어를 때리는 판정 (튕기기 전에만)
            if (!hitDone && !reflected)
            {
                var hit = Physics2D.OverlapBox(new Vector2(x + frontOff, groundY + height * 0.5f), new Vector2(hitW, height), 0f, playerLayer);
                if (hit != null)
                {
                    hit.GetComponentInParent<IDamageable>()?.TakeDamage(damage, new Vector2(x, groundY));
                    hitDone = true;
                    if (parryMode) { Resolve(Result.Hit); yield break; }
                }
            }

            yield return null;
        }

        if (parryMode) Resolve(Result.Dodged); else Finish();
    }

    private bool reflectRequested;

    /// <summary>플레이어의 뻥/기본 공격이 맞음 = 쳐냄. 패링 모드에서 아직 안 튕겼을 때만 반응</summary>
    public void TakeDamage(int amount, Vector2 hitFrom)
    {
        if (!parryMode || reflected || resolved) return;
        reflectRequested = true;
    }

    private void Resolve(Result r)
    {
        if (resolved) return;
        resolved = true;
        runner?.Unregister(this);   // 이벤트가 그로기를 일으켜 Runner 가 위험물을 정리해도 이 충격파는 건드리지 않게
        switch (r)
        {
            case Result.Parried: ParryEvents.RaiseSucceeded(); break;
            case Result.Hit: ParryEvents.RaiseFailed(); break;
            case Result.Dodged: ParryEvents.RaiseDodged(); break;
        }
        var cb = onResolved; onResolved = null;
        Finish();
        cb?.Invoke(r);
    }

    private void Finish()
    {
        if (routine != null) { StopCoroutine(routine); routine = null; }
        runner?.Unregister(this);
        reflectRequested = false;
        if (parryBox != null) parryBox.enabled = false;
        sr.enabled = false;
        if (release != null) release(this); else gameObject.SetActive(false);
    }

    public void Cancel()
    {
        if (routine != null) { StopCoroutine(routine); routine = null; }
        onResolved = null;
        resolved = true;
        reflectRequested = false;
        if (parryBox != null) parryBox.enabled = false;
        sr.enabled = false;
        if (release != null) release(this); else gameObject.SetActive(false);
    }
}