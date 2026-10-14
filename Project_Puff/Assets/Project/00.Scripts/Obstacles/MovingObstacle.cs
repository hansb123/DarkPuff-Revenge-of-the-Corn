using System.Collections;
using UnityEngine;

/// <summary>
/// 정해진 길을 따라 움직이는 장애물. 왔다갔다 하는 톱날, 순찰하는 가시 블록 등.
/// Offsets는 시작 위치 기준 상대 좌표라서 오브젝트를 옮겨도 길이 같이 움직인다.
/// Auto Start를 끄면 TrapTrigger가 발동시킬 때까지 가만히 있다 (갑자기 날아오는 톱날).
/// Rigidbody2D(Kinematic)를 붙이면 FixedUpdate에서 물리 이동, 없으면 Transform으로 이동한다.
/// </summary>
public class MovingObstacle : TrapBase
{
    private enum PathMode { PingPong, Loop }

    [Header("Path")]
    [Tooltip("시작 위치에서 얼마나 떨어진 곳들을 순서대로 지나갈지. 예: (4, 0) 하나면 오른쪽 4칸까지 왕복")]
    [SerializeField] private Vector2[] offsets = { new Vector2(4f, 0f) };
    [SerializeField] private PathMode mode = PathMode.PingPong;
    [SerializeField] private float speed = 3f;
    [Tooltip("각 지점에 도착하면 쉬는 시간 (초)")]
    [SerializeField] private float waitAtPoints;

    [Header("Start")]
    [Tooltip("켜면 씬 시작 때(그리고 리셋 후) 바로 움직임. 끄면 TrapTrigger가 발동시킬 때까지 정지")]
    [SerializeField] private bool autoStart = true;

    [Header("Spin (선택)")]
    [Tooltip("톱날처럼 제자리에서 도는 속도(도/초). 비워 두면 이 오브젝트가 돎")]
    [SerializeField] private Transform spinTarget;
    [SerializeField] private float spinSpeed;

    private Rigidbody2D rb;
    private Vector2[] points;
    private int target;
    private int dir;
    private float waitTimer;
    private bool moving;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody2D>();
        if (spinTarget == null) spinTarget = transform;
        BuildPath();
    }

    protected override void Start()
    {
        base.Start();
        if (autoStart) Activate();
    }

    protected override void OnActivate()
    {
        moving = true;
    }

    protected override void OnReset()
    {
        moving = false;
        if (rb != null) rb.position = startPosition;
        BuildPath();
        if (autoStart) Activate();
    }

    private void BuildPath()
    {
        int n = offsets != null ? offsets.Length : 0;
        points = new Vector2[n + 1];
        points[0] = startPosition;
        for (int i = 0; i < n; i++) points[i + 1] = (Vector2)startPosition + offsets[i];

        target = points.Length > 1 ? 1 : 0;
        dir = 1;
        waitTimer = 0f;
    }

    private void Update()
    {
        if (spinSpeed != 0f && moving) spinTarget.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
        if (rb == null) Step(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (rb != null) Step(Time.fixedDeltaTime);
    }

    private void Step(float dt)
    {
        if (!moving || points.Length < 2) return;

        if (waitTimer > 0f)
        {
            waitTimer -= dt;
            return;
        }

        Vector2 current = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 next = Vector2.MoveTowards(current, points[target], speed * dt);

        if (rb != null) rb.MovePosition(next);
        else transform.position = next;

        if ((next - points[target]).sqrMagnitude > 0.0001f) return;

        // 지점에 도착
        waitTimer = waitAtPoints;

        if (mode == PathMode.Loop)
        {
            target = (target + 1) % points.Length;
        }
        else
        {
            if (target + dir < 0 || target + dir >= points.Length) dir = -dir;
            target += dir;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 origin = Application.isPlaying ? (Vector2)startPosition : (Vector2)transform.position;

        Gizmos.color = Color.red;
        Vector2 prev = origin;
        if (offsets != null)
        {
            foreach (var off in offsets)
            {
                Vector2 p = origin + off;
                Gizmos.DrawLine(prev, p);
                Gizmos.DrawWireSphere(p, 0.15f);
                prev = p;
            }

            if (mode == PathMode.Loop) Gizmos.DrawLine(prev, origin);
        }
    }
}
