using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 독수리 보스 사망 연출 (추락 + 폭풍이 걷힘 + 바닥 붕괴 + 암전).
///  1) 마지막 일격: 히트스톱 + 흰 번쩍 + 흔들림
///  2) 독수리가 몸을 떨다 빙글빙글 돌며 추락 → 쿵 (큰 흔들림, 먼지, 깃털)
///  3) 쓰러져 멍하니 있다가 깃털이 되어 흩어짐
///  4) 폭풍이 걷힘: 비/번개/먹구름 페이드아웃, 따뜻한 빛과 햇살
///  5) 잠깐의 고요 → 플레이어가 서 있는 바닥이 무너지며 추락 → 암전
///  6) Completed 이벤트 (쿠키 스테이지 시작 / 씬 전환 등 연결), 옵션으로 클리어창(Result) 바로 표시
/// 플래시/암전용 UI 는 자동 생성되므로 따로 만들 필요 없음.
/// </summary>
public class EagleDeathSequence : MonoBehaviour
{
    [Header("필수")]
    [SerializeField] private EagleBoss boss;
    [SerializeField] private EagleMotion motion;
    [SerializeField] private Transform player;
    [SerializeField] private EagleFloorCollapse floor;

    [Header("플레이어 제어 잠금 (사망 연출 중 비활성화)")]
    [SerializeField] private Behaviour[] lockBehaviours;

    [Header("타이밍")]
    [SerializeField] private float hitStopTime = 0.22f;
    [SerializeField] private float flinchTime = 0.7f;
    [SerializeField] private float spinTurns = 2.5f;
    [SerializeField] private float dazedTime = 1.3f;
    [SerializeField] private float dissolveTime = 2.0f;
    [SerializeField] private float clearTime = 3.2f;
    [SerializeField] private float calmPause = 1.3f;
    [Tooltip("바닥 붕괴 시작 → 암전 시작까지 (균열 시간 + 떨어지는 시간)")]
    [SerializeField] private float collapseToBlackout = 3.2f;
    [SerializeField] private float blackoutTime = 1.2f;
    [SerializeField] private float blackoutHold = 0.8f;

    [Header("폭풍이 걷힘 — 사라질 것들")]
    [SerializeField] private SpriteRenderer[] fadeOutSprites;
    [SerializeField] private Graphic[] fadeOutGraphics;
    [SerializeField] private ParticleSystem[] stopParticles;
    [Tooltip("연출 끝에 완전히 꺼버릴 오브젝트 (비 파티클, 번개 매니저, 어두운 오버레이 등)")]
    [SerializeField] private GameObject[] disableAfterClear;
    [Tooltip("바닥이 무너질 때 꺼야 하는 것 (아래로 떨어진 플레이어를 리스폰시키는 킬존 등)")]
    [SerializeField] private GameObject[] disableAtCollapse;
    [Tooltip("보스 HUD(체력바, 분노 게이지)들 — 사망 시 숨김")]
    [SerializeField] private GameObject[] hideOnDeath;

    [Header("햇살 (fx_sunbeam_1/2/3, fx_sun_glow)")]
    [SerializeField] private Sprite[] sunbeamSprites;
    [SerializeField] private Sprite sunGlowSprite;
    [SerializeField] private int beamSortingOrder = -5;
    [SerializeField] private Color warmTint = new Color(1f, 0.82f, 0.55f, 0.22f);

    [Header("깃털 / 먼지 (비워두면 사각형으로 대체)")]
    [SerializeField] private Sprite[] featherSprites;
    [SerializeField] private Color featherColor = new Color(0.45f, 0.32f, 0.22f, 1f);

    [Header("카메라")]
    [Tooltip("바닥이 무너질 때 카메라 추적을 끄고 플레이어를 따라 내려가게 할 때 사용")]
    [SerializeField] private Behaviour[] cameraFollowScripts;
    [SerializeField] private bool cameraFollowFall = true;

    [Header("오디오")]
    [SerializeField] private AudioSource stageBgm;
    [SerializeField] private AudioSource calmBgm;
    [SerializeField, Range(0f, 1f)] private float calmVolume = 0.6f;

    [Header("끝")]
    [SerializeField] private bool showResultAtEnd = false;
    [SerializeField] private GameModeController gameMode;
    public UnityEvent Completed;

    public bool HasPlayed { get; private set; }

    private Image flashImg, blackImg, warmImg;
    private Camera cam;

    private void Awake() { if (boss == null) boss = FindFirstObjectByType<EagleBoss>(); }
    private void OnEnable() { if (boss != null) boss.Defeated += OnDefeated; }
    private void OnDisable() { if (boss != null) boss.Defeated -= OnDefeated; }

    private void OnDefeated()
    {
        if (HasPlayed) return;
        HasPlayed = true;
        StartCoroutine(Sequence());
    }

    [ContextMenu("Debug/사망 연출 테스트")]
    private void DebugPlay() { if (!HasPlayed) { HasPlayed = true; StartCoroutine(Sequence()); } }

    private IEnumerator Sequence()
    {
        cam = Camera.main;
        BuildOverlays();
        if (player == null) { var p = GameObject.FindGameObjectWithTag("Player"); if (p != null) player = p.transform; }
        foreach (var g in hideOnDeath) if (g != null) g.SetActive(false);

        // 플레이어 잠금 (속도도 멈춤)
        if (lockBehaviours != null) foreach (var b in lockBehaviours) if (b != null) b.enabled = false;
        if (player != null)
        {
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        // 1) 히트스톱 + 번쩍
        yield return Flash(1f, 0.04f);
        Time.timeScale = 0.02f;
        EagleCamShake.Add(0.5f, hitStopTime + 0.3f);
        yield return new WaitForSecondsRealtime(hitStopTime);
        Time.timeScale = 1f;
        StartCoroutine(FadeImage(flashImg, 1f, 0f, 0.35f));

        // 2) 추락
        bool landed = false;
        yield return motion.DeathFall(flinchTime, spinTurns, () =>
        {
            landed = true;
            EagleCamShake.Add(0.7f, 0.9f);
            GameSfx.Play(GameSfxId.EagleStomp);
            Vector3 gp = new Vector3(motion.transform.position.x, motion.GroundY, 0f);
            StartCoroutine(Burst(gp, 14, null, Color.white * 0.9f, 6f, 0f, 0.5f, 0.1f, true));
            StartCoroutine(Burst(motion.transform.position, 18, featherSprites, featherColor, 4f, 7f, 1.8f, 0.35f, false));
        });
        if (!landed) EagleCamShake.Add(0.7f, 0.9f);

        // 3) 쓰러져 멍하니 → 깃털로 흩어짐
        yield return new WaitForSeconds(dazedTime);
        yield return Dissolve();

        // 4) 폭풍이 걷힘
        yield return ClearStorm();

        // 5) 고요
        yield return new WaitForSeconds(calmPause);

        // 6) 바닥 붕괴
        foreach (var g in disableAtCollapse) if (g != null) g.SetActive(false);
        if (cameraFollowFall && cameraFollowScripts != null)
            foreach (var b in cameraFollowScripts) if (b != null) b.enabled = false;
        float px = player != null ? player.position.x : 0f;
        float floorTop = FloorTopAt(px);
        if (floor != null) StartCoroutine(floor.Collapse(px, floorTop));
        if (cameraFollowFall && cam != null && player != null) StartCoroutine(FollowFall());

        // 균열~붕괴~추락이 진행되는 동안 기다렸다가 암전
        yield return new WaitForSeconds(collapseToBlackout);
        yield return FadeImageRoutine(blackImg, 0f, 1f, blackoutTime);
        yield return new WaitForSecondsRealtime(blackoutHold);

        if (showResultAtEnd)
        {
            if (gameMode == null) gameMode = FindFirstObjectByType<GameModeController>();
            if (gameMode != null) gameMode.SetMode(GameMode.Result);
        }
        Completed?.Invoke();
    }

    // ───────── 구성 요소 ─────────

    private float FloorTopAt(float x)
    {
        // 플레이어 발밑을 아래로 레이캐스트해서 바닥 윗면 높이를 얻는다 (실패 시 보스 지면 높이)
        if (player != null)
        {
            foreach (var h in Physics2D.RaycastAll(new Vector2(x, player.position.y + 0.5f), Vector2.down, 6f))
                if (h.collider != null && !h.collider.isTrigger && h.collider.transform != player && !h.collider.transform.IsChildOf(player)) return h.point.y;
        }
        return motion != null ? motion.GroundY : 0f;
    }

    private IEnumerator Dissolve()
    {
        var rends = motion.GetComponentsInChildren<SpriteRenderer>();
        var starts = new Color[rends.Length];
        for (int i = 0; i < rends.Length; i++) starts[i] = rends[i].color;
        Vector3 c = motion.transform.position;
        float acc = 0f;
        for (float t = 0f; t < dissolveTime; t += Time.deltaTime)
        {
            float k = t / dissolveTime;
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] == null) continue;
                var col = starts[i]; col.a = starts[i].a * (1f - k * k); rends[i].color = col;
            }
            acc += Time.deltaTime * Mathf.Lerp(8f, 28f, k);
            while (acc >= 1f)
            {
                acc -= 1f;
                Vector3 pos = c + new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(-0.6f, 1.4f), 0f);
                StartCoroutine(Burst(pos, 1, featherSprites, featherColor, 1.5f, -1.2f, 2.2f, 0.28f, false, true));
            }
            yield return null;
        }
        foreach (var r in rends) if (r != null) r.enabled = false;
        motion.gameObject.SetActive(false);
    }

    private IEnumerator ClearStorm()
    {
        foreach (var ps in stopParticles) if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (stageBgm != null) StartCoroutine(FadeAudio(stageBgm, 0f, clearTime * 0.8f, true));
        if (calmBgm != null) { calmBgm.volume = 0f; calmBgm.Play(); StartCoroutine(FadeAudio(calmBgm, calmVolume, clearTime, false)); }

        var beams = SpawnSunbeams();
        var sprStart = new Color[fadeOutSprites.Length];
        for (int i = 0; i < sprStart.Length; i++) if (fadeOutSprites[i] != null) sprStart[i] = fadeOutSprites[i].color;
        var gfxStart = new Color[fadeOutGraphics.Length];
        for (int i = 0; i < gfxStart.Length; i++) if (fadeOutGraphics[i] != null) gfxStart[i] = fadeOutGraphics[i].color;

        for (float t = 0f; t < clearTime; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / clearTime);
            for (int i = 0; i < sprStart.Length; i++)
                if (fadeOutSprites[i] != null) { var c = sprStart[i]; c.a *= 1f - k; fadeOutSprites[i].color = c; }
            for (int i = 0; i < gfxStart.Length; i++)
                if (fadeOutGraphics[i] != null) { var c = gfxStart[i]; c.a *= 1f - k; fadeOutGraphics[i].color = c; }
            if (warmImg != null) { var c = warmTint; c.a = warmTint.a * k; warmImg.color = c; }
            foreach (var b in beams)
                if (b.sr != null)
                {
                    float fk = Mathf.Clamp01((k - b.delay) / 0.5f);
                    var c = b.sr.color; c.a = b.maxAlpha * fk * (0.85f + 0.15f * Mathf.Sin(Time.time * b.swaySpeed + b.phase)); b.sr.color = c;
                    b.sr.transform.localRotation = Quaternion.Euler(0f, 0f, b.baseAngle + Mathf.Sin(Time.time * b.swaySpeed * 0.6f + b.phase) * 1.5f);
                }
            yield return null;
        }
        foreach (var g in disableAfterClear) if (g != null) g.SetActive(false);
        foreach (var s in fadeOutSprites) if (s != null) s.enabled = false;
        foreach (var g in fadeOutGraphics) if (g != null) g.enabled = false;
        if (calmBgm != null) calmBgm.volume = calmVolume;
        StartCoroutine(KeepBeams(beams));
    }

    private class Beam { public SpriteRenderer sr; public float delay, maxAlpha, swaySpeed, phase, baseAngle; }

    private List<Beam> SpawnSunbeams()
    {
        var list = new List<Beam>();
        if (cam == null) return list;
        float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
        float[] xs = { -0.55f, 0.05f, 0.6f, -0.15f };
        float[] angs = { 14f, 10f, 16f, 12f };
        if (sunbeamSprites != null)
            for (int i = 0; i < sunbeamSprites.Length && i < 4; i++)
            {
                if (sunbeamSprites[i] == null) continue;
                var sr = new GameObject("SunBeam" + i).AddComponent<SpriteRenderer>();
                sr.sprite = sunbeamSprites[i]; sr.sortingOrder = beamSortingOrder;
                sr.transform.SetParent(cam.transform, false);
                float h = sr.sprite.bounds.size.y;
                float s = (halfH * 2.2f) / Mathf.Max(0.01f, h);
                sr.transform.localScale = new Vector3(s, s, 1f);
                sr.transform.localPosition = new Vector3(xs[i] * halfW, halfH - (h * s) * 0.5f + 0.5f, 10f);
                sr.color = new Color(1f, 0.95f, 0.78f, 0f);
                list.Add(new Beam { sr = sr, delay = 0.15f * i, maxAlpha = 0.5f, swaySpeed = Random.Range(0.6f, 1.1f), phase = Random.Range(0f, 6f), baseAngle = angs[i] });
            }
        if (sunGlowSprite != null)
        {
            var sr = new GameObject("SunGlow").AddComponent<SpriteRenderer>();
            sr.sprite = sunGlowSprite; sr.sortingOrder = beamSortingOrder - 1;
            sr.transform.SetParent(cam.transform, false);
            float s = (halfH * 2.6f) / Mathf.Max(0.01f, sr.sprite.bounds.size.y);
            sr.transform.localScale = new Vector3(s, s, 1f);
            sr.transform.localPosition = new Vector3(-halfW * 0.4f, halfH * 0.85f, 10f);
            sr.color = new Color(1f, 0.92f, 0.7f, 0f);
            list.Add(new Beam { sr = sr, delay = 0f, maxAlpha = 0.65f, swaySpeed = 0.5f, phase = 0f, baseAngle = 0f });
        }
        return list;
    }

    private IEnumerator KeepBeams(List<Beam> beams)
    {
        while (blackImg == null || blackImg.color.a < 0.99f)
        {
            foreach (var b in beams)
                if (b.sr != null)
                {
                    var c = b.sr.color; c.a = b.maxAlpha * (0.85f + 0.15f * Mathf.Sin(Time.time * b.swaySpeed + b.phase)); b.sr.color = c;
                    b.sr.transform.localRotation = Quaternion.Euler(0f, 0f, b.baseAngle + Mathf.Sin(Time.time * b.swaySpeed * 0.6f + b.phase) * 1.5f);
                }
            yield return null;
        }
    }

    private IEnumerator FollowFall()
    {
        float startY = cam.transform.position.y;
        float vy = 0f;
        while (blackImg == null || blackImg.color.a < 0.99f)
        {
            if (player != null && player.position.y < cam.transform.position.y - 1.2f)
            {
                var p = cam.transform.position;
                float target = player.position.y + 1f;
                p.y = Mathf.SmoothDamp(p.y, target, ref vy, 0.25f);
                cam.transform.position = p;
            }
            yield return null;
        }
    }

    // ───────── 파티클 / 오버레이 도우미 ─────────

    private IEnumerator Burst(Vector3 pos, int count, Sprite[] sprites, Color color, float speed, float gravity, float life, float size, bool streak, bool drift = false)
    {
        var objs = new Transform[count]; var srs = new SpriteRenderer[count]; var vel = new Vector2[count]; var spin = new float[count];
        for (int i = 0; i < count; i++)
        {
            var sr = new GameObject("Fx").AddComponent<SpriteRenderer>();
            bool useSpr = sprites != null && sprites.Length > 0 && sprites[Random.Range(0, sprites.Length)] != null;
            sr.sprite = useSpr ? sprites[Random.Range(0, sprites.Length)] : EagleVis.White;
            sr.sortingOrder = 200; sr.color = color;
            sr.transform.position = pos;
            float s = size * Random.Range(0.7f, 1.3f);
            sr.transform.localScale = streak ? new Vector3(s * 4f, s * 0.5f, 1f) : (useSpr ? Vector3.one * s : new Vector3(s * 0.6f, s, 1f));
            float a = Random.Range(0f, Mathf.PI * 2f);
            Vector2 d = streak ? new Vector2(Random.value < 0.5f ? -1f : 1f, Random.Range(0.02f, 0.3f)) : new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.8f + 0.4f);
            vel[i] = d * speed * Random.Range(0.5f, 1.2f);
            spin[i] = Random.Range(-240f, 240f);
            objs[i] = sr.transform; srs[i] = sr;
            if (streak) sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(vel[i].y, vel[i].x) * Mathf.Rad2Deg);
        }
        for (float t = 0f; t < life; t += Time.deltaTime)
        {
            float k = t / life;
            for (int i = 0; i < count; i++)
            {
                if (objs[i] == null) continue;
                vel[i].y -= gravity * (drift ? -0.1f : 1f) * Time.deltaTime * (streak ? 0f : 0.35f);
                if (drift) vel[i].x += Mathf.Sin(Time.time * 3f + i) * Time.deltaTime * 2f;
                vel[i] *= streak ? (1f - 4f * Time.deltaTime) : (1f - 0.6f * Time.deltaTime);
                objs[i].position += (Vector3)(vel[i] * Time.deltaTime);
                if (!streak) objs[i].Rotate(0f, 0f, spin[i] * Time.deltaTime);
                var c = color; c.a = color.a * (1f - k * k); srs[i].color = c;
            }
            yield return null;
        }
        for (int i = 0; i < count; i++) if (objs[i] != null) Destroy(objs[i].gameObject);
    }

    private void BuildOverlays()
    {
        var cv = new GameObject("DeathSeqCanvas").AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 5000;
        warmImg = MakeFull(cv.transform, new Color(warmTint.r, warmTint.g, warmTint.b, 0f));
        flashImg = MakeFull(cv.transform, new Color(1f, 1f, 1f, 0f));
        blackImg = MakeFull(cv.transform, new Color(0f, 0f, 0f, 0f));
        warmImg.raycastTarget = flashImg.raycastTarget = blackImg.raycastTarget = false;
    }

    private static Image MakeFull(Transform parent, Color c)
    {
        var img = new GameObject("Overlay").AddComponent<Image>();
        img.transform.SetParent(parent, false);
        var rt = img.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        img.color = c; return img;
    }

    private IEnumerator Flash(float a, float t) { yield return FadeImageRoutine(flashImg, 0f, a, t); }
    private IEnumerator FadeImage(Image img, float from, float to, float t) { yield return FadeImageRoutine(img, from, to, t); }

    private IEnumerator FadeImageRoutine(Image img, float from, float to, float time)
    {
        if (img == null) yield break;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            var c = img.color; c.a = Mathf.Lerp(from, to, t / Mathf.Max(0.001f, time)); img.color = c; yield return null;
        }
        var e = img.color; e.a = to; img.color = e;
    }

    private IEnumerator FadeAudio(AudioSource src, float to, float time, bool stopAtEnd)
    {
        float from = src.volume;
        for (float t = 0f; t < time; t += Time.deltaTime) { src.volume = Mathf.Lerp(from, to, t / time); yield return null; }
        src.volume = to; if (stopAtEnd) src.Stop();
    }
}