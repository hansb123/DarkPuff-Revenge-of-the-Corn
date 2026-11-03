using System.Collections;
using UnityEngine;

/// <summary>
/// 포효 연출. 독수리가 Roar 포즈가 되는 순간(패링당함, 페이즈 전환, 으스댐) 다음을 함께 재생한다.
///  - 입에서 퍼져나가는 음파 고리 (코드로 그려서 이미지 필요 없음)
///  - 몸이 순간 커졌다 돌아오는 부풀림
///  - 카메라 흔들림 (끌 수 있음)
/// 보스 오브젝트에 붙이기만 하면 된다. 칸은 비워 두면 자동으로 찾는다.
/// </summary>
public class EagleRoarFx : MonoBehaviour
{
    [SerializeField] private EaglePoseController pose;
    [SerializeField] private SpriteRenderer bossRenderer;

    [Header("음파 고리")]
    [SerializeField] private int rings = 3;
    [Tooltip("고리와 고리 사이 시간(초)")]
    [SerializeField] private float ringInterval = 0.16f;
    [Tooltip("퍼지는 데 걸리는 시간(초)")]
    [SerializeField] private float ringLife = 0.7f;
    [Tooltip("시작 크기 / 끝 크기 (유닛)")]
    [SerializeField] private float ringStartSize = 0.8f;
    [SerializeField] private float ringEndSize = 6f;
    [SerializeField] private Color ringColor = new Color(1f, 0.9f, 0.55f, 0.85f);
    [Tooltip("입 위치 = 눈에서 이만큼 떨어진 곳 (독수리가 왼쪽을 볼 때 기준, 오른쪽을 보면 자동으로 뒤집힘)")]
    [SerializeField] private Vector2 mouthOffset = new Vector2(-0.9f, -0.35f);
    [SerializeField] private int sortingOrder = 8;

    [Header("의성어 이미지 (삐애애액!)")]
    [Tooltip("roar_text_1, roar_text_2 …  중 하나가 무작위로 뜬다. 비우면 안 나옴")]
    [SerializeField] private Sprite[] textSprites;
    [Tooltip("그림 폭(유닛)")]
    [SerializeField] private float textWidth = 4.2f;
    [Tooltip("입 위치에서 이만큼 위/앞에 뜸 (독수리가 왼쪽을 볼 때 기준, 반대쪽이면 자동으로 뒤집힘)")]
    [SerializeField] private Vector2 textOffset = new Vector2(-1.6f, 1.5f);
    [SerializeField] private float textLife = 0.9f;
    [Tooltip("켜면 패링당해서 포효할 때만 나옴. 끄면 모든 포효(페이즈 전환 등)에서 나옴")]
    [SerializeField] private bool textOnlyAfterParry = true;

    [Header("몸 부풀림")]
    [Tooltip("포효 순간 커지는 비율 (0.08 = 8%)")]
    [SerializeField] private float puff = 0.08f;
    [SerializeField] private float puffTime = 0.25f;

    [Header("카메라 흔들림")]
    [SerializeField] private bool cameraShake = true;
    [SerializeField] private float shakeStrength = 0.18f;
    [SerializeField] private float shakeTime = 0.45f;

    private static Sprite ringSprite;
    private Vector3 baseScale;
    private Coroutine puffRoutine, shakeRoutine, ringRoutine;
    private Vector3 camOffset;
    private float parriedAt = -99f;

    private void Awake()
    {
        if (pose == null) pose = GetComponent<EaglePoseController>();
        if (bossRenderer == null) bossRenderer = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        EaglePose.Changed += OnPose;
        ParryEvents.Succeeded += OnParried;
    }

    private void OnParried() => parriedAt = Time.time;

    private void OnDisable()
    {
        EaglePose.Changed -= OnPose;
        ParryEvents.Succeeded -= OnParried;
        StopAllCoroutines();
        transform.localScale = baseScale;
        RemoveCamOffset();
    }

    private void OnPose(EaglePose.Id id)
    {
        if (id != EaglePose.Id.Roar) { if (puffRoutine != null) { StopCoroutine(puffRoutine); puffRoutine = null; transform.localScale = baseScale; } return; }
        transform.localScale = baseScale;

        bool fromParry = Time.time - parriedAt < 1.5f;
        if (textSprites != null && textSprites.Length > 0 && (!textOnlyAfterParry || fromParry))
            StartCoroutine(PopText());

        if (ringRoutine != null) StopCoroutine(ringRoutine);
        ringRoutine = StartCoroutine(Rings());
        if (puff > 0f) { if (puffRoutine != null) StopCoroutine(puffRoutine); puffRoutine = StartCoroutine(Puff()); }
        if (cameraShake && Camera.main != null) { if (shakeRoutine != null) StopCoroutine(shakeRoutine); shakeRoutine = StartCoroutine(Shake()); }
    }

    private Vector3 MouthWorld()
    {
        Vector2 eye = pose != null && pose.HasEye ? pose.EyeLocal : Vector2.zero;
        bool flip = bossRenderer != null && bossRenderer.flipX;
        Vector2 m = mouthOffset; if (flip) m.x = -m.x;
        return transform.TransformPoint(new Vector3(eye.x + m.x, eye.y + m.y, 0f));
    }

    private IEnumerator Rings()
    {
        for (int i = 0; i < rings; i++)
        {
            StartCoroutine(OneRing(MouthWorld()));
            yield return new WaitForSeconds(ringInterval);
        }
        ringRoutine = null;
    }

    /// <summary>삐애애액! 같은 글자가 입 근처에서 쾅 튀어나왔다가 떨리며 사라진다</summary>
    private IEnumerator PopText()
    {
        var sp = textSprites[Random.Range(0, textSprites.Length)];
        if (sp == null) yield break;
        bool flip = bossRenderer != null && bossRenderer.flipX;
        Vector2 off = textOffset; if (flip) off.x = -off.x;
        Vector3 basePos = MouthWorld() + new Vector3(off.x, off.y, 0f);

        var go = new GameObject("RoarText");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp;
        sr.sortingOrder = sortingOrder + 2;
        float fit = textWidth / Mathf.Max(0.01f, sp.bounds.size.x);
        float rot = Random.Range(-8f, 8f);

        for (float t = 0f; t < textLife; t += Time.deltaTime)
        {
            float k = t / textLife;
            // 0~0.15: 크게 튀어나옴(오버슛) → 1.0 으로 안착 → 끝에서 살짝 커지며 사라짐
            float pop = k < 0.15f ? Mathf.Lerp(0.2f, 1.25f, k / 0.15f) : (k < 0.3f ? Mathf.Lerp(1.25f, 1f, (k - 0.15f) / 0.15f) : 1f + (k - 0.3f) * 0.12f);
            Vector2 j = Random.insideUnitCircle * 0.05f;   // 떨림
            go.transform.position = basePos + new Vector3(j.x, j.y + k * 0.35f, 0f);
            go.transform.localScale = Vector3.one * (fit * pop);
            go.transform.rotation = Quaternion.Euler(0f, 0f, rot + Mathf.Sin(t * 40f) * 2f);
            sr.color = new Color(1f, 1f, 1f, k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);
            yield return null;
        }
        Destroy(go);
    }

    private IEnumerator OneRing(Vector3 at)
    {
        var go = new GameObject("RoarRing");
        go.transform.position = at;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = RingSprite();
        sr.sortingOrder = sortingOrder;
        for (float t = 0f; t < ringLife; t += Time.deltaTime)
        {
            float k = t / ringLife;
            float size = Mathf.Lerp(ringStartSize, ringEndSize, 1f - (1f - k) * (1f - k));   // 처음엔 빠르게 퍼지고 점점 느려짐
            go.transform.localScale = new Vector3(size, size, 1f);
            var c = ringColor; c.a *= 1f - k;
            sr.color = c;
            yield return null;
        }
        Destroy(go);
    }

    private IEnumerator Puff()
    {
        Vector3 b = baseScale;
        for (float t = 0f; t < puffTime; t += Time.deltaTime)
        {
            float k = Mathf.Sin(t / puffTime * Mathf.PI);   // 커졌다 돌아옴
            transform.localScale = b * (1f + puff * k);
            yield return null;
        }
        transform.localScale = b;
        puffRoutine = null;
    }

    private IEnumerator Shake()
    {
        var cam = Camera.main.transform;
        for (float t = 0f; t < shakeTime; t += Time.deltaTime)
        {
            RemoveCamOffset();
            float fade = 1f - t / shakeTime;
            Vector2 r = Random.insideUnitCircle * (shakeStrength * fade);
            camOffset = new Vector3(r.x, r.y, 0f);
            cam.position += camOffset;
            yield return null;
        }
        RemoveCamOffset();
        shakeRoutine = null;
    }

    private void RemoveCamOffset()
    {
        if (camOffset != Vector3.zero && Camera.main != null) Camera.main.transform.position -= camOffset;
        camOffset = Vector3.zero;
    }

    private static Sprite RingSprite()
    {
        if (ringSprite != null) return ringSprite;
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.1f);   // 얇은 고리
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        tex.Apply();
        ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);   // 지름 1유닛
        return ringSprite;
    }
}
