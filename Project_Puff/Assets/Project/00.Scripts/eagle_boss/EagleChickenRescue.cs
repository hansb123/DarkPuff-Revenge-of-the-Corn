using System.Collections;
using UnityEngine;

/// <summary>
/// 바닥이 무너져 추락하는 플레이어를 실사 닭이 날아와 태워 구해주는 연출.
/// 오른쪽에서 날아와(닭 스프라이트는 왼쪽을 봄) 떨어지는 플레이어 밑으로 파고들어 등에 태우고,
/// 날갯짓하며 왼쪽 위로 날아간다. 끝난 뒤 EagleDeathSequence 가 암전을 시작한다.
/// 닭 오브젝트는 코드로 생성하므로 스프라이트만 넣으면 된다.
/// </summary>
public class EagleChickenRescue : MonoBehaviour
{
    [Header("닭 스프라이트 (chicken_boss_color: idle_1, flap, idle_2 … 순서대로 반복)")]
    [SerializeField] private Sprite[] flyFrames;
    [SerializeField] private float flapFps = 8f;
    [Tooltip("닭 키(월드 단위). 플레이어 키의 2.5배 정도 추천")]
    [SerializeField] private float chickenHeight = 2.6f;
    [SerializeField] private int sortingOrder = 300;

    [Header("태우기")]
    [Tooltip("닭 중심 기준 플레이어가 앉는 위치 (월드 단위)")]
    [SerializeField] private Vector2 saddleOffset = new Vector2(0.15f, 0.9f);
    [SerializeField] private float approachTime = 1.3f;
    [Tooltip("닭이 화면 오른쪽 밖에서 시작하는 거리 (카메라 반폭 기준 여유)")]
    [SerializeField] private float startMargin = 3f;
    [SerializeField] private float carryTime = 1.6f;
    [SerializeField] private Vector2 carryVelocity = new Vector2(-4.5f, 2.2f);

    [Header("효과음 (선택)")]
    [SerializeField] private AudioClip swoopSfx;
    [SerializeField] private AudioClip catchSfx;
    [SerializeField] private AudioSource audioSrc;

    public bool Done { get; private set; }

    public IEnumerator Rescue(Transform player)
    {
        Done = false;
        var cam = Camera.main;
        float halfW = cam != null ? cam.orthographicSize * cam.aspect : 10f;
        if (flyFrames == null || flyFrames.Length == 0 || flyFrames[0] == null)
            Debug.LogWarning("[EagleChickenRescue] flyFrames 가 비어 있어서 닭이 보이지 않습니다. 스프라이트를 넣어주세요.");
        var go = new GameObject("RescueChicken");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = sortingOrder;
        if (flyFrames != null && flyFrames.Length > 0 && flyFrames[0] != null)
        {
            sr.sprite = flyFrames[0];
            float s = chickenHeight / Mathf.Max(0.01f, sr.sprite.bounds.size.y);
            go.transform.localScale = new Vector3(s, s, 1f);
        }
        float camX = cam != null ? cam.transform.position.x : player.position.x;
        Vector3 pos = new Vector3(camX + halfW + startMargin, player.position.y - 3.5f, 0f);
        go.transform.position = pos;
        if (swoopSfx != null) Play(swoopSfx);

        var rb = player.GetComponent<Rigidbody2D>();
        float flapT = 0f;

        // 1) 접근: 오른쪽에서 날아오며 플레이어의 낙하 높이를 그대로 따라간다 (낙하가 아무리 빨라도 화면 안에서 만남)
        float startX = pos.x, gap = 3.5f;
        for (float t = 0f; t < approachTime; t += Time.deltaTime)
        {
            float k = t / approachTime; float e = 1f - (1f - k) * (1f - k);          // ease-out
            Vector3 hold = player.position - (Vector3)saddleOffset;
            go.transform.position = new Vector3(Mathf.Lerp(startX, hold.x, e), hold.y - gap * (1f - e), 0f);
            Animate(sr, ref flapT);
            yield return null;
        }
        pos = player.position - (Vector3)saddleOffset;
        go.transform.position = pos;

        // 2) 태움: 플레이어 물리를 끄고 닭 위에 고정
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.simulated = false; }
        if (catchSfx != null) Play(catchSfx);
        EagleCamShake.Add(0.35f, 0.4f);
        Vector3 from = player.position;
        for (float k = 0f; k < 0.25f; k += Time.deltaTime)
        {
            Animate(sr, ref flapT);
            player.position = Vector3.Lerp(from, go.transform.position + (Vector3)saddleOffset, k / 0.25f);
            yield return null;
        }

        // 3) 비행: 왼쪽 위로, 위아래로 살짝 출렁이며
        float elapsed = 0f;
        while (elapsed < carryTime)
        {
            elapsed += Time.deltaTime;
            Vector3 v = (Vector3)carryVelocity * Mathf.SmoothStep(0.3f, 1f, elapsed / 0.6f);
            go.transform.position += v * Time.deltaTime + Vector3.up * (Mathf.Sin(elapsed * 6f) * 0.02f);
            player.position = go.transform.position + (Vector3)saddleOffset;
            Animate(sr, ref flapT);
            yield return null;
        }
        Done = true;
    }

    private void Animate(SpriteRenderer sr, ref float flapT)
    {
        if (flyFrames == null || flyFrames.Length < 2) return;
        flapT += Time.deltaTime * flapFps;
        sr.sprite = flyFrames[Mathf.FloorToInt(flapT) % flyFrames.Length];
    }

    private void Play(AudioClip c)
    {
        if (audioSrc == null) audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.PlayOneShot(c);
    }
}