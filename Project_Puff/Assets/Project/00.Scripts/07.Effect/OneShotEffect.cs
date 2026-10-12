using UnityEngine;

/// <summary>
/// 한 번 재생하고 사라지는 스프라이트 프레임 이펙트 (휘두르기, 적중 등).
/// 프리팹 구성: 빈 오브젝트 + SpriteRenderer + 이 컴포넌트. frames에 스프라이트를 순서대로 넣는다.
/// 지금은 끝나면 Destroy하지만, 오브젝트 풀을 도입하면 이 한 곳(Finish)만 풀 반환으로 바꾸면 된다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class OneShotEffect : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float fps = 24f;

    private SpriteRenderer sr;
    private float startTime;
    private bool playing;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        sr.enabled = false;   // Play가 불리기 전에는 숨김
    }

    /// <summary>
    /// 위치, 좌우 반전, 월드 기준 가로 크기(유닛)를 정해 재생한다.
    /// 프리팹 자체 크기에 상관없이 첫 프레임이 worldWidth 유닛이 되도록 맞춘다.
    /// </summary>
    public void Play(Vector2 position, bool flipX, float worldWidth)
    {
        if (frames == null || frames.Length == 0)
        {
            Debug.LogWarning("[OneShotEffect] frames가 비어 있습니다.", this);
            Destroy(gameObject);
            return;
        }

        transform.position = position;

        float spriteWidth = frames[0].bounds.size.x;
        float scale = spriteWidth > 0f ? worldWidth / spriteWidth : 1f;
        transform.localScale = new Vector3(scale, scale, 1f);

        sr.flipX = flipX;
        sr.sprite = frames[0];
        sr.enabled = true;

        startTime = Time.time;
        playing = true;
    }

    private void Update()
    {
        if (!playing) return;

        int index = Mathf.FloorToInt((Time.time - startTime) * fps);

        if (index >= frames.Length)
        {
            Finish();
            return;
        }

        sr.sprite = frames[index];
    }

    private void Finish()
    {
        playing = false;
        Destroy(gameObject);   // 풀링을 쓰면 여기서 반환
    }
}
