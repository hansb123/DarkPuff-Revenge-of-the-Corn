using System.Collections;
using UnityEngine;

/// <summary>
/// 발동하면 지정한 방향으로 순식간에 튀어나오는 함정. 바닥 가시, 벽에서 나오는 창 등.
/// 숨은 위치(오브젝트를 둔 자리)에서 Pop Offset만큼 이동한다.
/// Auto Start + Repeat Interval을 쓰면 TrapTrigger 없이 주기적으로 나왔다 들어가는 가시도 만들 수 있다.
/// 같은 오브젝트에 Obstacle 컴포넌트가 있어야 한다.
/// </summary>
public class PopUpObstacle : TrapBase
{
    [Header("Motion")]
    [Tooltip("숨은 위치에서 튀어나오는 거리/방향. 바닥 가시면 (0, 1)")]
    [SerializeField] private Vector2 popOffset = new Vector2(0f, 1f);
    [Tooltip("발동 뒤 튀어나오기 전 대기 (초). 0이면 즉시 = 깜짝 함정")]
    [SerializeField] private float delay;
    [Tooltip("지직거리는 예고 시간 (초). 대기 시간 끝 무렵에 경고음이 남. 0이면 예고 없음")]
    [SerializeField] private float warnTime;
    [SerializeField] private float popTime = 0.08f;

    [Header("Stay / Repeat")]
    [Tooltip("튀어나온 채 머무는 시간 (초). 0 이하면 리셋될 때까지 계속 나와 있음")]
    [SerializeField] private float stayTime;
    [SerializeField] private float retractTime = 0.3f;
    [Tooltip("들어간 뒤 다시 나오기까지 (초). 0이면 한 번만. Stay Time이 0보다 커야 의미가 있음")]
    [SerializeField] private float repeatInterval;
    [Tooltip("켜면 씬 시작 때(그리고 리셋 후) 스스로 발동. 주기적 가시용")]
    [SerializeField] private bool autoStart;

    [Header("Danger")]
    [SerializeField] private Obstacle obstacle;
    [Tooltip("켜면 나와 있는 동안에만 위험")]
    [SerializeField] private bool damageOnlyWhenOut = true;

    protected override void Awake()
    {
        base.Awake();
        if (obstacle == null) obstacle = GetComponent<Obstacle>();
        if (obstacle == null)
            Debug.LogWarning($"[PopUpObstacle] '{name}'에 Obstacle 컴포넌트가 없어서 닿아도 다치지 않습니다.", this);
    }

    protected override void Start()
    {
        base.Start();
        ApplyHiddenDanger();
        if (autoStart) Activate();
    }

    protected override void OnActivate()
    {
        StartCoroutine(PopRoutine());
    }

    protected override void OnReset()
    {
        ApplyHiddenDanger();
        if (autoStart) Activate();
    }

    private void ApplyHiddenDanger()
    {
        if (obstacle != null) obstacle.DamageEnabled = !damageOnlyWhenOut;
    }

    private IEnumerator PopRoutine()
    {
        Vector3 hidden = startPosition;
        Vector3 shown = startPosition + (Vector3)popOffset;
        float firstDelay = delay;        // 첫 번째만 지연, 반복할 때는 예고만

        while (true)
        {
            // 대기 + 예고
            float wait = Mathf.Max(0f, firstDelay - warnTime);
            if (wait > 0f) yield return new WaitForSeconds(wait);

            if (warnTime > 0f)
            {
                GameSfx.PlayAt(GameSfxId.TrapWarn, transform.position);
                yield return new WaitForSeconds(warnTime);
            }

            // 튀어나옴 (처음엔 빠르고 끝에서 부드럽게)
            GameSfx.PlayAt(GameSfxId.TrapSpikePop, transform.position);
            if (obstacle != null) obstacle.DamageEnabled = true;
            yield return Move(hidden, shown, popTime, easeOut: true);

            if (stayTime <= 0f) yield break;             // 계속 나와 있음
            yield return new WaitForSeconds(stayTime);

            // 들어감
            yield return Move(shown, hidden, retractTime, easeOut: false);
            if (obstacle != null && damageOnlyWhenOut) obstacle.DamageEnabled = false;

            if (repeatInterval <= 0f) yield break;
            yield return new WaitForSeconds(repeatInterval);
            firstDelay = 0f;
        }
    }

    private IEnumerator Move(Vector3 from, Vector3 to, float time, bool easeOut)
    {
        if (time <= 0f)
        {
            transform.position = to;
            yield break;
        }

        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / time);
            if (easeOut) u = 1f - (1f - u) * (1f - u);
            transform.position = Vector3.LerpUnclamped(from, to, u);
            yield return null;
        }
        transform.position = to;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 hidden = Application.isPlaying ? startPosition : transform.position;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(hidden, hidden + (Vector3)popOffset);
        Gizmos.DrawWireSphere(hidden + (Vector3)popOffset, 0.15f);
    }
}
