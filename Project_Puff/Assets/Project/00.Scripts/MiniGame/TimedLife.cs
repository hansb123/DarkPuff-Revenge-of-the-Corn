using UnityEngine;

/// <summary>발판 뻥튀기가 만든 임시 발판 등에 붙인다. 시간이 지나면 깜빡이다 사라진다.</summary>
public class TimedLife : MonoBehaviour
{
    [SerializeField] private float lifeTime = 5f;
    [SerializeField] private float blinkTime = 1f;
    [SerializeField] private SpriteRenderer sr;

    private float age;

    private void Awake()
    {
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
    }

    private void Update()
    {
        age += Time.deltaTime;
        float left = lifeTime - age;
        if (sr != null)
        {
            bool faint = left < blinkTime && Mathf.FloorToInt(left * 10f) % 2 == 0;
            Color c = sr.color; c.a = faint ? 0.3f : 1f; sr.color = c;
        }
        if (left <= 0f) Destroy(gameObject);
    }
}
