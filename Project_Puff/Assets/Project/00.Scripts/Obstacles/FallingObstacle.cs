using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 발동하면 흔들리다가(경고) 아래로 떨어지는 함정. 천장 블록, 떨어지는 가시, 낙석 등.
/// 떨어지는 동안에만 위험하게 만들 수 있고(Damage Only While Falling), 바닥에 닿으면 멈춘다.
/// 같은 오브젝트에 Obstacle 컴포넌트와 Collider2D가 있어야 한다. (Rigidbody2D는 필요 없음)
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class FallingObstacle : TrapBase
{
    [Header("Timing")]
    [Tooltip("발동 뒤 흔들리기 시작할 때까지 (초)")]
    [SerializeField] private float delay;
    [Tooltip("떨어지기 전에 흔들리는 시간 (초). 플레이어가 눈치채고 피할 시간. 0이면 바로 떨어짐")]
    [SerializeField] private float shakeTime = 0.4f;
    [SerializeField] private float shakeAmount = 0.06f;

    [Header("Fall")]
    [SerializeField] private float gravity = 40f;
    [SerializeField] private float maxFallSpeed = 25f;
    [Tooltip("여기에 닿으면 멈춤 (Ground 레이어)")]
    [SerializeField] private LayerMask groundLayer;

    [Header("Danger")]
    [SerializeField] private Obstacle obstacle;
    [Tooltip("켜면 떨어지는 동안에만 위험. 꺼 두면 가만히 있을 때도 닿으면 다침")]
    [SerializeField] private bool damageOnlyWhileFalling = true;

    [Header("After Landing")]
    [Tooltip("바닥에 닿고 이 시간(초) 뒤에 사라짐. 0이면 계속 남아 있음 (발판처럼 쓸 수 있음)")]
    [SerializeField] private float vanishAfterLanding;

    private Collider2D col;
    private ContactFilter2D groundFilter;
    private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();

    protected override void Awake()
    {
        base.Awake();
        col = GetComponent<Collider2D>();
        if (obstacle == null) obstacle = GetComponent<Obstacle>();

        groundFilter = new ContactFilter2D();
        groundFilter.SetLayerMask(groundLayer);
        groundFilter.useTriggers = false;

        if (obstacle == null)
            Debug.LogWarning($"[FallingObstacle] '{name}'에 Obstacle 컴포넌트가 없어서 닿아도 다치지 않습니다.", this);
    }

    protected override void Start()
    {
        base.Start();
        ApplyIdleDanger();
    }

    protected override void OnActivate()
    {
        StartCoroutine(FallRoutine());
    }

    protected override void OnReset()
    {
        gameObject.SetActive(true);
        ApplyIdleDanger();
    }

    private void ApplyIdleDanger()
    {
        if (obstacle != null) obstacle.DamageEnabled = !damageOnlyWhileFalling;
    }

    private IEnumerator FallRoutine()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        // 1) 흔들림 (경고)
        if (shakeTime > 0f)
        {
            GameSfx.PlayAt(GameSfxId.TrapWarn, transform.position);

            float t = 0f;
            while (t < shakeTime)
            {
                t += Time.deltaTime;
                transform.position = startPosition + Vector3.right * (Mathf.Sin(t * 70f) * shakeAmount);
                yield return null;
            }
            transform.position = startPosition;
        }

        // 2) 낙하
        if (obstacle != null) obstacle.DamageEnabled = true;

        float speed = 0f;
        while (true)
        {
            speed = Mathf.Min(speed + gravity * Time.deltaTime, maxFallSpeed);
            float move = speed * Time.deltaTime;
            bool landed = false;

            Bounds b = col.bounds;
            int count = Physics2D.BoxCast(b.center, b.size, 0f, Vector2.down, groundFilter, hits, move + 0.01f);
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider == col) continue;      // 자기 자신은 무시
                move = Mathf.Min(move, hits[i].distance);
                landed = true;
            }

            transform.position += Vector3.down * move;
            if (landed) break;

            yield return null;
        }

        // 3) 착지
        GameSfx.PlayAt(GameSfxId.TrapFallHit, transform.position);
        if (obstacle != null && damageOnlyWhileFalling) obstacle.DamageEnabled = false;

        if (vanishAfterLanding > 0f)
        {
            yield return new WaitForSeconds(vanishAfterLanding);
            gameObject.SetActive(false);      // 리셋 때 OnReset에서 다시 켜짐
        }
    }
}
