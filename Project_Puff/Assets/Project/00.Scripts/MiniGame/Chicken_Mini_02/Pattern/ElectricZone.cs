using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 전기 바닥 한 구간. 경고(노란 점멸) → 감전(번쩍) → 꺼짐.
/// 감전 중에 '바닥에 발이 닿아 있는' 플레이어만 피해를 입는다 (점프 중/활공 중/바닥 위 발판 위는 안전).
/// 프리팹: 루트(ElectricZone) + 자식 Warn(SpriteRenderer, floor_warn) / Shock(SpriteRenderer, floor_shock_1~3). 피벗은 Bottom Center 권장.
/// 두 스프라이트 모두 Tiled(Continuous)로 바꿔 가로 길이만큼 이어 붙인다 — 코드가 모드와 크기를 설정한다.
/// </summary>
public class ElectricZone : MonoBehaviour, IPatternHazard
{
    [SerializeField] private SpriteRenderer warn;
    [SerializeField] private SpriteRenderer shock;
    [SerializeField] private Sprite[] shockFrames;     // floor_shock_1~3
    [SerializeField] private float height = 0.9f;
    [SerializeField] private float shockFps = 18f;

    [Header("충돌")]
    [SerializeField] private int damage = 1;
    [Tooltip("발이 바닥에서 이 높이 안에 있으면 '바닥에 닿음'으로 본다")]
    [SerializeField] private float footTolerance = 0.25f;
    [SerializeField] private float hitInterval = 0.6f;

    private Action<ElectricZone> release;
    private PatternRunner runner;
    private bool ended;

    public void SetReleaser(Action<ElectricZone> r) => release = r;

    public void Play(PatternRunner owner, float left, float right, float groundY, Transform player, float warnTime, float shockTime)
    {
        runner = owner; ended = false;
        runner?.Register(this);
        StopAllCoroutines();
        StartCoroutine(Run(left, right, groundY, player, warnTime, shockTime));
    }

    private IEnumerator Run(float left, float right, float groundY, Transform player, float warnTime, float shockTime)
    {
        float width = right - left;
        transform.position = new Vector3((left + right) * 0.5f, groundY, 0f);
        Layout(warn, width);
        Layout(shock, width);
        shock.enabled = false;
        warn.enabled = true;

        // 경고
        GameSfx.Play(GameSfxId.FloorWarn);
        for (float t = 0f; t < warnTime; t += Time.deltaTime)
        {
            var c = warn.color; c.a = Mathf.FloorToInt(t * 10f) % 2 == 0 ? 1f : 0.35f; warn.color = c;
            yield return null;
        }
        warn.enabled = false;

        // 감전
        GameSfx.Play(GameSfxId.FloorShock);
        shock.enabled = true;
        var dmgTarget = player != null ? player.GetComponentInParent<IDamageable>() : null;
        var col = player != null ? player.GetComponentInChildren<Collider2D>() : null;
        var pc = player != null ? player.GetComponentInParent<PlayerController>() : null;
        float nextHit = 0f;
        for (float t = 0f; t < shockTime; t += Time.deltaTime)
        {
            if (shockFrames != null && shockFrames.Length > 0) shock.sprite = shockFrames[Mathf.FloorToInt(t * shockFps) % shockFrames.Length];
            if (Time.time >= nextHit && dmgTarget != null && col != null)
            {
                var b = col.bounds;
                bool inside = b.max.x > left && b.min.x < right;
                bool onFloor = b.min.y <= groundY + footTolerance && (pc == null || pc.IsGrounded);
                if (inside && onFloor)
                {
                    dmgTarget.TakeDamage(damage, new Vector2(transform.position.x, groundY));
                    nextHit = Time.time + hitInterval;
                }
            }
            yield return null;
        }

        GameSfx.Play(GameSfxId.FloorShockEnd);
        shock.enabled = false;
        Finish();
    }

    private void Layout(SpriteRenderer sr, float width)
    {
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = new Vector2(width, height);
        sr.transform.localPosition = Vector3.zero;
        sr.transform.localScale = Vector3.one;
        var c = sr.color; c.a = 1f; sr.color = c;
    }

    public void Cancel()
    {
        StopAllCoroutines();
        if (warn != null) warn.enabled = false;
        if (shock != null) shock.enabled = false;
        runner?.Unregister(this);
        runner = null;
        if (ended) return;
        ended = true;
        if (release != null) release(this); else Destroy(gameObject);
    }

    private void Finish()
    {
        runner?.Unregister(this);
        runner = null;
        if (ended) return;
        ended = true;
        if (release != null) release(this); else Destroy(gameObject);
    }
}
