using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 킬 게이트. 지정한 몬스터를 전부 잡아야 열리는 가로막(Ground 레이어 벽).
/// 위의 숫자판에 남은 마릿수가 표시되고, 한 마리 잡을 때마다 줄어든다. 0이 되면 문이 떨리다가 아래로 가라앉는다.
/// 숫자는 폰트가 아니라 숫자 스프라이트(digit_0~9)로 표시한다 (TextMeshPro 불필요).
///
/// 구성:
///   KillGate (이 스크립트, 빈 오브젝트)
///     ├ Blocker  : 가로막. BoxCollider2D(Ground 레이어) + 자식으로 gate_block 타일들(세로로 쌓기) + gate_cap(맨 위) + gate_lock
///     └ Panel    : 숫자판 (gate_panel). 자식 Tens/Ones = 숫자 SpriteRenderer 2개
/// 몬스터는 Targets 에 직접 끌어다 넣는다 (씬에 배치된 것들). 이미 죽었거나 사라진 몬스터는 잡은 것으로 센다.
/// </summary>
public class KillGate : MonoBehaviour
{
    [Header("잡아야 할 몬스터 (씬 오브젝트를 끌어다 넣기)")]
    [SerializeField] private EnemyBase[] targets;

    [Header("가로막")]
    [Tooltip("가라앉을 오브젝트 (콜라이더가 있는 가로막 루트)")]
    [SerializeField] private Transform blocker;
    [Tooltip("열릴 때 이동할 거리 (기본: 아래로 가로막 높이만큼). 보통 (0, -높이)")]
    [SerializeField] private Vector2 openOffset = new Vector2(0f, -4f);
    [SerializeField] private float shakeTime = 0.5f;
    [SerializeField] private float shakeAmount = 0.06f;
    [SerializeField] private float sinkTime = 1.0f;
    [Tooltip("다 열린 뒤 가로막 오브젝트를 꺼버림")]
    [SerializeField] private bool disableBlockerAfterOpen = true;

    [Header("숫자판")]
    [SerializeField] private Transform panel;
    [Tooltip("digit_0 ~ digit_9 순서대로")]
    [SerializeField] private Sprite[] digitSprites;
    [SerializeField] private SpriteRenderer tensRenderer;
    [SerializeField] private SpriteRenderer onesRenderer;
    [Tooltip("한 자리일 때 숫자가 놓일 위치(판 기준 로컬). 두 자리면 좌우로 spacing/2 씩 벌어짐")]
    [SerializeField] private Vector2 digitCenter = new Vector2(0.32f, 0f);
    [SerializeField] private float digitSpacing = 0.5f;
    [Tooltip("숫자가 바뀔 때 통통 튀는 크기 (1 = 없음)")]
    [SerializeField] private float popScale = 1.35f;

    [Header("이벤트 (효과음/연출 연결용)")]
    public UnityEvent OnCountChanged;
    public UnityEvent OnOpened;

    private int remaining = -1;
    private bool opened;
    private Vector3 blockerStart;
    private Vector3 panelStartScale;

    public int Remaining => Mathf.Max(0, remaining);
    public bool IsOpen => opened;

    private void Awake()
    {
        if (blocker != null) blockerStart = blocker.position;
        if (panel != null) panelStartScale = panel.localScale;
    }

    private void Start()
    {
        remaining = CountAlive();
        ShowNumber(remaining);
        if (remaining <= 0) OpenInstant();
    }

    private int CountAlive()
    {
        int n = 0;
        if (targets == null) return 0;
        foreach (var t in targets)
            if (t != null && !t.IsDead) n++;       // null = 이미 사라짐(잡은 것), IsDead = 죽음
        return n;
    }

    private void Update()
    {
        if (opened) return;

        int now = CountAlive();
        if (now == remaining) return;

        bool decreased = now < remaining;
        remaining = now;
        ShowNumber(remaining);
        if (decreased) { OnCountChanged?.Invoke(); if (panel != null) StartCoroutine(Pop()); }

        if (remaining <= 0) StartCoroutine(OpenRoutine());
    }

    // ---------------- 숫자 표시 ----------------

    private void ShowNumber(int n)
    {
        n = Mathf.Clamp(n, 0, 99);
        if (digitSprites == null || digitSprites.Length < 10) return;

        if (n >= 10)
        {
            SetDigit(tensRenderer, n / 10, digitCenter + Vector2.left * digitSpacing * 0.5f);
            SetDigit(onesRenderer, n % 10, digitCenter + Vector2.right * digitSpacing * 0.5f);
        }
        else
        {
            if (tensRenderer != null) tensRenderer.enabled = false;
            SetDigit(onesRenderer, n, digitCenter);
        }
    }

    private void SetDigit(SpriteRenderer sr, int v, Vector2 localPos)
    {
        if (sr == null) return;
        sr.enabled = true;
        sr.sprite = digitSprites[v];
        sr.transform.localPosition = new Vector3(localPos.x, localPos.y, sr.transform.localPosition.z);
    }

    private IEnumerator Pop()
    {
        float t = 0f, dur = 0.18f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = 1f + (popScale - 1f) * Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
            if (panel != null) panel.localScale = panelStartScale * k;
            yield return null;
        }
        if (panel != null) panel.localScale = panelStartScale;
    }

    // ---------------- 열기 ----------------

    private void OpenInstant()
    {
        opened = true;
        if (blocker != null)
        {
            if (disableBlockerAfterOpen) blocker.gameObject.SetActive(false);
            else blocker.position = blockerStart + (Vector3)openOffset;
        }
        if (panel != null) panel.gameObject.SetActive(false);
    }

    private IEnumerator OpenRoutine()
    {
        opened = true;
        OnOpened?.Invoke();

        if (panel != null) StartCoroutine(HidePanel());

        if (blocker != null)
        {
            // 떨림
            float t = 0f;
            while (t < shakeTime)
            {
                t += Time.deltaTime;
                float dx = (Mathf.PerlinNoise(Time.time * 40f, 0f) - 0.5f) * 2f * shakeAmount;
                blocker.position = blockerStart + new Vector3(dx, 0f, 0f);
                yield return null;
            }
            // 가라앉음 (콜라이더는 이동 중에도 남아 있다가 끝에 꺼짐 → 완전히 내려간 뒤 지나갈 수 있음)
            Vector3 end = blockerStart + (Vector3)openOffset;
            t = 0f;
            while (t < sinkTime)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / sinkTime);
                blocker.position = Vector3.Lerp(blockerStart, end, k);
                yield return null;
            }
            blocker.position = end;
            if (disableBlockerAfterOpen) blocker.gameObject.SetActive(false);
        }
    }

    private IEnumerator HidePanel()
    {
        yield return new WaitForSeconds(0.4f);
        float t = 0f, dur = 0.25f;
        while (t < dur)
        {
            t += Time.deltaTime;
            if (panel != null) panel.localScale = panelStartScale * Mathf.Lerp(1f, 0f, t / dur);
            yield return null;
        }
        if (panel != null) panel.gameObject.SetActive(false);
    }

    // ---------------- 에디터 표시 ----------------

    private void OnDrawGizmosSelected()
    {
        if (targets == null) return;
        Gizmos.color = Color.yellow;
        foreach (var t in targets)
            if (t != null) Gizmos.DrawLine(transform.position, t.transform.position);
    }
}