using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 전기 바닥 패턴에서 바닥에 깔리는 절연 매트(안전 구역). 이 위에 서 있으면 감전되지 않는다.
/// 프리팹: 루트(SafeMat) + 자식 SpriteRenderer 하나(safe_mat, 피벗 Bottom Center). 콜라이더 필요 없음 (X 범위로만 판정).
/// </summary>
public class SafeMat : MonoBehaviour, IPatternHazard
{
    [SerializeField] private SpriteRenderer sprite;
    [Tooltip("매트가 솟아오르는 시간")]
    [SerializeField] private float riseTime = 0.25f;

    private Action<SafeMat> release;
    private PatternRunner runner;
    private bool ended;
    public float Width { get; private set; }

    public void SetReleaser(Action<SafeMat> r) => release = r;

    public void Show(PatternRunner owner, float centerX, float groundY, float width)
    {
        runner = owner; ended = false; Width = width;
        runner?.Register(this);
        StopAllCoroutines();
        transform.position = new Vector3(centerX, groundY, 0f);
        float w = sprite != null && sprite.sprite != null ? sprite.sprite.bounds.size.x : 1f;
        float sc = w > 0f ? width / w : 1f;
        if (sprite != null) { sprite.transform.localScale = new Vector3(sc, 0.01f, 1f); sprite.enabled = true; }
        StartCoroutine(Rise(sc));
    }

    private IEnumerator Rise(float sc)
    {
        for (float t = 0f; t < riseTime; t += Time.deltaTime)
        {
            float k = t / riseTime;
            sprite.transform.localScale = new Vector3(sc, sc * k, 1f);
            yield return null;
        }
        sprite.transform.localScale = new Vector3(sc, sc, 1f);
    }

    public void Hide() { Cancel(); }

    public void Cancel()
    {
        StopAllCoroutines();
        if (sprite != null) sprite.enabled = false;
        runner?.Unregister(this);
        runner = null;
        if (ended) return;
        ended = true;
        if (release != null) release(this); else Destroy(gameObject);
    }
}
