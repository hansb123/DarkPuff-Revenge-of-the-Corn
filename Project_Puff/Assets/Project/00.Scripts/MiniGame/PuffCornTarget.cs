using System;
using UnityEngine;

/// <summary>
/// 미니게임에서 불쑥 튀어나오는 뻥튀기. 마우스로 클릭하면 보상, 시간 안에 못 누르면 놓친 것(StageMiniGame이 하트를 깎음).
/// 프리팹: SpriteRenderer + CircleCollider2D(클릭 판정용, Is Trigger 켜기 권장) + 이 스크립트.
/// 보상 종류(Kind)는 프리팹마다 정한다: 빛 뻥튀기 / 발판 뻥튀기.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PuffCornTarget : MonoBehaviour
{
    public enum RewardKind { Light, Platform }

    [SerializeField] private RewardKind kind = RewardKind.Light;
    [SerializeField] private float lifeTime = 2.5f;
    [SerializeField] private float popTime = 0.15f;
    [Tooltip("사라지기 직전 이 시간 동안 깜빡여서 곧 사라진다고 알려 준다")]
    [SerializeField] private float blinkTime = 0.8f;
    [SerializeField] private SpriteRenderer sr;

    private float age;
    private bool done;
    private Vector3 baseScale;

    public RewardKind Kind => kind;

    /// <summary>(자신, 클릭했는가). 시간이 다 돼서 사라지면 false</summary>
    public event Action<PuffCornTarget, bool> Finished;

    private void Awake()
    {
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
        baseScale = transform.localScale;
        transform.localScale = Vector3.zero;
    }

    private void Update()
    {
        age += Time.deltaTime;

        // 톡 튀어나오는 연출
        float k = popTime > 0f ? Mathf.Clamp01(age / popTime) : 1f;
        float overshoot = 1f + 0.25f * Mathf.Sin(k * Mathf.PI);
        transform.localScale = baseScale * (k * overshoot);

        if (sr != null)
        {
            float left = lifeTime - age;
            bool faint = left < blinkTime && Mathf.FloorToInt(left * 10f) % 2 == 0;
            Color c = sr.color; c.a = faint ? 0.35f : 1f; sr.color = c;
        }

        if (age >= lifeTime) Finish(false);
    }

    /// <summary>StageMiniGame이 클릭을 감지하면 호출</summary>
    public void Click() => Finish(true);

    private void Finish(bool collected)
    {
        if (done) return;
        done = true;
        Finished?.Invoke(this, collected);
        Destroy(gameObject);
    }
}
