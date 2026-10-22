using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 메카 닭의 눈에서 나오는 레이저 한 줄.
///   충전(Charge): 눈에서 붉은 점선(조준선)이 플레이어 쪽으로 천천히 따라다님 (눈이 달아오름)
///   고정(Lock)  : 조준선이 멈추고 빨갛게 깜빡임 + 삑삑 (마지막으로 피할 여유)
///   발사(Fire)  : 빔이 쏘아져 눈에서 바닥까지 일직선. 닿으면 데미지. 높은 난이도에서는 발사 중에도 천천히 플레이어를 따라감
///   끝(End)     : 빔이 사라짐
/// 프리팹 구성(루트는 눈 위치로 옮겨지고 회전함, 자식은 전부 로컬 위치 0):
///   Aim(SpriteRenderer, laser_aim_dash) / Beam(laser_beam_1~3) / Flare(laser_eye_flare_1~3) / Hit(laser_hit_1~4) / Reticle(laser_target)
/// Aim, Beam 스프라이트는 임포트에서 Mesh Type = Full Rect, Wrap Mode = Repeat, 피벗 = Left(왼쪽 가운데)로 해 둔다 (Tiled 모드로 길이만큼 이어 붙임).
/// </summary>
public class LaserBeam : MonoBehaviour, IPatternHazard
{
    [Header("스프라이트 (자식)")]
    [SerializeField] private SpriteRenderer aim;
    [SerializeField] private SpriteRenderer beam;
    [SerializeField] private SpriteRenderer flare;
    [SerializeField] private SpriteRenderer hit;
    [SerializeField] private SpriteRenderer reticle;
    [SerializeField] private Sprite[] beamFrames;     // laser_beam_1~3
    [SerializeField] private Sprite[] flareFrames;    // laser_eye_flare_1~3
    [SerializeField] private Sprite[] hitFrames;      // laser_hit_1~4

    [Header("크기 (월드 단위)")]
    [SerializeField] private float aimThickness = 0.14f;
    [SerializeField] private float beamThickness = 0.7f;
    [SerializeField] private float flareSize = 1.4f;
    [SerializeField] private float hitSize = 1.8f;
    [SerializeField] private float reticleSize = 1.3f;
    [SerializeField] private float beamFps = 20f;

    [Header("충돌")]
    [Tooltip("빔의 맞는 굵기(반지름). 보이는 두께보다 약간 작게")]
    [SerializeField] private float hitRadius = 0.28f;
    [SerializeField] private int damage = 1;
    [SerializeField] private LayerMask playerLayer;
    [Tooltip("계속 닿아 있어도 이 시간(초)마다 한 번씩만 맞음 (무적 시간과 별개)")]
    [SerializeField] private float hitInterval = 0.5f;

    [Header("조준")]
    [Tooltip("플레이어 위치에서 이만큼 위를 겨냥 (피벗이 발이면 몸 중앙쯤)")]
    [SerializeField] private float aimHeight = 0.6f;
    [Tooltip("빔이 가리킬 수 있는 가장 낮은 각도(수평 아래로 몇 도). 너무 눕지 않게")]
    [SerializeField] private float minDownDegrees = 12f;

    public enum Phase { Charge, Lock, Fire, End }
    /// <summary>메카 닭 포즈 등이 구독 (단계, 눈 번호)</summary>
    public static event Action<Phase, int> PhaseChanged;

    [Serializable]
    public struct Settings
    {
        public float chargeTime, lockTime, fireTime;
        public float trackDegPerSec;     // 충전 중 따라가는 각속도
        public float sweepDegPerSec;     // 발사 중 따라가는 각속도 (0 = 고정)
        public float aimOffsetX;         // 겨냥점을 좌우로 비킴 (양쪽 눈이 따로 노릴 때)
        public float groundY;
    }

    private Action<LaserBeam> release;
    private PatternRunner runner;
    private Coroutine routine;
    private Transform origin, target;
    private int eyeIndex;
    private bool ended;

    public void SetReleaser(Action<LaserBeam> r) => release = r;

    public void Launch(PatternRunner owner, Transform eye, Transform aimTarget, int eyeIdx, Settings s)
    {
        runner = owner; origin = eye; target = aimTarget; eyeIndex = eyeIdx; ended = false;
        runner?.Register(this);
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Run(s));
    }

    private IEnumerator Run(Settings s)
    {
        transform.localScale = Vector3.one;
        HideAll();

        float angle = InitialAngle(s);
        SetOrigin();
        PhaseChanged?.Invoke(Phase.Charge, eyeIndex);

        // 1) 충전 + 추적
        ShowSprite(flare, flareFrames, 0, flareSize, 0.5f);
        SetTiled(aim, aimThickness);
        SetAlpha(aim, 0.55f);
        GameSfx.Play(GameSfxId.LaserCharge);
        for (float t = 0f; t < s.chargeTime; t += Time.deltaTime)
        {
            SetOrigin();
            angle = MoveAngle(angle, DesiredAngle(s), s.trackDegPerSec * Time.deltaTime);
            ApplyAim(angle, s.groundY, false);
            float k = t / Mathf.Max(0.01f, s.chargeTime);
            AnimateFlare(t, 0.5f + 0.5f * k, 0.6f + 0.6f * k);
            yield return null;
        }

        // 2) 고정 (깜빡 + 삑삑)
        PhaseChanged?.Invoke(Phase.Lock, eyeIndex);
        float nextBeep = 0f;
        for (float t = 0f; t < s.lockTime; t += Time.deltaTime)
        {
            SetOrigin();
            ApplyAim(angle, s.groundY, true);
            SetAlpha(aim, Mathf.FloorToInt(t * 16f) % 2 == 0 ? 1f : 0.35f);
            AnimateFlare(t, 1f, 1.3f);
            if (t >= nextBeep) { GameSfx.Play(GameSfxId.LaserLock); nextBeep = t + 0.16f; }
            yield return null;
        }

        // 3) 발사
        PhaseChanged?.Invoke(Phase.Fire, eyeIndex);
        GameSfx.Play(GameSfxId.LaserFire);
        if (aim != null) aim.enabled = false;
        if (reticle != null) reticle.enabled = false;
        SetTiled(beam, beamThickness);
        float nextHit = 0f;
        for (float t = 0f; t < s.fireTime; t += Time.deltaTime)
        {
            SetOrigin();
            if (s.sweepDegPerSec > 0f) angle = MoveAngle(angle, DesiredAngle(s), s.sweepDegPerSec * Time.deltaTime);
            Vector2 o = origin.position;
            float len = Length(o, angle, s.groundY);
            Vector2 dir = Dir(angle);

            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            if (beam != null)
            {
                beam.enabled = true;
                if (beamFrames != null && beamFrames.Length > 0) beam.sprite = beamFrames[Mathf.FloorToInt(t * beamFps) % beamFrames.Length];
                var sz = beam.size; sz.x = len; beam.size = sz;
                // 끝으로 갈수록 가늘어지는 맥박
                float pulse = 1f + Mathf.Sin(t * 40f) * 0.08f;
                beam.transform.localScale = new Vector3(1f, ThicknessScale(beam, beamThickness) * pulse, 1f);
            }
            AnimateFlare(t, 1f, 1.5f);
            if (hit != null && hitFrames != null && hitFrames.Length > 0)
            {
                hit.enabled = true;
                hit.sprite = hitFrames[Mathf.FloorToInt(t * 16f) % hitFrames.Length];
                SetWorldWidth(hit, hitSize);
                hit.transform.position = o + dir * len;
                hit.transform.rotation = Quaternion.identity;
            }

            if (Time.time >= nextHit)
            {
                var h = Physics2D.CircleCast(o, hitRadius, dir, len, playerLayer);
                if (h.collider != null)
                {
                    var dmg = h.collider.GetComponentInParent<IDamageable>();
                    if (dmg != null) { dmg.TakeDamage(damage, o); nextHit = Time.time + hitInterval; }
                }
            }
            yield return null;
        }

        // 4) 끝: 꺼짐
        GameSfx.Play(GameSfxId.LaserEnd);
        HideAll();
        Finish();
    }

    // ───────────── 각도 / 위치 ─────────────

    private void SetOrigin()
    {
        if (origin != null) transform.position = origin.position;
    }

    private Vector2 AimPoint(Settings s)
    {
        if (target == null) return (Vector2)transform.position + Vector2.down * 5f;
        return (Vector2)target.position + new Vector2(s.aimOffsetX, aimHeight);
    }

    private float DesiredAngle(Settings s)
    {
        Vector2 d = AimPoint(s) - (Vector2)transform.position;
        return ClampDown(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    private float InitialAngle(Settings s) => -90f;   // 처음엔 똑바로 아래에서 출발해 플레이어 쪽으로 돌아감

    private float ClampDown(float deg)
    {
        // 아래쪽 반원만: -180+min ~ -min
        if (deg > 0f) deg = deg > 90f ? -180f + minDownDegrees : -minDownDegrees;
        return Mathf.Clamp(deg, -180f + minDownDegrees, -minDownDegrees);
    }

    private static float MoveAngle(float cur, float want, float maxStep) => Mathf.MoveTowardsAngle(cur, want, maxStep);
    private static Vector2 Dir(float deg) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));

    private static float Length(Vector2 o, float deg, float groundY)
    {
        Vector2 d = Dir(deg);
        float len = d.y < -0.01f ? (groundY - o.y) / d.y : 40f;
        return Mathf.Clamp(len, 1f, 60f);
    }

    // ───────────── 표시 ─────────────

    private void ApplyAim(float angle, float groundY, bool locked)
    {
        Vector2 o = transform.position;
        float len = Length(o, angle, groundY);
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        if (aim != null)
        {
            aim.enabled = true;
            var sz = aim.size; sz.x = len; aim.size = sz;
            aim.transform.localScale = new Vector3(1f, ThicknessScale(aim, aimThickness) * (locked ? 1.6f : 1f), 1f);
            aim.color = locked ? new Color(1f, 0.25f, 0.2f, aim.color.a) : new Color(1f, 1f, 1f, aim.color.a);
        }
        if (reticle != null && reticle.sprite != null)
        {
            reticle.enabled = true;
            SetWorldWidth(reticle, reticleSize);
            reticle.transform.position = o + Dir(angle) * len;
            reticle.transform.rotation = Quaternion.identity;
        }
    }

    private void AnimateFlare(float t, float alpha, float scaleMul)
    {
        if (flare == null || flareFrames == null || flareFrames.Length == 0) return;
        flare.enabled = true;
        flare.sprite = flareFrames[Mathf.Min(flareFrames.Length - 1, Mathf.FloorToInt(t * 6f) % flareFrames.Length)];
        SetWorldWidth(flare, flareSize * scaleMul);
        var c = Color.white; c.a = Mathf.Clamp01(alpha); flare.color = c;
        flare.transform.localPosition = Vector3.zero;
        flare.transform.rotation = Quaternion.identity;
    }

    private void HideAll()
    {
        foreach (var sr in new[] { aim, beam, flare, hit, reticle }) if (sr != null) sr.enabled = false;
    }

    private static void SetTiled(SpriteRenderer sr, float thickness)
    {
        if (sr == null) return;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.enabled = true;
        sr.transform.localPosition = Vector3.zero;
        sr.transform.localRotation = Quaternion.identity;
    }

    private static float ThicknessScale(SpriteRenderer sr, float thickness)
    {
        if (sr == null || sr.sprite == null) return 1f;
        float h = sr.sprite.bounds.size.y;
        return h > 0f ? thickness / h : 1f;
    }

    private static void ShowSprite(SpriteRenderer sr, Sprite[] frames, int i, float width, float alpha)
    {
        if (sr == null) return;
        if (frames != null && frames.Length > 0) sr.sprite = frames[i];
        sr.enabled = true;
        var c = Color.white; c.a = alpha; sr.color = c;
        SetWorldWidth(sr, width);
    }

    private static void SetWorldWidth(SpriteRenderer sr, float worldWidth)
    {
        if (sr == null || sr.sprite == null) return;
        float w = sr.sprite.bounds.size.x;
        float s = w > 0f ? worldWidth / w : 1f;
        sr.transform.localScale = new Vector3(s, s, 1f);
    }

    private static void SetAlpha(SpriteRenderer sr, float a)
    {
        if (sr == null) return;
        var c = sr.color; c.a = a; sr.color = c;
    }

    // ───────────── 정리 ─────────────

    public void Cancel()
    {
        if (routine != null) { StopCoroutine(routine); routine = null; }
        HideAll();
        runner = null;
        Finish();
    }

    private void Finish()
    {
        if (ended) return;
        ended = true;
        PhaseChanged?.Invoke(Phase.End, eyeIndex);
        runner?.Unregister(this);
        runner = null;
        routine = null;
        if (release != null) release(this); else Destroy(gameObject);
    }
}
