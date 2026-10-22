using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 닭의 공격 패턴을 계속 골라서 실행하는 지휘자.
/// 흐름: 시작 대기 → (난이도에 맞는 패턴 선택 → 실행 → 휴식) 반복.
/// 난이도는 분노 게이지 진행도(0~1)를 그대로 쓴다. 게이지가 차오를수록 패턴이 빨라지고, 새 패턴이 해금된다.
/// 사용: ChickenIntroSequence.Completed 에 Begin() 을, AngerGaugeHud.Completed(클리어) 에 Stop() 을 연결.
/// 하트가 0이 돼 처음부터 다시 시작하면(RespawnManager) 남은 공격을 치우고 잠시 뒤 다시 시작한다.
/// </summary>
public class PatternRunner : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public AttackPattern pattern;
        [Tooltip("분노 게이지가 이 값 이상일 때부터 나오기 시작 (0 = 처음부터)")]
        [Range(0f, 1f)] public float unlockAt = 0f;
        [Tooltip("뽑힐 확률 비중")]
        public float weight = 1f;
    }

    [Header("패턴 목록")]
    [SerializeField] private List<Entry> patterns = new List<Entry>();

    [Header("무대 (닭 공격이 떨어지는 범위)")]
    [SerializeField] private Transform leftLimit;
    [SerializeField] private Transform rightLimit;
    [Tooltip("바닥 높이. 이 오브젝트의 Y 가 바닥선")]
    [SerializeField] private Transform groundLine;

    [Header("난이도")]
    [Tooltip("연결하면 분노 게이지 진행도를 난이도로 사용")]
    [SerializeField] private AngerGaugeHud anger;
    [Tooltip("게이지를 연결하지 않았을 때의 고정 난이도 (테스트용)")]
    [Range(0f, 1f)][SerializeField] private float manualDifficulty = 0f;

    [Header("흐름")]
    [SerializeField] private float startDelay = 1.0f;
    [Tooltip("난이도 1일 때 휴식 시간이 원래의 몇 배가 되는지")]
    [Range(0.1f, 1f)][SerializeField] private float restAtMax = 0.4f;
    [SerializeField] private bool restartOnRespawn = true;
    [SerializeField] private float restartDelay = 1.2f;

    [Header("풀링 (포트폴리오 측정용 스위치)")]
    [Tooltip("끄면 Instantiate/Destroy 로 동작. Profiler로 풀링 전/후를 비교할 때 사용")]
    [SerializeField] private bool usePooling = true;

    public event Action<AttackPattern> PatternStarted;
    public event Action<AttackPattern> PatternEnded;

    public bool IsRunning { get; private set; }
    public AttackPattern Current { get; private set; }
    public float Difficulty => anger != null ? anger.Progress : manualDifficulty;

    private Transform player;
    private Transform poolRoot;
    private Coroutine loop;
    private Coroutine restartRoutine;
    private readonly Dictionary<int, object> pools = new Dictionary<int, object>();
    private readonly HashSet<IPatternHazard> hazards = new HashSet<IPatternHazard>();
    private AttackPattern last;

    private void Awake()
    {
        poolRoot = new GameObject("PatternPool").transform;
        poolRoot.SetParent(transform, false);
    }

    private void Start()
    {
        var health = FindFirstObjectByType<PlayerHealth>();
        if (health != null) player = health.transform;
        else Debug.LogWarning("[PatternRunner] 씬에서 PlayerHealth(플레이어)를 찾지 못했습니다.", this);

        if (anger == null) anger = FindFirstObjectByType<AngerGaugeHud>();

        if (RespawnManager.Instance != null) RespawnManager.Instance.RespawnApplied += OnRespawn;
    }

    private void OnDestroy()
    {
        if (RespawnManager.Instance != null) RespawnManager.Instance.RespawnApplied -= OnRespawn;
    }

    // ───────────── 외부 호출 ─────────────

    public void Begin()
    {
        if (IsRunning) return;
        if (restartRoutine != null) { StopCoroutine(restartRoutine); restartRoutine = null; }
        IsRunning = true;
        loop = StartCoroutine(Loop());
    }

    /// <summary>패턴을 멈추고 남아 있는 알 등을 모두 치운다</summary>
    public void Stop()
    {
        IsRunning = false;
        if (loop != null) { StopCoroutine(loop); loop = null; }
        if (restartRoutine != null) { StopCoroutine(restartRoutine); restartRoutine = null; }
        ClearHazards();
        Current = null;
    }

    // ───────────── 오브젝트 추적 / 풀 ─────────────

    public void Register(IPatternHazard h) => hazards.Add(h);
    public void Unregister(IPatternHazard h) => hazards.Remove(h);

    private void ClearHazards()
    {
        if (hazards.Count == 0) return;
        var copy = new List<IPatternHazard>(hazards);
        hazards.Clear();
        foreach (var h in copy) h.Cancel();
    }

    /// <summary>프리팹별로 풀을 하나씩 만들어 재사용</summary>
    public PrefabPool<T> GetPool<T>(T prefab, Action<T, PrefabPool<T>> onCreated = null) where T : Component
    {
        int key = prefab.GetInstanceID();
        if (pools.TryGetValue(key, out var existing)) return (PrefabPool<T>)existing;

        var pool = new PrefabPool<T>(prefab, poolRoot, usePooling, 8, 48, onCreated);
        pools[key] = pool;
        return pool;
    }

    // ───────────── 실행 ─────────────

    private IEnumerator Loop()
    {
        yield return new WaitForSeconds(startDelay);

        while (IsRunning)
        {
            float d = Difficulty;
            var pattern = Pick(d);
            if (pattern == null) { yield return null; continue; }

            last = pattern;
            Current = pattern;
            PatternStarted?.Invoke(pattern);

            yield return pattern.Run(MakeContext(d));

            PatternEnded?.Invoke(pattern);
            Current = null;

            float rest = pattern.RestAfter * Mathf.Lerp(1f, restAtMax, Difficulty);
            yield return new WaitForSeconds(rest);
        }
    }

    private PatternContext MakeContext(float difficulty)
    {
        return new PatternContext
        {
            Runner = this,
            Player = player,
            LeftX = leftLimit != null ? leftLimit.position.x : -8f,
            RightX = rightLimit != null ? rightLimit.position.x : 8f,
            GroundY = groundLine != null ? groundLine.position.y : 0f,
            Difficulty = difficulty,
        };
    }

    private AttackPattern Pick(float d)
    {
        var candidates = new List<Entry>();
        foreach (var e in patterns)
            if (e.pattern != null && e.unlockAt <= d && e.weight > 0f) candidates.Add(e);
        if (candidates.Count == 0) return null;

        // 같은 패턴이 연속으로 나오지 않게 (후보가 둘 이상일 때)
        if (candidates.Count > 1) candidates.RemoveAll(e => e.pattern == last);

        float total = 0f;
        foreach (var e in candidates) total += e.weight;
        float r = UnityEngine.Random.value * total;
        foreach (var e in candidates)
        {
            r -= e.weight;
            if (r <= 0f) return e.pattern;
        }
        return candidates[candidates.Count - 1].pattern;
    }

    // ───────────── 재시작 ─────────────

    private void OnRespawn()
    {
        if (!restartOnRespawn || !IsRunning) return;
        // 미니게임(체크포인트를 안 쓰는 모드)에서 처음부터 다시 시작할 때만
        if (RespawnManager.Instance != null && RespawnManager.Instance.UseCheckpoints) return;

        Stop();
        restartRoutine = StartCoroutine(RestartAfterDelay());
    }

    private IEnumerator RestartAfterDelay()
    {
        yield return new WaitForSeconds(restartDelay);
        restartRoutine = null;
        // 분노 게이지도 처음부터 다시 차오르게 (HUD는 재시작 때 0으로 비워지기만 하고 스스로 돌지 않는다)
        if (anger != null && anger.LastDuration > 0f) anger.Run(anger.LastDuration);
        Begin();
    }

    // 테스트용 (⋮ 메뉴)
    [ContextMenu("Debug/Begin")] private void DebugBegin() => Begin();
    [ContextMenu("Debug/Stop")] private void DebugStop() => Stop();
}